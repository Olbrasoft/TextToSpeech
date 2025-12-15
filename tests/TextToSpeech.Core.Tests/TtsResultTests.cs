using Olbrasoft.TextToSpeech.Core.Models;

namespace TextToSpeech.Core.Tests;

public class TtsResultTests
{
    [Fact]
    public void Ok_CreatesSuccessfulResult()
    {
        // Arrange
        var audioData = new MemoryAudioData { Data = new byte[] { 1, 2, 3 } };
        var providerUsed = "TestProvider";
        var generationTime = TimeSpan.FromMilliseconds(100);
        var audioDuration = TimeSpan.FromSeconds(2);

        // Act
        var result = TtsResult.Ok(audioData, providerUsed, generationTime, audioDuration);

        // Assert
        Assert.True(result.Success);
        Assert.Null(result.ErrorMessage);
        Assert.Same(audioData, result.Audio);
        Assert.Equal(providerUsed, result.ProviderUsed);
        Assert.Equal(generationTime, result.GenerationTime);
        Assert.Equal(audioDuration, result.AudioDuration);
    }

    [Fact]
    public void Ok_WithoutAudioDuration_HasNullDuration()
    {
        // Arrange
        var audioData = new MemoryAudioData { Data = new byte[] { 1, 2, 3 } };

        // Act
        var result = TtsResult.Ok(audioData, "TestProvider", TimeSpan.FromMilliseconds(50));

        // Assert
        Assert.True(result.Success);
        Assert.Null(result.AudioDuration);
    }

    [Fact]
    public void Fail_CreatesFailedResult()
    {
        // Arrange
        var errorMessage = "Test error";
        var providerUsed = "TestProvider";
        var generationTime = TimeSpan.FromMilliseconds(50);

        // Act
        var result = TtsResult.Fail(errorMessage, providerUsed, generationTime);

        // Assert
        Assert.False(result.Success);
        Assert.Equal(errorMessage, result.ErrorMessage);
        Assert.Null(result.Audio);
        Assert.Equal(providerUsed, result.ProviderUsed);
        Assert.Equal(generationTime, result.GenerationTime);
    }

    [Fact]
    public void Fail_WithoutProvider_HasNullProvider()
    {
        // Arrange & Act
        var result = TtsResult.Fail("Error");

        // Assert
        Assert.False(result.Success);
        Assert.Null(result.ProviderUsed);
    }

    [Fact]
    public void Fail_WithDefaultGenerationTime_HasZeroGenerationTime()
    {
        // Arrange & Act
        var result = TtsResult.Fail("Error");

        // Assert
        Assert.Equal(TimeSpan.Zero, result.GenerationTime);
    }
}
