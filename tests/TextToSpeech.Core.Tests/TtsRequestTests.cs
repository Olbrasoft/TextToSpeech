using Olbrasoft.TextToSpeech.Core.Models;

namespace TextToSpeech.Core.Tests;

public class TtsRequestTests
{
    [Fact]
    public void Text_IsRequired()
    {
        // Arrange & Act
        var request = new TtsRequest { Text = "Hello, world!" };

        // Assert
        Assert.Equal("Hello, world!", request.Text);
    }

    [Fact]
    public void Rate_DefaultValue_IsZero()
    {
        // Arrange & Act
        var request = new TtsRequest { Text = "Test" };

        // Assert
        Assert.Equal(0, request.Rate);
    }

    [Fact]
    public void Pitch_DefaultValue_IsZero()
    {
        // Arrange & Act
        var request = new TtsRequest { Text = "Test" };

        // Assert
        Assert.Equal(0, request.Pitch);
    }

    [Fact]
    public void Voice_IsOptional()
    {
        // Arrange & Act
        var request = new TtsRequest { Text = "Test" };

        // Assert
        Assert.Null(request.Voice);
    }

    [Fact]
    public void PreferredProvider_IsOptional()
    {
        // Arrange & Act
        var request = new TtsRequest { Text = "Test" };

        // Assert
        Assert.Null(request.PreferredProvider);
    }

    [Fact]
    public void AllProperties_CanBeSet()
    {
        // Arrange & Act
        var request = new TtsRequest
        {
            Text = "Hello",
            Voice = "cs-CZ-AntoninNeural",
            Rate = 10,
            Pitch = -5,
            PreferredProvider = "EdgeTTS-HTTP"
        };

        // Assert
        Assert.Equal("Hello", request.Text);
        Assert.Equal("cs-CZ-AntoninNeural", request.Voice);
        Assert.Equal(10, request.Rate);
        Assert.Equal(-5, request.Pitch);
        Assert.Equal("EdgeTTS-HTTP", request.PreferredProvider);
    }
}
