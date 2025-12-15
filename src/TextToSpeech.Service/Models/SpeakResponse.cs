namespace Olbrasoft.TextToSpeech.Service.Models;

/// <summary>
/// Response model for TTS synthesis.
/// </summary>
public sealed record SpeakResponse
{
    /// <summary>
    /// Gets whether synthesis was successful.
    /// </summary>
    public bool Success { get; init; }

    /// <summary>
    /// Gets the provider that produced the audio.
    /// </summary>
    public string? ProviderUsed { get; init; }

    /// <summary>
    /// Gets the audio duration (playback length).
    /// </summary>
    public TimeSpan? AudioDuration { get; init; }

    /// <summary>
    /// Gets the generation time (latency).
    /// </summary>
    public TimeSpan GenerationTime { get; init; }

    /// <summary>
    /// Gets the error message if synthesis failed.
    /// </summary>
    public string? ErrorMessage { get; init; }

    /// <summary>
    /// Gets the file path if using file output mode.
    /// </summary>
    public string? FilePath { get; init; }
}
