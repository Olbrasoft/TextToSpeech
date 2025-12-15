namespace Olbrasoft.TextToSpeech.Core.Models;

/// <summary>
/// Represents information about a TTS voice.
/// </summary>
public sealed record VoiceInfo
{
    /// <summary>
    /// Gets the voice identifier (e.g., "cs-CZ-AntoninNeural").
    /// </summary>
    public required string Id { get; init; }

    /// <summary>
    /// Gets the language code (e.g., "cs-CZ").
    /// </summary>
    public required string Language { get; init; }

    /// <summary>
    /// Gets the display name (e.g., "Antonín").
    /// </summary>
    public string? DisplayName { get; init; }

    /// <summary>
    /// Gets the gender (e.g., "Male", "Female").
    /// </summary>
    public string? Gender { get; init; }
}
