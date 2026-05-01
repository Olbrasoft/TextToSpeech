namespace Olbrasoft.TextToSpeech.Providers.GoogleCloud;

/// <summary>
/// Persistence callback for per-key TTS usage. The provider keeps an in-memory
/// counter for routing decisions and notifies the store after every synthesis,
/// so usage and failure state survive process restarts.
/// </summary>
/// <remarks>
/// Implementations are typically backed by a database (e.g. EF Core). The provider
/// calls these methods on a background-friendly path - implementations should be
/// fast and non-blocking; exceptions are caught and logged, never propagated to
/// the synthesis result.
/// </remarks>
public interface IApiKeyUsageStore
{
    /// <summary>
    /// Loads the persisted usage snapshot for the given key on provider startup.
    /// Return <c>null</c> if no record exists yet.
    /// </summary>
    /// <param name="keyName">The key's display name (e.g. "primary", "fallback-1").</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<ApiKeyUsageRecord?> LoadAsync(string keyName, CancellationToken cancellationToken = default);

    /// <summary>
    /// Persists the latest usage snapshot for a key (called after each call,
    /// success or failure).
    /// </summary>
    Task SaveAsync(ApiKeyUsageRecord record, CancellationToken cancellationToken = default);
}

/// <summary>
/// Persisted snapshot of a single API key's usage and health. Loaded on startup
/// and used to seed the in-memory state, with normalization applied: elapsed
/// cooldowns reset to <see cref="ApiKeyState.Available"/>; counters reset when
/// the persisted month no longer matches the current UTC month; and a key
/// otherwise <see cref="ApiKeyState.Available"/> is upgraded to
/// <see cref="ApiKeyState.MonthlyLimitExceeded"/> when its persisted counter
/// already meets the configured cap (other parked states such as
/// <see cref="ApiKeyState.RateLimited"/> / <see cref="ApiKeyState.Invalid"/>
/// are kept as persisted). The record is therefore not applied verbatim - it
/// is reconciled with current wall-clock state.
/// </summary>
public sealed record ApiKeyUsageRecord
{
    /// <summary>Display name of the key (matches <see cref="ApiKeyConfig.Name"/>).</summary>
    public required string KeyName { get; init; }

    /// <summary>UTC year+month the counter applies to (e.g. 2026-05).</summary>
    public required int Year { get; init; }

    /// <summary>UTC month (1-12).</summary>
    public required int Month { get; init; }

    /// <summary>Characters synthesized in this UTC month.</summary>
    public required long MonthlyCharacterCount { get; init; }

    /// <summary>Total successful syntheses (lifetime, persisted across restarts).</summary>
    public long TotalSuccesses { get; init; }

    /// <summary>Total failed syntheses (lifetime, persisted across restarts).</summary>
    public long TotalFailures { get; init; }

    /// <summary>Number of consecutive failures since the last success.</summary>
    public int ConsecutiveFailures { get; init; }

    /// <summary>UTC timestamp of the last successful call (null if never).</summary>
    public DateTime? LastSuccessUtc { get; init; }

    /// <summary>UTC timestamp of the last failed call (null if never).</summary>
    public DateTime? LastErrorUtc { get; init; }

    /// <summary>Free-form reason of the last error (e.g. "429", "401", "MonthlyLimitExceeded").</summary>
    public string? LastErrorReason { get; init; }

    /// <summary>
    /// Routing state at the moment the snapshot was saved. Restored on startup so
    /// rate-limited / quota-exceeded / invalid keys remain parked across process
    /// restarts. Defaults to <see cref="ApiKeyState.Available"/>.
    /// </summary>
    public ApiKeyState State { get; init; } = ApiKeyState.Available;

    /// <summary>
    /// UTC time until which the key is on cooldown (null if available or invalid).
    /// On hydrate, expired cooldowns reset the key to <see cref="ApiKeyState.Available"/>.
    /// </summary>
    public DateTime? CooldownUntilUtc { get; init; }
}
