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
    /// Gets or sets the voice to use.
    /// Default: cs-CZ-AntoninNeural
    /// </summary>
    public string Voice { get; set; } = "cs-CZ-AntoninNeural";

    /// <summary>
    /// Gets or sets the speech rate (e.g., "+10%", "-5%", "+0%").
    /// Default: +0%
    /// </summary>
    public string Rate { get; set; } = "+0%";

    /// <summary>
    /// Gets or sets the volume (e.g., "+0%", "+10%", "-5%").
    /// Default: +0%
    /// </summary>
    public string Volume { get; set; } = "+0%";

    /// <summary>
    /// Gets or sets the pitch (e.g., "+0Hz", "-5st", "+5Hz").
    /// Default: +0Hz
    /// </summary>
    public string Pitch { get; set; } = "+0Hz";

    /// <summary>
    /// Gets or sets the output audio format.
    /// Supported formats: audio-24khz-48kbitrate-mono-mp3, audio-24khz-96kbitrate-mono-mp3
    /// Default: audio-24khz-96kbitrate-mono-mp3
    /// </summary>
    public string OutputFormat { get; set; } = "audio-24khz-96kbitrate-mono-mp3";

    /// <summary>
    /// Gets or sets the request timeout.
    /// Default: 30 seconds
    /// </summary>
    public TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(30);
}
