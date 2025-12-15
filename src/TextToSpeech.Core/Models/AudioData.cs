namespace Olbrasoft.TextToSpeech.Core.Models;

/// <summary>
/// Base class for audio output data. Use pattern matching to handle different modes.
/// </summary>
public abstract class AudioData
{
    /// <summary>
    /// Gets the MIME content type of the audio (e.g., "audio/mpeg", "audio/wav").
    /// </summary>
    public string ContentType { get; init; } = "audio/mpeg";
}

/// <summary>
/// Audio data stored in memory as byte array.
/// </summary>
public sealed class MemoryAudioData : AudioData
{
    /// <summary>
    /// Gets the audio data as byte array.
    /// </summary>
    public required byte[] Data { get; init; }
}

/// <summary>
/// Audio data saved to file system.
/// </summary>
public sealed class FileAudioData : AudioData
{
    /// <summary>
    /// Gets the path to the audio file.
    /// </summary>
    public required string FilePath { get; init; }
}
