using System.ComponentModel.DataAnnotations;

namespace Olbrasoft.TextToSpeech.Core.Models;

/// <summary>
/// Represents a text-to-speech synthesis request.
/// </summary>
public sealed class TtsRequest : IValidatableObject
{
    /// <summary>
    /// Maximum allowed text length (characters).
    /// </summary>
    public const int MaxTextLength = 10000;

    /// <summary>
    /// Minimum allowed rate value.
    /// </summary>
    public const int MinRate = -100;

    /// <summary>
    /// Maximum allowed rate value.
    /// </summary>
    public const int MaxRate = 100;

    /// <summary>
    /// Minimum allowed pitch value.
    /// </summary>
    public const int MinPitch = -100;

    /// <summary>
    /// Maximum allowed pitch value.
    /// </summary>
    public const int MaxPitch = 100;

    /// <summary>
    /// Gets the text to synthesize.
    /// </summary>
    [Required(ErrorMessage = "Text is required")]
    [StringLength(MaxTextLength, ErrorMessage = "Text cannot exceed {1} characters")]
    public required string Text { get; init; }

    /// <summary>
    /// Gets the voice identifier (e.g., "cs-CZ-AntoninNeural").
    /// If not specified, the provider's default voice is used.
    /// </summary>
    public string? Voice { get; init; }

    /// <summary>
    /// Gets the speech rate adjustment (-100 to +100, default 0).
    /// Negative values slow down speech, positive values speed it up.
    /// </summary>
    [Range(MinRate, MaxRate, ErrorMessage = "Rate must be between {1} and {2}")]
    public int Rate { get; init; } = 0;

    /// <summary>
    /// Gets the pitch adjustment (-100 to +100, default 0).
    /// Negative values lower pitch, positive values raise it.
    /// </summary>
    [Range(MinPitch, MaxPitch, ErrorMessage = "Pitch must be between {1} and {2}")]
    public int Pitch { get; init; } = 0;

    /// <summary>
    /// Gets the optional preferred provider name.
    /// When set, orchestration will try this provider first before falling back to others.
    /// </summary>
    public string? PreferredProvider { get; init; }

    /// <summary>
    /// Validates the request and returns validation errors.
    /// </summary>
    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (string.IsNullOrWhiteSpace(Text))
        {
            yield return new ValidationResult(
                "Text cannot be empty or whitespace",
                [nameof(Text)]);
        }
    }

    /// <summary>
    /// Validates the request and throws if invalid.
    /// </summary>
    /// <exception cref="ValidationException">Thrown when the request is invalid.</exception>
    public void EnsureValid()
    {
        var context = new ValidationContext(this);
        var results = new List<ValidationResult>();

        if (!Validator.TryValidateObject(this, context, results, validateAllProperties: true))
        {
            var errors = string.Join("; ", results.Select(r => r.ErrorMessage));
            throw new ValidationException($"Invalid TTS request: {errors}");
        }
    }

    /// <summary>
    /// Checks if the request is valid.
    /// </summary>
    /// <param name="errors">When invalid, contains the validation errors.</param>
    /// <returns>True if valid, false otherwise.</returns>
    public bool IsValid(out IReadOnlyList<string> errors)
    {
        var context = new ValidationContext(this);
        var results = new List<ValidationResult>();

        var isValid = Validator.TryValidateObject(this, context, results, validateAllProperties: true);
        errors = results.Select(r => r.ErrorMessage ?? "Unknown error").ToList();
        return isValid;
    }
}
