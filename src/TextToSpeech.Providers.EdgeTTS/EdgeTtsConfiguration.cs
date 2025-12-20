namespace Olbrasoft.TextToSpeech.Providers.EdgeTTS;

/// <summary>
/// Configuration for EdgeTTS WebSocket provider.
/// Uses direct WebSocket communication with Microsoft Edge TTS API.
/// </summary>
public sealed class EdgeTtsConfiguration
{
    /// <summary>
    /// Configuration section name for appsettings.json.
    /// </summary>
    public const string SectionName = "TTS:EdgeTTS";

    /// <summary>
    /// WebSocket base URL for Microsoft Edge TTS.
    /// </summary>
    internal const string BASE_URL = "speech.platform.bing.com/consumer/speech/synthesize/readaloud";

    /// <summary>
    /// Trusted client token for Microsoft Edge TTS API.
    /// </summary>
    internal const string TRUSTED_CLIENT_TOKEN = "6A5AA1D4EAFF4E9FB37E23D68491D6F4";

    /// <summary>
    /// Full WebSocket URL with client token.
    /// </summary>
    internal const string WSS_URL = $"wss://{BASE_URL}/edge/v1?TrustedClientToken={TRUSTED_CLIENT_TOKEN}";

    /// <summary>
    /// Chromium version used in User-Agent header.
    /// </summary>
    internal const string CHROMIUM_FULL_VERSION = "143.0.3650.75";

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
    /// Supported formats: audio-24khz-48kbitrate-mono-mp3, audio-24khz-96kbitrate-mono-mp3, audio-48khz-96kbitrate-mono-mp3
    /// Default: audio-24khz-96kbitrate-mono-mp3
    /// </summary>
    public string OutputFormat { get; set; } = "audio-24khz-96kbitrate-mono-mp3";

    /// <summary>
    /// Gets or sets the WebSocket connection timeout.
    /// Default: 30 seconds
    /// </summary>
    public TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(30);
}
