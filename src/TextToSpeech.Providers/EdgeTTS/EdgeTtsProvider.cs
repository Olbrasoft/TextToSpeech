using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Olbrasoft.TextToSpeech.Core.Interfaces;
using Olbrasoft.TextToSpeech.Core.Models;
using Olbrasoft.TextToSpeech.Providers.Configuration;

namespace Olbrasoft.TextToSpeech.Providers.EdgeTTS;

/// <summary>
/// TTS provider using local EdgeTTS HTTP server.
/// Calls the EdgeTTS WebSocket server running on localhost:5555.
/// </summary>
public sealed class EdgeTtsProvider : ITtsProvider
{
    private readonly ILogger<EdgeTtsProvider> _logger;
    private readonly HttpClient _httpClient;
    private readonly EdgeTtsConfiguration _config;
    private readonly IOutputConfiguration _outputConfig;
    private DateTime? _lastSuccessTime;

    /// <summary>
    /// Initializes a new instance of EdgeTtsProvider.
    /// </summary>
    public EdgeTtsProvider(
        ILogger<EdgeTtsProvider> logger,
        IHttpClientFactory httpClientFactory,
        IOptions<EdgeTtsConfiguration> config,
        IOutputConfiguration outputConfig)
    {
        _logger = logger;
        _httpClient = httpClientFactory.CreateClient("EdgeTtsServer");
        _config = config.Value;
        _outputConfig = outputConfig;
    }

    /// <inheritdoc />
    public string Name => "EdgeTTS-HTTP";

    /// <inheritdoc />
    public async Task<TtsResult> SynthesizeAsync(TtsRequest request, CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();

        try
        {
            var httpRequest = new
            {
                text = request.Text,
                voice = request.Voice ?? _config.DefaultVoice,
                rate = FormatRate(request.Rate),
                volume = "+0%",
                pitch = FormatPitch(request.Pitch),
                play = false  // Don't play on server, return audio file path
            };

            var content = new StringContent(
                JsonSerializer.Serialize(httpRequest),
                Encoding.UTF8,
                "application/json");

            var url = $"{_config.BaseUrl}/api/speech/speak";
            _logger.LogDebug("Calling EdgeTTS server at {Url}", url);

            var response = await _httpClient.PostAsync(url, content, cancellationToken);
            stopwatch.Stop();

            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync(cancellationToken);
                _logger.LogWarning("EdgeTTS server returned {Status}: {Error}", response.StatusCode, error);
                return TtsResult.Fail($"EdgeTTS server error: {response.StatusCode}", Name, stopwatch.Elapsed);
            }

            var responseText = await response.Content.ReadAsStringAsync(cancellationToken);
            _logger.LogDebug("EdgeTTS server response: {Response}", responseText);

            using var doc = JsonDocument.Parse(responseText);
            var root = doc.RootElement;

            if (root.TryGetProperty("success", out var successProp) && !successProp.GetBoolean())
            {
                var message = root.TryGetProperty("message", out var msgProp) ? msgProp.GetString() : "Unknown error";
                _logger.LogWarning("EdgeTTS generation failed: {Message}", message);
                return TtsResult.Fail(message ?? "EdgeTTS generation failed", Name, stopwatch.Elapsed);
            }

            // Parse audio file path from response
            if (root.TryGetProperty("message", out var messageProp))
            {
                var message = messageProp.GetString() ?? "";
                var pathStart = message.IndexOf(": ", StringComparison.Ordinal);
                if (pathStart > 0)
                {
                    var audioPath = message[(pathStart + 2)..].Trim();
                    if (File.Exists(audioPath))
                    {
                        _logger.LogDebug("Reading audio from: {Path}", audioPath);
                        var audioBytes = await File.ReadAllBytesAsync(audioPath, cancellationToken);

                        _lastSuccessTime = DateTime.UtcNow;
                        var audioData = CreateAudioData(audioBytes, request.Text);

                        return TtsResult.Ok(audioData, Name, stopwatch.Elapsed);
                    }
                    _logger.LogWarning("Audio file not found: {Path}", audioPath);
                }
            }

            return TtsResult.Fail("No audio data received from EdgeTTS", Name, stopwatch.Elapsed);
        }
        catch (HttpRequestException ex)
        {
            stopwatch.Stop();
            _logger.LogError(ex, "Failed to connect to EdgeTTS server at {Url}", _config.BaseUrl);
            return TtsResult.Fail($"Connection failed: {ex.Message}", Name, stopwatch.Elapsed);
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            _logger.LogError(ex, "Error generating audio with EdgeTTS provider");
            return TtsResult.Fail($"Unexpected error: {ex.Message}", Name, stopwatch.Elapsed);
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

    private AudioData CreateAudioData(byte[] audioBytes, string text)
    {
        if (_outputConfig.Mode == AudioOutputMode.Memory)
        {
            return new MemoryAudioData { Data = audioBytes };
        }

        // File mode
        var directory = _outputConfig.OutputDirectory ?? Path.GetTempPath();
        Directory.CreateDirectory(directory);

        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text)))[..8];
        var timestamp = DateTime.UtcNow.ToString("yyyyMMdd_HHmmss");
        var fileName = _outputConfig.FileNamePattern
            .Replace("{provider}", Name)
            .Replace("{timestamp}", timestamp)
            .Replace("{hash}", hash);

        var filePath = Path.Combine(directory, fileName);
        File.WriteAllBytes(filePath, audioBytes);

        return new FileAudioData { FilePath = filePath };
    }
}
