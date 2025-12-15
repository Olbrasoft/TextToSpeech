namespace Olbrasoft.TextToSpeech.Providers.Google;

/// <summary>
/// Configuration for Google TTS (gTTS) provider.
/// Uses Python gTTS library via command line.
/// </summary>
public sealed class GoogleTtsConfiguration
{
    /// <summary>
    /// Configuration section name.
    /// </summary>
    public const string SectionName = "TTS:GoogleTTS";

    /// <summary>
    /// Language code for gTTS (e.g., "cs" for Czech, "en" for English).
    /// </summary>
    public string Language { get; set; } = "cs";

    /// <summary>
    /// Path to gtts-cli executable. If empty, assumes it's in PATH.
    /// </summary>
    public string GttsCliPath { get; set; } = "gtts-cli";

    /// <summary>
    /// Slow mode (speaks more slowly). Default: false.
    /// </summary>
    public bool Slow { get; set; } = false;

    /// <summary>
    /// Command timeout in seconds.
    /// </summary>
    public int TimeoutSeconds { get; set; } = 60;
}
