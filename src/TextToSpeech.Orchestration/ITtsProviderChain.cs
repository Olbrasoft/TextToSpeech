using Olbrasoft.TextToSpeech.Core.Models;

namespace Olbrasoft.TextToSpeech.Orchestration;

/// <summary>
/// Orchestrates TTS synthesis across multiple providers with fallback support.
/// </summary>
public interface ITtsProviderChain
{
    /// <summary>
    /// Synthesizes speech using the provider chain with automatic fallback.
    /// </summary>
    /// <param name="request">The TTS request.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The synthesis result from the first successful provider.</returns>
    Task<TtsResult> SynthesizeAsync(TtsRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the current status of all providers in the chain.
    /// </summary>
    IReadOnlyList<ProviderChainStatus> GetProvidersStatus();
}

/// <summary>
/// Status of a provider in the chain.
/// </summary>
public sealed record ProviderChainStatus
{
    /// <summary>
    /// Gets the provider name.
    /// </summary>
    public required string ProviderName { get; init; }

    /// <summary>
    /// Gets the configured priority.
    /// </summary>
    public int Priority { get; init; }

    /// <summary>
    /// Gets whether the provider is enabled in configuration.
    /// </summary>
    public bool Enabled { get; init; }

    /// <summary>
    /// Gets the circuit breaker state.
    /// </summary>
    public CircuitState CircuitState { get; init; }

    /// <summary>
    /// Gets the time when circuit will attempt to reset (if open).
    /// </summary>
    public DateTime? CircuitResetTime { get; init; }

    /// <summary>
    /// Gets the number of consecutive failures.
    /// </summary>
    public int ConsecutiveFailures { get; init; }
}

/// <summary>
/// Circuit breaker state.
/// </summary>
public enum CircuitState
{
    /// <summary>
    /// Circuit is closed - requests flow normally.
    /// </summary>
    Closed,

    /// <summary>
    /// Circuit is open - requests are rejected.
    /// </summary>
    Open,

    /// <summary>
    /// Circuit is half-open - allowing test requests.
    /// </summary>
    HalfOpen
}
