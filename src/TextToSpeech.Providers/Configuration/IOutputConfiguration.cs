namespace Olbrasoft.TextToSpeech.Providers.Configuration;

/// <summary>
/// Defines how audio output should be handled (memory or file).
/// </summary>
public interface IOutputConfiguration
{
    /// <summary>
    /// Gets the output mode (Memory or File).
    /// </summary>
    AudioOutputMode Mode { get; }

    /// <summary>
    /// Gets the directory for file output (required when Mode is File).
    /// </summary>
    string? OutputDirectory { get; }

    /// <summary>
    /// Gets the file name pattern for generated files.
    /// Supports placeholders: {provider}, {timestamp}, {hash}
    /// </summary>
    string FileNamePattern { get; }
}

/// <summary>
/// Specifies how synthesized audio should be stored.
/// </summary>
public enum AudioOutputMode
{
    /// <summary>
    /// Audio is returned in memory as byte array. Fast, no disk I/O.
    /// </summary>
    Memory,

    /// <summary>
    /// Audio is saved to disk. Better for caching and debugging.
    /// </summary>
    File
}
