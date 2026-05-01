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
    Invalid,

    /// <summary>
    /// Key encountered a temporary error (e.g., HTTP 400, 5xx).
    /// Will become available after a short cooldown period.
    /// </summary>
    TemporaryError,

    /// <summary>
    /// Key has reached its configured monthly character limit (e.g., 1M chars on
    /// Chirp3-HD free tier). Google will not return 429/403 - we self-throttle to
    /// avoid silent paid usage. Becomes <see cref="Available"/> again on the 1st
    /// day of the next month (UTC).
    /// </summary>
    MonthlyLimitExceeded
}
