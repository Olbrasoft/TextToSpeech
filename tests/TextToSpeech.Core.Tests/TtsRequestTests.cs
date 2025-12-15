using System.ComponentModel.DataAnnotations;
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
    public void IsValid_ValidRequest_ReturnsTrue()
    {
        // Arrange
        var request = new TtsRequest { Text = "Test" };

        // Act
        var isValid = request.IsValid(out var errors);

        // Assert
        Assert.True(isValid);
        Assert.Empty(errors);
    }

    [Fact]
    public void IsValid_WhitespaceText_ReturnsFalse()
    {
        // Arrange
        var request = new TtsRequest { Text = "   " };

        // Act
        var isValid = request.IsValid(out var errors);

        // Assert
        Assert.False(isValid);
        Assert.NotEmpty(errors);
        // Required attribute catches whitespace-only strings
        Assert.Contains(errors, e => e.Contains("Text"));
    }

    [Fact]
    public void IsValid_RateOutOfRange_ReturnsFalse()
    {
        // Arrange
        var request = new TtsRequest { Text = "Test", Rate = 150 };

        // Act
        var isValid = request.IsValid(out var errors);

        // Assert
        Assert.False(isValid);
        Assert.Contains(errors, e => e.Contains("Rate"));
    }

    [Fact]
    public void IsValid_PitchOutOfRange_ReturnsFalse()
    {
        // Arrange
        var request = new TtsRequest { Text = "Test", Pitch = -150 };

        // Act
        var isValid = request.IsValid(out var errors);

        // Assert
        Assert.False(isValid);
        Assert.Contains(errors, e => e.Contains("Pitch"));
    }

    [Fact]
    public void EnsureValid_InvalidRequest_ThrowsValidationException()
    {
        // Arrange
        var request = new TtsRequest { Text = "   " };

        // Act & Assert
        Assert.Throws<ValidationException>(() => request.EnsureValid());
    }

    [Fact]
    public void EnsureValid_ValidRequest_DoesNotThrow()
    {
        // Arrange
        var request = new TtsRequest { Text = "Test" };

        // Act & Assert
        var exception = Record.Exception(() => request.EnsureValid());
        Assert.Null(exception);
    }

    [Fact]
    public void Constants_HaveExpectedValues()
    {
        // Assert
        Assert.Equal(10000, TtsRequest.MaxTextLength);
        Assert.Equal(-100, TtsRequest.MinRate);
        Assert.Equal(100, TtsRequest.MaxRate);
        Assert.Equal(-100, TtsRequest.MinPitch);
        Assert.Equal(100, TtsRequest.MaxPitch);
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
