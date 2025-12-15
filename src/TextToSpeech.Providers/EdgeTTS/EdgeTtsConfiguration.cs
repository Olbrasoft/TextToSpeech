namespace Olbrasoft.TextToSpeech.Providers.EdgeTTS;

/// <summary>
/// Configuration for EdgeTTS HTTP provider.
/// </summary>
public sealed class EdgeTtsConfiguration
{
    /// <summary>
    /// Configuration section name for appsettings.json.
    /// </summary>
    public const string SectionName = "TTS:EdgeTTS";

    /// <summary>
    /// Gets or sets the base URL for EdgeTTS server.
    /// Default: http://localhost:5555
    /// </summary>
    public string BaseUrl { get; set; } = "http://localhost:5555";

    /// <summary>
    /// Gets or sets the default voice to use.
    /// Default: cs-CZ-AntoninNeural
    /// </summary>
    public string DefaultVoice { get; set; } = "cs-CZ-AntoninNeural";

    /// <summary>
    /// Gets or sets the request timeout.
    /// Default: 30 seconds
    /// </summary>
    public TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(30);
}
