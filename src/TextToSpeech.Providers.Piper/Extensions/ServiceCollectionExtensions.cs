using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Olbrasoft.TextToSpeech.Core.Interfaces;
using Olbrasoft.TextToSpeech.Core.Services;

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

        // Register audio data factory if not already registered
        services.TryAddSingleton<IAudioDataFactory, AudioDataFactory>();

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

        // Register audio data factory if not already registered
        services.TryAddSingleton<IAudioDataFactory, AudioDataFactory>();

        services.AddSingleton<ITtsProvider, PiperTtsProvider>();

        return services;
    }
}
