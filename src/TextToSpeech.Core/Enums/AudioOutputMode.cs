namespace Olbrasoft.TextToSpeech.Core.Enums;

/// <summary>
/// Specifies how synthesized audio should be stored.
/// </summary>
public enum AudioOutputMode
{
    /// <summary>
    /// Audio is returned in memory as byte array. Fast, no disk I/O.
    /// </summary>
    Memory,

    /// <summary>
    /// Audio is saved to disk. Better for caching and debugging.
    /// </summary>
    File
}
