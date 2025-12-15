namespace Olbrasoft.TextToSpeech.Core;

/// <summary>
/// Provides text hashing utilities for file naming.
/// </summary>
public static class TextHasher
{
    /// <summary>
    /// Computes a short hash string from text for use in file names.
    /// </summary>
    /// <param name="text">The text to hash.</param>
    /// <returns>An 8-character hex string.</returns>
    public static string ComputeHash(string text)
    {
        var hashBytes = System.Security.Cryptography.SHA256.HashData(
            System.Text.Encoding.UTF8.GetBytes(text ?? string.Empty));
        return Convert.ToHexString(hashBytes)[..8];
    }
}
