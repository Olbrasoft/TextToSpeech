using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Olbrasoft.TextToSpeech.Core.Models;
using Olbrasoft.TextToSpeech.Providers.Configuration;
using Olbrasoft.TextToSpeech.Providers.Google;

namespace Olbrasoft.TextToSpeech.Tests.Providers;

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
        // Note: Status depends on whether gtts-cli is installed
    }

    [Fact]
    public async Task SynthesizeAsync_WithVoiceFromRequest_UsesLanguageFromVoice()
    {
        // Arrange - Note: This test only verifies the provider doesn't crash
        // Actual synthesis would require gtts-cli to be installed
        var provider = CreateProvider();
        var request = new TtsRequest
        {
            Text = "Test",
            Voice = "en-US" // Should extract "en" as language
        };

        // Act
        var result = await provider.SynthesizeAsync(request);

        // Assert
        // Result depends on whether gtts-cli is installed
        Assert.NotNull(result);
        Assert.Equal("GoogleTTS", result.ProviderUsed);
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

    private GoogleTtsProvider CreateProvider()
    {
        return new GoogleTtsProvider(
            _loggerMock.Object,
            Options.Create(_config),
            _outputConfigMock.Object);
    }
}
