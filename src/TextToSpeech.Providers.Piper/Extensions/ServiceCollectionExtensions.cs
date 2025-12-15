using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Olbrasoft.TextToSpeech.Core.Interfaces;

namespace Olbrasoft.TextToSpeech.Providers.Piper.Extensions;

/// <summary>
/// Extension methods for registering Piper TTS provider with dependency injection.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Adds the Piper TTS provider to the service collection.
    /// This is a separate package due to heavy ONNX runtime dependency.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configuration">The configuration.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddPiperTts(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.Configure<PiperConfiguration>(configuration.GetSection(PiperConfiguration.SectionName));
        services.AddSingleton<ITtsProvider, PiperTtsProvider>();

        return services;
    }

    /// <summary>
    /// Adds the Piper TTS provider with custom configuration.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configure">Configuration action.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddPiperTts(
        this IServiceCollection services,
        Action<PiperConfiguration> configure)
    {
        services.Configure(configure);
        services.AddSingleton<ITtsProvider, PiperTtsProvider>();

        return services;
    }
}
