namespace Olbrasoft.TextToSpeech.Core.Models;

/// <summary>
/// Represents the result of a TTS synthesis operation.
/// </summary>
public sealed class TtsResult
{
    /// <summary>
    /// Gets whether the synthesis was successful.
    /// </summary>
    public bool Success { get; init; }

    /// <summary>
    /// Gets the error message if synthesis failed.
    /// </summary>
    public string? ErrorMessage { get; init; }

    /// <summary>
    /// Gets the synthesized audio data (null if synthesis failed).
    /// </summary>
    public AudioData? Audio { get; init; }

    /// <summary>
    /// Gets the name of the provider that produced this result.
    /// </summary>
    public string? ProviderUsed { get; init; }

    /// <summary>
    /// Gets the duration of the audio content (playback length).
    /// </summary>
    public TimeSpan? AudioDuration { get; init; }

    /// <summary>
    /// Gets the time taken to generate the audio (latency).
    /// </summary>
    public TimeSpan GenerationTime { get; init; }

    /// <summary>
    /// Gets the errors from all attempted providers (populated by orchestration layer).
    /// Useful for debugging when multiple providers were tried.
    /// </summary>
    public IReadOnlyList<ProviderError>? AttemptedProviders { get; init; }

    /// <summary>
    /// Creates a successful result.
    /// </summary>
    public static TtsResult Ok(AudioData audio, string providerUsed, TimeSpan generationTime, TimeSpan? audioDuration = null)
        => new()
        {
            Success = true,
            Audio = audio,
            ProviderUsed = providerUsed,
            GenerationTime = generationTime,
            AudioDuration = audioDuration
        };

    /// <summary>
    /// Creates a failed result.
    /// </summary>
    public static TtsResult Fail(string errorMessage, string? providerUsed = null, TimeSpan generationTime = default)
        => new()
        {
            Success = false,
            ErrorMessage = errorMessage,
            ProviderUsed = providerUsed,
            GenerationTime = generationTime
        };
}
