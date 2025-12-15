using Olbrasoft.TextToSpeech.Core.Enums;

namespace Olbrasoft.TextToSpeech.Providers.Piper;

/// <summary>
/// Configuration for Piper TTS provider (offline neural TTS).
/// </summary>
public sealed class PiperConfiguration
{
    /// <summary>
    /// Configuration section name.
    /// </summary>
    public const string SectionName = "TTS:Piper";

    /// <summary>
    /// Path to the Piper voice model (.onnx file).
    /// </summary>
    public string ModelPath { get; set; } = string.Empty;

    /// <summary>
    /// Path to piper executable. If empty, assumes it's in PATH.
    /// </summary>
    public string PiperPath { get; set; } = "piper";

    /// <summary>
    /// Output directory for audio files.
    /// </summary>
    public string? OutputDirectory { get; set; }

    /// <summary>
    /// Output mode: Memory or File.
    /// </summary>
    public AudioOutputMode OutputMode { get; set; } = AudioOutputMode.Memory;

    /// <summary>
    /// Dictionary of voice profiles keyed by profile name (e.g., "default", "fast").
    /// </summary>
    public Dictionary<string, PiperVoiceProfile> Profiles { get; set; } = new(StringComparer.OrdinalIgnoreCase)
    {
        ["default"] = new()
    };

    /// <summary>
    /// Default profile to use if no specific profile is requested.
    /// </summary>
    public string DefaultProfile { get; set; } = "default";

    /// <summary>
    /// Command timeout in seconds.
    /// </summary>
    public int TimeoutSeconds { get; set; } = 60;
}

/// <summary>
/// Piper voice profile configuration.
/// </summary>
public sealed class PiperVoiceProfile
{
    /// <summary>
    /// Phoneme length scale. Lower = faster speech.
    /// Default: 1.0, Recommended for faster: 0.5
    /// </summary>
    public double LengthScale { get; set; } = 1.0;

    /// <summary>
    /// Generator noise scale. Controls voice variation.
    /// Default: 0.667
    /// </summary>
    public double NoiseScale { get; set; } = 0.667;

    /// <summary>
    /// Phoneme width noise scale. Controls pronunciation variation.
    /// Default: 0.8
    /// </summary>
    public double NoiseWScale { get; set; } = 0.8;

    /// <summary>
    /// Seconds of silence after each sentence.
    /// Default: 0.2
    /// </summary>
    public double SentenceSilence { get; set; } = 0.2;

    /// <summary>
    /// Volume multiplier.
    /// Default: 1.0
    /// </summary>
    public double Volume { get; set; } = 1.0;

    /// <summary>
    /// Speaker ID (for multi-speaker models).
    /// Default: 0
    /// </summary>
    public int Speaker { get; set; } = 0;
}

