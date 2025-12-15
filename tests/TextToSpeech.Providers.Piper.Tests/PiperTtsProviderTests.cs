using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Olbrasoft.TextToSpeech.Core.Models;
using Olbrasoft.TextToSpeech.Providers.Piper;

namespace TextToSpeech.Providers.Piper.Tests;

public class PiperTtsProviderTests
{
    private readonly Mock<ILogger<PiperTtsProvider>> _loggerMock;
    private readonly PiperConfiguration _config;

    public PiperTtsProviderTests()
    {
        _loggerMock = new Mock<ILogger<PiperTtsProvider>>();

        _config = new PiperConfiguration
        {
            ModelPath = "/nonexistent/model.onnx",
            PiperPath = "piper",
            OutputMode = AudioOutputMode.Memory,
            TimeoutSeconds = 60,
            Profiles = new Dictionary<string, PiperVoiceProfile>(StringComparer.OrdinalIgnoreCase)
            {
                ["default"] = new PiperVoiceProfile()
            }
        };
    }

    [Fact]
    public void Name_ReturnsPiper()
    {
        // Arrange
        var provider = CreateProvider();

        // Act
        var name = provider.Name;

        // Assert
        Assert.Equal("Piper", name);
    }

    [Fact]
    public async Task GetInfoAsync_ModelNotFound_ReturnsUnavailable()
    {
        // Arrange
        var provider = CreateProvider();

        // Act
        var info = await provider.GetInfoAsync();

        // Assert
        Assert.Equal("Piper", info.Name);
        Assert.Equal(ProviderStatus.Unavailable, info.Status);
    }

    [Fact]
    public async Task SynthesizeAsync_PiperUnavailable_ReturnsFail()
    {
        // Arrange
        var provider = CreateProvider();
        var request = new TtsRequest { Text = "Test" };

        // Act
        var result = await provider.SynthesizeAsync(request);

        // Assert
        Assert.False(result.Success);
        Assert.True(
            result.ErrorMessage?.Contains("not installed", StringComparison.OrdinalIgnoreCase) == true ||
            result.ErrorMessage?.Contains("not found", StringComparison.OrdinalIgnoreCase) == true,
            $"Expected error message to contain 'not installed' or 'not found', but got: {result.ErrorMessage}");
    }

    [Fact]
    public void SourceProfile_CanBeSet()
    {
        // Arrange
        var provider = CreateProvider();

        // Act
        provider.SourceProfile = "fast";

        // Assert
        Assert.Equal("fast", provider.SourceProfile);
    }

    [Fact]
    public async Task SynthesizeAsync_RateAdjustment_DoesNotCrash()
    {
        // Arrange
        var provider = CreateProvider();
        var request = new TtsRequest
        {
            Text = "Test",
            Rate = 50
        };

        // Act
        var result = await provider.SynthesizeAsync(request);

        // Assert - Will fail due to missing model, but shouldn't crash
        Assert.False(result.Success);
        Assert.Equal("Piper", result.ProviderUsed);
    }

    private PiperTtsProvider CreateProvider()
    {
        return new PiperTtsProvider(
            _loggerMock.Object,
            Options.Create(_config));
    }
}
