namespace Olbrasoft.TextToSpeech.Core.Models;

/// <summary>
/// Represents an error that occurred during a provider synthesis attempt.
/// Used by orchestration layer to track failed attempts for debugging.
/// </summary>
/// <param name="ProviderName">The name of the provider that failed.</param>
/// <param name="ErrorMessage">The error message.</param>
/// <param name="AttemptDuration">How long the attempt took before failing.</param>
public sealed record ProviderError(
    string ProviderName,
    string ErrorMessage,
    TimeSpan AttemptDuration);
