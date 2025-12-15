using Olbrasoft.TextToSpeech.Core.Interfaces;

namespace Olbrasoft.TextToSpeech.Providers;

/// <summary>
/// Factory for resolving TTS providers by name.
/// </summary>
public interface ITtsProviderFactory
{
    /// <summary>
    /// Gets a provider by its name.
    /// </summary>
    /// <param name="name">Provider name (e.g., "EdgeTTS-HTTP", "Azure", "Piper").</param>
    /// <returns>The provider instance or null if not found.</returns>
    ITtsProvider? GetProvider(string name);

    /// <summary>
    /// Gets all registered providers.
    /// </summary>
    IEnumerable<ITtsProvider> GetAllProviders();

    /// <summary>
    /// Gets the names of all registered providers.
    /// </summary>
    IEnumerable<string> GetProviderNames();
}
