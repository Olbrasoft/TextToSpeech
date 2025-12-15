using Olbrasoft.TextToSpeech.Core.Enums;
using Olbrasoft.TextToSpeech.Core.Models;

namespace Olbrasoft.TextToSpeech.Core.Services;

/// <summary>
/// Factory for creating AudioData instances based on output mode.
/// </summary>
public interface IAudioDataFactory
{
    /// <summary>
    /// Creates an AudioData instance based on the specified output mode.
    /// </summary>
    /// <param name="audioBytes">The audio data bytes.</param>
    /// <param name="text">The original text (used for hashing in file names).</param>
    /// <param name="providerName">The name of the TTS provider.</param>
    /// <param name="mode">The output mode (Memory or File).</param>
    /// <param name="outputDirectory">The directory for file output (optional, defaults to temp).</param>
    /// <param name="fileNamePattern">The pattern for file names (supports {provider}, {timestamp}, {hash}).</param>
    /// <param name="existingFilePath">An existing file path to use instead of generating new one.</param>
    /// <param name="contentType">The MIME content type of the audio.</param>
    /// <returns>An AudioData instance (MemoryAudioData or FileAudioData).</returns>
    AudioData Create(
        byte[] audioBytes,
        string text,
        string providerName,
        AudioOutputMode mode,
        string? outputDirectory = null,
        string fileNamePattern = "{provider}_{timestamp}_{hash}.mp3",
        string? existingFilePath = null,
        string contentType = "audio/mpeg");
}
