using System.Diagnostics;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Olbrasoft.TextToSpeech.Core.Interfaces;
using Olbrasoft.TextToSpeech.Core.Models;
using Olbrasoft.TextToSpeech.Core.Services;
using Olbrasoft.TextToSpeech.Providers.Configuration;

namespace Olbrasoft.TextToSpeech.Providers.VoiceRss;

/// <summary>
/// TTS provider using VoiceRSS API.
/// Czech male voice "Josef", requires API key (free tier: 350 requests/day).
/// </summary>
public sealed class VoiceRssProvider : ITtsProvider
{
    private const string ApiBaseUrl = "https://api.voicerss.org/";

    private readonly ILogger<VoiceRssProvider> _logger;
    private readonly HttpClient _httpClient;
    private readonly VoiceRssConfiguration _config;
    private readonly IOutputConfiguration _outputConfig;
    private readonly IAudioDataFactory _audioDataFactory;
    private readonly string? _apiKey;
    private DateTime? _lastSuccessTime;

    /// <summary>
    /// Initializes a new instance of VoiceRssProvider.
    /// </summary>
    public VoiceRssProvider(
        ILogger<VoiceRssProvider> logger,
        IHttpClientFactory httpClientFactory,
        IOptions<VoiceRssConfiguration> config,
        IOutputConfiguration outputConfig,
        IAudioDataFactory audioDataFactory)
    {
        _logger = logger;
        _httpClient = httpClientFactory.CreateClient("VoiceRSS");
        _config = config.Value;
        _outputConfig = outputConfig;
        _audioDataFactory = audioDataFactory;
        _apiKey = LoadApiKey();
    }

    /// <inheritdoc />
    public string Name => "VoiceRSS";

    /// <summary>
    /// Gets whether the provider is available (has API key).
    /// </summary>
    public bool IsAvailable => !string.IsNullOrEmpty(_apiKey);

    /// <inheritdoc />
    public async Task<TtsResult> SynthesizeAsync(TtsRequest request, CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();

        if (string.IsNullOrEmpty(_apiKey))
        {
            _logger.LogWarning("VoiceRSS API key not configured");
            return TtsResult.Fail("VoiceRSS API key not configured", Name, stopwatch.Elapsed);
        }

        try
        {
            // Convert rate from -100/+100 to VoiceRSS -10/+10 scale
            var speed = _config.Speed + (request.Rate / 10);
            speed = Math.Clamp(speed, -10, 10);

            // Build query parameters
            var queryParams = new Dictionary<string, string>
            {
                ["key"] = _apiKey,
                ["hl"] = _config.Language,
                ["v"] = request.Voice ?? _config.DefaultVoice,
                ["src"] = request.Text,
                ["c"] = _config.AudioCodec,
                ["f"] = _config.AudioFormat,
                ["r"] = speed.ToString()
            };

            var queryString = string.Join("&", queryParams.Select(kvp =>
                $"{Uri.EscapeDataString(kvp.Key)}={Uri.EscapeDataString(kvp.Value)}"));

            var url = $"{ApiBaseUrl}?{queryString}";

            _logger.LogDebug("Calling VoiceRSS API for text: {Text}",
                request.Text.Length > 50 ? request.Text[..50] + "..." : request.Text);

            var response = await _httpClient.GetAsync(url, cancellationToken);

            stopwatch.Stop();

            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync(cancellationToken);
                _logger.LogWarning("VoiceRSS API returned {Status}: {Error}", response.StatusCode, error);
                return TtsResult.Fail($"VoiceRSS API error: {response.StatusCode}", Name, stopwatch.Elapsed);
            }

            // Check content type - VoiceRSS returns error messages as text/plain
            var contentType = response.Content.Headers.ContentType?.MediaType;
            if (contentType == "text/plain")
            {
                var errorText = await response.Content.ReadAsStringAsync(cancellationToken);
                _logger.LogWarning("VoiceRSS API error: {Error}", errorText);
                return TtsResult.Fail($"VoiceRSS error: {errorText}", Name, stopwatch.Elapsed);
            }

            var audioBytes = await response.Content.ReadAsByteArrayAsync(cancellationToken);
            _logger.LogDebug("VoiceRSS generated {Bytes} bytes of audio", audioBytes.Length);

            if (audioBytes.Length == 0)
            {
                return TtsResult.Fail("VoiceRSS returned empty audio", Name, stopwatch.Elapsed);
            }

            _lastSuccessTime = DateTime.UtcNow;

            var audioContentType = GetContentType(_config.AudioCodec);
            var audioData = _audioDataFactory.Create(
                audioBytes,
                request.Text,
                Name,
                _outputConfig.Mode,
                _outputConfig.OutputDirectory,
                _outputConfig.FileNamePattern,
                contentType: audioContentType);

            // Estimate audio duration
            var audioDuration = EstimateAudioDuration(audioBytes.Length);

            return TtsResult.Ok(audioData, Name, stopwatch.Elapsed, audioDuration);
        }
        catch (HttpRequestException ex)
        {
            stopwatch.Stop();
            _logger.LogError(ex, "Failed to connect to VoiceRSS API");
            return TtsResult.Fail($"Connection failed: {ex.Message}", Name, stopwatch.Elapsed);
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            _logger.LogError(ex, "Error generating audio with VoiceRSS");
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
                // Czech voices
                new Core.Models.VoiceInfo { Id = "Josef", Language = "cs-cz", DisplayName = "Josef", Gender = "Male" },
                // English voices
                new Core.Models.VoiceInfo { Id = "John", Language = "en-us", DisplayName = "John", Gender = "Male" },
                new Core.Models.VoiceInfo { Id = "Mary", Language = "en-us", DisplayName = "Mary", Gender = "Female" },
                new Core.Models.VoiceInfo { Id = "Mike", Language = "en-us", DisplayName = "Mike", Gender = "Male" },
                new Core.Models.VoiceInfo { Id = "Linda", Language = "en-gb", DisplayName = "Linda", Gender = "Female" }
            ]
        });
    }

    private string? LoadApiKey()
    {
        // First try direct API key from config
        if (!string.IsNullOrEmpty(_config.ApiKey))
        {
            return _config.ApiKey;
        }

        // Try environment variable
        var envKey = Environment.GetEnvironmentVariable("VOICERSS_API_KEY");
        if (!string.IsNullOrEmpty(envKey))
        {
            _logger.LogDebug("Using VoiceRSS API key from environment variable");
            return envKey;
        }

        // Try file
        if (!string.IsNullOrEmpty(_config.ApiKeyFile))
        {
            try
            {
                var keyPath = _config.ApiKeyFile;

                // Expand ~ to home directory
                if (keyPath.StartsWith("~/"))
                {
                    keyPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), keyPath[2..]);
                }

                if (!File.Exists(keyPath))
                {
                    _logger.LogDebug("VoiceRSS API key file not found: {Path}", keyPath);
                    return null;
                }

                // Read file and extract API key
                var content = File.ReadAllText(keyPath);

                // Try to find API_KEY= line
                foreach (var line in content.Split('\n'))
                {
                    var trimmed = line.Trim();
                    if (trimmed.StartsWith("API_KEY="))
                    {
                        _logger.LogDebug("Loaded VoiceRSS API key from file");
                        return trimmed["API_KEY=".Length..].Trim();
                    }
                }

                // If no API_KEY= found, assume whole file is the key
                var key = content.Trim();
                if (key.Length == 32) // VoiceRSS keys are 32 chars
                {
                    _logger.LogDebug("Loaded VoiceRSS API key from file");
                    return key;
                }

                _logger.LogWarning("Could not parse API key from file: {Path}", keyPath);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error loading VoiceRSS API key from file");
            }
        }

        _logger.LogWarning("VoiceRSS API key not configured");
        return null;
    }

    private static string GetContentType(string audioCodec) =>
        audioCodec.ToUpperInvariant() switch
        {
            "MP3" => "audio/mpeg",
            "WAV" => "audio/wav",
            "OGG" => "audio/ogg",
            "AAC" => "audio/aac",
            _ => "audio/mpeg"
        };

    private static TimeSpan EstimateAudioDuration(int audioBytes)
    {
        // Rough estimate for 44kHz 16-bit mono MP3: ~88kbps = 11000 bytes/second
        const int bytesPerSecond = 11000;
        var seconds = (double)audioBytes / bytesPerSecond;
        return TimeSpan.FromSeconds(seconds);
    }
}
