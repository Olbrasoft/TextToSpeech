namespace TextToSpeech.Core.Tests;

using Olbrasoft.TextToSpeech.Core;

public class TextHasherTests
{
    [Fact]
    public void ComputeHash_ReturnsEightCharacterString()
    {
        // Act
        var hash = TextHasher.ComputeHash("Hello, world!");

        // Assert
        Assert.Equal(8, hash.Length);
    }

    [Fact]
    public void ComputeHash_ReturnsSameHashForSameText()
    {
        // Arrange
        var text = "Test text for hashing";

        // Act
        var hash1 = TextHasher.ComputeHash(text);
        var hash2 = TextHasher.ComputeHash(text);

        // Assert
        Assert.Equal(hash1, hash2);
    }

    [Fact]
    public void ComputeHash_ReturnsDifferentHashForDifferentText()
    {
        // Act
        var hash1 = TextHasher.ComputeHash("First text");
        var hash2 = TextHasher.ComputeHash("Second text");

        // Assert
        Assert.NotEqual(hash1, hash2);
    }

    [Fact]
    public void ComputeHash_HandlesEmptyString()
    {
        // Act
        var hash = TextHasher.ComputeHash(string.Empty);

        // Assert
        Assert.Equal(8, hash.Length);
    }

    [Fact]
    public void ComputeHash_HandlesNullString()
    {
        // Act
        var hash = TextHasher.ComputeHash(null!);

        // Assert
        Assert.Equal(8, hash.Length);
    }

    [Fact]
    public void ComputeHash_ReturnsHexString()
    {
        // Act
        var hash = TextHasher.ComputeHash("test");

        // Assert
        Assert.True(hash.All(c => char.IsLetterOrDigit(c)));
        Assert.True(hash.All(c => c is >= '0' and <= '9' or >= 'A' and <= 'F'));
    }
}
