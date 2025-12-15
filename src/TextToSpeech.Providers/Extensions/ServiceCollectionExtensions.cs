using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Olbrasoft.TextToSpeech.Core.Interfaces;
using Olbrasoft.TextToSpeech.Providers.Configuration;
using Olbrasoft.TextToSpeech.Providers.EdgeTTS;

namespace Olbrasoft.TextToSpeech.Providers.Extensions;

/// <summary>
/// Extension methods for registering TTS providers with dependency injection.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Adds TTS providers to the service collection.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configuration">The configuration.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddTtsProviders(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // Register output configuration
        services.Configure<OutputConfiguration>(configuration.GetSection(OutputConfiguration.SectionName));
        services.AddSingleton<IOutputConfiguration>(sp =>
            sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<OutputConfiguration>>().Value);

        // Add EdgeTTS provider
        services.AddEdgeTtsProvider(configuration);

        // Register factory
        services.AddSingleton<ITtsProviderFactory, TtsProviderFactory>();

        return services;
    }

    /// <summary>
    /// Adds the EdgeTTS HTTP provider.
    /// </summary>
    public static IServiceCollection AddEdgeTtsProvider(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.Configure<EdgeTtsConfiguration>(configuration.GetSection(EdgeTtsConfiguration.SectionName));

        services.AddHttpClient("EdgeTtsServer", (sp, client) =>
        {
            var config = configuration.GetSection(EdgeTtsConfiguration.SectionName).Get<EdgeTtsConfiguration>()
                         ?? new EdgeTtsConfiguration();
            client.BaseAddress = new Uri(config.BaseUrl);
            client.Timeout = config.Timeout;
        });

        services.AddSingleton<ITtsProvider, EdgeTtsProvider>();

        return services;
    }
}
