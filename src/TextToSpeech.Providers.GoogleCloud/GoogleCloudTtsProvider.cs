using System.Diagnostics;
using System.Text;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using Olbrasoft.TextToSpeech.Core.Interfaces;
using Olbrasoft.TextToSpeech.Core.Models;

namespace Olbrasoft.TextToSpeech.Providers.GoogleCloud;

/// <summary>
/// TTS provider using Google Cloud Text-to-Speech API.
/// Supports high-quality Chirp3-HD voices including Czech male voices.
/// Requires a Google Cloud API key.
/// </summary>
public sealed class GoogleCloudTtsProvider : ITtsProvider, IDisposable
{
    private readonly ILogger<GoogleCloudTtsProvider> _logger;
    private readonly GoogleCloudTtsConfiguration _config;
    private readonly HttpClient _httpClient;
    private DateTime? _lastSuccessTime;

    /// <summary>
    /// Initializes a new instance of GoogleCloudTtsProvider.
    /// </summary>
    public GoogleCloudTtsProvider(
        ILogger<GoogleCloudTtsProvider> logger,
        IOptions<GoogleCloudTtsConfiguration> config,
        HttpClient? httpClient = null)
    {
        _logger = logger;
        _config = config.Value;
        _httpClient = httpClient ?? new HttpClient();
        _httpClient.Timeout = _config.Timeout;
    }

    /// <inheritdoc />
    public string Name => "GoogleCloud";

    /// <inheritdoc />
    public async Task<TtsResult> SynthesizeAsync(TtsRequest request, CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();

        try
        {
            if (string.IsNullOrEmpty(_config.ApiKey))
            {
                _logger.LogError("Google Cloud API key is not configured");
                return TtsResult.Fail("Google Cloud API key is not configured", Name, stopwatch.Elapsed);
            }

            var voice = !string.IsNullOrEmpty(request.Voice) ? request.Voice : _config.Voice;
            var speakingRate = CalculateSpeakingRate(request.Rate);
            var pitch = CalculatePitch(request.Pitch);

            _logger.LogDebug(
                "Synthesizing with Google Cloud TTS: Voice={Voice}, Rate={Rate}, Pitch={Pitch}",
                voice, speakingRate, pitch);

            // Build request body
            var requestBody = new
            {
                input = new { text = request.Text },
                voice = new
                {
                    languageCode = ExtractLanguageCode(voice),
                    name = voice
                },
                audioConfig = new
                {
                    audioEncoding = _config.AudioEncoding,
                    speakingRate,
                    pitch,
                    volumeGainDb = _config.VolumeGainDb,
                    sampleRateHertz = _config.SampleRateHertz
                }
            };

            var json = JsonConvert.SerializeObject(requestBody);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            // Call Google Cloud TTS API
            var url = $"{GoogleCloudTtsConfiguration.API_ENDPOINT}?key={_config.ApiKey}";
            var response = await _httpClient.PostAsync(url, content, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
                _logger.LogError(
                    "Google Cloud TTS API error: {StatusCode} - {Error}",
                    response.StatusCode, errorContent);
                return TtsResult.Fail(
                    $"API error: {response.StatusCode}",
                    Name,
                    stopwatch.Elapsed);
            }

            var responseJson = await response.Content.ReadAsStringAsync(cancellationToken);
            var responseObj = JsonConvert.DeserializeObject<dynamic>(responseJson);

            if (responseObj?.audioContent == null)
            {
                _logger.LogError("No audio content received from Google Cloud TTS API");
                return TtsResult.Fail("No audio content received", Name, stopwatch.Elapsed);
            }

            string audioContentBase64 = responseObj.audioContent;
            byte[] audioBytes = Convert.FromBase64String(audioContentBase64);

            stopwatch.Stop();

            _logger.LogInformation(
                "Google Cloud TTS synthesis successful: {Bytes} bytes in {Ms}ms",
                audioBytes.Length, stopwatch.ElapsedMilliseconds);

            _lastSuccessTime = DateTime.UtcNow;

            // Return audio data in memory
            var audioData = new MemoryAudioData
            {
                Data = audioBytes,
                ContentType = _config.AudioEncoding == "MP3" ? "audio/mpeg" : "audio/wav"
            };

            return TtsResult.Ok(audioData, Name, stopwatch.Elapsed);
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            _logger.LogError(ex, "Error generating audio with Google Cloud TTS provider");
            return TtsResult.Fail($"Google Cloud TTS error: {ex.Message}", Name, stopwatch.Elapsed);
        }
    }

    /// <inheritdoc />
    public Task<TtsProviderInfo> GetInfoAsync(CancellationToken cancellationToken = default)
    {
        // 16 Czech male Chirp3-HD voices
        var czechMaleVoices = new[]
        {
            "cs-CZ-Chirp3-HD-Achird", "cs-CZ-Chirp3-HD-Algenib", "cs-CZ-Chirp3-HD-Algieba",
            "cs-CZ-Chirp3-HD-Alnilam", "cs-CZ-Chirp3-HD-Charon", "cs-CZ-Chirp3-HD-Enceladus",
            "cs-CZ-Chirp3-HD-Fenrir", "cs-CZ-Chirp3-HD-Iapetus", "cs-CZ-Chirp3-HD-Orus",
            "cs-CZ-Chirp3-HD-Puck", "cs-CZ-Chirp3-HD-Rasalgethi", "cs-CZ-Chirp3-HD-Sadachbia",
            "cs-CZ-Chirp3-HD-Sadaltager", "cs-CZ-Chirp3-HD-Schedar", "cs-CZ-Chirp3-HD-Umbriel",
            "cs-CZ-Chirp3-HD-Zubenelgenubi"
        };

        var voices = czechMaleVoices.Select(v => new VoiceInfo
        {
            Id = v,
            Language = "cs-CZ",
            DisplayName = v.Split('-').Last(),
            Gender = "Male"
        }).ToList();

        return Task.FromResult(new TtsProviderInfo
        {
            Name = Name,
            Status = string.IsNullOrEmpty(_config.ApiKey)
                ? ProviderStatus.Unavailable
                : ProviderStatus.Available,
            LastSuccessTime = _lastSuccessTime,
            SupportedVoices = voices
        });
    }

    /// <summary>
    /// Calculates speaking rate from integer rate (-100 to +100).
    /// Maps: -100 → 0.25, 0 → 1.0, +100 → 4.0
    /// </summary>
    private double CalculateSpeakingRate(int rate)
    {
        if (rate == 0) return _config.SpeakingRate;

        // Map -100..+100 to 0.25..4.0
        // rate = 0 -> 1.0
        // rate = 100 -> 4.0
        // rate = -100 -> 0.25
        var normalized = rate / 100.0; // -1.0 to +1.0
        return normalized >= 0
            ? 1.0 + (normalized * 3.0)  // 1.0 to 4.0
            : 1.0 + (normalized * 0.75); // 0.25 to 1.0
    }

    /// <summary>
    /// Calculates pitch from integer pitch (-100 to +100).
    /// Maps: -100 → -20.0, 0 → 0.0, +100 → +20.0
    /// </summary>
    private double CalculatePitch(int pitch)
    {
        if (pitch == 0) return _config.Pitch;

        // Map -100..+100 to -20.0..+20.0 semitones
        return (pitch / 100.0) * 20.0;
    }

    /// <summary>
    /// Extracts language code from voice name (e.g., "cs-CZ-Chirp3-HD-Achird" → "cs-CZ").
    /// </summary>
    private static string ExtractLanguageCode(string voice)
    {
        var parts = voice.Split('-');
        return parts.Length >= 2 ? $"{parts[0]}-{parts[1]}" : "cs-CZ";
    }

    public void Dispose()
    {
        _httpClient?.Dispose();
    }
}
