namespace Olbrasoft.TextToSpeech.Orchestration.Configuration;

/// <summary>
/// Configuration for TTS orchestration layer.
/// </summary>
public sealed class OrchestrationConfig
{
    /// <summary>
    /// Configuration section name for appsettings.json.
    /// </summary>
    public const string SectionName = "TTS:Orchestration";

    /// <summary>
    /// Gets or sets the provider configurations in priority order.
    /// </summary>
    public List<ProviderConfig> Providers { get; set; } = [];
}

/// <summary>
/// Configuration for a single provider in the chain.
/// </summary>
public sealed class ProviderConfig
{
    /// <summary>
    /// Gets or sets the provider name (e.g., "EdgeTTS-HTTP", "Azure", "Piper").
    /// </summary>
    public string Name { get; set; } = "";

    /// <summary>
    /// Gets or sets the priority (lower = higher priority).
    /// </summary>
    public int Priority { get; set; }

    /// <summary>
    /// Gets or sets whether this provider is enabled.
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Gets or sets the circuit breaker configuration for this provider.
    /// </summary>
    public CircuitBreakerConfig CircuitBreaker { get; set; } = new();
}

/// <summary>
/// Circuit breaker configuration.
/// </summary>
public sealed class CircuitBreakerConfig
{
    /// <summary>
    /// Gets or sets the failure threshold before opening the circuit.
    /// Default: 3
    /// </summary>
    public int FailureThreshold { get; set; } = 3;

    /// <summary>
    /// Gets or sets the time to wait before attempting to close the circuit.
    /// Default: 5 minutes
    /// </summary>
    public TimeSpan ResetTimeout { get; set; } = TimeSpan.FromMinutes(5);

    /// <summary>
    /// Gets or sets whether to use exponential backoff for reset timeout.
    /// Default: true
    /// </summary>
    public bool UseExponentialBackoff { get; set; } = true;

    /// <summary>
    /// Gets or sets the maximum reset timeout when using exponential backoff.
    /// Default: 60 minutes
    /// </summary>
    public TimeSpan MaxResetTimeout { get; set; } = TimeSpan.FromMinutes(60);
}
