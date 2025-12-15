using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Olbrasoft.TextToSpeech.Core.Models;
using Olbrasoft.TextToSpeech.Providers.Configuration;
using Olbrasoft.TextToSpeech.Providers.Google;

namespace TextToSpeech.Providers.Tests;

public class GoogleTtsProviderTests
{
    private readonly Mock<ILogger<GoogleTtsProvider>> _loggerMock;
    private readonly Mock<IOutputConfiguration> _outputConfigMock;
    private readonly GoogleTtsConfiguration _config;

    public GoogleTtsProviderTests()
    {
        _loggerMock = new Mock<ILogger<GoogleTtsProvider>>();
        _outputConfigMock = new Mock<IOutputConfiguration>();
        _outputConfigMock.Setup(x => x.Mode).Returns(AudioOutputMode.Memory);

        _config = new GoogleTtsConfiguration
        {
            Language = "cs",
            GttsCliPath = "gtts-cli",
            Slow = false,
            TimeoutSeconds = 60
        };
    }

    [Fact]
    public void Name_ReturnsGoogleTTS()
    {
        // Arrange
        var provider = CreateProvider();

        // Act
        var name = provider.Name;

        // Assert
        Assert.Equal("GoogleTTS", name);
    }

    [Fact]
    public async Task GetInfoAsync_ReturnsProviderInfo()
    {
        // Arrange
        var provider = CreateProvider();

        // Act
        var info = await provider.GetInfoAsync();

        // Assert
        Assert.Equal("GoogleTTS", info.Name);
        Assert.NotEmpty(info.SupportedVoices);
    }

    [Fact]
    public async Task SynthesizeAsync_InvalidGttsPath_ReturnsFail()
    {
        // Arrange
        var invalidConfig = new GoogleTtsConfiguration
        {
            Language = "cs",
            GttsCliPath = "/nonexistent/path/gtts-cli",
            TimeoutSeconds = 5
        };

        var provider = new GoogleTtsProvider(
            _loggerMock.Object,
            Options.Create(invalidConfig),
            _outputConfigMock.Object);

        var request = new TtsRequest { Text = "Test" };

        // Act
        var result = await provider.SynthesizeAsync(request);

        // Assert
        Assert.False(result.Success);
    }

    [Fact]
    public async Task SupportedVoices_IncludesCzech()
    {
        // Arrange
        var provider = CreateProvider();

        // Act
        var info = await provider.GetInfoAsync();
        var czechVoice = info.SupportedVoices.FirstOrDefault(v => v.Language == "cs");

        // Assert
        Assert.NotNull(czechVoice);
        Assert.Equal("cs", czechVoice.Id);
    }

    [Fact]
    public void GoogleTtsConfiguration_DefaultValues()
    {
        // Arrange & Act
        var config = new GoogleTtsConfiguration();

        // Assert
        Assert.Equal("cs", config.Language);
        Assert.Equal("gtts-cli", config.GttsCliPath);
        Assert.False(config.Slow);
        Assert.Equal(60, config.TimeoutSeconds);
    }

    private GoogleTtsProvider CreateProvider()
    {
        return new GoogleTtsProvider(
            _loggerMock.Object,
            Options.Create(_config),
            _outputConfigMock.Object);
    }
}
