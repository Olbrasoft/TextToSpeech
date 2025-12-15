using Olbrasoft.TextToSpeech.Core.Models;

namespace Olbrasoft.TextToSpeech.Core.Interfaces;

/// <summary>
/// Defines a TTS provider that can synthesize speech from text.
/// </summary>
public interface ITtsProvider
{
    /// <summary>
    /// Gets the unique name identifying this provider (e.g., "EdgeTTS", "Azure", "Piper").
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Synthesizes speech from the given text request.
    /// </summary>
    /// <param name="request">The TTS request containing text and voice settings.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The synthesis result containing audio data or error information.</returns>
    Task<TtsResult> SynthesizeAsync(TtsRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets information about this provider including status and supported voices.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Provider information.</returns>
    Task<TtsProviderInfo> GetInfoAsync(CancellationToken cancellationToken = default);
}
