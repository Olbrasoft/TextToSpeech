namespace Olbrasoft.TextToSpeech.Providers.GoogleCloud;

/// <summary>
/// Represents the availability state of an API key.
/// </summary>
public enum ApiKeyState
{
    /// <summary>
    /// Key is ready to use.
    /// </summary>
    Available,

    /// <summary>
    /// Key is temporarily unavailable due to rate limiting (HTTP 429).
    /// Will become available after cooldown period.
    /// </summary>
    RateLimited,

    /// <summary>
    /// Key has exceeded its quota (HTTP 403).
    /// Will become available after cooldown period (typically 24 hours).
    /// </summary>
    QuotaExceeded,

    /// <summary>
    /// Key is permanently invalid (HTTP 401).
    /// Will not be used again.
    /// </summary>
    Invalid
}
