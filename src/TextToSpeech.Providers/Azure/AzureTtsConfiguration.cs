namespace Olbrasoft.TextToSpeech.Providers.Azure;

/// <summary>
/// Configuration for Azure Cognitive Services Speech TTS provider.
/// </summary>
public sealed class AzureTtsConfiguration
{
    /// <summary>
    /// Configuration section name.
    /// </summary>
    public const string SectionName = "TTS:AzureTTS";

    /// <summary>
    /// Azure Speech Services subscription key.
    /// Hosting application is responsible for loading this from secure storage.
    /// </summary>
    public string SubscriptionKey { get; set; } = string.Empty;

    /// <summary>
    /// Azure Speech Services region (e.g., "westeurope", "eastus").
    /// Default: westeurope.
    /// </summary>
    public string Region { get; set; } = "westeurope";

    /// <summary>
    /// Voice name for synthesis (e.g., "cs-CZ-AntoninNeural").
    /// </summary>
    public string Voice { get; set; } = "cs-CZ-AntoninNeural";

    /// <summary>
    /// Speech rate (e.g., "+10%", "-5%", "+0%").
    /// Default: +0%
    /// </summary>
    public string Rate { get; set; } = "+0%";

    /// <summary>
    /// Volume (e.g., "+0%", "+10%", "-5%").
    /// Default: +0%
    /// </summary>
    public string Volume { get; set; } = "+0%";

    /// <summary>
    /// Pitch (e.g., "+0Hz", "-5st", "+5Hz").
    /// Default: +0Hz
    /// </summary>
    public string Pitch { get; set; } = "+0Hz";

    /// <summary>
    /// Output audio format.
    /// Supported: Audio24Khz48KBitRateMonoMp3, Audio24Khz96KBitRateMonoMp3, Audio48Khz96KBitRateMonoMp3, Audio48Khz192KBitRateMonoMp3
    /// Default: Audio24Khz48KBitRateMonoMp3 (good quality, reasonable size).
    /// </summary>
    public string OutputFormat { get; set; } = "Audio24Khz48KBitRateMonoMp3";

    /// <summary>
    /// Connection timeout.
    /// </summary>
    public TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(30);
}
