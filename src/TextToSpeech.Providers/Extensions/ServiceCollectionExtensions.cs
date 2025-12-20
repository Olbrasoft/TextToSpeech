using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Olbrasoft.TextToSpeech.Core.Interfaces;
using Olbrasoft.TextToSpeech.Core.Services;
using Olbrasoft.TextToSpeech.Providers.Azure;
using Olbrasoft.TextToSpeech.Providers.Configuration;
using Olbrasoft.TextToSpeech.Providers.EdgeTTS;
using Olbrasoft.TextToSpeech.Providers.Google;
using Olbrasoft.TextToSpeech.Providers.VoiceRss;

namespace Olbrasoft.TextToSpeech.Providers.Extensions;

/// <summary>
/// Extension methods for registering TTS providers with dependency injection.
/// IMPORTANT: This library does NOT configure providers - it only registers them.
/// The hosting application is responsible for calling services.Configure&lt;T&gt;()
/// to populate configuration objects from appsettings.json, environment variables,
/// database, Key Vault, or any other source.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Adds TTS providers to the service collection.
    /// NOTE: Configuration objects must be populated by hosting app BEFORE calling this method.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configuration">The configuration (used for HttpClient setup only).</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddTtsProviders(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // Register IOutputConfiguration adapter (no Configure call - hosting app must do it)
        services.AddSingleton<IOutputConfiguration>(sp =>
            sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<OutputConfiguration>>().Value);

        // Register audio data factory
        services.AddSingleton<IAudioDataFactory, AudioDataFactory>();

        // Add EdgeTTS provider
        services.AddEdgeTtsProvider(configuration);

        // Add Azure TTS provider
        services.AddAzureTtsProvider(configuration);

        // Add VoiceRSS provider
        services.AddVoiceRssProvider(configuration);

        // Add Google TTS provider
        services.AddGoogleTtsProvider(configuration);

        // Register factory
        services.AddSingleton<ITtsProviderFactory, TtsProviderFactory>();

        return services;
    }

    /// <summary>
    /// Adds the EdgeTTS HTTP provider.
    /// NOTE: Hosting app must call services.Configure&lt;EdgeTtsConfiguration&gt;() first.
    /// </summary>
    public static IServiceCollection AddEdgeTtsProvider(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // Configure HttpClient using values from IOptions (populated by hosting app)
        services.AddHttpClient("EdgeTtsServer", (sp, client) =>
        {
            var config = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<EdgeTtsConfiguration>>().Value;
            client.BaseAddress = new Uri(config.BaseUrl);
            client.Timeout = config.Timeout;
        });

        services.AddSingleton<ITtsProvider, EdgeTtsProvider>();

        return services;
    }

    /// <summary>
    /// Adds the Azure Cognitive Services TTS provider.
    /// NOTE: Hosting app must call services.Configure&lt;AzureTtsConfiguration&gt;() first.
    /// </summary>
    public static IServiceCollection AddAzureTtsProvider(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // No Configure call - hosting app must populate AzureTtsConfiguration
        services.AddSingleton<ITtsProvider, AzureTtsProvider>();

        return services;
    }

    /// <summary>
    /// Adds the VoiceRSS TTS provider.
    /// NOTE: Hosting app must call services.Configure&lt;VoiceRssConfiguration&gt;() first.
    /// </summary>
    public static IServiceCollection AddVoiceRssProvider(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // Configure HttpClient using values from IOptions (populated by hosting app)
        services.AddHttpClient("VoiceRSS", (sp, client) =>
        {
            var config = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<VoiceRssConfiguration>>().Value;
            client.Timeout = config.Timeout;
        });

        services.AddSingleton<ITtsProvider, VoiceRssProvider>();

        return services;
    }

    /// <summary>
    /// Adds the Google TTS (gTTS) provider.
    /// NOTE: Hosting app must call services.Configure&lt;GoogleTtsConfiguration&gt;() first.
    /// </summary>
    public static IServiceCollection AddGoogleTtsProvider(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // No Configure call - hosting app must populate GoogleTtsConfiguration
        services.AddSingleton<ITtsProvider, GoogleTtsProvider>();

        return services;
    }
}
