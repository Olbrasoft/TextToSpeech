using Olbrasoft.TextToSpeech.Core.Models;

namespace Olbrasoft.TextToSpeech.Tests.Models;

public class TtsRequestTests
{
    [Fact]
    public void TtsRequest_RequiredText_MustBeSet()
    {
        // Arrange & Act
        var request = new TtsRequest { Text = "Hello, world!" };

        // Assert
        Assert.Equal("Hello, world!", request.Text);
    }

    [Fact]
    public void TtsRequest_DefaultRate_IsZero()
    {
        // Arrange & Act
        var request = new TtsRequest { Text = "Test" };

        // Assert
        Assert.Equal(0, request.Rate);
    }

    [Fact]
    public void TtsRequest_DefaultPitch_IsZero()
    {
        // Arrange & Act
        var request = new TtsRequest { Text = "Test" };

        // Assert
        Assert.Equal(0, request.Pitch);
    }

    [Fact]
    public void TtsRequest_Voice_IsOptional()
    {
        // Arrange & Act
        var request = new TtsRequest { Text = "Test" };

        // Assert
        Assert.Null(request.Voice);
    }

    [Fact]
    public void TtsRequest_PreferredProvider_IsOptional()
    {
        // Arrange & Act
        var request = new TtsRequest { Text = "Test" };

        // Assert
        Assert.Null(request.PreferredProvider);
    }

    [Fact]
    public void TtsRequest_AllProperties_CanBeSet()
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
