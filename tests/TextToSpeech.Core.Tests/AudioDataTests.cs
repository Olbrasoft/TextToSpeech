using Olbrasoft.TextToSpeech.Core.Models;

namespace TextToSpeech.Core.Tests;

public class AudioDataTests
{
    [Fact]
    public void MemoryAudioData_DefaultContentType_IsAudioMpeg()
    {
        // Arrange & Act
        var data = new MemoryAudioData { Data = new byte[] { 1, 2, 3 } };

        // Assert
        Assert.Equal("audio/mpeg", data.ContentType);
    }

    [Fact]
    public void MemoryAudioData_CustomContentType_IsPreserved()
    {
        // Arrange & Act
        var data = new MemoryAudioData
        {
            Data = new byte[] { 1, 2, 3 },
            ContentType = "audio/wav"
        };

        // Assert
        Assert.Equal("audio/wav", data.ContentType);
    }

    [Fact]
    public void MemoryAudioData_StoresData()
    {
        // Arrange
        var bytes = new byte[] { 1, 2, 3, 4, 5 };

        // Act
        var data = new MemoryAudioData { Data = bytes };

        // Assert
        Assert.Equal(bytes, data.Data);
    }

    [Fact]
    public void FileAudioData_DefaultContentType_IsAudioMpeg()
    {
        // Arrange & Act
        var data = new FileAudioData { FilePath = "/tmp/test.mp3" };

        // Assert
        Assert.Equal("audio/mpeg", data.ContentType);
    }

    [Fact]
    public void FileAudioData_StoresFilePath()
    {
        // Arrange
        var path = "/tmp/test.mp3";

        // Act
        var data = new FileAudioData { FilePath = path };

        // Assert
        Assert.Equal(path, data.FilePath);
    }

    [Fact]
    public void FileAudioData_CustomContentType_IsPreserved()
    {
        // Arrange & Act
        var data = new FileAudioData
        {
            FilePath = "/tmp/test.wav",
            ContentType = "audio/wav"
        };

        // Assert
        Assert.Equal("audio/wav", data.ContentType);
    }
}
