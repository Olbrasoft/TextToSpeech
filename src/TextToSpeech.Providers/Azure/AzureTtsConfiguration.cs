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
    /// Can also be set via environment variable AZURE_SPEECH_KEY.
    /// </summary>
    public string SubscriptionKey { get; set; } = string.Empty;

    /// <summary>
    /// Azure Speech Services region (e.g., "westeurope", "eastus").
    /// Can also be set via environment variable AZURE_SPEECH_REGION.
    /// Default: westeurope.
    /// </summary>
    public string Region { get; set; } = "westeurope";

    /// <summary>
    /// Default voice name for synthesis (e.g., "cs-CZ-AntoninNeural").
    /// Can also be set via environment variable AZURE_SPEECH_VOICE.
    /// </summary>
    public string DefaultVoice { get; set; } = "cs-CZ-AntoninNeural";

    /// <summary>
    /// Output audio format.
    /// Default: Audio24Khz48KBitRateMonoMp3 (good quality, reasonable size).
    /// </summary>
    public string OutputFormat { get; set; } = "Audio24Khz48KBitRateMonoMp3";

    /// <summary>
    /// Connection timeout.
    /// </summary>
    public TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(30);
}
