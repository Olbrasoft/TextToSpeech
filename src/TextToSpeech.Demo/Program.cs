using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Olbrasoft.TextToSpeech.Core.Interfaces;
using Olbrasoft.TextToSpeech.Core.Models;
using Olbrasoft.TextToSpeech.Orchestration;
using Olbrasoft.TextToSpeech.Orchestration.Configuration;
using Olbrasoft.TextToSpeech.Orchestration.Extensions;
using Olbrasoft.TextToSpeech.Providers.Azure;
using Olbrasoft.TextToSpeech.Providers.Configuration;
using Olbrasoft.TextToSpeech.Providers.EdgeTTS;
using Olbrasoft.TextToSpeech.Providers.Extensions;
using Olbrasoft.TextToSpeech.Providers.Google;
using Olbrasoft.TextToSpeech.Providers.Piper;
using Olbrasoft.TextToSpeech.Providers.Piper.Extensions;
using Olbrasoft.TextToSpeech.Providers.VoiceRss;

namespace Olbrasoft.TextToSpeech.Demo;

class Program
{
    static async Task Main(string[] args)
    {
        Console.WriteLine("==============================================");
        Console.WriteLine("   TextToSpeech Library - Demo Application");
        Console.WriteLine("==============================================");
        Console.WriteLine();

        // Build configuration
        var configuration = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
            .AddEnvironmentVariables()
            .Build();

        // Build DI container
        var services = new ServiceCollection();
        ConfigureServices(services, configuration);
        var serviceProvider = services.BuildServiceProvider();

        // Show menu and run demo
        while (true)
        {
            Console.WriteLine();
            Console.WriteLine("Select TTS Provider:");
            Console.WriteLine("1. Azure Cognitive Services TTS (requires API key)");
            Console.WriteLine("2. EdgeTTS (HTTP) (requires EdgeTTS server running)");
            Console.WriteLine("3. VoiceRSS (requires API key)");
            Console.WriteLine("4. Google TTS (gTTS) (free, no key required)");
            Console.WriteLine("5. Piper (local ONNX) (requires model file)");
            Console.WriteLine("6. Orchestration (with fallback) - DEMO FALLBACK");
            Console.WriteLine("0. Exit");
            Console.WriteLine();
            Console.WriteLine("Note: Some providers require API keys or external services.");
            Console.WriteLine("      Option 4 (Google TTS) should work without configuration.");
            Console.WriteLine("      Option 6 demonstrates automatic fallback mechanism.");
            Console.WriteLine();
            Console.Write("Your choice: ");

            var choice = Console.ReadLine();

            if (choice == "0")
            {
                Console.WriteLine("Goodbye!");
                break;
            }

            if (choice == "6")
            {
                // Orchestration uses ITtsProviderChain, not ITtsProvider
                var chain = serviceProvider.GetRequiredService<ITtsProviderChain>();
                await RunOrchestrationDemo(chain);
            }
            else
            {
                ITtsProvider? provider = choice switch
                {
                    "1" => serviceProvider.GetServices<ITtsProvider>().FirstOrDefault(p => p.Name == "AzureTTS"),
                    "2" => serviceProvider.GetServices<ITtsProvider>().FirstOrDefault(p => p.Name == "EdgeTTS-HTTP"),
                    "3" => serviceProvider.GetServices<ITtsProvider>().FirstOrDefault(p => p.Name == "VoiceRSS"),
                    "4" => serviceProvider.GetServices<ITtsProvider>().FirstOrDefault(p => p.Name == "GoogleTTS"),
                    "5" => serviceProvider.GetServices<ITtsProvider>().FirstOrDefault(p => p.Name == "Piper"),
                    _ => null
                };

                if (provider == null)
                {
                    Console.WriteLine("Invalid choice!");
                    continue;
                }

                await RunDemo(provider);
            }
        }
    }

    private static void ConfigureServices(IServiceCollection services, IConfiguration configuration)
    {
        // IMPORTANT: This is the hosting application pattern!
        // Step 1: Configure all providers (load values from appsettings.json, env vars, etc.)
        ConfigureTtsProviders(services, configuration);

        // Step 2: Register TTS provider services (library only registers, doesn't configure)
        services.AddTtsProviders(configuration);
        services.AddPiperTts(configuration);
        services.AddTtsOrchestration(configuration);
    }

    private static void ConfigureTtsProviders(IServiceCollection services, IConfiguration configuration)
    {
        // Output configuration
        services.Configure<OutputConfiguration>(
            configuration.GetSection(OutputConfiguration.SectionName));

        // Azure TTS configuration (NO KEY - for fallback demo)
        services.Configure<AzureTtsConfiguration>(options =>
        {
            var section = configuration.GetSection(AzureTtsConfiguration.SectionName);
            section.Bind(options);

            // Hosting app could load from environment variables here
            var envKey = Environment.GetEnvironmentVariable("AZURE_SPEECH_KEY");
            if (!string.IsNullOrEmpty(envKey))
            {
                options.SubscriptionKey = envKey;
            }
            // NOTE: Key is intentionally empty in appsettings.json to demo fallback!
        });

        // EdgeTTS configuration
        services.Configure<EdgeTtsConfiguration>(
            configuration.GetSection(EdgeTtsConfiguration.SectionName));

        // VoiceRSS configuration
        services.Configure<VoiceRssConfiguration>(options =>
        {
            var section = configuration.GetSection(VoiceRssConfiguration.SectionName);
            section.Bind(options);

            var envKey = Environment.GetEnvironmentVariable("VOICERSS_API_KEY");
            if (!string.IsNullOrEmpty(envKey))
            {
                options.ApiKey = envKey;
            }
        });

        // Google TTS configuration
        services.Configure<GoogleTtsConfiguration>(
            configuration.GetSection(GoogleTtsConfiguration.SectionName));

        // Piper configuration
        services.Configure<PiperConfiguration>(
            configuration.GetSection(PiperConfiguration.SectionName));

        // Orchestration configuration (fallback chain)
        services.Configure<OrchestrationConfig>(
            configuration.GetSection(OrchestrationConfig.SectionName));
    }

    private static async Task RunDemo(ITtsProvider provider)
    {
        Console.WriteLine();
        Console.WriteLine($"━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
        Console.WriteLine($"Provider: {provider.Name}");
        Console.WriteLine($"━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");

        // Generate test text with current date/time (proves it's not a recording)
        var now = DateTime.Now;
        var text = $"Toto je kontrolní text pro testování TTS knihovny. " +
                   $"Dnes je {now:dddd}, {now:d} a čas je {now:HH:mm:ss}. " +
                   $"Tato nahrávka je vygenerována v reálném čase, není to přednahraný audio soubor.";

        Console.WriteLine($"Text: {text}");
        Console.WriteLine();
        Console.WriteLine("Generating audio...");

        var request = new TtsRequest
        {
            Text = text,
            Rate = 0,
            Pitch = 0
        };

        var result = await provider.SynthesizeAsync(request);

        Console.WriteLine();
        Console.WriteLine($"━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
        Console.WriteLine($"Result:");
        Console.WriteLine($"━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
        Console.WriteLine($"Success: {result.Success}");
        Console.WriteLine($"Provider: {result.ProviderUsed}");
        Console.WriteLine($"Generation Time: {result.GenerationTime.TotalMilliseconds:F0} ms");

        if (result.Success && result.Audio != null)
        {
            string? audioFilePath = null;

            if (result.Audio is FileAudioData fileData)
            {
                Console.WriteLine($"File Path: {fileData.FilePath}");
                Console.WriteLine($"✓ Audio saved successfully!");
                audioFilePath = fileData.FilePath;
            }
            else if (result.Audio is MemoryAudioData memoryData)
            {
                Console.WriteLine($"Audio Size: {memoryData.Data.Length:N0} bytes");
                Console.WriteLine($"Content Type: {memoryData.ContentType}");
                Console.WriteLine($"✓ Audio generated in memory!");
            }

            if (result.AudioDuration.HasValue)
            {
                Console.WriteLine($"Audio Length: {result.AudioDuration.Value.TotalSeconds:F1} seconds");
            }

            // Auto-play audio file on Linux
            if (!string.IsNullOrEmpty(audioFilePath) && File.Exists(audioFilePath))
            {
                Console.WriteLine();
                Console.WriteLine("▶️  Playing audio...");
                PlayAudioFile(audioFilePath);
            }
        }
        else
        {
            Console.WriteLine($"✗ Error: {result.ErrorMessage}");
        }

        Console.WriteLine($"━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
    }

    private static async Task RunOrchestrationDemo(ITtsProviderChain chain)
    {
        Console.WriteLine();
        Console.WriteLine($"━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
        Console.WriteLine($"Provider: Orchestration (with fallback)");
        Console.WriteLine($"━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
        Console.WriteLine();
        Console.WriteLine("⚠️  FALLBACK DEMO:");
        Console.WriteLine("   Azure TTS has NO API KEY in configuration.");
        Console.WriteLine("   Orchestration will automatically fallback to EdgeTTS!");
        Console.WriteLine();

        // Generate test text with current date/time (proves it's not a recording)
        var now = DateTime.Now;
        var text = $"Toto je kontrolní text pro testování TTS knihovny. " +
                   $"Dnes je {now:dddd}, {now:d} a čas je {now:HH:mm:ss}. " +
                   $"Tato nahrávka je vygenerována v reálném čase, není to přednahraný audio soubor.";

        Console.WriteLine($"Text: {text}");
        Console.WriteLine();
        Console.WriteLine("Generating audio...");

        var request = new TtsRequest
        {
            Text = text,
            Rate = 0,
            Pitch = 0
        };

        var result = await chain.SynthesizeAsync(request);

        Console.WriteLine();
        Console.WriteLine($"━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
        Console.WriteLine($"Result:");
        Console.WriteLine($"━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
        Console.WriteLine($"Success: {result.Success}");
        Console.WriteLine($"Provider Used: {result.ProviderUsed}");
        Console.WriteLine($"Generation Time: {result.GenerationTime.TotalMilliseconds:F0} ms");

        if (result.Success && result.Audio != null)
        {
            string? audioFilePath = null;

            if (result.Audio is FileAudioData fileData)
            {
                Console.WriteLine($"File Path: {fileData.FilePath}");
                Console.WriteLine($"✓ Audio saved successfully!");
                audioFilePath = fileData.FilePath;
            }
            else if (result.Audio is MemoryAudioData memoryData)
            {
                Console.WriteLine($"Audio Size: {memoryData.Data.Length:N0} bytes");
                Console.WriteLine($"Content Type: {memoryData.ContentType}");
                Console.WriteLine($"✓ Audio generated in memory!");
            }

            if (result.AudioDuration.HasValue)
            {
                Console.WriteLine($"Audio Length: {result.AudioDuration.Value.TotalSeconds:F1} seconds");
            }

            // Show attempted providers (useful for debugging fallback)
            if (result.AttemptedProviders != null && result.AttemptedProviders.Count > 0)
            {
                Console.WriteLine();
                Console.WriteLine("Fallback chain:");
                foreach (var attempt in result.AttemptedProviders)
                {
                    Console.WriteLine($"  - {attempt.ProviderName}: {attempt.ErrorMessage}");
                }
            }

            // Auto-play audio file on Linux
            if (!string.IsNullOrEmpty(audioFilePath) && File.Exists(audioFilePath))
            {
                Console.WriteLine();
                Console.WriteLine("▶️  Playing audio...");
                PlayAudioFile(audioFilePath);
            }
        }
        else
        {
            Console.WriteLine($"✗ Error: {result.ErrorMessage}");
        }

        Console.WriteLine($"━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
    }

    private static void PlayAudioFile(string filePath)
    {
        try
        {
            // Find available audio player (same approach as PushToTalk project)
            var player = GetAvailableAudioPlayer();

            if (string.IsNullOrEmpty(player))
            {
                Console.WriteLine("⚠️  No audio player found (tried pw-cat, paplay, aplay)");
                Console.WriteLine($"   Install PipeWire or PulseAudio to enable playback");
                return;
            }

            // pw-cat needs -p flag for playback mode, others don't
            var arguments = player == "pw-cat"
                ? $"-p \"{filePath}\""
                : $"\"{filePath}\"";

            var processInfo = new System.Diagnostics.ProcessStartInfo
            {
                FileName = player,
                Arguments = arguments,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = System.Diagnostics.Process.Start(processInfo);
            if (process != null)
            {
                process.WaitForExit();

                if (process.ExitCode != 0)
                {
                    var error = process.StandardError.ReadToEnd();
                    Console.WriteLine($"⚠️  Playback failed: {error}");
                    Console.WriteLine($"   You can manually play: {player} {arguments}");
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"⚠️  Could not play audio: {ex.Message}");
        }
    }

    private static string? GetAvailableAudioPlayer()
    {
        // Try pw-cat (PipeWire) first - modern Linux audio system
        if (IsCommandAvailable("pw-cat"))
            return "pw-cat";

        // Fallback to paplay (PulseAudio) - older but widely supported
        if (IsCommandAvailable("paplay"))
            return "paplay";

        // Last resort: aplay (ALSA) - basic but universal
        if (IsCommandAvailable("aplay"))
            return "aplay";

        return null;
    }

    private static bool IsCommandAvailable(string command)
    {
        try
        {
            var startInfo = new System.Diagnostics.ProcessStartInfo
            {
                FileName = "which",
                Arguments = command,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                CreateNoWindow = true
            };

            using var process = System.Diagnostics.Process.Start(startInfo);
            if (process != null)
            {
                process.WaitForExit();
                return process.ExitCode == 0;
            }
        }
        catch
        {
            // Ignore
        }

        return false;
    }
}
