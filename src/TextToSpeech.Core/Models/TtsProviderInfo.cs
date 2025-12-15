namespace Olbrasoft.TextToSpeech.Core.Models;

/// <summary>
/// Provides information about a TTS provider's current status and capabilities.
/// </summary>
public sealed class TtsProviderInfo
{
    /// <summary>
    /// Gets the provider name.
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// Gets the current operational status.
    /// </summary>
    public ProviderStatus Status { get; init; }

    /// <summary>
    /// Gets the reason for the current status (e.g., "Model file not found").
    /// </summary>
    public string? StatusReason { get; init; }

    /// <summary>
    /// Gets the list of voices supported by this provider.
    /// </summary>
    public IReadOnlyList<VoiceInfo> SupportedVoices { get; init; } = [];

    /// <summary>
    /// Gets the timestamp of the last successful synthesis.
    /// </summary>
    public DateTime? LastSuccessTime { get; init; }

    /// <summary>
    /// Gets whether this provider is currently the default/primary.
    /// </summary>
    public bool IsDefault { get; init; }
}
