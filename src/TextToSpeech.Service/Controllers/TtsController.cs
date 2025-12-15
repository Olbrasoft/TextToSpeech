using Microsoft.AspNetCore.Mvc;
using Olbrasoft.TextToSpeech.Core.Models;
using Olbrasoft.TextToSpeech.Orchestration;
using Olbrasoft.TextToSpeech.Providers;
using Olbrasoft.TextToSpeech.Service.Models;

namespace Olbrasoft.TextToSpeech.Service.Controllers;

/// <summary>
/// TTS API controller.
/// </summary>
[ApiController]
[Route("api/tts")]
public sealed class TtsController : ControllerBase
{
    private readonly ILogger<TtsController> _logger;
    private readonly ITtsProviderChain _providerChain;
    private readonly ITtsProviderFactory _providerFactory;

    /// <summary>
    /// Initializes a new instance of TtsController.
    /// </summary>
    public TtsController(
        ILogger<TtsController> logger,
        ITtsProviderChain providerChain,
        ITtsProviderFactory providerFactory)
    {
        _logger = logger;
        _providerChain = providerChain;
        _providerFactory = providerFactory;
    }

    /// <summary>
    /// Synthesizes speech and returns audio bytes.
    /// </summary>
    [HttpPost("synthesize")]
    [ProducesResponseType(typeof(FileContentResult), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(SpeakResponse), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> Synthesize([FromBody] SpeakRequest request, CancellationToken cancellationToken)
    {
        _logger.LogInformation("TTS Synthesize: {Text}", request.Text.Length > 50 ? request.Text[..50] + "..." : request.Text);

        var ttsRequest = new TtsRequest
        {
            Text = request.Text,
            Voice = request.Voice,
            Rate = request.Rate,
            Pitch = request.Pitch,
            PreferredProvider = request.PreferredProvider
        };

        var result = await _providerChain.SynthesizeAsync(ttsRequest, cancellationToken);

        if (!result.Success || result.Audio == null)
        {
            return StatusCode(500, new SpeakResponse
            {
                Success = false,
                ErrorMessage = result.ErrorMessage,
                ProviderUsed = result.ProviderUsed,
                GenerationTime = result.GenerationTime
            });
        }

        return result.Audio switch
        {
            MemoryAudioData memory => File(memory.Data, memory.ContentType),
            FileAudioData file => Ok(new SpeakResponse
            {
                Success = true,
                ProviderUsed = result.ProviderUsed,
                GenerationTime = result.GenerationTime,
                AudioDuration = result.AudioDuration,
                FilePath = file.FilePath
            }),
            _ => StatusCode(500, new SpeakResponse { Success = false, ErrorMessage = "Unknown audio type" })
        };
    }

    /// <summary>
    /// Synthesizes speech and returns metadata (for Variant C - local playback).
    /// </summary>
    [HttpPost("speak")]
    [ProducesResponseType(typeof(SpeakResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> Speak([FromBody] SpeakRequest request, CancellationToken cancellationToken)
    {
        _logger.LogInformation("TTS Speak: {Text}", request.Text.Length > 50 ? request.Text[..50] + "..." : request.Text);

        var ttsRequest = new TtsRequest
        {
            Text = request.Text,
            Voice = request.Voice,
            Rate = request.Rate,
            Pitch = request.Pitch,
            PreferredProvider = request.PreferredProvider
        };

        var result = await _providerChain.SynthesizeAsync(ttsRequest, cancellationToken);

        return Ok(new SpeakResponse
        {
            Success = result.Success,
            ProviderUsed = result.ProviderUsed,
            GenerationTime = result.GenerationTime,
            AudioDuration = result.AudioDuration,
            ErrorMessage = result.ErrorMessage,
            FilePath = result.Audio is FileAudioData file ? file.FilePath : null
        });
    }

    /// <summary>
    /// Gets the list of all providers with their status.
    /// </summary>
    [HttpGet("providers")]
    [ProducesResponseType(typeof(IEnumerable<ProviderStatusResponse>), StatusCodes.Status200OK)]
    public IActionResult GetProviders()
    {
        var statuses = _providerChain.GetProvidersStatus();

        var response = statuses.Select(s => new ProviderStatusResponse
        {
            Name = s.ProviderName,
            Status = s.Enabled ? (s.CircuitState == CircuitState.Open ? "CircuitOpen" : "Available") : "Disabled",
            Priority = s.Priority,
            Enabled = s.Enabled,
            CircuitState = s.CircuitState.ToString(),
            CircuitResetTime = s.CircuitResetTime,
            ConsecutiveFailures = s.ConsecutiveFailures
        });

        return Ok(response);
    }

    /// <summary>
    /// Tests a specific provider.
    /// </summary>
    [HttpPost("test/{providerName}")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> TestProvider(string providerName, CancellationToken cancellationToken)
    {
        var provider = _providerFactory.GetProvider(providerName);
        if (provider == null)
        {
            return NotFound(new { error = $"Provider '{providerName}' not found" });
        }

        var testRequest = new TtsRequest { Text = "Test" };
        var startTime = DateTime.UtcNow;

        try
        {
            var result = await provider.SynthesizeAsync(testRequest, cancellationToken);
            var latency = DateTime.UtcNow - startTime;

            return Ok(new
            {
                success = result.Success,
                provider = providerName,
                latencyMs = latency.TotalMilliseconds,
                errorMessage = result.ErrorMessage
            });
        }
        catch (Exception ex)
        {
            var latency = DateTime.UtcNow - startTime;
            return Ok(new
            {
                success = false,
                provider = providerName,
                latencyMs = latency.TotalMilliseconds,
                errorMessage = ex.Message
            });
        }
    }
}
