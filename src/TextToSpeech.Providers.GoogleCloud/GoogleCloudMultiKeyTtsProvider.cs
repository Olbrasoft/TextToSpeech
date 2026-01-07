using System.Diagnostics;
using System.Net;
using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using Olbrasoft.TextToSpeech.Core.Interfaces;
using Olbrasoft.TextToSpeech.Core.Models;

namespace Olbrasoft.TextToSpeech.Providers.GoogleCloud;

/// <summary>
/// TTS provider using Google Cloud Text-to-Speech API with multiple API key support.
/// Automatically rotates between keys when rate limits or quotas are exceeded.
/// </summary>
public sealed class GoogleCloudMultiKeyTtsProvider : ITtsProvider, IDisposable
{
    private readonly ILogger<GoogleCloudMultiKeyTtsProvider> _logger;
    private readonly GoogleCloudMultiKeyConfiguration _config;
    private readonly HttpClient _httpClient;
    private readonly bool _ownsHttpClient;
    private readonly List<ApiKeyStatus> _keyStatuses;
    private readonly object _lock = new();
    private DateTime? _lastSuccessTime;

    /// <summary>
    /// Initializes a new instance of GoogleCloudMultiKeyTtsProvider.
    /// </summary>
    /// <param name="options">Configuration options.</param>
    /// <param name="configuration">Configuration containing resolved secret values from SecureStore.</param>
    /// <param name="logger">Logger instance.</param>
    /// <param name="httpClient">Optional HTTP client for testing.</param>
    /// <exception cref="InvalidOperationException">Thrown when a secret key is not found in configuration.</exception>
    public GoogleCloudMultiKeyTtsProvider(
        IOptions<GoogleCloudMultiKeyConfiguration> options,
        IConfiguration configuration,
        ILogger<GoogleCloudMultiKeyTtsProvider> logger,
        HttpClient? httpClient = null)
    {
        _config = options.Value;
        _logger = logger;

        // Track ownership for proper disposal (don't dispose externally-provided HttpClient)
        _ownsHttpClient = httpClient == null;

        // Validate configuration BEFORE creating HttpClient to avoid resource leaks
        var keyStatuses = _config.ApiKeySecrets
            .Select((keyConfig, index) =>
            {
                var actualKey = configuration[keyConfig.SecretKey];
                if (string.IsNullOrEmpty(actualKey))
                {
                    throw new InvalidOperationException(
                        $"Secret '{keyConfig.SecretKey}' not found in configuration. " +
                        "Ensure SecureStore is configured and the key exists in the vault.");
                }

                return new ApiKeyStatus
                {
                    Index = index,
                    Name = keyConfig.Name,
                    ActualKey = actualKey,
                    State = ApiKeyState.Available
                };
            })
            .ToList();

        // Only create/configure HttpClient after validation passes
        _httpClient = httpClient ?? new HttpClient();
        _httpClient.Timeout = _config.Timeout;
        _keyStatuses = keyStatuses;

        _logger.LogInformation(
            "GoogleCloudMultiKeyTtsProvider initialized with {KeyCount} API keys",
            _keyStatuses.Count);
    }

    /// <inheritdoc />
    public string Name => "GoogleCloudMultiKey";

    /// <inheritdoc />
    public async Task<TtsResult> SynthesizeAsync(TtsRequest request, CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();

        if (_keyStatuses.Count == 0)
        {
            _logger.LogError("No API keys configured for GoogleCloudMultiKey provider");
            return TtsResult.Fail("No API keys configured", Name, stopwatch.Elapsed);
        }

        // Try each available key until one succeeds
        // Max iterations = key count + 1 (safety margin to prevent infinite loops)
        var maxIterations = _keyStatuses.Count + 1;
        var iteration = 0;

        while (iteration++ < maxIterations)
        {
            var keyStatus = GetNextAvailableKey();
            if (keyStatus == null)
            {
                stopwatch.Stop();
                _logger.LogError("All Google Cloud TTS API keys exhausted");
                return TtsResult.Fail("All API keys exhausted or unavailable", Name, stopwatch.Elapsed);
            }

            _logger.LogDebug(
                "Using Google Cloud TTS key #{Index} ({Name})",
                keyStatus.Index, keyStatus.Name);

            var result = await TrySynthesizeWithKeyAsync(keyStatus, request, stopwatch, cancellationToken);

            if (result != null)
            {
                return result;
            }

            // result is null means we should try the next key
        }

        // Should never reach here, but safety fallback
        stopwatch.Stop();
        _logger.LogError("Max iterations reached in SynthesizeAsync - unexpected state");
        return TtsResult.Fail("Internal error: max iterations exceeded", Name, stopwatch.Elapsed);
    }

    private async Task<TtsResult?> TrySynthesizeWithKeyAsync(
        ApiKeyStatus keyStatus,
        TtsRequest request,
        Stopwatch stopwatch,
        CancellationToken cancellationToken)
    {
        try
        {
            var voice = !string.IsNullOrEmpty(request.Voice) ? request.Voice : _config.Voice;
            var speakingRate = CalculateSpeakingRate(request.Rate);
            var pitch = CalculatePitch(request.Pitch);

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
            using var content = new StringContent(json, Encoding.UTF8, "application/json");

            var url = $"{GoogleCloudMultiKeyConfiguration.ApiEndpoint}?key={keyStatus.ActualKey}";
            var response = await _httpClient.PostAsync(url, content, cancellationToken);

            return await HandleResponseAsync(keyStatus, response, stopwatch, cancellationToken);
        }
        catch (TaskCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (HttpRequestException ex)
        {
            _logger.LogWarning(
                ex,
                "HTTP error with key #{Index} ({Name}), trying next key",
                keyStatus.Index, keyStatus.Name);
            return null; // Try next key
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Unexpected error with key #{Index} ({Name})",
                keyStatus.Index, keyStatus.Name);
            return TtsResult.Fail($"Unexpected error: {ex.Message}", Name, stopwatch.Elapsed);
        }
    }

    private async Task<TtsResult?> HandleResponseAsync(
        ApiKeyStatus keyStatus,
        HttpResponseMessage response,
        Stopwatch stopwatch,
        CancellationToken cancellationToken)
    {
        var statusCode = response.StatusCode;

        switch (statusCode)
        {
            case HttpStatusCode.OK:
                return await HandleSuccessAsync(keyStatus, response, stopwatch, cancellationToken);

            case HttpStatusCode.TooManyRequests: // 429
                MarkKeyAsRateLimited(keyStatus);
                return null; // Try next key

            case HttpStatusCode.Forbidden: // 403
                MarkKeyAsQuotaExceeded(keyStatus);
                return null; // Try next key

            case HttpStatusCode.Unauthorized: // 401
                MarkKeyAsInvalid(keyStatus);
                return null; // Try next key

            case >= HttpStatusCode.InternalServerError: // 5xx
                _logger.LogWarning(
                    "Server error {StatusCode} with key #{Index} ({Name}), trying next key",
                    (int)statusCode, keyStatus.Index, keyStatus.Name);
                MarkKeyAsTemporaryError(keyStatus);
                return null; // Try next key

            default:
                var errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
                _logger.LogWarning(
                    "API error {StatusCode} with key #{Index} ({Name}), trying next key: {Error}",
                    (int)statusCode, keyStatus.Index, keyStatus.Name, errorContent);
                MarkKeyAsTemporaryError(keyStatus);
                return null; // Try next key on any error
        }
    }

    private async Task<TtsResult> HandleSuccessAsync(
        ApiKeyStatus keyStatus,
        HttpResponseMessage response,
        Stopwatch stopwatch,
        CancellationToken cancellationToken)
    {
        var responseJson = await response.Content.ReadAsStringAsync(cancellationToken);
        var responseObj = JsonConvert.DeserializeObject<dynamic>(responseJson);

        if (responseObj?.audioContent == null)
        {
            _logger.LogError("No audio content received from Google Cloud TTS API");
            return TtsResult.Fail("No audio content received", Name, stopwatch.Elapsed);
        }

        string audioContentBase64 = responseObj.audioContent;
        var audioBytes = Convert.FromBase64String(audioContentBase64);

        stopwatch.Stop();

        _logger.LogInformation(
            "Google Cloud TTS synthesis successful with key #{Index} ({Name}): {Bytes} bytes in {Ms}ms",
            keyStatus.Index, keyStatus.Name, audioBytes.Length, stopwatch.ElapsedMilliseconds);

        lock (_lock)
        {
            _lastSuccessTime = DateTime.UtcNow;
        }

        var audioData = new MemoryAudioData
        {
            Data = audioBytes,
            ContentType = _config.AudioEncoding == "MP3" ? "audio/mpeg" : "audio/wav"
        };

        return TtsResult.Ok(audioData, Name, stopwatch.Elapsed);
    }

    private ApiKeyStatus? GetNextAvailableKey()
    {
        lock (_lock)
        {
            var now = DateTime.UtcNow;

            // Filter out permanently invalid keys using explicit Where()
            foreach (var key in _keyStatuses.Where(k => k.State != ApiKeyState.Invalid))
            {
                // Check if key is available
                if (key.State == ApiKeyState.Available)
                    return key;

                // Check if cooldown expired
                if (key.CooldownUntil.HasValue && key.CooldownUntil.Value <= now)
                {
                    _logger.LogInformation(
                        "Key #{Index} ({Name}) cooldown expired, marking as available",
                        key.Index, key.Name);

                    key.State = ApiKeyState.Available;
                    key.CooldownUntil = null;
                    return key;
                }
            }

            return null; // All keys exhausted
        }
    }

    private void MarkKeyAsRateLimited(ApiKeyStatus keyStatus)
    {
        lock (_lock)
        {
            var cooldownUntil = DateTime.UtcNow.Add(_config.RateLimitCooldown);
            keyStatus.State = ApiKeyState.RateLimited;
            keyStatus.CooldownUntil = cooldownUntil;

            _logger.LogWarning(
                "Key #{Index} ({Name}) rate limited (429), marked as {State} until {Until:O}",
                keyStatus.Index, keyStatus.Name, keyStatus.State, cooldownUntil);
        }
    }

    private void MarkKeyAsQuotaExceeded(ApiKeyStatus keyStatus)
    {
        lock (_lock)
        {
            var cooldownUntil = DateTime.UtcNow.Add(_config.QuotaExceededCooldown);
            keyStatus.State = ApiKeyState.QuotaExceeded;
            keyStatus.CooldownUntil = cooldownUntil;

            _logger.LogWarning(
                "Key #{Index} ({Name}) quota exceeded (403), marked as {State} until {Until:O}",
                keyStatus.Index, keyStatus.Name, keyStatus.State, cooldownUntil);
        }
    }

    private void MarkKeyAsInvalid(ApiKeyStatus keyStatus)
    {
        lock (_lock)
        {
            keyStatus.State = ApiKeyState.Invalid;
            keyStatus.CooldownUntil = null;

            _logger.LogError(
                "Key #{Index} ({Name}) is invalid (401), permanently disabled",
                keyStatus.Index, keyStatus.Name);
        }
    }

    private void MarkKeyAsTemporaryError(ApiKeyStatus keyStatus)
    {
        lock (_lock)
        {
            // Short cooldown to allow trying other keys in this request
            // but allow retry in subsequent requests
            var cooldownUntil = DateTime.UtcNow.AddSeconds(5);
            keyStatus.State = ApiKeyState.TemporaryError;
            keyStatus.CooldownUntil = cooldownUntil;

            _logger.LogDebug(
                "Key #{Index} ({Name}) marked as temporary error until {Until:O}",
                keyStatus.Index, keyStatus.Name, cooldownUntil);
        }
    }

    /// <summary>
    /// Calculates speaking rate from integer rate (-100 to +100).
    /// Maps: -100 -> 0.25, 0 -> 1.0, +100 -> 4.0
    /// </summary>
    private double CalculateSpeakingRate(int rate)
    {
        if (rate == 0) return _config.SpeakingRate;

        var normalized = rate / 100.0;
        return normalized >= 0
            ? 1.0 + (normalized * 3.0)
            : 1.0 + (normalized * 0.75);
    }

    /// <summary>
    /// Calculates pitch from integer pitch (-100 to +100).
    /// Maps: -100 -> -20.0, 0 -> 0.0, +100 -> +20.0
    /// </summary>
    private double CalculatePitch(int pitch)
    {
        if (pitch == 0) return _config.Pitch;
        return (pitch / 100.0) * 20.0;
    }

    /// <summary>
    /// Extracts language code from voice name (e.g., "cs-CZ-Chirp3-HD-Achird" -> "cs-CZ").
    /// </summary>
    private static string ExtractLanguageCode(string voice)
    {
        var parts = voice.Split('-');
        return parts.Length >= 2 ? $"{parts[0]}-{parts[1]}" : "cs-CZ";
    }

    /// <inheritdoc />
    public Task<TtsProviderInfo> GetInfoAsync(CancellationToken cancellationToken = default)
    {
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

        // Determine status based on available keys
        // Note: _keyStatuses is immutable after construction, but we use lock for consistent state
        ProviderStatus status;
        DateTime? lastSuccess;
        lock (_lock)
        {
            var totalKeys = _keyStatuses.Count;
            var availableKeys = _keyStatuses.Count(k =>
                k.State == ApiKeyState.Available ||
                (k.CooldownUntil.HasValue && k.CooldownUntil.Value <= DateTime.UtcNow));

            status = totalKeys == 0
                ? ProviderStatus.Unavailable
                : availableKeys > 0
                    ? ProviderStatus.Available
                    : ProviderStatus.Degraded;

            lastSuccess = _lastSuccessTime;
        }

        return Task.FromResult(new TtsProviderInfo
        {
            Name = Name,
            Status = status,
            LastSuccessTime = lastSuccess,
            SupportedVoices = voices
        });
    }

    /// <inheritdoc />
    public void Dispose()
    {
        // Only dispose HttpClient if we created it (not externally provided)
        if (_ownsHttpClient)
        {
            _httpClient?.Dispose();
        }
    }

    /// <summary>
    /// Internal class tracking the state of a single API key.
    /// </summary>
    private sealed class ApiKeyStatus
    {
        public int Index { get; init; }
        public required string Name { get; init; }
        public required string ActualKey { get; init; }
        public ApiKeyState State { get; set; } = ApiKeyState.Available;
        public DateTime? CooldownUntil { get; set; }
    }
}
