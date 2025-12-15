namespace Olbrasoft.TextToSpeech.Core.Models;

/// <summary>
/// Represents a text-to-speech synthesis request.
/// </summary>
public sealed class TtsRequest
{
    /// <summary>
    /// Gets the text to synthesize.
    /// </summary>
    public required string Text { get; init; }

    /// <summary>
    /// Gets the voice identifier (e.g., "cs-CZ-AntoninNeural").
    /// If not specified, the provider's default voice is used.
    /// </summary>
    public string? Voice { get; init; }

    /// <summary>
    /// Gets the speech rate adjustment (-100 to +100, default 0).
    /// Negative values slow down speech, positive values speed it up.
    /// </summary>
    public int Rate { get; init; } = 0;

    /// <summary>
    /// Gets the pitch adjustment (-100 to +100, default 0).
    /// Negative values lower pitch, positive values raise it.
    /// </summary>
    public int Pitch { get; init; } = 0;

    /// <summary>
    /// Gets the optional preferred provider name.
    /// When set, orchestration will try this provider first before falling back to others.
    /// </summary>
    public string? PreferredProvider { get; init; }
}
