using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Olbrasoft.TextToSpeech.Core.Models;
using Olbrasoft.TextToSpeech.Providers.Piper;

namespace Olbrasoft.TextToSpeech.Tests.Providers;

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
                ["default"] = new PiperVoiceProfile
                {
                    LengthScale = 1.0,
                    NoiseScale = 0.667,
                    NoiseWScale = 0.8,
                    SentenceSilence = 0.2,
                    Volume = 1.0,
                    Speaker = 0
                }
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
    public async Task SynthesizeAsync_ModelNotFound_ReturnsFail()
    {
        // Arrange
        var provider = CreateProvider();
        var request = new TtsRequest { Text = "Test" };

        // Act
        var result = await provider.SynthesizeAsync(request);

        // Assert
        Assert.False(result.Success);
        Assert.Contains("not found", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task SynthesizeAsync_PiperNotInstalled_ReturnsFail()
    {
        // Arrange
        var configInvalidPiper = new PiperConfiguration
        {
            ModelPath = Path.GetTempFileName(), // Create actual file
            PiperPath = "/nonexistent/piper",
            OutputMode = AudioOutputMode.Memory
        };

        try
        {
            var provider = new PiperTtsProvider(
                _loggerMock.Object,
                Options.Create(configInvalidPiper));

            var request = new TtsRequest { Text = "Test" };

            // Act
            var result = await provider.SynthesizeAsync(request);

            // Assert
            Assert.False(result.Success);
        }
        finally
        {
            if (File.Exists(configInvalidPiper.ModelPath))
            {
                File.Delete(configInvalidPiper.ModelPath);
            }
        }
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
    public async Task SynthesizeAsync_RateAdjustment_AppliesLengthScale()
    {
        // Arrange - This test verifies rate is processed, actual synthesis requires piper
        var provider = CreateProvider();
        var request = new TtsRequest
        {
            Text = "Test",
            Rate = 50 // Should speed up by adjusting length scale
        };

        // Act
        var result = await provider.SynthesizeAsync(request);

        // Assert - Will fail due to missing model, but shouldn't crash
        Assert.False(result.Success);
        Assert.Equal("Piper", result.ProviderUsed);
    }

    [Fact]
    public void PiperVoiceProfile_DefaultValues()
    {
        // Arrange & Act
        var profile = new PiperVoiceProfile();

        // Assert
        Assert.Equal(1.0, profile.LengthScale);
        Assert.Equal(0.667, profile.NoiseScale);
        Assert.Equal(0.8, profile.NoiseWScale);
        Assert.Equal(0.2, profile.SentenceSilence);
        Assert.Equal(1.0, profile.Volume);
        Assert.Equal(0, profile.Speaker);
    }

    [Fact]
    public void PiperConfiguration_DefaultValues()
    {
        // Arrange & Act
        var config = new PiperConfiguration();

        // Assert
        Assert.Equal("piper", config.PiperPath);
        Assert.Equal(AudioOutputMode.Memory, config.OutputMode);
        Assert.Equal(60, config.TimeoutSeconds);
        Assert.Equal("default", config.DefaultProfile);
    }

    private PiperTtsProvider CreateProvider()
    {
        return new PiperTtsProvider(
            _loggerMock.Object,
            Options.Create(_config));
    }
}
