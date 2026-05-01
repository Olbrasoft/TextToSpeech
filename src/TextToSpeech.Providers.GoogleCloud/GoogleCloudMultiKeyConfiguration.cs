namespace Olbrasoft.TextToSpeech.Providers.GoogleCloud;

/// <summary>
/// Configuration for Google Cloud TTS provider with multiple API key support.
/// API keys are stored securely in SecureStore vault and referenced by secret key names.
/// </summary>
public sealed class GoogleCloudMultiKeyConfiguration
{
    /// <summary>
    /// Configuration section name for appsettings.json.
    /// </summary>
    public const string SectionName = "TTS:GoogleCloudMultiKey";

    /// <summary>
    /// Google Cloud TTS API endpoint.
    /// </summary>
    internal const string ApiEndpoint = "https://texttospeech.googleapis.com/v1/text:synthesize";

    /// <summary>
    /// Gets or sets the list of API key configurations.
    /// IMPORTANT: Contains only SecureStore key NAMES, not actual keys!
    /// Keys are resolved via IConfiguration (which includes SecureStore provider).
    /// </summary>
    public List<ApiKeyConfig> ApiKeySecrets { get; set; } = [];

    /// <summary>
    /// Gets or sets the default voice to use.
    /// Default: cs-CZ-Chirp3-HD-Achird (Czech male voice)
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

    /// <summary>
    /// Gets or sets the cooldown duration when a key hits rate limit (HTTP 429).
    /// Default: 1 hour
    /// </summary>
    public TimeSpan RateLimitCooldown { get; set; } = TimeSpan.FromHours(1);

    /// <summary>
    /// Gets or sets the cooldown duration when a key hits quota exceeded (HTTP 403).
    /// Default: 24 hours
    /// </summary>
    public TimeSpan QuotaExceededCooldown { get; set; } = TimeSpan.FromHours(24);

    /// <summary>
    /// Gets or sets the monthly character limit per key. When a key reaches this
    /// number of synthesized characters in the current UTC month, it is taken
    /// out of rotation until the 1st of the next month.
    /// Default: 1 000 000 (Google Chirp3-HD free tier).
    /// Set to 0 to disable the cap (key is never throttled by this counter).
    /// </summary>
    /// <remarks>
    /// Google does NOT return 429 when the Chirp3-HD free tier is exhausted - it
    /// silently bills $30 / 1M chars. This cap lets you stay strictly within the
    /// free tier across multiple billing accounts.
    /// </remarks>
    public long MonthlyCharacterLimit { get; set; } = 1_000_000;

    /// <summary>
    /// Gets or sets whether to rotate between available keys in round-robin order.
    /// When <c>false</c>, the provider always picks the first available key
    /// (legacy behavior). When <c>true</c>, traffic is spread evenly across keys
    /// so per-key counters approach their limit at roughly the same rate.
    /// Default: <c>true</c>.
    /// </summary>
    public bool EnableRoundRobin { get; set; } = true;
}

/// <summary>
/// Configuration for a single API key reference.
/// </summary>
public sealed class ApiKeyConfig
{
    /// <summary>
    /// Gets or sets the configuration key name for the API key.
    /// The provider reads it via IConfiguration[SecretKey].
    /// Example: "GoogleTTS:Key1" - resolved from SecureStore vault
    /// </summary>
    public string SecretKey { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the display name for logging (e.g., "primary", "fallback-1").
    /// IMPORTANT: This name is shown in logs, NOT the actual API key.
    /// </summary>
    public string Name { get; set; } = string.Empty;
}
