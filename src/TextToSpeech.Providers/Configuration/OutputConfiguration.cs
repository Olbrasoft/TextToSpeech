using Olbrasoft.TextToSpeech.Core.Enums;

namespace Olbrasoft.TextToSpeech.Providers.Configuration;

/// <summary>
/// Default implementation of IOutputConfiguration.
/// </summary>
public sealed class OutputConfiguration : IOutputConfiguration
{
    /// <summary>
    /// Configuration section name for appsettings.json.
    /// </summary>
    public const string SectionName = "TTS:Output";

    /// <inheritdoc />
    public AudioOutputMode Mode { get; set; } = AudioOutputMode.Memory;

    /// <inheritdoc />
    public string? OutputDirectory { get; set; }

    /// <inheritdoc />
    public string FileNamePattern { get; set; } = "{provider}_{timestamp}_{hash}.mp3";
}
