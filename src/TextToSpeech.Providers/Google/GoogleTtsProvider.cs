using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Olbrasoft.TextToSpeech.Core.Interfaces;
using Olbrasoft.TextToSpeech.Core.Models;
using Olbrasoft.TextToSpeech.Providers.Configuration;

namespace Olbrasoft.TextToSpeech.Providers.Google;

/// <summary>
/// TTS provider using Google Translate TTS (gTTS).
/// Uses Python gTTS library for reliable access (handles Google's anti-bot measures).
/// Czech female voice, no API key required.
/// </summary>
public sealed class GoogleTtsProvider : ITtsProvider
{
    private readonly ILogger<GoogleTtsProvider> _logger;
    private readonly GoogleTtsConfiguration _config;
    private readonly IOutputConfiguration _outputConfig;
    private DateTime? _lastSuccessTime;
    private bool? _isGttsInstalled;

    /// <summary>
    /// Initializes a new instance of GoogleTtsProvider.
    /// </summary>
    public GoogleTtsProvider(
        ILogger<GoogleTtsProvider> logger,
        IOptions<GoogleTtsConfiguration> config,
        IOutputConfiguration outputConfig)
    {
        _logger = logger;
        _config = config.Value;
        _outputConfig = outputConfig;
    }

    /// <inheritdoc />
    public string Name => "GoogleTTS";

    /// <summary>
    /// Gets whether the provider is available (gTTS installed).
    /// </summary>
    public bool IsAvailable => _isGttsInstalled ??= CheckGttsInstalled();

    /// <inheritdoc />
    public async Task<TtsResult> SynthesizeAsync(TtsRequest request, CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();

        if (!IsAvailable)
        {
            _logger.LogWarning("gTTS is not installed");
            return TtsResult.Fail("gTTS is not installed (pip install gtts)", Name, stopwatch.Elapsed);
        }

        var tempFile = Path.Combine(Path.GetTempPath(), $"gtts_{Guid.NewGuid():N}.mp3");

        try
        {
            // Get language from voice name if specified (e.g., "cs" from "cs-CZ")
            var language = _config.Language;
            if (!string.IsNullOrEmpty(request.Voice))
            {
                var langPart = request.Voice.Split('-')[0].ToLowerInvariant();
                if (langPart.Length == 2)
                {
                    language = langPart;
                }
            }

            // Build gtts-cli command - write text to stdin to avoid shell escaping issues
            var slowFlag = _config.Slow ? "--slow" : "";
            var args = $"-l {language} {slowFlag} -o \"{tempFile}\" -";

            _logger.LogDebug("Running gTTS for text: {Text}",
                request.Text.Length > 50 ? request.Text[..50] + "..." : request.Text);

            using var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = _config.GttsCliPath,
                    Arguments = args,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardInput = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                }
            };

            process.Start();

            // Write text to stdin (safer than command line arguments for special characters)
            await process.StandardInput.WriteAsync(request.Text);
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
                return TtsResult.Fail("gTTS process timed out", Name, stopwatch.Elapsed);
            }

            stopwatch.Stop();

            if (process.ExitCode != 0)
            {
                _logger.LogWarning("gTTS failed with exit code {ExitCode}: {Error}", process.ExitCode, stderr);
                return TtsResult.Fail($"gTTS error: {stderr}", Name, stopwatch.Elapsed);
            }

            if (!File.Exists(tempFile))
            {
                _logger.LogWarning("gTTS did not create output file");
                return TtsResult.Fail("gTTS did not create output file", Name, stopwatch.Elapsed);
            }

            var audioBytes = await File.ReadAllBytesAsync(tempFile, cancellationToken);
            _logger.LogDebug("gTTS generated {Bytes} bytes of audio", audioBytes.Length);

            if (audioBytes.Length == 0)
            {
                return TtsResult.Fail("gTTS returned empty audio", Name, stopwatch.Elapsed);
            }

            _lastSuccessTime = DateTime.UtcNow;
            var audioData = CreateAudioData(audioBytes, request.Text, tempFile);

            // Estimate audio duration
            var audioDuration = EstimateAudioDuration(audioBytes.Length);

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
            _logger.LogError(ex, "Error generating audio with gTTS");
            return TtsResult.Fail($"Unexpected error: {ex.Message}", Name, stopwatch.Elapsed);
        }
        finally
        {
            // Cleanup temp file if in memory mode
            if (_outputConfig.Mode == AudioOutputMode.Memory)
            {
                try { if (File.Exists(tempFile)) File.Delete(tempFile); } catch { }
            }
        }
    }

    /// <inheritdoc />
    public Task<TtsProviderInfo> GetInfoAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult(new TtsProviderInfo
        {
            Name = Name,
            Status = IsAvailable ? ProviderStatus.Available : ProviderStatus.Unavailable,
            LastSuccessTime = _lastSuccessTime,
            SupportedVoices =
            [
                // gTTS uses language codes, not specific voices
                new Core.Models.VoiceInfo { Id = "cs", Language = "cs", DisplayName = "Czech (Female)", Gender = "Female" },
                new Core.Models.VoiceInfo { Id = "en", Language = "en", DisplayName = "English (Female)", Gender = "Female" },
                new Core.Models.VoiceInfo { Id = "de", Language = "de", DisplayName = "German (Female)", Gender = "Female" },
                new Core.Models.VoiceInfo { Id = "fr", Language = "fr", DisplayName = "French (Female)", Gender = "Female" },
                new Core.Models.VoiceInfo { Id = "es", Language = "es", DisplayName = "Spanish (Female)", Gender = "Female" }
            ]
        });
    }

    private bool CheckGttsInstalled()
    {
        try
        {
            using var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = _config.GttsCliPath,
                    Arguments = "--version",
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                }
            };

            process.Start();
            process.WaitForExit(5000);
            var isInstalled = process.ExitCode == 0;

            if (isInstalled)
            {
                _logger.LogInformation("gTTS is available");
            }
            else
            {
                _logger.LogWarning("gTTS is not available (install with: pip install gtts)");
            }

            return isInstalled;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to check gTTS availability");
            return false;
        }
    }

    private AudioData CreateAudioData(byte[] audioBytes, string text, string tempFile)
    {
        if (_outputConfig.Mode == AudioOutputMode.Memory)
        {
            return new MemoryAudioData { Data = audioBytes, ContentType = "audio/mpeg" };
        }

        // File mode - move temp file to output directory
        var directory = _outputConfig.OutputDirectory ?? Path.GetTempPath();
        Directory.CreateDirectory(directory);

        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text)))[..8];
        var timestamp = DateTime.UtcNow.ToString("yyyyMMdd_HHmmss");
        var fileName = _outputConfig.FileNamePattern
            .Replace("{provider}", Name)
            .Replace("{timestamp}", timestamp)
            .Replace("{hash}", hash);

        var filePath = Path.Combine(directory, fileName);

        // Move temp file to destination
        if (File.Exists(filePath))
        {
            File.Delete(filePath);
        }
        File.Move(tempFile, filePath);

        return new FileAudioData { FilePath = filePath };
    }

    private static TimeSpan EstimateAudioDuration(int audioBytes)
    {
        // Rough estimate for MP3: ~16kbps (gTTS uses relatively low bitrate) = 2000 bytes/second
        const int bytesPerSecond = 2000;
        var seconds = (double)audioBytes / bytesPerSecond;
        return TimeSpan.FromSeconds(seconds);
    }
}
