namespace Olbrasoft.TextToSpeech.Providers.GoogleCloud;

/// <summary>
/// Read-only snapshot of a single API key's runtime state. Returned by
/// <see cref="GoogleCloudMultiKeyTtsProvider.GetKeysUsage"/> for dashboards
/// and health checks.
/// </summary>
public sealed record ApiKeyUsageSnapshot
{
    /// <summary>Zero-based index of the key in the configured list.</summary>
    public required int Index { get; init; }

    /// <summary>Display name of the key (e.g. "primary", "fallback-1").</summary>
    public required string Name { get; init; }

    /// <summary>Current routing state.</summary>
    public required ApiKeyState State { get; init; }

    /// <summary>UTC time until which the key is on cooldown (null if available).</summary>
    public DateTime? CooldownUntilUtc { get; init; }

    /// <summary>Characters synthesized this UTC month.</summary>
    public long MonthlyCharacterCount { get; init; }

    /// <summary>Configured monthly character limit (informational; 0 = no limit).</summary>
    public long MonthlyCharacterLimit { get; init; }

    /// <summary>
    /// Total successful calls. Persisted via <see cref="IApiKeyUsageStore"/> when
    /// configured, so this is a lifetime counter across restarts; otherwise it is
    /// process-local.
    /// </summary>
    public long TotalSuccesses { get; init; }

    /// <summary>
    /// Total failed calls. Persisted via <see cref="IApiKeyUsageStore"/> when
    /// configured, so this is a lifetime counter across restarts; otherwise it is
    /// process-local.
    /// </summary>
    public long TotalFailures { get; init; }

    /// <summary>Consecutive failures since the last success.</summary>
    public int ConsecutiveFailures { get; init; }

    /// <summary>UTC timestamp of the last successful call (null if never).</summary>
    public DateTime? LastSuccessUtc { get; init; }

    /// <summary>UTC timestamp of the last failed call (null if never).</summary>
    public DateTime? LastErrorUtc { get; init; }

    /// <summary>Reason of the last error (HTTP status or sentinel like "MonthlyLimitExceeded").</summary>
    public string? LastErrorReason { get; init; }
}
