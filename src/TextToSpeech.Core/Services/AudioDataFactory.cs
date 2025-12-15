using Olbrasoft.TextToSpeech.Core.Enums;
using Olbrasoft.TextToSpeech.Core.Models;

namespace Olbrasoft.TextToSpeech.Core.Services;

/// <summary>
/// Default implementation of IAudioDataFactory.
/// </summary>
public sealed class AudioDataFactory : IAudioDataFactory
{
    /// <inheritdoc />
    public AudioData Create(
        byte[] audioBytes,
        string text,
        string providerName,
        AudioOutputMode mode,
        string? outputDirectory = null,
        string fileNamePattern = "{provider}_{timestamp}_{hash}.mp3",
        string? existingFilePath = null,
        string contentType = "audio/mpeg")
    {
        ArgumentNullException.ThrowIfNull(audioBytes);
        ArgumentException.ThrowIfNullOrWhiteSpace(providerName);

        if (mode == AudioOutputMode.Memory)
        {
            return new MemoryAudioData
            {
                Data = audioBytes,
                ContentType = contentType
            };
        }

        // File mode - use existing path or generate new one
        var filePath = existingFilePath ?? GenerateFilePath(text, providerName, outputDirectory, fileNamePattern);

        // Only write if no existing file provided
        if (existingFilePath is null)
        {
            File.WriteAllBytes(filePath, audioBytes);
        }

        return new FileAudioData
        {
            FilePath = filePath,
            ContentType = contentType
        };
    }

    private static string GenerateFilePath(
        string text,
        string providerName,
        string? outputDirectory,
        string fileNamePattern)
    {
        var directory = outputDirectory ?? Path.GetTempPath();
        Directory.CreateDirectory(directory);

        var hash = TextHasher.ComputeHash(text);
        var timestamp = DateTime.UtcNow.ToString("yyyyMMdd_HHmmss");

        var fileName = fileNamePattern
            .Replace("{provider}", providerName, StringComparison.OrdinalIgnoreCase)
            .Replace("{timestamp}", timestamp, StringComparison.OrdinalIgnoreCase)
            .Replace("{hash}", hash, StringComparison.OrdinalIgnoreCase);

        return Path.Combine(directory, fileName);
    }
}
