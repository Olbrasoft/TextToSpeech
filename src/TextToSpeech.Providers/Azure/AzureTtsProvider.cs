using System.Diagnostics;
using Microsoft.CognitiveServices.Speech;
using Microsoft.CognitiveServices.Speech.Audio;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Olbrasoft.TextToSpeech.Core.Interfaces;
using Olbrasoft.TextToSpeech.Core.Models;
using Olbrasoft.TextToSpeech.Core.Services;
using Olbrasoft.TextToSpeech.Providers.Configuration;

namespace Olbrasoft.TextToSpeech.Providers.Azure;

/// <summary>
/// TTS provider using Azure Cognitive Services Speech.
/// High-quality neural voices with SSML support.
/// </summary>
public sealed class AzureTtsProvider : ITtsProvider, IDisposable
{
    private readonly ILogger<AzureTtsProvider> _logger;
    private readonly AzureTtsConfiguration _config;
    private readonly IOutputConfiguration _outputConfig;
    private readonly IAudioDataFactory _audioDataFactory;
    private readonly string _subscriptionKey;
    private readonly string _region;
    private SpeechConfig? _speechConfig;
    private bool _isConfigured;
    private bool _disposed;
    private DateTime? _lastSuccessTime;

    /// <summary>
    /// Initializes a new instance of AzureTtsProvider.
    /// </summary>
    public AzureTtsProvider(
        ILogger<AzureTtsProvider> logger,
        IOptions<AzureTtsConfiguration> config,
        IOutputConfiguration outputConfig,
        IAudioDataFactory audioDataFactory)
    {
        _logger = logger;
        _config = config.Value;
        _outputConfig = outputConfig;
        _audioDataFactory = audioDataFactory;

        // Get subscription key from options or environment variable
        _subscriptionKey = !string.IsNullOrEmpty(_config.SubscriptionKey)
            ? _config.SubscriptionKey
            : Environment.GetEnvironmentVariable("AZURE_SPEECH_KEY") ?? string.Empty;

        // Get region from options or environment variable
        _region = !string.IsNullOrEmpty(_config.Region) && _config.Region != "westeurope"
            ? _config.Region
            : Environment.GetEnvironmentVariable("AZURE_SPEECH_REGION") ?? _config.Region;

        // Validate configuration
        _isConfigured = !string.IsNullOrEmpty(_subscriptionKey) && !string.IsNullOrEmpty(_region);

        if (_isConfigured)
        {
            try
            {
                _speechConfig = SpeechConfig.FromSubscription(_subscriptionKey, _region);

                // Get voice from options or environment variable
                var voice = !string.IsNullOrEmpty(_config.Voice)
                    ? _config.Voice
                    : Environment.GetEnvironmentVariable("AZURE_SPEECH_VOICE") ?? "cs-CZ-AntoninNeural";

                _speechConfig.SpeechSynthesisVoiceName = voice;

                // Set output format
                _speechConfig.SetSpeechSynthesisOutputFormat(GetOutputFormat(_config.OutputFormat));

                _logger.LogInformation("Azure TTS configured: region={Region}, voice={Voice}", _region, voice);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to configure Azure Speech SDK");
                _isConfigured = false;
            }
        }
        else
        {
            _logger.LogWarning("Azure TTS not configured - missing AZURE_SPEECH_KEY or AZURE_SPEECH_REGION");
        }
    }

    /// <inheritdoc />
    public string Name => "AzureTTS";

    /// <summary>
    /// Gets whether the provider is available (configured).
    /// </summary>
    public bool IsAvailable => _isConfigured && _speechConfig != null;

    /// <inheritdoc />
    public async Task<TtsResult> SynthesizeAsync(TtsRequest request, CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();

        if (!IsAvailable || _speechConfig == null)
        {
            _logger.LogWarning("Azure TTS not available - not configured");
            return TtsResult.Fail("Azure TTS not configured", Name, stopwatch.Elapsed);
        }

        try
        {
            // Create SSML with voice config
            var ssml = CreateSsml(request);

            _logger.LogDebug("Azure TTS generating audio for: {Text}",
                request.Text.Length > 50 ? request.Text[..50] + "..." : request.Text);
            _logger.LogTrace("Azure TTS SSML: {Ssml}", ssml);

            // Use pull audio output stream to get raw audio bytes
            using var audioStream = AudioOutputStream.CreatePullStream();
            using var audioConfig = AudioConfig.FromStreamOutput(audioStream);
            using var synthesizer = new SpeechSynthesizer(_speechConfig, audioConfig);

            // Synthesize with SSML
            using var result = await synthesizer.SpeakSsmlAsync(ssml);

            stopwatch.Stop();

            if (result.Reason == ResultReason.SynthesizingAudioCompleted)
            {
                var audioBytes = result.AudioData;
                _logger.LogDebug("Azure TTS generated {Bytes} bytes of audio", audioBytes.Length);

                _lastSuccessTime = DateTime.UtcNow;
                var audioData = _audioDataFactory.Create(
                    audioBytes,
                    request.Text,
                    Name,
                    _outputConfig.Mode,
                    _outputConfig.OutputDirectory,
                    _outputConfig.FileNamePattern);

                // Estimate audio duration (rough estimate based on MP3 bitrate)
                var audioDuration = EstimateAudioDuration(audioBytes.Length);

                return TtsResult.Ok(audioData, Name, stopwatch.Elapsed, audioDuration);
            }

            if (result.Reason == ResultReason.Canceled)
            {
                var cancellation = SpeechSynthesisCancellationDetails.FromResult(result);

                if (cancellation.Reason == CancellationReason.Error)
                {
                    _logger.LogError("Azure TTS error: {ErrorCode} - {ErrorDetails}",
                        cancellation.ErrorCode, cancellation.ErrorDetails);
                    return TtsResult.Fail($"Azure error: {cancellation.ErrorCode} - {cancellation.ErrorDetails}", Name, stopwatch.Elapsed);
                }

                _logger.LogWarning("Azure TTS cancelled: {Reason}", cancellation.Reason);
                return TtsResult.Fail($"Azure TTS cancelled: {cancellation.Reason}", Name, stopwatch.Elapsed);
            }

            _logger.LogWarning("Azure TTS unexpected result: {Reason}", result.Reason);
            return TtsResult.Fail($"Unexpected result: {result.Reason}", Name, stopwatch.Elapsed);
        }
        catch (OperationCanceledException)
        {
            stopwatch.Stop();
            return TtsResult.Fail("Operation cancelled", Name, stopwatch.Elapsed);
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            _logger.LogError(ex, "Error generating audio with Azure TTS");
            return TtsResult.Fail($"Unexpected error: {ex.Message}", Name, stopwatch.Elapsed);
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
                new Core.Models.VoiceInfo { Id = "cs-CZ-AntoninNeural", Language = "cs-CZ", DisplayName = "Antonín", Gender = "Male" },
                new Core.Models.VoiceInfo { Id = "cs-CZ-VlastaNeural", Language = "cs-CZ", DisplayName = "Vlasta", Gender = "Female" },
                new Core.Models.VoiceInfo { Id = "en-US-GuyNeural", Language = "en-US", DisplayName = "Guy", Gender = "Male" },
                new Core.Models.VoiceInfo { Id = "en-US-JennyNeural", Language = "en-US", DisplayName = "Jenny", Gender = "Female" },
                new Core.Models.VoiceInfo { Id = "en-GB-RyanNeural", Language = "en-GB", DisplayName = "Ryan", Gender = "Male" },
                new Core.Models.VoiceInfo { Id = "de-DE-ConradNeural", Language = "de-DE", DisplayName = "Conrad", Gender = "Male" }
            ]
        });
    }

    /// <summary>
    /// Creates SSML markup with voice configuration.
    /// Uses configuration values as defaults if request doesn't specify them.
    /// </summary>
    private string CreateSsml(TtsRequest request)
    {
        // Use request rate/pitch if specified, otherwise use config
        var rate = request.Rate != 0
            ? $"{request.Rate:+#;-#;0}%"
            : (!string.IsNullOrEmpty(_config.Rate) ? _config.Rate : "default");

        var pitch = request.Pitch != 0
            ? $"{request.Pitch:+#;-#;0}Hz"
            : (!string.IsNullOrEmpty(_config.Pitch) ? _config.Pitch : "default");

        // Escape XML special characters in text
        var escapedText = System.Security.SecurityElement.Escape(request.Text) ?? request.Text;

        // Get voice from request or use config/default
        var voice = !string.IsNullOrEmpty(request.Voice)
            ? request.Voice
            : _speechConfig?.SpeechSynthesisVoiceName ?? _config.Voice;

        // Determine language from voice name
        var lang = voice.Split('-').Length >= 2 ? $"{voice.Split('-')[0]}-{voice.Split('-')[1]}" : "cs-CZ";

        return $@"<speak version='1.0' xmlns='http://www.w3.org/2001/10/synthesis' xml:lang='{lang}'>
    <voice name='{voice}'>
        <prosody rate='{rate}' pitch='{pitch}'>
            {escapedText}
        </prosody>
    </voice>
</speak>";
    }

    private static TimeSpan EstimateAudioDuration(int audioBytes)
    {
        // Rough estimate: 48kbps MP3 = 6000 bytes/second
        const int bytesPerSecond = 6000;
        var seconds = (double)audioBytes / bytesPerSecond;
        return TimeSpan.FromSeconds(seconds);
    }

    private static SpeechSynthesisOutputFormat GetOutputFormat(string format)
    {
        return format switch
        {
            "Audio16Khz32KBitRateMonoMp3" => SpeechSynthesisOutputFormat.Audio16Khz32KBitRateMonoMp3,
            "Audio16Khz64KBitRateMonoMp3" => SpeechSynthesisOutputFormat.Audio16Khz64KBitRateMonoMp3,
            "Audio24Khz48KBitRateMonoMp3" => SpeechSynthesisOutputFormat.Audio24Khz48KBitRateMonoMp3,
            "Audio24Khz96KBitRateMonoMp3" => SpeechSynthesisOutputFormat.Audio24Khz96KBitRateMonoMp3,
            "Audio48Khz96KBitRateMonoMp3" => SpeechSynthesisOutputFormat.Audio48Khz96KBitRateMonoMp3,
            "Audio48Khz192KBitRateMonoMp3" => SpeechSynthesisOutputFormat.Audio48Khz192KBitRateMonoMp3,
            "Riff16Khz16BitMonoPcm" => SpeechSynthesisOutputFormat.Riff16Khz16BitMonoPcm,
            "Riff24Khz16BitMonoPcm" => SpeechSynthesisOutputFormat.Riff24Khz16BitMonoPcm,
            _ => SpeechSynthesisOutputFormat.Audio24Khz48KBitRateMonoMp3
        };
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        // SpeechConfig doesn't implement IDisposable
    }
}
