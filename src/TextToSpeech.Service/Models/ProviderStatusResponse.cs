namespace Olbrasoft.TextToSpeech.Service.Models;

/// <summary>
/// Response model for provider status.
/// </summary>
public sealed record ProviderStatusResponse
{
    /// <summary>
    /// Gets the provider name.
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// Gets the provider status.
    /// </summary>
    public required string Status { get; init; }

    /// <summary>
    /// Gets the configured priority.
    /// </summary>
    public int Priority { get; init; }

    /// <summary>
    /// Gets whether the provider is enabled.
    /// </summary>
    public bool Enabled { get; init; }

    /// <summary>
    /// Gets the circuit breaker state.
    /// </summary>
    public string? CircuitState { get; init; }

    /// <summary>
    /// Gets the time when circuit will reset (if open).
    /// </summary>
    public DateTime? CircuitResetTime { get; init; }

    /// <summary>
    /// Gets the number of consecutive failures.
    /// </summary>
    public int ConsecutiveFailures { get; init; }
}
