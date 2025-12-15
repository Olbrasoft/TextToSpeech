namespace Olbrasoft.TextToSpeech.Service.Models;

/// <summary>
/// Request model for TTS synthesis.
/// </summary>
public sealed record SpeakRequest
{
    /// <summary>
    /// Gets the text to synthesize.
    /// </summary>
    public required string Text { get; init; }

    /// <summary>
    /// Gets the voice identifier (e.g., "cs-CZ-AntoninNeural").
    /// </summary>
    public string? Voice { get; init; }

    /// <summary>
    /// Gets the speech rate adjustment (-100 to +100).
    /// </summary>
    public int Rate { get; init; } = 0;

    /// <summary>
    /// Gets the pitch adjustment (-100 to +100).
    /// </summary>
    public int Pitch { get; init; } = 0;

    /// <summary>
    /// Gets the optional preferred provider name.
    /// </summary>
    public string? PreferredProvider { get; init; }
}
