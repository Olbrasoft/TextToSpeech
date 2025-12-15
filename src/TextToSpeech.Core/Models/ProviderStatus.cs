namespace Olbrasoft.TextToSpeech.Core.Models;

/// <summary>
/// Represents the operational status of a TTS provider.
/// </summary>
public enum ProviderStatus
{
    /// <summary>
    /// Provider is available and functioning normally.
    /// </summary>
    Available,

    /// <summary>
    /// Provider is temporarily unavailable (e.g., circuit breaker open).
    /// </summary>
    Unavailable,

    /// <summary>
    /// Provider has been explicitly disabled in configuration.
    /// </summary>
    Disabled,

    /// <summary>
    /// Provider is degraded (functioning but with issues).
    /// </summary>
    Degraded
}
