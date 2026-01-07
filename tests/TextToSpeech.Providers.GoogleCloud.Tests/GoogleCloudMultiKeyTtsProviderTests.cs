using System.Net;
using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Newtonsoft.Json;
using Olbrasoft.TextToSpeech.Core.Models;
using Olbrasoft.TextToSpeech.Providers.GoogleCloud;

namespace TextToSpeech.Providers.GoogleCloud.Tests;

public class GoogleCloudMultiKeyTtsProviderTests
{
    private readonly Mock<ILogger<GoogleCloudMultiKeyTtsProvider>> _loggerMock;

    public GoogleCloudMultiKeyTtsProviderTests()
    {
        _loggerMock = new Mock<ILogger<GoogleCloudMultiKeyTtsProvider>>();
    }

    [Fact]
    public void Name_ReturnsGoogleCloudMultiKey()
    {
        // Arrange
        var provider = CreateProvider(["key1"], CreateSuccessHandler());

        // Act & Assert
        Assert.Equal("GoogleCloudMultiKey", provider.Name);
    }

    [Fact]
    public void Constructor_ThrowsWhenSecretKeyNotFound()
    {
        // Arrange
        var config = CreateConfig(
        [
            new ApiKeyConfig { SecretKey = "NonExistent:Key", Name = "missing" }
        ]);

        var emptyConfiguration = new ConfigurationBuilder().Build();

        // Act & Assert
        InvalidOperationException? ex = null;
        try
        {
            using var httpClient = new HttpClient();
            _ = new GoogleCloudMultiKeyTtsProvider(
                Options.Create(config),
                emptyConfiguration,
                _loggerMock.Object,
                httpClient);
        }
        catch (InvalidOperationException e)
        {
            ex = e;
        }

        Assert.NotNull(ex);
        Assert.Contains("NonExistent:Key", ex.Message);
        Assert.Contains("not found in configuration", ex.Message);
    }

    [Fact]
    public void Constructor_LoadsKeysFromConfiguration()
    {
        // Arrange & Act
        var provider = CreateProvider(["key1", "key2", "key3"], CreateSuccessHandler());

        // Assert - provider created without exception, keys loaded
        Assert.Equal("GoogleCloudMultiKey", provider.Name);
    }

    [Fact]
    public async Task SynthesizeAsync_FirstKeySucceeds_ReturnsAudio()
    {
        // Arrange
        var audioBytes = Encoding.UTF8.GetBytes("fake audio data");
        var provider = CreateProvider(["key1"], CreateSuccessHandler(audioBytes));
        var request = new TtsRequest { Text = "Hello world" };

        // Act
        var result = await provider.SynthesizeAsync(request);

        // Assert
        Assert.True(result.Success);
        Assert.Equal("GoogleCloudMultiKey", result.ProviderUsed);
        Assert.NotNull(result.Audio);
        var memoryAudio = Assert.IsType<MemoryAudioData>(result.Audio);
        Assert.Equal(audioBytes, memoryAudio.Data);
    }

    [Fact]
    public async Task SynthesizeAsync_FirstKeyRateLimited_FallsBackToSecond()
    {
        // Arrange
        var audioBytes = Encoding.UTF8.GetBytes("audio from second key");
        var callCount = 0;

        var handler = new MockHttpMessageHandler(request =>
        {
            callCount++;
            if (callCount == 1)
            {
                return new HttpResponseMessage(HttpStatusCode.TooManyRequests)
                {
                    Content = new StringContent("Rate limit exceeded")
                };
            }

            return CreateSuccessResponse(audioBytes);
        });

        var provider = CreateProvider(["key1", "key2"], handler);
        var ttsRequest = new TtsRequest { Text = "Test" };

        // Act
        var result = await provider.SynthesizeAsync(ttsRequest);

        // Assert
        Assert.True(result.Success);
        Assert.Equal(2, callCount);
        var memoryAudio = Assert.IsType<MemoryAudioData>(result.Audio);
        Assert.Equal(audioBytes, memoryAudio.Data);
    }

    [Fact]
    public async Task SynthesizeAsync_FirstKeyQuotaExceeded_FallsBackToSecond()
    {
        // Arrange
        var audioBytes = Encoding.UTF8.GetBytes("audio from second key");
        var callCount = 0;

        var handler = new MockHttpMessageHandler(request =>
        {
            callCount++;
            if (callCount == 1)
            {
                return new HttpResponseMessage(HttpStatusCode.Forbidden)
                {
                    Content = new StringContent("Quota exceeded")
                };
            }

            return CreateSuccessResponse(audioBytes);
        });

        var provider = CreateProvider(["key1", "key2"], handler);
        var ttsRequest = new TtsRequest { Text = "Test" };

        // Act
        var result = await provider.SynthesizeAsync(ttsRequest);

        // Assert
        Assert.True(result.Success);
        Assert.Equal(2, callCount);
    }

    [Fact]
    public async Task SynthesizeAsync_KeyInvalid_PermanentlyDisabled()
    {
        // Arrange
        var audioBytes = Encoding.UTF8.GetBytes("audio from second key");
        var callCount = 0;

        var handler = new MockHttpMessageHandler(request =>
        {
            callCount++;
            if (callCount == 1)
            {
                return new HttpResponseMessage(HttpStatusCode.Unauthorized)
                {
                    Content = new StringContent("Invalid API key")
                };
            }

            return CreateSuccessResponse(audioBytes);
        });

        var provider = CreateProvider(["invalid-key", "valid-key"], handler);

        // Act - First call: invalid key falls back to valid
        var result1 = await provider.SynthesizeAsync(new TtsRequest { Text = "Test 1" });

        // Second call should skip invalid key and go directly to valid
        var result2 = await provider.SynthesizeAsync(new TtsRequest { Text = "Test 2" });

        // Assert
        Assert.True(result1.Success);
        Assert.True(result2.Success);
        Assert.Equal(3, callCount); // 1 (invalid) + 1 (valid) + 1 (valid again)
    }

    [Fact]
    public async Task SynthesizeAsync_ServerError_TriesThenFallback()
    {
        // Arrange
        var audioBytes = Encoding.UTF8.GetBytes("audio from second key");
        var callCount = 0;

        var handler = new MockHttpMessageHandler(request =>
        {
            callCount++;
            if (callCount == 1)
            {
                return new HttpResponseMessage(HttpStatusCode.InternalServerError)
                {
                    Content = new StringContent("Server error")
                };
            }

            return CreateSuccessResponse(audioBytes);
        });

        var provider = CreateProvider(["key1", "key2"], handler);
        var ttsRequest = new TtsRequest { Text = "Test" };

        // Act
        var result = await provider.SynthesizeAsync(ttsRequest);

        // Assert
        Assert.True(result.Success);
        Assert.Equal(2, callCount);
    }

    [Fact]
    public async Task SynthesizeAsync_AllKeysExhausted_ReturnsFail()
    {
        // Arrange
        var handler = new MockHttpMessageHandler(_ =>
            new HttpResponseMessage(HttpStatusCode.Unauthorized)
            {
                Content = new StringContent("Invalid")
            });

        var provider = CreateProvider(["key1", "key2"], handler);
        var request = new TtsRequest { Text = "Test" };

        // Act
        var result = await provider.SynthesizeAsync(request);

        // Assert
        Assert.False(result.Success);
        Assert.Contains("exhausted", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task GetNextAvailableKey_CooldownExpired_KeyBecomesAvailable()
    {
        // Arrange - config with very short cooldown
        var config = new GoogleCloudMultiKeyConfiguration
        {
            ApiKeySecrets =
            [
                new ApiKeyConfig { SecretKey = "Key1", Name = "primary" }
            ],
            RateLimitCooldown = TimeSpan.FromMilliseconds(50) // Very short for testing
        };

        var callCount = 0;
        var audioBytes = Encoding.UTF8.GetBytes("success");

        var handler = new MockHttpMessageHandler(request =>
        {
            callCount++;
            if (callCount == 1)
            {
                return new HttpResponseMessage(HttpStatusCode.TooManyRequests);
            }

            return CreateSuccessResponse(audioBytes);
        });

        var configuration = CreateConfiguration(["actual-key-1"]);
        using var httpClient = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(30) };

        var provider = new GoogleCloudMultiKeyTtsProvider(
            Options.Create(config),
            configuration,
            _loggerMock.Object,
            httpClient);

        // Act - First call fails with rate limit
        var result1 = await provider.SynthesizeAsync(new TtsRequest { Text = "Test 1" });

        // Wait for cooldown to expire (3x safety margin for CI stability)
        await Task.Delay(150);

        // Second call should work - key is available again
        var result2 = await provider.SynthesizeAsync(new TtsRequest { Text = "Test 2" });

        // Assert
        Assert.False(result1.Success); // Only one key, rate limited
        Assert.True(result2.Success);  // Key available after cooldown
    }

    [Fact]
    public async Task GetInfoAsync_ReturnsCorrectStatus()
    {
        // Arrange
        var provider = CreateProvider(["key1"], CreateSuccessHandler());

        // Act
        var info = await provider.GetInfoAsync();

        // Assert
        Assert.Equal("GoogleCloudMultiKey", info.Name);
        Assert.Equal(ProviderStatus.Available, info.Status);
        Assert.NotEmpty(info.SupportedVoices);
        Assert.All(info.SupportedVoices, v => Assert.Equal("cs-CZ", v.Language));
    }

    [Fact]
    public async Task GetInfoAsync_NoKeys_ReturnsUnavailable()
    {
        // Arrange
        var config = new GoogleCloudMultiKeyConfiguration
        {
            ApiKeySecrets = [] // Empty list
        };

        var configuration = new ConfigurationBuilder().Build();
        using var httpClient = new HttpClient(CreateSuccessHandler());

        var provider = new GoogleCloudMultiKeyTtsProvider(
            Options.Create(config),
            configuration,
            _loggerMock.Object,
            httpClient);

        // Act
        var info = await provider.GetInfoAsync();

        // Assert
        Assert.Equal(ProviderStatus.Unavailable, info.Status);
    }

    [Fact]
    public void ApiKeyState_HasExpectedValues()
    {
        // Assert enum values exist
        Assert.Equal(0, (int)ApiKeyState.Available);
        Assert.Equal(1, (int)ApiKeyState.RateLimited);
        Assert.Equal(2, (int)ApiKeyState.QuotaExceeded);
        Assert.Equal(3, (int)ApiKeyState.Invalid);
    }

    [Fact]
    public void GoogleCloudMultiKeyConfiguration_DefaultValues()
    {
        // Arrange & Act
        var config = new GoogleCloudMultiKeyConfiguration();

        // Assert
        Assert.Empty(config.ApiKeySecrets);
        Assert.Equal("cs-CZ-Chirp3-HD-Achird", config.Voice);
        Assert.Equal("MP3", config.AudioEncoding);
        Assert.Equal(1.0, config.SpeakingRate);
        Assert.Equal(0.0, config.Pitch);
        Assert.Equal(24000, config.SampleRateHertz);
        Assert.Equal(TimeSpan.FromSeconds(30), config.Timeout);
        Assert.Equal(TimeSpan.FromHours(1), config.RateLimitCooldown);
        Assert.Equal(TimeSpan.FromHours(24), config.QuotaExceededCooldown);
    }

    [Fact]
    public async Task GetInfoAsync_AllKeysInCooldown_ReturnsDegraded()
    {
        // Arrange - config with long cooldown
        var config = new GoogleCloudMultiKeyConfiguration
        {
            ApiKeySecrets =
            [
                new ApiKeyConfig { SecretKey = "Key1", Name = "primary" },
                new ApiKeyConfig { SecretKey = "Key2", Name = "secondary" }
            ],
            RateLimitCooldown = TimeSpan.FromHours(1) // Long cooldown
        };

        var handler = new MockHttpMessageHandler(_ =>
            new HttpResponseMessage(HttpStatusCode.TooManyRequests)
            {
                Content = new StringContent("Rate limit exceeded")
            });

        var configuration = CreateConfiguration(["key-1", "key-2"]);
        using var httpClient = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(30) };

        var provider = new GoogleCloudMultiKeyTtsProvider(
            Options.Create(config),
            configuration,
            _loggerMock.Object,
            httpClient);

        // Act - Rate limit both keys
        _ = await provider.SynthesizeAsync(new TtsRequest { Text = "Test" });

        // Check status - all keys in cooldown
        var info = await provider.GetInfoAsync();

        // Assert
        Assert.Equal(ProviderStatus.Degraded, info.Status);
    }

    [Fact]
    public async Task SynthesizeAsync_CancellationRequested_ThrowsTaskCanceledException()
    {
        // Arrange
        var config = new GoogleCloudMultiKeyConfiguration
        {
            ApiKeySecrets =
            [
                new ApiKeyConfig { SecretKey = "Key1", Name = "primary" }
            ]
        };

        // Handler that simulates a slow response (uses async handler with cancellation support)
        var handler = new MockHttpMessageHandler(async (request, ct) =>
        {
            await Task.Delay(1000, ct); // Simulate slow response with cancellation
            return CreateSuccessResponse(Encoding.UTF8.GetBytes("audio"));
        });

        var configuration = CreateConfiguration(["key-1"]);
        using var httpClient = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(30) };

        var provider = new GoogleCloudMultiKeyTtsProvider(
            Options.Create(config),
            configuration,
            _loggerMock.Object,
            httpClient);

        // Act - Use pre-cancelled token
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        // Assert
        await Assert.ThrowsAsync<TaskCanceledException>(
            () => provider.SynthesizeAsync(new TtsRequest { Text = "Test" }, cts.Token));
    }

    #region Helper Methods

    private GoogleCloudMultiKeyTtsProvider CreateProvider(
        string[] keys,
        HttpMessageHandler handler)
    {
        var config = CreateConfig(keys.Select((k, i) =>
            new ApiKeyConfig { SecretKey = $"Key{i + 1}", Name = $"key-{i + 1}" }).ToList());

        var configuration = CreateConfiguration(keys);
        var httpClient = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(30) };

        return new GoogleCloudMultiKeyTtsProvider(
            Options.Create(config),
            configuration,
            _loggerMock.Object,
            httpClient);
    }

    private static GoogleCloudMultiKeyConfiguration CreateConfig(IList<ApiKeyConfig> keyConfigs)
    {
        return new GoogleCloudMultiKeyConfiguration
        {
            ApiKeySecrets = keyConfigs.ToList()
        };
    }

    private static IConfiguration CreateConfiguration(string[] keys)
    {
        var configData = new Dictionary<string, string?>();
        for (var i = 0; i < keys.Length; i++)
        {
            configData[$"Key{i + 1}"] = keys[i];
        }

        return new ConfigurationBuilder()
            .AddInMemoryCollection(configData)
            .Build();
    }

    private static MockHttpMessageHandler CreateSuccessHandler(byte[]? audioBytes = null)
    {
        return new MockHttpMessageHandler(_ => CreateSuccessResponse(audioBytes));
    }

    private static HttpResponseMessage CreateSuccessResponse(byte[]? audioBytes = null)
    {
        var audio = audioBytes ?? Encoding.UTF8.GetBytes("fake audio");
        var response = new
        {
            audioContent = Convert.ToBase64String(audio)
        };

        return new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(JsonConvert.SerializeObject(response), Encoding.UTF8, "application/json")
        };
    }

    #endregion
}

/// <summary>
/// Mock HTTP message handler for testing.
/// </summary>
public class MockHttpMessageHandler : HttpMessageHandler
{
    private readonly Func<HttpRequestMessage, HttpResponseMessage>? _syncHandler;
    private readonly Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>>? _asyncHandler;

    public MockHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> handler)
    {
        _syncHandler = handler;
    }

    public MockHttpMessageHandler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> asyncHandler)
    {
        _asyncHandler = asyncHandler;
    }

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        if (_asyncHandler != null)
        {
            return await _asyncHandler(request, cancellationToken);
        }

        return _syncHandler!(request);
    }
}
