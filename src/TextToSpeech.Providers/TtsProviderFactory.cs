using Olbrasoft.TextToSpeech.Core.Interfaces;

namespace Olbrasoft.TextToSpeech.Providers;

/// <summary>
/// Default implementation of ITtsProviderFactory.
/// </summary>
public sealed class TtsProviderFactory : ITtsProviderFactory
{
    private readonly Dictionary<string, ITtsProvider> _providers;

    /// <summary>
    /// Initializes a new instance of TtsProviderFactory with the given providers.
    /// </summary>
    public TtsProviderFactory(IEnumerable<ITtsProvider> providers)
    {
        _providers = providers.ToDictionary(p => p.Name, StringComparer.OrdinalIgnoreCase);
    }

    /// <inheritdoc />
    public ITtsProvider? GetProvider(string name)
    {
        return _providers.GetValueOrDefault(name);
    }

    /// <inheritdoc />
    public IEnumerable<ITtsProvider> GetAllProviders()
    {
        return _providers.Values;
    }

    /// <inheritdoc />
    public IEnumerable<string> GetProviderNames()
    {
        return _providers.Keys;
    }
}
