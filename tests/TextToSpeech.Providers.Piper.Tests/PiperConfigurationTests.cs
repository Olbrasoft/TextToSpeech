using Olbrasoft.TextToSpeech.Core.Enums;
using Olbrasoft.TextToSpeech.Providers.Piper;

namespace TextToSpeech.Providers.Piper.Tests;

public class PiperConfigurationTests
{
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
        Assert.Empty(config.ModelPath);
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
    public void PiperConfiguration_SectionName_IsCorrect()
    {
        // Assert
        Assert.Equal("TTS:Piper", PiperConfiguration.SectionName);
    }

    [Fact]
    public void PiperConfiguration_Profiles_CanBeAdded()
    {
        // Arrange
        var config = new PiperConfiguration();

        // Act
        config.Profiles["fast"] = new PiperVoiceProfile { LengthScale = 0.5 };

        // Assert
        Assert.True(config.Profiles.ContainsKey("fast"));
        Assert.Equal(0.5, config.Profiles["fast"].LengthScale);
    }
}
