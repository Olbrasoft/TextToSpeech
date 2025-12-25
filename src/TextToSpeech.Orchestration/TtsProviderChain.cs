using System.Collections.Concurrent;
using System.Diagnostics;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Olbrasoft.TextToSpeech.Core.Interfaces;
using Olbrasoft.TextToSpeech.Core.Models;
using Olbrasoft.TextToSpeech.Orchestration.Configuration;
using Olbrasoft.TextToSpeech.Providers;
using Polly;
using Polly.CircuitBreaker;

namespace Olbrasoft.TextToSpeech.Orchestration;

/// <summary>
/// Default implementation of ITtsProviderChain with Polly circuit breakers.
/// </summary>
public sealed class TtsProviderChain : ITtsProviderChain
{
    private readonly ILogger<TtsProviderChain> _logger;
    private readonly ITtsProviderFactory _providerFactory;
    private readonly OrchestrationConfig _config;
    private readonly TimeProvider _timeProvider;
    private readonly ConcurrentDictionary<string, ProviderState> _providerStates = new();

    /// <summary>
    /// Initializes a new instance of TtsProviderChain.
    /// </summary>
    public TtsProviderChain(
        ILogger<TtsProviderChain> logger,
        ITtsProviderFactory providerFactory,
        IOptions<OrchestrationConfig> config,
        TimeProvider? timeProvider = null)
    {
        _logger = logger;
        _providerFactory = providerFactory;
        _config = config.Value;
        _timeProvider = timeProvider ?? TimeProvider.System;

        // Initialize provider states
        foreach (var providerConfig in _config.Providers.Where(p => p.Enabled))
        {
            _providerStates[providerConfig.Name] = new ProviderState(providerConfig, _timeProvider);
        }

        _logger.LogInformation(
            "TTS provider chain initialized with {Count} providers: {Providers}",
            _providerStates.Count,
            string.Join(", ", GetOrderedProviderNames()));
    }

    /// <inheritdoc />
    public async Task<TtsResult> SynthesizeAsync(TtsRequest request, CancellationToken cancellationToken = default)
    {
        var errors = new List<ProviderError>();
        var providers = GetOrderedProviders(request);

        foreach (var (provider, state) in providers)
        {
            // Check circuit breaker
            if (state.IsCircuitOpen)
            {
                _logger.LogDebug("Provider '{Provider}' circuit is open, skipping", provider.Name);
                errors.Add(new ProviderError(provider.Name, "Circuit breaker open", TimeSpan.Zero));
                continue;
            }

            var stopwatch = Stopwatch.StartNew();
            try
            {
                _logger.LogDebug("Trying provider '{Provider}'...", provider.Name);

                var result = await provider.SynthesizeAsync(request, cancellationToken);
                stopwatch.Stop();

                if (result.Success && result.Audio != null)
                {
                    state.RecordSuccess();
                    _logger.LogDebug("Provider '{Provider}' succeeded in {Elapsed}ms",
                        provider.Name, stopwatch.ElapsedMilliseconds);

                    // Include failed attempts for debugging
                    if (errors.Count > 0)
                    {
                        return new TtsResult
                        {
                            Success = result.Success,
                            Audio = result.Audio,
                            ProviderUsed = result.ProviderUsed,
                            GenerationTime = result.GenerationTime,
                            AudioDuration = result.AudioDuration,
                            AttemptedProviders = errors
                        };
                    }
                    return result;
                }

                // Provider returned failure result
                state.RecordFailure();
                var errorMsg = result.ErrorMessage ?? "Provider returned no audio";
                _logger.LogWarning("Provider '{Provider}' failed: {Error}", provider.Name, errorMsg);
                errors.Add(new ProviderError(provider.Name, errorMsg, stopwatch.Elapsed));
            }
            catch (BrokenCircuitException)
            {
                stopwatch.Stop();
                _logger.LogWarning("Provider '{Provider}' circuit breaker triggered", provider.Name);
                errors.Add(new ProviderError(provider.Name, "Circuit breaker triggered", stopwatch.Elapsed));
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                state.RecordFailure();
                _logger.LogError(ex, "Provider '{Provider}' threw exception", provider.Name);
                errors.Add(new ProviderError(provider.Name, ex.Message, stopwatch.Elapsed));
            }
        }

        _logger.LogError("All {Count} TTS providers failed for text: {Text}",
            errors.Count, request.Text.Length > 50 ? request.Text[..50] + "..." : request.Text);

        return new TtsResult
        {
            Success = false,
            ErrorMessage = $"All {errors.Count} providers failed",
            AttemptedProviders = errors,
            GenerationTime = errors.Aggregate(TimeSpan.Zero, (sum, e) => sum + e.AttemptDuration)
        };
    }

    /// <inheritdoc />
    public IReadOnlyList<ProviderChainStatus> GetProvidersStatus()
    {
        return _config.Providers
            .Select(config =>
            {
                var state = _providerStates.GetValueOrDefault(config.Name);
                return new ProviderChainStatus
                {
                    ProviderName = config.Name,
                    Priority = config.Priority,
                    Enabled = config.Enabled,
                    CircuitState = state?.CurrentCircuitState ?? CircuitState.Closed,
                    CircuitResetTime = state?.CircuitResetTime,
                    ConsecutiveFailures = state?.ConsecutiveFailures ?? 0
                };
            })
            .ToList();
    }

    private IEnumerable<string> GetOrderedProviderNames()
    {
        return _config.Providers
            .Where(p => p.Enabled)
            .OrderBy(p => p.Priority)
            .Select(p => p.Name);
    }

    private IEnumerable<(ITtsProvider Provider, ProviderState State)> GetOrderedProviders(TtsRequest request)
    {
        List<ProviderConfig> orderedConfigs;

        // If ProviderFallbackChain is specified in request, use it (overrides config)
        if (request.ProviderFallbackChain != null && request.ProviderFallbackChain.Count > 0)
        {
            _logger.LogDebug(
                "Using custom provider chain from request (Agent: {Agent}, Instance: {Instance}): {Chain}",
                request.AgentName ?? "Unknown",
                request.AgentInstanceId ?? "N/A",
                string.Join(" -> ", request.ProviderFallbackChain));

            orderedConfigs = new List<ProviderConfig>();
            foreach (var providerName in request.ProviderFallbackChain)
            {
                var config = _config.Providers.FirstOrDefault(p =>
                    p.Enabled && p.Name.Equals(providerName, StringComparison.OrdinalIgnoreCase));

                if (config != null)
                {
                    orderedConfigs.Add(config);
                }
                else
                {
                    _logger.LogWarning(
                        "Provider '{Provider}' from fallback chain not found or disabled, skipping",
                        providerName);
                }
            }

            if (orderedConfigs.Count == 0)
            {
                _logger.LogWarning("No valid providers in fallback chain, using default configuration");
                orderedConfigs = _config.Providers
                    .Where(p => p.Enabled)
                    .OrderBy(p => p.Priority)
                    .ToList();
            }
        }
        else
        {
            // Use default configuration with optional PreferredProvider
            orderedConfigs = _config.Providers
                .Where(p => p.Enabled)
                .OrderBy(p => p.Priority)
                .ToList();

            // Handle PreferredProvider - move to front if exists
            if (!string.IsNullOrEmpty(request.PreferredProvider))
            {
                var preferredConfig = orderedConfigs.FirstOrDefault(p =>
                    p.Name.Equals(request.PreferredProvider, StringComparison.OrdinalIgnoreCase));

                if (preferredConfig != null)
                {
                    orderedConfigs.Remove(preferredConfig);
                    orderedConfigs.Insert(0, preferredConfig);
                }
                else
                {
                    _logger.LogWarning("PreferredProvider '{Provider}' not found, using default chain", request.PreferredProvider);
                }
            }
        }

        foreach (var config in orderedConfigs)
        {
            var provider = _providerFactory.GetProvider(config.Name);
            var state = _providerStates.GetValueOrDefault(config.Name);

            if (provider != null && state != null)
            {
                yield return (provider, state);
            }
        }
    }

    /// <summary>
    /// Tracks circuit breaker state for a single provider.
    /// Thread-safe implementation using lock for state synchronization.
    /// </summary>
    private sealed class ProviderState
    {
        private readonly ProviderConfig _config;
        private readonly TimeProvider _timeProvider;
        private readonly object _lock = new();
        private int _consecutiveFailures;
        private int _failureMultiplier = 1;
        private DateTimeOffset? _circuitOpenUntil;

        public ProviderState(ProviderConfig config, TimeProvider timeProvider)
        {
            _config = config;
            _timeProvider = timeProvider;
        }

        public int ConsecutiveFailures
        {
            get
            {
                lock (_lock)
                {
                    return _consecutiveFailures;
                }
            }
        }

        public CircuitState CurrentCircuitState
        {
            get
            {
                lock (_lock)
                {
                    if (_circuitOpenUntil == null) return CircuitState.Closed;
                    if (_timeProvider.GetUtcNow() >= _circuitOpenUntil) return CircuitState.HalfOpen;
                    return CircuitState.Open;
                }
            }
        }

        public DateTime? CircuitResetTime
        {
            get
            {
                lock (_lock)
                {
                    return _circuitOpenUntil?.UtcDateTime;
                }
            }
        }

        public bool IsCircuitOpen => CurrentCircuitState == CircuitState.Open;

        public void RecordSuccess()
        {
            lock (_lock)
            {
                _consecutiveFailures = 0;
                _failureMultiplier = 1;
                _circuitOpenUntil = null;
            }
        }

        public void RecordFailure()
        {
            lock (_lock)
            {
                _consecutiveFailures++;

                if (_consecutiveFailures >= _config.CircuitBreaker.FailureThreshold)
                {
                    // Open circuit
                    var timeout = _config.CircuitBreaker.ResetTimeout;

                    if (_config.CircuitBreaker.UseExponentialBackoff)
                    {
                        timeout = TimeSpan.FromTicks(
                            Math.Min(
                                timeout.Ticks * _failureMultiplier,
                                _config.CircuitBreaker.MaxResetTimeout.Ticks));
                        _failureMultiplier *= 2;
                    }

                    _circuitOpenUntil = _timeProvider.GetUtcNow() + timeout;
                }
            }
        }
    }
}
