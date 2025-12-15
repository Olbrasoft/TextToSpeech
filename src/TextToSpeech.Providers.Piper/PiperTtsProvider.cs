using System.Diagnostics;
using System.Globalization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Olbrasoft.TextToSpeech.Core;
using Olbrasoft.TextToSpeech.Core.Enums;
using Olbrasoft.TextToSpeech.Core.Interfaces;
using Olbrasoft.TextToSpeech.Core.Models;
using Olbrasoft.TextToSpeech.Core.Services;

namespace Olbrasoft.TextToSpeech.Providers.Piper;

/// <summary>
/// Offline TTS provider using Piper neural TTS.
/// Uses piper CLI to generate audio from ONNX models.
/// Last resort fallback when online providers fail.
/// </summary>
public sealed class PiperTtsProvider : ITtsProvider
{
    private readonly ILogger<PiperTtsProvider> _logger;
    private readonly PiperConfiguration _config;
    private readonly IAudioDataFactory _audioDataFactory;
    private bool? _isPiperInstalled;
    private DateTime? _lastSuccessTime;

    /// <summary>
    /// Initializes a new instance of PiperTtsProvider.
    /// </summary>
    public PiperTtsProvider(
        ILogger<PiperTtsProvider> logger,
        IOptions<PiperConfiguration> config,
        IAudioDataFactory audioDataFactory)
    {
        _logger = logger;
        _config = config.Value;
        _audioDataFactory = audioDataFactory;
    }

    /// <inheritdoc />
    public string Name => "Piper";

    /// <summary>
    /// Gets whether the provider is available (piper installed and model exists).
    /// </summary>
    public bool IsAvailable => CheckPiperInstalled() && File.Exists(_config.ModelPath);

    /// <summary>
    /// Gets or sets the source profile key for voice configuration (e.g., "fast", "default").
    /// Used to select the appropriate Piper voice profile.
    /// </summary>
    public string? SourceProfile { get; set; }

    /// <inheritdoc />
    public async Task<TtsResult> SynthesizeAsync(TtsRequest request, CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();

        if (!CheckPiperInstalled())
        {
            _logger.LogWarning("Piper is not installed");
            return TtsResult.Fail("Piper is not installed", Name, stopwatch.Elapsed);
        }

        if (!File.Exists(_config.ModelPath))
        {
            _logger.LogWarning("Piper model not found at: {Path}", _config.ModelPath);
            return TtsResult.Fail($"Piper model not found: {_config.ModelPath}", Name, stopwatch.Elapsed);
        }

        var tempWav = Path.Combine(Path.GetTempPath(), $"piper_{Guid.NewGuid():N}.wav");

        try
        {
            // Get Piper voice profile based on source or default
            var profileKey = SourceProfile ?? _config.DefaultProfile;
            if (!_config.Profiles.TryGetValue(profileKey, out var profile))
            {
                _config.Profiles.TryGetValue("default", out profile);
                profile ??= new PiperVoiceProfile();
            }

            // Apply rate adjustment from request (convert -100/+100 to length scale)
            var lengthScale = profile.LengthScale;
            if (request.Rate != 0)
            {
                // Rate -100 = 2x slower (lengthScale * 2), Rate +100 = 2x faster (lengthScale * 0.5)
                var speedMultiplier = 1.0 - (request.Rate / 200.0);
                lengthScale *= speedMultiplier;
                lengthScale = Math.Clamp(lengthScale, 0.25, 2.0);
            }

            // Build Piper command arguments
            var args = $"--model \"{_config.ModelPath}\" --output_file \"{tempWav}\" " +
                $"--length-scale {lengthScale.ToString(CultureInfo.InvariantCulture)} " +
                $"--noise-scale {profile.NoiseScale.ToString(CultureInfo.InvariantCulture)} " +
                $"--noise-w-scale {profile.NoiseWScale.ToString(CultureInfo.InvariantCulture)} " +
                $"--sentence-silence {profile.SentenceSilence.ToString(CultureInfo.InvariantCulture)} " +
                $"--volume {profile.Volume.ToString(CultureInfo.InvariantCulture)} " +
                $"--speaker {profile.Speaker}";

            _logger.LogDebug("Running Piper TTS with profile '{Profile}' for text: {Text}",
                profileKey, request.Text.Length > 50 ? request.Text[..50] + "..." : request.Text);

            using var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = _config.PiperPath,
                    Arguments = args,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardInput = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                }
            };

            process.Start();

            // Send text via stdin
            await process.StandardInput.WriteLineAsync(request.Text);
            process.StandardInput.Close();

            // Read stderr for error messages
            var stderr = await process.StandardError.ReadToEndAsync(cancellationToken);

            using var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(_config.TimeoutSeconds));
            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);

            try
            {
                await process.WaitForExitAsync(linkedCts.Token);
            }
            catch (OperationCanceledException) when (timeoutCts.IsCancellationRequested)
            {
                try { process.Kill(); } catch { }
                stopwatch.Stop();
                return TtsResult.Fail("Piper process timed out", Name, stopwatch.Elapsed);
            }

            stopwatch.Stop();

            if (process.ExitCode != 0)
            {
                _logger.LogWarning("Piper failed with exit code {ExitCode}: {Error}", process.ExitCode, stderr);
                return TtsResult.Fail($"Piper error: {stderr}", Name, stopwatch.Elapsed);
            }

            if (!File.Exists(tempWav))
            {
                _logger.LogWarning("Piper did not create output file");
                return TtsResult.Fail("Piper did not create output file", Name, stopwatch.Elapsed);
            }

            var audioBytes = await File.ReadAllBytesAsync(tempWav, cancellationToken);
            _logger.LogDebug("Piper generated {Bytes} bytes of audio (WAV)", audioBytes.Length);

            if (audioBytes.Length == 0)
            {
                return TtsResult.Fail("Piper returned empty audio", Name, stopwatch.Elapsed);
            }

            _lastSuccessTime = DateTime.UtcNow;

            // If file mode, move temp file to output directory
            string? existingFilePath = null;
            if (_config.OutputMode == AudioOutputMode.File)
            {
                existingFilePath = MoveToOutputDirectory(tempWav, request.Text);
            }

            var audioData = _audioDataFactory.Create(
                audioBytes,
                request.Text,
                Name,
                _config.OutputMode,
                _config.OutputDirectory,
                "{provider}_{timestamp}_{hash}.wav",
                existingFilePath,
                contentType: "audio/wav");

            // Estimate audio duration from WAV header (if present)
            var audioDuration = EstimateAudioDuration(audioBytes);

            return TtsResult.Ok(audioData, Name, stopwatch.Elapsed, audioDuration);
        }
        catch (OperationCanceledException)
        {
            stopwatch.Stop();
            return TtsResult.Fail("Operation cancelled", Name, stopwatch.Elapsed);
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            _logger.LogError(ex, "Error generating audio with Piper");
            return TtsResult.Fail($"Unexpected error: {ex.Message}", Name, stopwatch.Elapsed);
        }
        finally
        {
            // Cleanup temp file if in memory mode
            if (_config.OutputMode == AudioOutputMode.Memory)
            {
                try { if (File.Exists(tempWav)) File.Delete(tempWav); } catch { }
            }
        }
    }

    /// <inheritdoc />
    public Task<TtsProviderInfo> GetInfoAsync(CancellationToken cancellationToken = default)
    {
        var piperInstalled = CheckPiperInstalled();
        var modelExists = File.Exists(_config.ModelPath);

        var status = piperInstalled switch
        {
            false => ProviderStatus.Unavailable,
            true when !modelExists => ProviderStatus.Unavailable,
            _ => ProviderStatus.Available
        };

        return Task.FromResult(new TtsProviderInfo
        {
            Name = Name,
            Status = status,
            LastSuccessTime = _lastSuccessTime,
            SupportedVoices = GetAvailableVoices()
        });
    }

    private bool CheckPiperInstalled()
    {
        if (_isPiperInstalled.HasValue)
        {
            return _isPiperInstalled.Value;
        }

        try
        {
            using var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = _config.PiperPath,
                    Arguments = "--help",
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                }
            };

            process.Start();
            process.WaitForExit(5000);
            _isPiperInstalled = true; // piper --help returns non-zero but exists

            _logger.LogInformation("Piper is available at: {Path}", _config.PiperPath);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Piper is not available");
            _isPiperInstalled = false;
            return false;
        }
    }

    private List<VoiceInfo> GetAvailableVoices()
    {
        var voices = new List<VoiceInfo>();

        // Check if model exists and extract voice info from filename
        if (File.Exists(_config.ModelPath))
        {
            var fileName = Path.GetFileNameWithoutExtension(_config.ModelPath);
            // Extract language and voice name from filename like "cs_CZ-jirka-medium"
            var parts = fileName.Split('-');
            if (parts.Length >= 2)
            {
                var langParts = parts[0].Split('_');
                var language = langParts.Length >= 2 ? $"{langParts[0]}-{langParts[1]}" : parts[0];
                var voiceName = parts[1];

                voices.Add(new VoiceInfo
                {
                    Id = fileName,
                    Language = language,
                    DisplayName = char.ToUpper(voiceName[0]) + voiceName[1..],
                    Gender = "Unknown"
                });
            }
        }

        return voices;
    }

    private string MoveToOutputDirectory(string tempWavPath, string text)
    {
        var directory = _config.OutputDirectory ?? Path.GetTempPath();
        Directory.CreateDirectory(directory);

        var hash = TextHasher.ComputeHash(text);
        var timestamp = DateTime.UtcNow.ToString("yyyyMMdd_HHmmss");
        var fileName = $"{Name}_{timestamp}_{hash}.wav";
        var filePath = Path.Combine(directory, fileName);

        if (File.Exists(filePath))
        {
            File.Delete(filePath);
        }
        File.Move(tempWavPath, filePath);

        return filePath;
    }

    private static TimeSpan EstimateAudioDuration(byte[] wavBytes)
    {
        // Try to read WAV header to get exact duration
        if (wavBytes.Length >= 44)
        {
            try
            {
                // WAV format: bytes 24-27 = sample rate, bytes 40-43 = data size
                var sampleRate = BitConverter.ToInt32(wavBytes, 24);
                var bitsPerSample = BitConverter.ToInt16(wavBytes, 34);
                var channels = BitConverter.ToInt16(wavBytes, 22);
                var dataSize = BitConverter.ToInt32(wavBytes, 40);

                if (sampleRate > 0 && bitsPerSample > 0 && channels > 0)
                {
                    var bytesPerSecond = sampleRate * channels * (bitsPerSample / 8);
                    var seconds = (double)dataSize / bytesPerSecond;
                    return TimeSpan.FromSeconds(seconds);
                }
            }
            catch
            {
                // Fall through to estimate
            }
        }

        // Rough estimate: Piper uses 22050 Hz, 16-bit mono = 44100 bytes/second
        const int defaultBytesPerSecond = 44100;
        var estimatedSeconds = (double)wavBytes.Length / defaultBytesPerSecond;
        return TimeSpan.FromSeconds(estimatedSeconds);
    }
}
