using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Moq.Protected;
using Olbrasoft.TextToSpeech.Core.Enums;
using Olbrasoft.TextToSpeech.Core.Models;
using Olbrasoft.TextToSpeech.Core.Services;
using Olbrasoft.TextToSpeech.Providers.Configuration;
using Olbrasoft.TextToSpeech.Providers.VoiceRss;
using System.Net;

namespace TextToSpeech.Providers.Tests;

public class VoiceRssProviderTests
{
    private readonly Mock<ILogger<VoiceRssProvider>> _loggerMock;
    private readonly Mock<IOutputConfiguration> _outputConfigMock;
    private readonly Mock<IAudioDataFactory> _audioDataFactoryMock;
    private readonly VoiceRssConfiguration _config;

    public VoiceRssProviderTests()
    {
        _loggerMock = new Mock<ILogger<VoiceRssProvider>>();
        _outputConfigMock = new Mock<IOutputConfiguration>();
        _outputConfigMock.Setup(x => x.Mode).Returns(AudioOutputMode.Memory);

        _audioDataFactoryMock = new Mock<IAudioDataFactory>();
        _audioDataFactoryMock
            .Setup(x => x.Create(
                It.IsAny<byte[]>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<AudioOutputMode>(),
                It.IsAny<string?>(),
                It.IsAny<string>(),
                It.IsAny<string?>(),
                It.IsAny<string>()))
            .Returns((byte[] bytes, string text, string provider, AudioOutputMode mode, string? dir, string pattern, string? existing, string contentType) =>
                new MemoryAudioData { Data = bytes, ContentType = contentType });

        _config = new VoiceRssConfiguration
        {
            ApiKey = "test-api-key-32-characters-ok!",
            Language = "cs-cz",
            DefaultVoice = "Josef",
            AudioCodec = "MP3"
        };
    }

    [Fact]
    public void Name_ReturnsVoiceRSS()
    {
        // Arrange
        var provider = CreateProvider(CreateMockHttpClient(HttpStatusCode.OK, new byte[] { 0xFF, 0xFB }));

        // Act
        var name = provider.Name;

        // Assert
        Assert.Equal("VoiceRSS", name);
    }

    [Fact]
    public async Task SynthesizeAsync_NoApiKey_ReturnsFail()
    {
        // Arrange
        var configNoKey = new VoiceRssConfiguration { ApiKey = "" };
        var httpClientFactoryMock = new Mock<IHttpClientFactory>();
        httpClientFactoryMock.Setup(x => x.CreateClient("VoiceRSS"))
            .Returns(new HttpClient { Timeout = TimeSpan.FromSeconds(30) });

        var provider = new VoiceRssProvider(
            _loggerMock.Object,
            httpClientFactoryMock.Object,
            Options.Create(configNoKey),
            _outputConfigMock.Object,
            _audioDataFactoryMock.Object);

        var request = new TtsRequest { Text = "Test" };

        // Act
        var result = await provider.SynthesizeAsync(request);

        // Assert
        Assert.False(result.Success);
        Assert.Contains("API key not configured", result.ErrorMessage);
    }

    [Fact]
    public async Task SynthesizeAsync_Success_ReturnsAudioData()
    {
        // Arrange
        var audioBytes = new byte[] { 0xFF, 0xFB, 0x90, 0x00, 0x01, 0x02, 0x03 };
        var provider = CreateProvider(CreateMockHttpClient(HttpStatusCode.OK, audioBytes, "audio/mpeg"));
        var request = new TtsRequest { Text = "Test" };

        // Act
        var result = await provider.SynthesizeAsync(request);

        // Assert
        Assert.True(result.Success);
        Assert.NotNull(result.Audio);
        Assert.IsType<MemoryAudioData>(result.Audio);
        var memoryData = (MemoryAudioData)result.Audio;
        Assert.Equal(audioBytes.Length, memoryData.Data.Length);
    }

    [Fact]
    public async Task SynthesizeAsync_ApiError_ReturnsFail()
    {
        // Arrange
        var provider = CreateProvider(CreateMockHttpClient(HttpStatusCode.OK,
            System.Text.Encoding.UTF8.GetBytes("ERROR: Invalid API key"), "text/plain"));
        var request = new TtsRequest { Text = "Test" };

        // Act
        var result = await provider.SynthesizeAsync(request);

        // Assert
        Assert.False(result.Success);
        Assert.Contains("VoiceRSS error", result.ErrorMessage);
    }

    [Fact]
    public async Task GetInfoAsync_WithApiKey_ReturnsAvailable()
    {
        // Arrange
        var provider = CreateProvider(CreateMockHttpClient(HttpStatusCode.OK, new byte[] { 0xFF, 0xFB }));

        // Act
        var info = await provider.GetInfoAsync();

        // Assert
        Assert.Equal("VoiceRSS", info.Name);
        Assert.Equal(ProviderStatus.Available, info.Status);
    }

    [Fact]
    public async Task GetInfoAsync_NoApiKey_ReturnsUnavailable()
    {
        // Arrange
        var configNoKey = new VoiceRssConfiguration { ApiKey = "" };
        var httpClientFactoryMock = new Mock<IHttpClientFactory>();
        httpClientFactoryMock.Setup(x => x.CreateClient("VoiceRSS"))
            .Returns(new HttpClient { Timeout = TimeSpan.FromSeconds(30) });

        var provider = new VoiceRssProvider(
            _loggerMock.Object,
            httpClientFactoryMock.Object,
            Options.Create(configNoKey),
            _outputConfigMock.Object,
            _audioDataFactoryMock.Object);

        // Act
        var info = await provider.GetInfoAsync();

        // Assert
        Assert.Equal(ProviderStatus.Unavailable, info.Status);
    }

    private VoiceRssProvider CreateProvider(HttpClient httpClient)
    {
        var httpClientFactoryMock = new Mock<IHttpClientFactory>();
        httpClientFactoryMock.Setup(x => x.CreateClient("VoiceRSS")).Returns(httpClient);

        return new VoiceRssProvider(
            _loggerMock.Object,
            httpClientFactoryMock.Object,
            Options.Create(_config),
            _outputConfigMock.Object,
            _audioDataFactoryMock.Object);
    }

    private static HttpClient CreateMockHttpClient(HttpStatusCode statusCode, byte[] content, string contentType = "audio/mpeg")
    {
        var mockHandler = new Mock<HttpMessageHandler>();
        mockHandler.Protected()
            .Setup<Task<HttpResponseMessage>>("SendAsync", ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = statusCode,
                Content = new ByteArrayContent(content) { Headers = { ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(contentType) } }
            });

        return new HttpClient(mockHandler.Object);
    }
}
