using Olbrasoft.TextToSpeech.Core.Enums;

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
