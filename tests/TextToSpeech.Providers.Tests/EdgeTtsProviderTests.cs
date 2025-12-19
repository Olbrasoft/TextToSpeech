using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Moq.Protected;
using Olbrasoft.TextToSpeech.Core.Enums;
using Olbrasoft.TextToSpeech.Core.Models;
using Olbrasoft.TextToSpeech.Core.Services;
using Olbrasoft.TextToSpeech.Providers.Configuration;
using Olbrasoft.TextToSpeech.Providers.EdgeTTS;
using System.Net;
using System.Text;

namespace TextToSpeech.Providers.Tests;

public class EdgeTtsProviderTests
{
    private readonly Mock<ILogger<EdgeTtsProvider>> _loggerMock;
    private readonly Mock<IOutputConfiguration> _outputConfigMock;
    private readonly Mock<IAudioDataFactory> _audioDataFactoryMock;
    private readonly EdgeTtsConfiguration _config;

    public EdgeTtsProviderTests()
    {
        _loggerMock = new Mock<ILogger<EdgeTtsProvider>>();
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

        _config = new EdgeTtsConfiguration
        {
            BaseUrl = "http://localhost:5555",
            Voice = "cs-CZ-AntoninNeural",
            Timeout = TimeSpan.FromSeconds(30)
        };
    }

    [Fact]
    public void Name_ReturnsEdgeTtsHttp()
    {
        // Arrange
        var provider = CreateProvider(CreateMockHttpClient(HttpStatusCode.OK, "{}"));

        // Act
        var name = provider.Name;

        // Assert
        Assert.Equal("EdgeTTS-HTTP", name);
    }

    [Fact]
    public async Task SynthesizeAsync_ServerError_ReturnsFail()
    {
        // Arrange
        var provider = CreateProvider(CreateMockHttpClient(HttpStatusCode.InternalServerError, "Server error"));
        var request = new TtsRequest { Text = "Test" };

        // Act
        var result = await provider.SynthesizeAsync(request);

        // Assert
        Assert.False(result.Success);
        Assert.Contains("EdgeTTS server error", result.ErrorMessage);
        Assert.Equal("EdgeTTS-HTTP", result.ProviderUsed);
    }

    [Fact]
    public async Task SynthesizeAsync_SuccessWithFilePath_ReturnsOk()
    {
        // Arrange
        var tempFile = Path.Combine(Path.GetTempPath(), "test_audio.mp3");
        try
        {
            await File.WriteAllBytesAsync(tempFile, new byte[] { 0xFF, 0xFB, 0x90, 0x00 });

            var response = $"{{\"success\": true, \"message\": \"Audio generated at: {tempFile}\"}}";
            var provider = CreateProvider(CreateMockHttpClient(HttpStatusCode.OK, response));
            var request = new TtsRequest { Text = "Test" };

            // Act
            var result = await provider.SynthesizeAsync(request);

            // Assert
            Assert.True(result.Success);
            Assert.NotNull(result.Audio);
            Assert.IsType<MemoryAudioData>(result.Audio);
            Assert.Equal("EdgeTTS-HTTP", result.ProviderUsed);
        }
        finally
        {
            if (File.Exists(tempFile)) File.Delete(tempFile);
        }
    }

    [Fact]
    public async Task SynthesizeAsync_ConnectionFailed_ReturnsFail()
    {
        // Arrange
        var mockHandler = new Mock<HttpMessageHandler>();
        mockHandler.Protected()
            .Setup<Task<HttpResponseMessage>>("SendAsync", ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>())
            .ThrowsAsync(new HttpRequestException("Connection refused"));

        var httpClient = new HttpClient(mockHandler.Object) { BaseAddress = new Uri(_config.BaseUrl) };
        var httpClientFactoryMock = new Mock<IHttpClientFactory>();
        httpClientFactoryMock.Setup(x => x.CreateClient("EdgeTtsServer")).Returns(httpClient);

        var provider = new EdgeTtsProvider(
            _loggerMock.Object,
            httpClientFactoryMock.Object,
            Options.Create(_config),
            _outputConfigMock.Object,
            _audioDataFactoryMock.Object);

        var request = new TtsRequest { Text = "Test" };

        // Act
        var result = await provider.SynthesizeAsync(request);

        // Assert
        Assert.False(result.Success);
        Assert.Contains("Connection failed", result.ErrorMessage);
    }

    [Fact]
    public async Task GetInfoAsync_ReturnsProviderInfo()
    {
        // Arrange
        var provider = CreateProvider(CreateMockHttpClient(HttpStatusCode.OK, "{}"));

        // Act
        var info = await provider.GetInfoAsync();

        // Assert
        Assert.Equal("EdgeTTS-HTTP", info.Name);
        Assert.Equal(ProviderStatus.Available, info.Status);
        Assert.NotEmpty(info.SupportedVoices);
    }

    private EdgeTtsProvider CreateProvider(HttpClient httpClient)
    {
        var httpClientFactoryMock = new Mock<IHttpClientFactory>();
        httpClientFactoryMock.Setup(x => x.CreateClient("EdgeTtsServer")).Returns(httpClient);

        return new EdgeTtsProvider(
            _loggerMock.Object,
            httpClientFactoryMock.Object,
            Options.Create(_config),
            _outputConfigMock.Object,
            _audioDataFactoryMock.Object);
    }

    private HttpClient CreateMockHttpClient(HttpStatusCode statusCode, string content)
    {
        var mockHandler = new Mock<HttpMessageHandler>();
        mockHandler.Protected()
            .Setup<Task<HttpResponseMessage>>("SendAsync", ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = statusCode,
                Content = new StringContent(content, Encoding.UTF8, "application/json")
            });

        return new HttpClient(mockHandler.Object) { BaseAddress = new Uri(_config.BaseUrl) };
    }
}
