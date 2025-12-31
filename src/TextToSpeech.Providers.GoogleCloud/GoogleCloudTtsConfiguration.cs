namespace Olbrasoft.TextToSpeech.Providers.GoogleCloud;

/// <summary>
/// Configuration for Google Cloud Text-to-Speech API provider.
/// </summary>
public sealed class GoogleCloudTtsConfiguration
{
    /// <summary>
    /// Configuration section name for appsettings.json.
    /// </summary>
    public const string SectionName = "TTS:GoogleCloud";

    /// <summary>
    /// Google Cloud TTS API endpoint.
    /// </summary>
    internal const string API_ENDPOINT = "https://texttospeech.googleapis.com/v1/text:synthesize";

    /// <summary>
    /// Gets or sets the Google Cloud API key.
    /// Required for authentication.
    /// </summary>
    public string ApiKey { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the voice to use.
    /// Default: cs-CZ-Chirp3-HD-Achird (Czech male voice)
    /// See: https://cloud.google.com/text-to-speech/docs/voices
    /// </summary>
    public string Voice { get; set; } = "cs-CZ-Chirp3-HD-Achird";

    /// <summary>
    /// Gets or sets the audio encoding format.
    /// Supported: MP3, LINEAR16, OGG_OPUS
    /// Default: MP3
    /// </summary>
    public string AudioEncoding { get; set; } = "MP3";

    /// <summary>
    /// Gets or sets the speaking rate.
    /// Range: 0.25 to 4.0 (1.0 is normal)
    /// Default: 1.0
    /// </summary>
    public double SpeakingRate { get; set; } = 1.0;

    /// <summary>
    /// Gets or sets the pitch adjustment.
    /// Range: -20.0 to 20.0 semitones (0.0 is normal)
    /// Default: 0.0
    /// </summary>
    public double Pitch { get; set; } = 0.0;

    /// <summary>
    /// Gets or sets the volume gain in dB.
    /// Range: -96.0 to 16.0
    /// Default: 0.0
    /// </summary>
    public double VolumeGainDb { get; set; } = 0.0;

    /// <summary>
    /// Gets or sets the sample rate in hertz.
    /// Default: 24000 (24kHz)
    /// </summary>
    public int SampleRateHertz { get; set; } = 24000;

    /// <summary>
    /// Gets or sets the HTTP request timeout.
    /// Default: 30 seconds
    /// </summary>
    public TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(30);
}
