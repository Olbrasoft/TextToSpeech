using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Olbrasoft.TextToSpeech.Core.Interfaces;
using Olbrasoft.TextToSpeech.Core.Services;

namespace Olbrasoft.TextToSpeech.Providers.Piper.Extensions;

/// <summary>
/// Extension methods for registering Piper TTS provider with dependency injection.
/// IMPORTANT: This library does NOT configure the provider - it only registers services.
/// The hosting application is responsible for calling services.Configure&lt;PiperConfiguration&gt;()
/// to populate configuration from appsettings.json or other source.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Adds the Piper TTS provider to the service collection.
    /// This is a separate package due to heavy ONNX runtime dependency.
    /// NOTE: Hosting app must call services.Configure&lt;PiperConfiguration&gt;() first.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configuration">The configuration (not used, kept for compatibility).</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddPiperTts(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // No Configure call - hosting app must populate PiperConfiguration

        // Register audio data factory if not already registered
        services.TryAddSingleton<IAudioDataFactory, AudioDataFactory>();

        services.AddSingleton<ITtsProvider, PiperTtsProvider>();

        return services;
    }

    /// <summary>
    /// Adds the Piper TTS provider with inline configuration (fluent API).
    /// This overload is kept for convenience - it's a valid pattern for hosting apps.
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
