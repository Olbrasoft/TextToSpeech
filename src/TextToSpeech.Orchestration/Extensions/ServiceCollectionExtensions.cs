using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Olbrasoft.TextToSpeech.Orchestration.Configuration;

namespace Olbrasoft.TextToSpeech.Orchestration.Extensions;

/// <summary>
/// Extension methods for registering TTS orchestration with dependency injection.
/// IMPORTANT: This library does NOT configure orchestration - it only registers services.
/// The hosting application is responsible for calling services.Configure&lt;OrchestrationConfig&gt;()
/// to populate configuration from appsettings.json or other source.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Adds TTS orchestration services to the service collection.
    /// NOTE: Hosting app must call services.Configure&lt;OrchestrationConfig&gt;() first.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configuration">The configuration (not used, kept for compatibility).</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddTtsOrchestration(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // No Configure call - hosting app must populate OrchestrationConfig
        services.AddSingleton<ITtsProviderChain, TtsProviderChain>();

        return services;
    }

    /// <summary>
    /// Adds TTS orchestration services with inline configuration (fluent API).
    /// This overload is kept for convenience - it's a valid pattern for hosting apps.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configure">Configuration action.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddTtsOrchestration(
        this IServiceCollection services,
        Action<OrchestrationConfig> configure)
    {
        services.Configure(configure);
        services.AddSingleton<ITtsProviderChain, TtsProviderChain>();

        return services;
    }
}
