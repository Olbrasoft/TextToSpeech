using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Olbrasoft.TextToSpeech.Orchestration.Configuration;

namespace Olbrasoft.TextToSpeech.Orchestration.Extensions;

/// <summary>
/// Extension methods for registering TTS orchestration with dependency injection.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Adds TTS orchestration services to the service collection.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configuration">The configuration.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddTtsOrchestration(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.Configure<OrchestrationConfig>(configuration.GetSection(OrchestrationConfig.SectionName));
        services.AddSingleton<ITtsProviderChain, TtsProviderChain>();

        return services;
    }

    /// <summary>
    /// Adds TTS orchestration services with custom configuration.
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
