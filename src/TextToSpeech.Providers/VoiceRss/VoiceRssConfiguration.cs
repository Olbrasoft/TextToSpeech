namespace Olbrasoft.TextToSpeech.Providers.VoiceRss;

/// <summary>
/// Configuration for VoiceRSS TTS provider.
/// </summary>
public sealed class VoiceRssConfiguration
{
    /// <summary>
    /// Configuration section name.
    /// </summary>
    public const string SectionName = "TTS:VoiceRSS";

    /// <summary>
    /// API key for VoiceRSS service.
    /// Can also be set via environment variable VOICERSS_API_KEY.
    /// </summary>
    public string ApiKey { get; set; } = string.Empty;

    /// <summary>
    /// Path to file containing API key.
    /// Supports ~ for home directory (e.g., "~/Dokumenty/credentials/voicerss.txt").
    /// </summary>
    public string ApiKeyFile { get; set; } = string.Empty;

    /// <summary>
    /// Language code (e.g., "cs-cz" for Czech, "en-us" for US English).
    /// </summary>
    public string Language { get; set; } = "cs-cz";

    /// <summary>
    /// Voice name (e.g., "Josef" for Czech male voice).
    /// </summary>
    public string Voice { get; set; } = "Josef";

    /// <summary>
    /// Speech rate (e.g., "+0%", "+10%", "-5%").
    /// VoiceRSS uses Speed parameter internally (-10 to 10).
    /// Default: +0%
    /// </summary>
    public string Rate { get; set; } = "+0%";

    /// <summary>
    /// Volume (not supported by VoiceRSS, kept for consistency).
    /// Default: +0%
    /// </summary>
    public string Volume { get; set; } = "+0%";

    /// <summary>
    /// Speech speed (-10 to 10, 0 is normal). Positive = faster.
    /// This is the native VoiceRSS parameter.
    /// </summary>
    public int Speed { get; set; } = 0;

    /// <summary>
    /// Audio format (e.g., "44khz_16bit_mono", "22khz_16bit_stereo").
    /// </summary>
    public string AudioFormat { get; set; } = "44khz_16bit_mono";

    /// <summary>
    /// Audio codec (MP3, WAV, OGG, AAC, CAF).
    /// </summary>
    public string AudioCodec { get; set; } = "MP3";

    /// <summary>
    /// Request timeout.
    /// </summary>
    public TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(30);
}
