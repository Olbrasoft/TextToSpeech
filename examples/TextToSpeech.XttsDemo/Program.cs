using System.Diagnostics;

namespace Olbrasoft.TextToSpeech.XttsDemo;

/// <summary>
/// Demo application for XTTS (Extended Text-to-Speech) using finetuned model.
/// Generates speech from text using a Python XTTS wrapper and plays it via pw-cat.
/// </summary>
class Program
{
    // Configuration for Jan Hyhlík voice (trained model)
    private const string BaseModelPath = "/home/jirka/projekty/xtts-test/output_hyhlik_cpu/run/training/XTTS_v2.0_original_model_files";
    private const string FinetunedCheckpoint = "/home/jirka/projekty/xtts-test/output_hyhlik_cpu_continue/run/training/GPT_XTTS_FT-December-24-2025_10+28PM-eff0407/best_model_9685.pth";
    private const string ReferenceAudio = "/home/jirka/projekty/xtts-test/hyhlik_dataset/wavs/vocals_sample_001_5.7s.wav";
    private const string Language = "cs";

    static async Task<int> Main(string[] args)
    {
        Console.WriteLine("╔═══════════════════════════════════════════════════════════╗");
        Console.WriteLine("║   XTTS Demo - Finetuned Text-to-Speech (Jan Hyhlík)      ║");
        Console.WriteLine("╚═══════════════════════════════════════════════════════════╝");
        Console.WriteLine();

        // Get text input
        string text;
        if (args.Length > 0)
        {
            text = string.Join(" ", args);
        }
        else
        {
            Console.Write("Enter text to synthesize (Czech): ");
            text = Console.ReadLine() ?? string.Empty;
        }

        if (string.IsNullOrWhiteSpace(text))
        {
            Console.WriteLine("❌ No text provided.");
            return 1;
        }

        Console.WriteLine();
        Console.WriteLine($"📝 Text: {text}");
        Console.WriteLine();

        try
        {
            // Generate audio
            var audioFile = await GenerateSpeechAsync(text);

            // Play audio
            await PlayAudioAsync(audioFile);

            // Cleanup
            if (File.Exists(audioFile))
            {
                File.Delete(audioFile);
                Console.WriteLine("🧹 Cleaned up temporary audio file");
            }

            Console.WriteLine();
            Console.WriteLine("✅ Done!");
            return 0;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Error: {ex.Message}");
            return 1;
        }
    }

    /// <summary>
    /// Generate speech from text using Python XTTS wrapper.
    /// </summary>
    static async Task<string> GenerateSpeechAsync(string text)
    {
        Console.WriteLine("🎙️  Generating speech...");

        // Find Python script path (relative to this executable)
        var exeDir = AppContext.BaseDirectory;
        var scriptPath = Path.Combine(exeDir, "python", "xtts_generate.py");

        if (!File.Exists(scriptPath))
        {
            // Try from project directory during development
            scriptPath = Path.Combine(Directory.GetCurrentDirectory(), "python", "xtts_generate.py");
        }

        if (!File.Exists(scriptPath))
        {
            throw new FileNotFoundException($"XTTS generator script not found: {scriptPath}");
        }

        // Create temporary output file
        var outputFile = Path.Combine(Path.GetTempPath(), $"xtts_output_{Guid.NewGuid()}.wav");

        // Build Python command
        var pythonArgs = BuildPythonArguments(scriptPath, text, outputFile);

        Console.WriteLine($"   Script: {scriptPath}");
        Console.WriteLine($"   Output: {outputFile}");
        Console.WriteLine();

        // Execute Python script using virtual environment
        var pythonExecutable = GetPythonExecutable();

        var psi = new ProcessStartInfo
        {
            FileName = pythonExecutable,
            Arguments = pythonArgs,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };

        using var process = new Process { StartInfo = psi };

        var outputTask = new TaskCompletionSource<string>();
        var errorOutput = new List<string>();

        process.OutputDataReceived += (sender, e) =>
        {
            if (!string.IsNullOrEmpty(e.Data))
            {
                Console.WriteLine($"   [Python] {e.Data}");
            }
        };

        process.ErrorDataReceived += (sender, e) =>
        {
            if (!string.IsNullOrEmpty(e.Data))
            {
                errorOutput.Add(e.Data);
                Console.WriteLine($"   [Python] {e.Data}");
            }
        };

        process.Start();
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        await process.WaitForExitAsync();

        if (process.ExitCode != 0)
        {
            var error = string.Join(Environment.NewLine, errorOutput);
            throw new Exception($"Python script failed with exit code {process.ExitCode}:{Environment.NewLine}{error}");
        }

        if (!File.Exists(outputFile))
        {
            throw new FileNotFoundException($"Audio file was not created: {outputFile}");
        }

        Console.WriteLine("✅ Speech generated successfully");
        Console.WriteLine();

        return outputFile;
    }

    /// <summary>
    /// Build command-line arguments for Python XTTS generator.
    /// </summary>
    static string BuildPythonArguments(string scriptPath, string text, string outputFile)
    {
        var args = new List<string>
        {
            $"\"{scriptPath}\"",
            $"--base-model \"{BaseModelPath}\"",
            $"--finetuned \"{FinetunedCheckpoint}\"",
            $"--reference-audio \"{ReferenceAudio}\"",
            $"--text \"{EscapeArgument(text)}\"",
            $"--output \"{outputFile}\"",
            $"--language {Language}",
            "--temperature 0.75",
            "--repetition-penalty 3.0",
            "--top-k 50",
            "--top-p 0.85",
            "--device cpu"
        };

        return string.Join(" ", args);
    }

    /// <summary>
    /// Escape text for command-line argument.
    /// </summary>
    static string EscapeArgument(string text)
    {
        return text.Replace("\"", "\\\"");
    }

    /// <summary>
    /// Play audio file using pw-cat (PipeWire).
    /// </summary>
    static async Task PlayAudioAsync(string audioFile)
    {
        Console.WriteLine("🔊 Playing audio...");

        if (!IsCommandAvailable("pw-cat"))
        {
            Console.WriteLine("⚠️  pw-cat not found. Skipping playback.");
            Console.WriteLine($"   Audio file saved at: {audioFile}");
            return;
        }

        var psi = new ProcessStartInfo
        {
            FileName = "pw-cat",
            Arguments = $"-p \"{audioFile}\"",
            UseShellExecute = false,
            CreateNoWindow = true,
        };

        using var process = Process.Start(psi);
        if (process != null)
        {
            await process.WaitForExitAsync();
            Console.WriteLine("✅ Playback finished");
        }
    }

    /// <summary>
    /// Get Python executable path (prefer virtual environment).
    /// </summary>
    static string GetPythonExecutable()
    {
        // Try virtual environment first (from xtts-test project)
        var venvPython = "/home/jirka/projekty/xtts-test/xtts-env/bin/python3";
        if (File.Exists(venvPython))
        {
            return venvPython;
        }

        // Fallback to system python3
        return "python3";
    }

    /// <summary>
    /// Check if a command is available in PATH.
    /// </summary>
    static bool IsCommandAvailable(string command)
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = "which",
                Arguments = command,
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            };

            using var process = Process.Start(psi);
            if (process != null)
            {
                process.WaitForExit();
                return process.ExitCode == 0;
            }
        }
        catch
        {
            // Ignore errors
        }

        return false;
    }
}
