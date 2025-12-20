using System.Diagnostics;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Olbrasoft.TextToSpeech.Core.Interfaces;
using Olbrasoft.TextToSpeech.Core.Models;

namespace Olbrasoft.TextToSpeech.Providers.EdgeTTS;

/// <summary>
/// TTS provider using direct Microsoft Edge TTS WebSocket API.
/// Communicates directly with Microsoft's speech synthesis service via WebSocket.
/// </summary>
public sealed class EdgeTtsProvider : ITtsProvider, IDisposable
{
    private readonly ILogger<EdgeTtsProvider> _logger;
    private readonly EdgeTtsConfiguration _config;
    private DateTime? _lastSuccessTime;

    /// <summary>
    /// Initializes a new instance of EdgeTtsProvider.
    /// </summary>
    public EdgeTtsProvider(
        ILogger<EdgeTtsProvider> logger,
        IOptions<EdgeTtsConfiguration> config)
    {
        _logger = logger;
        _config = config.Value;
    }

    /// <inheritdoc />
    public string Name => "EdgeTTS-WebSocket";

    /// <inheritdoc />
    public async Task<TtsResult> SynthesizeAsync(TtsRequest request, CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();

        try
        {
            var voice = !string.IsNullOrEmpty(request.Voice) ? request.Voice : _config.Voice;
            var rate = request.Rate != 0 ? FormatRate(request.Rate) : _config.Rate;
            var pitch = request.Pitch != 0 ? FormatPitch(request.Pitch) : _config.Pitch;

            _logger.LogDebug("Synthesizing with EdgeTTS: Voice={Voice}, Rate={Rate}, Pitch={Pitch}",
                voice, rate, pitch);

            using var client = new EdgeTtsWebSocketClient(_config);
            var audioBytes = await client.GenerateAudioAsync(
                request.Text,
                voice,
                rate,
                _config.Volume,
                pitch,
                cancellationToken);

            stopwatch.Stop();

            if (audioBytes == null || audioBytes.Length == 0)
            {
                _logger.LogWarning("EdgeTTS returned no audio data");
                return TtsResult.Fail("No audio data received from EdgeTTS", Name, stopwatch.Elapsed);
            }

            _logger.LogInformation("EdgeTTS synthesis successful: {Bytes} bytes in {Ms}ms",
                audioBytes.Length, stopwatch.ElapsedMilliseconds);

            _lastSuccessTime = DateTime.UtcNow;

            // Return audio data in memory
            var audioData = new MemoryAudioData
            {
                Data = audioBytes,
                ContentType = "audio/mpeg"
            };

            return TtsResult.Ok(audioData, Name, stopwatch.Elapsed);
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            _logger.LogError(ex, "Error generating audio with EdgeTTS provider");
            return TtsResult.Fail($"EdgeTTS error: {ex.Message}", Name, stopwatch.Elapsed);
        }
    }

    /// <inheritdoc />
    public Task<TtsProviderInfo> GetInfoAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult(new TtsProviderInfo
        {
            Name = Name,
            Status = ProviderStatus.Available,
            LastSuccessTime = _lastSuccessTime,
            SupportedVoices =
            [
                new VoiceInfo { Id = "cs-CZ-AntoninNeural", Language = "cs-CZ", DisplayName = "Antonín", Gender = "Male" },
                new VoiceInfo { Id = "cs-CZ-VlastaNeural", Language = "cs-CZ", DisplayName = "Vlasta", Gender = "Female" },
                new VoiceInfo { Id = "en-US-GuyNeural", Language = "en-US", DisplayName = "Guy", Gender = "Male" },
                new VoiceInfo { Id = "en-US-JennyNeural", Language = "en-US", DisplayName = "Jenny", Gender = "Female" }
            ]
        });
    }

    /// <summary>
    /// Formats rate integer to EdgeTTS string format (e.g., 15 → "+15%", -10 → "-10%").
    /// </summary>
    private static string FormatRate(int rate) =>
        rate >= 0 ? $"+{rate}%" : $"{rate}%";

    /// <summary>
    /// Formats pitch integer to EdgeTTS string format (e.g., 5 → "+5Hz", -5 → "-5Hz").
    /// </summary>
    private static string FormatPitch(int pitch) =>
        pitch >= 0 ? $"+{pitch}Hz" : $"{pitch}Hz";

    public void Dispose()
    {
        // Nothing to dispose currently - EdgeTtsWebSocketClient is used per request
    }
}
