using System.Diagnostics;
using System.Net;
using System.Text;
using System.Threading.Channels;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using Olbrasoft.TextToSpeech.Core.Interfaces;
using Olbrasoft.TextToSpeech.Core.Models;

namespace Olbrasoft.TextToSpeech.Providers.GoogleCloud;

/// <summary>
/// TTS provider using Google Cloud Text-to-Speech API with multiple API key support.
/// Rotates between keys in round-robin order, tracks per-key monthly character usage
/// (auto-reset on the 1st of each month UTC), and falls over on rate limits, quota
/// exhaustion, invalid keys, or transient errors.
/// </summary>
public sealed class GoogleCloudMultiKeyTtsProvider : ITtsProvider, IDisposable
{
    private readonly ILogger<GoogleCloudMultiKeyTtsProvider> _logger;
    private readonly GoogleCloudMultiKeyConfiguration _config;
    private readonly HttpClient _httpClient;
    private readonly bool _ownsHttpClient;
    private readonly IApiKeyUsageStore? _usageStore;
    private readonly List<ApiKeyStatus> _keyStatuses;
    private readonly object _lock = new();
    private DateTime? _lastSuccessTime;
    private int _roundRobinCursor;

    // Bounded persistence pipeline: producer (synthesis path) drops a record
    // into a per-key slot; the background worker drains slots one at a time.
    // Last-write-wins per key — superseded records are simply replaced before
    // the worker picks them up, which is what we want (we only care about the
    // freshest counter, not the history of intermediate values).
    private readonly Channel<string>? _persistSignal;
    private readonly Dictionary<string, ApiKeyUsageRecord>? _pendingPersists;
    private readonly Task? _persistLoopTask;
    private readonly CancellationTokenSource? _persistCts;
    private readonly Task _hydrationTask = Task.CompletedTask;

    /// <summary>
    /// Initializes a new instance of GoogleCloudMultiKeyTtsProvider.
    /// </summary>
    /// <param name="options">Configuration options.</param>
    /// <param name="configuration">Configuration containing resolved secret values from SecureStore.</param>
    /// <param name="logger">Logger instance.</param>
    /// <param name="httpClient">Optional HTTP client for testing.</param>
    /// <param name="usageStore">Optional persistence store for per-key usage stats.</param>
    /// <exception cref="InvalidOperationException">Thrown when a secret key is not found in configuration.</exception>
    public GoogleCloudMultiKeyTtsProvider(
        IOptions<GoogleCloudMultiKeyConfiguration> options,
        IConfiguration configuration,
        ILogger<GoogleCloudMultiKeyTtsProvider> logger,
        HttpClient? httpClient = null,
        IApiKeyUsageStore? usageStore = null)
    {
        _config = options.Value;
        _logger = logger;
        _usageStore = usageStore;

        _ownsHttpClient = httpClient == null;

        var keyStatuses = _config.ApiKeySecrets
            .Select((keyConfig, index) =>
            {
                var actualKey = configuration[keyConfig.SecretKey];
                if (string.IsNullOrEmpty(actualKey))
                {
                    throw new InvalidOperationException(
                        $"Secret '{keyConfig.SecretKey}' not found in configuration. " +
                        "Ensure SecureStore is configured and the key exists in the vault.");
                }

                return new ApiKeyStatus
                {
                    Index = index,
                    Name = string.IsNullOrEmpty(keyConfig.Name) ? $"key-{index + 1}" : keyConfig.Name,
                    ActualKey = actualKey,
                    State = ApiKeyState.Available
                };
            })
            .ToList();

        _httpClient = httpClient ?? new HttpClient();
        _httpClient.Timeout = _config.Timeout;
        _keyStatuses = keyStatuses;

        if (_usageStore != null)
        {
            // Background persistence worker: bounded last-write-wins queue.
            // Capacity 1 is intentional - the channel only signals "something
            // dirty" and the worker drains _pendingPersists which already
            // collapses duplicates per key.
            _persistSignal = Channel.CreateBounded<string>(new BoundedChannelOptions(1)
            {
                FullMode = BoundedChannelFullMode.DropWrite,
                SingleReader = true,
                SingleWriter = false
            });
            _pendingPersists = new Dictionary<string, ApiKeyUsageRecord>(StringComparer.Ordinal);
            _persistCts = new CancellationTokenSource();
            _persistLoopTask = Task.Run(() => PersistLoopAsync(_persistCts.Token));

            // Hydrate asynchronously to avoid sync-over-async on the constructor
            // path. SynthesizeAsync awaits this task before touching any key
            // state, so hydration cannot clobber counters incremented by an
            // in-flight call (the previous design had a clobber race).
            _hydrationTask = Task.Run(HydrateFromStoreAsync);
        }

        _logger.LogInformation(
            "GoogleCloudMultiKeyTtsProvider initialized with {KeyCount} API keys (round-robin: {RoundRobin}, monthly cap: {Limit})",
            _keyStatuses.Count, _config.EnableRoundRobin, _config.MonthlyCharacterLimit);
    }

    /// <summary>
    /// Awaits initial hydration from the configured <see cref="IApiKeyUsageStore"/>.
    /// <see cref="SynthesizeAsync"/> already awaits this internally, so callers
    /// only need it to render a consistent snapshot via <see cref="GetKeysUsage"/>
    /// or <see cref="GetInfoAsync"/> before any synthesis has happened.
    /// </summary>
    public Task WaitForHydrationAsync() => _hydrationTask;

    /// <inheritdoc />
    public string Name => "GoogleCloudMultiKey";

    /// <inheritdoc />
    public async Task<TtsResult> SynthesizeAsync(TtsRequest request, CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();

        if (_keyStatuses.Count == 0)
        {
            _logger.LogError("No API keys configured for GoogleCloudMultiKey provider");
            return TtsResult.Fail("No API keys configured", Name, stopwatch.Elapsed);
        }

        // Wait for initial hydration before touching any key state. Without
        // this, hydration could clobber counters that a concurrent synthesis
        // already incremented (race observed in PR #21 review). Subsequent
        // calls hit a completed task - no measurable cost. Honors the caller's
        // cancellation token so a slow/hung backing store cannot pin the call.
        if (!_hydrationTask.IsCompletedSuccessfully)
        {
            await _hydrationTask.WaitAsync(cancellationToken).ConfigureAwait(false);
        }

        var charCount = request.Text?.Length ?? 0;

        var maxIterations = _keyStatuses.Count + 1;
        var iteration = 0;

        while (iteration++ < maxIterations)
        {
            var keyStatus = GetNextAvailableKey(charCount);
            if (keyStatus == null)
            {
                stopwatch.Stop();
                _logger.LogError("All Google Cloud TTS API keys exhausted");
                return TtsResult.Fail("All API keys exhausted or unavailable", Name, stopwatch.Elapsed);
            }

            _logger.LogDebug(
                "Using Google Cloud TTS key #{Index} ({Name})",
                keyStatus.Index, keyStatus.Name);

            var result = await TrySynthesizeWithKeyAsync(keyStatus, request, charCount, stopwatch, cancellationToken);

            if (result != null)
            {
                return result;
            }
        }

        stopwatch.Stop();
        _logger.LogError("Max iterations reached in SynthesizeAsync - unexpected state");
        return TtsResult.Fail("Internal error: max iterations exceeded", Name, stopwatch.Elapsed);
    }

    private async Task<TtsResult?> TrySynthesizeWithKeyAsync(
        ApiKeyStatus keyStatus,
        TtsRequest request,
        int charCount,
        Stopwatch stopwatch,
        CancellationToken cancellationToken)
    {
        try
        {
            var voice = !string.IsNullOrEmpty(request.Voice) ? request.Voice : _config.Voice;
            var speakingRate = CalculateSpeakingRate(request.Rate);
            var pitch = CalculatePitch(request.Pitch);

            var requestBody = new
            {
                input = new { text = request.Text },
                voice = new
                {
                    languageCode = ExtractLanguageCode(voice),
                    name = voice
                },
                audioConfig = new
                {
                    audioEncoding = _config.AudioEncoding,
                    speakingRate,
                    pitch,
                    volumeGainDb = _config.VolumeGainDb,
                    sampleRateHertz = _config.SampleRateHertz
                }
            };

            var json = JsonConvert.SerializeObject(requestBody);
            using var content = new StringContent(json, Encoding.UTF8, "application/json");

            var url = $"{GoogleCloudMultiKeyConfiguration.ApiEndpoint}?key={keyStatus.ActualKey}";
            var response = await _httpClient.PostAsync(url, content, cancellationToken);

            return await HandleResponseAsync(keyStatus, response, charCount, stopwatch, cancellationToken);
        }
        catch (TaskCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (HttpRequestException ex)
        {
            _logger.LogWarning(
                ex,
                "HTTP error with key #{Index} ({Name}), trying next key",
                keyStatus.Index, keyStatus.Name);
            RecordFailure(keyStatus, "HttpRequestException");
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Unexpected error with key #{Index} ({Name})",
                keyStatus.Index, keyStatus.Name);
            RecordFailure(keyStatus, ex.GetType().Name);
            return TtsResult.Fail($"Unexpected error: {ex.Message}", Name, stopwatch.Elapsed);
        }
    }

    private async Task<TtsResult?> HandleResponseAsync(
        ApiKeyStatus keyStatus,
        HttpResponseMessage response,
        int charCount,
        Stopwatch stopwatch,
        CancellationToken cancellationToken)
    {
        var statusCode = response.StatusCode;

        switch (statusCode)
        {
            case HttpStatusCode.OK:
                return await HandleSuccessAsync(keyStatus, response, charCount, stopwatch, cancellationToken);

            case HttpStatusCode.TooManyRequests: // 429
                MarkKeyAsRateLimited(keyStatus);
                return null;

            case HttpStatusCode.Forbidden: // 403
                MarkKeyAsQuotaExceeded(keyStatus);
                return null;

            case HttpStatusCode.Unauthorized: // 401
                MarkKeyAsInvalid(keyStatus);
                return null;

            case >= HttpStatusCode.InternalServerError: // 5xx
                _logger.LogWarning(
                    "Server error {StatusCode} with key #{Index} ({Name}), trying next key",
                    (int)statusCode, keyStatus.Index, keyStatus.Name);
                MarkKeyAsTemporaryError(keyStatus, ((int)statusCode).ToString());
                return null;

            case HttpStatusCode.BadRequest: // 400
                return await HandleBadRequestAsync(keyStatus, response, stopwatch, cancellationToken);

            default:
                var errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
                _logger.LogWarning(
                    "API error {StatusCode} with key #{Index} ({Name}), trying next key: {Error}",
                    (int)statusCode, keyStatus.Index, keyStatus.Name, errorContent);
                MarkKeyAsTemporaryError(keyStatus, ((int)statusCode).ToString());
                return null;
        }
    }

    private async Task<TtsResult?> HandleBadRequestAsync(
        ApiKeyStatus keyStatus,
        HttpResponseMessage response,
        Stopwatch stopwatch,
        CancellationToken cancellationToken)
    {
        var errorContent = await response.Content.ReadAsStringAsync(cancellationToken);

        if (errorContent.Contains("API key not valid", StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogWarning(
                "API key error (400) with key #{Index} ({Name}), marking as invalid: {Error}",
                keyStatus.Index, keyStatus.Name, errorContent);
            MarkKeyAsInvalid(keyStatus);
            return null;
        }

        _logger.LogError(
            "Bad request (400) with key #{Index} ({Name}): {Error}",
            keyStatus.Index, keyStatus.Name, errorContent);
        RecordFailure(keyStatus, "400");
        stopwatch.Stop();
        return TtsResult.Fail($"Bad request: {errorContent}", Name, stopwatch.Elapsed);
    }

    private async Task<TtsResult> HandleSuccessAsync(
        ApiKeyStatus keyStatus,
        HttpResponseMessage response,
        int charCount,
        Stopwatch stopwatch,
        CancellationToken cancellationToken)
    {
        var responseJson = await response.Content.ReadAsStringAsync(cancellationToken);
        var responseObj = JsonConvert.DeserializeObject<dynamic>(responseJson);

        if (responseObj?.audioContent == null)
        {
            _logger.LogError("No audio content received from Google Cloud TTS API");
            RecordFailure(keyStatus, "NoAudioContent");
            return TtsResult.Fail("No audio content received", Name, stopwatch.Elapsed);
        }

        string audioContentBase64 = responseObj.audioContent;
        var audioBytes = Convert.FromBase64String(audioContentBase64);

        stopwatch.Stop();

        _logger.LogInformation(
            "Google Cloud TTS synthesis successful with key #{Index} ({Name}): {Bytes} bytes in {Ms}ms",
            keyStatus.Index, keyStatus.Name, audioBytes.Length, stopwatch.ElapsedMilliseconds);

        RecordSuccess(keyStatus, charCount);

        var audioData = new MemoryAudioData
        {
            Data = audioBytes,
            ContentType = _config.AudioEncoding == "MP3" ? "audio/mpeg" : "audio/wav"
        };

        return TtsResult.Ok(audioData, Name, stopwatch.Elapsed);
    }

    /// <summary>
    /// Picks the next key that is currently usable. Honors the round-robin cursor,
    /// resets month boundaries lazily, expires cooldowns, and refuses keys whose
    /// projected post-call counter would exceed the configured monthly cap.
    /// </summary>
    private ApiKeyStatus? GetNextAvailableKey(int charCount)
    {
        lock (_lock)
        {
            var now = DateTime.UtcNow;
            ResetMonthlyCountersIfNewMonth(now);

            var count = _keyStatuses.Count;
            if (count == 0) return null;

            var startIndex = _config.EnableRoundRobin
                ? Math.Abs(_roundRobinCursor) % count
                : 0;

            for (var offset = 0; offset < count; offset++)
            {
                var idx = (startIndex + offset) % count;
                var key = _keyStatuses[idx];

                if (key.State == ApiKeyState.Invalid) continue;

                if (key.State != ApiKeyState.Available
                    && key.CooldownUntil.HasValue
                    && key.CooldownUntil.Value <= now)
                {
                    _logger.LogInformation(
                        "Key #{Index} ({Name}) cooldown expired, marking as available",
                        key.Index, key.Name);
                    key.State = ApiKeyState.Available;
                    key.CooldownUntil = null;
                }

                if (key.State != ApiKeyState.Available) continue;

                if (_config.MonthlyCharacterLimit > 0
                    && key.MonthlyCharacterCount + charCount > _config.MonthlyCharacterLimit)
                {
                    // Skip THIS call but keep key Available - it may still fit
                    // smaller requests later in the month.
                    continue;
                }

                if (_config.EnableRoundRobin)
                {
                    _roundRobinCursor = (idx + 1) % count;
                }

                return key;
            }

            return null;
        }
    }

    private void ResetMonthlyCountersIfNewMonth(DateTime nowUtc)
    {
        foreach (var key in _keyStatuses)
        {
            if (key.CounterYear == nowUtc.Year && key.CounterMonth == nowUtc.Month) continue;

            _logger.LogInformation(
                "Key #{Index} ({Name}) monthly counter reset {Old:yyyy-MM} -> {New:yyyy-MM}",
                key.Index, key.Name,
                new DateTime(key.CounterYear, key.CounterMonth, 1),
                new DateTime(nowUtc.Year, nowUtc.Month, 1));

            key.CounterYear = nowUtc.Year;
            key.CounterMonth = nowUtc.Month;
            key.MonthlyCharacterCount = 0;

            if (key.State == ApiKeyState.MonthlyLimitExceeded)
            {
                key.State = ApiKeyState.Available;
                key.CooldownUntil = null;
            }
        }
    }

    private void MarkKeyAsMonthlyLimitExceededLocked(ApiKeyStatus key, DateTime nowUtc)
    {
        var nextMonth = new DateTime(nowUtc.Year, nowUtc.Month, 1, 0, 0, 0, DateTimeKind.Utc).AddMonths(1);
        key.State = ApiKeyState.MonthlyLimitExceeded;
        key.CooldownUntil = nextMonth;
        key.LastErrorReason = "MonthlyLimitExceeded";
        key.LastErrorUtc = nowUtc;

        _logger.LogWarning(
            "Key #{Index} ({Name}) reached monthly cap ({Used}/{Limit} chars), held until {Until:yyyy-MM-dd}",
            key.Index, key.Name, key.MonthlyCharacterCount, _config.MonthlyCharacterLimit, nextMonth);

        PersistAsync(key);
    }

    private void MarkKeyAsRateLimited(ApiKeyStatus keyStatus)
    {
        lock (_lock)
        {
            var cooldownUntil = DateTime.UtcNow.Add(_config.RateLimitCooldown);
            keyStatus.State = ApiKeyState.RateLimited;
            keyStatus.CooldownUntil = cooldownUntil;
            keyStatus.LastErrorReason = "429";
            keyStatus.LastErrorUtc = DateTime.UtcNow;
            keyStatus.TotalFailures++;
            keyStatus.ConsecutiveFailures++;

            _logger.LogWarning(
                "Key #{Index} ({Name}) rate limited (429), marked as {State} until {Until:O}",
                keyStatus.Index, keyStatus.Name, keyStatus.State, cooldownUntil);
        }

        PersistAsync(keyStatus);
    }

    private void MarkKeyAsQuotaExceeded(ApiKeyStatus keyStatus)
    {
        lock (_lock)
        {
            var cooldownUntil = DateTime.UtcNow.Add(_config.QuotaExceededCooldown);
            keyStatus.State = ApiKeyState.QuotaExceeded;
            keyStatus.CooldownUntil = cooldownUntil;
            keyStatus.LastErrorReason = "403";
            keyStatus.LastErrorUtc = DateTime.UtcNow;
            keyStatus.TotalFailures++;
            keyStatus.ConsecutiveFailures++;

            _logger.LogWarning(
                "Key #{Index} ({Name}) quota exceeded (403), marked as {State} until {Until:O}",
                keyStatus.Index, keyStatus.Name, keyStatus.State, cooldownUntil);
        }

        PersistAsync(keyStatus);
    }

    private void MarkKeyAsInvalid(ApiKeyStatus keyStatus)
    {
        lock (_lock)
        {
            keyStatus.State = ApiKeyState.Invalid;
            keyStatus.CooldownUntil = null;
            keyStatus.LastErrorReason = "401/Invalid";
            keyStatus.LastErrorUtc = DateTime.UtcNow;
            keyStatus.TotalFailures++;
            keyStatus.ConsecutiveFailures++;

            _logger.LogError(
                "Key #{Index} ({Name}) is invalid (401), permanently disabled",
                keyStatus.Index, keyStatus.Name);
        }

        PersistAsync(keyStatus);
    }

    private void MarkKeyAsTemporaryError(ApiKeyStatus keyStatus, string reason)
    {
        lock (_lock)
        {
            var cooldownUntil = DateTime.UtcNow.AddSeconds(5);
            keyStatus.State = ApiKeyState.TemporaryError;
            keyStatus.CooldownUntil = cooldownUntil;
            keyStatus.LastErrorReason = reason;
            keyStatus.LastErrorUtc = DateTime.UtcNow;
            keyStatus.TotalFailures++;
            keyStatus.ConsecutiveFailures++;

            _logger.LogDebug(
                "Key #{Index} ({Name}) marked as temporary error until {Until:O}",
                keyStatus.Index, keyStatus.Name, cooldownUntil);
        }

        PersistAsync(keyStatus);
    }

    private void RecordSuccess(ApiKeyStatus keyStatus, int charCount)
    {
        lock (_lock)
        {
            var now = DateTime.UtcNow;
            ResetMonthlyCountersIfNewMonth(now);
            keyStatus.MonthlyCharacterCount += charCount;
            keyStatus.TotalSuccesses++;
            keyStatus.ConsecutiveFailures = 0;
            keyStatus.LastSuccessUtc = now;
            _lastSuccessTime = now;

            if (_config.MonthlyCharacterLimit > 0
                && keyStatus.MonthlyCharacterCount >= _config.MonthlyCharacterLimit
                && keyStatus.State == ApiKeyState.Available)
            {
                MarkKeyAsMonthlyLimitExceededLocked(keyStatus, now);
                return;
            }
        }

        PersistAsync(keyStatus);
    }

    private void RecordFailure(ApiKeyStatus keyStatus, string reason)
    {
        lock (_lock)
        {
            keyStatus.TotalFailures++;
            keyStatus.ConsecutiveFailures++;
            keyStatus.LastErrorReason = reason;
            keyStatus.LastErrorUtc = DateTime.UtcNow;
        }

        PersistAsync(keyStatus);
    }

    private void PersistAsync(ApiKeyStatus keyStatus)
    {
        if (_usageStore == null || _persistSignal == null || _pendingPersists == null) return;

        ApiKeyUsageRecord record;
        lock (_lock)
        {
            record = ToRecord(keyStatus);
        }

        // Last-write-wins: replace any pending record for this key. The worker
        // collapses bursts, so high-frequency synthesis produces O(keys) saves
        // per drain cycle, not O(calls).
        lock (_pendingPersists)
        {
            _pendingPersists[record.KeyName] = record;
        }

        // Channel capacity is 1; the kick is just "something is dirty".
        // BoundedChannelFullMode.DropWrite means duplicate kicks are no-ops.
        _persistSignal.Writer.TryWrite(record.KeyName);
    }

    private async Task PersistLoopAsync(CancellationToken cancellationToken)
    {
        try
        {
            await foreach (var _ in _persistSignal!.Reader.ReadAllAsync(cancellationToken).ConfigureAwait(false))
            {
                await DrainPendingAsync(cancellationToken).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException)
        {
            // shutdown
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Persistence loop terminated unexpectedly");
        }

        // Final drain on shutdown so the latest state is not lost.
        try
        {
            await DrainPendingAsync(CancellationToken.None).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Final persistence drain failed");
        }
    }

    private async Task DrainPendingAsync(CancellationToken cancellationToken)
    {
        List<ApiKeyUsageRecord> batch;
        lock (_pendingPersists!)
        {
            if (_pendingPersists.Count == 0) return;
            batch = [.. _pendingPersists.Values];
            _pendingPersists.Clear();
        }

        foreach (var record in batch)
        {
            try
            {
                await _usageStore!.SaveAsync(record, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to persist usage for key {Name}", record.KeyName);
            }
        }
    }

    private ApiKeyUsageRecord ToRecord(ApiKeyStatus key) => new()
    {
        KeyName = key.Name,
        Year = key.CounterYear,
        Month = key.CounterMonth,
        MonthlyCharacterCount = key.MonthlyCharacterCount,
        TotalSuccesses = key.TotalSuccesses,
        TotalFailures = key.TotalFailures,
        ConsecutiveFailures = key.ConsecutiveFailures,
        LastSuccessUtc = key.LastSuccessUtc,
        LastErrorUtc = key.LastErrorUtc,
        LastErrorReason = key.LastErrorReason,
        State = key.State,
        CooldownUntilUtc = key.CooldownUntil
    };

    private async Task HydrateFromStoreAsync()
    {
        var nowUtc = DateTime.UtcNow;
        foreach (var key in _keyStatuses)
        {
            try
            {
                var record = await _usageStore!.LoadAsync(key.Name).ConfigureAwait(false);
                if (record == null) continue;

                lock (_lock)
                {
                    if (record.Year == nowUtc.Year && record.Month == nowUtc.Month)
                    {
                        key.MonthlyCharacterCount = record.MonthlyCharacterCount;
                        key.CounterYear = record.Year;
                        key.CounterMonth = record.Month;
                    }
                    else
                    {
                        key.CounterYear = nowUtc.Year;
                        key.CounterMonth = nowUtc.Month;
                        key.MonthlyCharacterCount = 0;
                    }

                    key.TotalSuccesses = record.TotalSuccesses;
                    key.TotalFailures = record.TotalFailures;
                    key.ConsecutiveFailures = record.ConsecutiveFailures;
                    key.LastSuccessUtc = record.LastSuccessUtc;
                    key.LastErrorUtc = record.LastErrorUtc;
                    key.LastErrorReason = record.LastErrorReason;

                    // Restore parked state across restarts. Expired cooldowns
                    // are reset to Available (the live ResetMonthlyCounters /
                    // GetNextAvailableKey paths would do the same on next call,
                    // but we want a clean snapshot from the start).
                    key.State = record.State;
                    key.CooldownUntil = record.CooldownUntilUtc;

                    if (key.State != ApiKeyState.Invalid
                        && key.CooldownUntil.HasValue
                        && key.CooldownUntil.Value <= nowUtc)
                    {
                        key.State = ApiKeyState.Available;
                        key.CooldownUntil = null;
                    }

                    // If the persisted month matches and the counter already
                    // hit the cap, reflect that as MonthlyLimitExceeded instead
                    // of leaving stale State from before the cap was added.
                    if (_config.MonthlyCharacterLimit > 0
                        && key.MonthlyCharacterCount >= _config.MonthlyCharacterLimit
                        && key.State == ApiKeyState.Available)
                    {
                        var nextMonth = new DateTime(nowUtc.Year, nowUtc.Month, 1, 0, 0, 0, DateTimeKind.Utc).AddMonths(1);
                        key.State = ApiKeyState.MonthlyLimitExceeded;
                        key.CooldownUntil = nextMonth;
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to hydrate usage for key {Name}; starting fresh", key.Name);
            }
        }
    }

    /// <summary>
    /// Returns a thread-safe snapshot of all keys' current state, suitable for
    /// dashboards and health checks. Order matches the configured key order.
    /// </summary>
    public IReadOnlyList<ApiKeyUsageSnapshot> GetKeysUsage()
    {
        lock (_lock)
        {
            ResetMonthlyCountersIfNewMonth(DateTime.UtcNow);
            return _keyStatuses
                .Select(k => new ApiKeyUsageSnapshot
                {
                    Index = k.Index,
                    Name = k.Name,
                    State = k.State,
                    CooldownUntilUtc = k.CooldownUntil,
                    MonthlyCharacterCount = k.MonthlyCharacterCount,
                    MonthlyCharacterLimit = _config.MonthlyCharacterLimit,
                    TotalSuccesses = k.TotalSuccesses,
                    TotalFailures = k.TotalFailures,
                    ConsecutiveFailures = k.ConsecutiveFailures,
                    LastSuccessUtc = k.LastSuccessUtc,
                    LastErrorUtc = k.LastErrorUtc,
                    LastErrorReason = k.LastErrorReason
                })
                .ToList();
        }
    }

    private double CalculateSpeakingRate(int rate)
    {
        if (rate == 0) return _config.SpeakingRate;

        var normalized = rate / 100.0;
        return normalized >= 0
            ? 1.0 + (normalized * 3.0)
            : 1.0 + (normalized * 0.75);
    }

    private double CalculatePitch(int pitch)
    {
        if (pitch == 0) return _config.Pitch;
        return (pitch / 100.0) * 20.0;
    }

    private static string ExtractLanguageCode(string voice)
    {
        var parts = voice.Split('-');
        return parts.Length >= 2 ? $"{parts[0]}-{parts[1]}" : "cs-CZ";
    }

    /// <inheritdoc />
    public async Task<TtsProviderInfo> GetInfoAsync(CancellationToken cancellationToken = default)
    {
        // Surface persisted state on the first call after restart. Honors
        // the caller's cancellation so a slow store can't hang dashboards.
        if (!_hydrationTask.IsCompletedSuccessfully)
        {
            await _hydrationTask.WaitAsync(cancellationToken).ConfigureAwait(false);
        }

        var czechMaleVoices = new[]
        {
            "cs-CZ-Chirp3-HD-Achird", "cs-CZ-Chirp3-HD-Algenib", "cs-CZ-Chirp3-HD-Algieba",
            "cs-CZ-Chirp3-HD-Alnilam", "cs-CZ-Chirp3-HD-Charon", "cs-CZ-Chirp3-HD-Enceladus",
            "cs-CZ-Chirp3-HD-Fenrir", "cs-CZ-Chirp3-HD-Iapetus", "cs-CZ-Chirp3-HD-Orus",
            "cs-CZ-Chirp3-HD-Puck", "cs-CZ-Chirp3-HD-Rasalgethi", "cs-CZ-Chirp3-HD-Sadachbia",
            "cs-CZ-Chirp3-HD-Sadaltager", "cs-CZ-Chirp3-HD-Schedar", "cs-CZ-Chirp3-HD-Umbriel",
            "cs-CZ-Chirp3-HD-Zubenelgenubi"
        };

        var voices = czechMaleVoices.Select(v => new VoiceInfo
        {
            Id = v,
            Language = "cs-CZ",
            DisplayName = v.Split('-').Last(),
            Gender = "Male"
        }).ToList();

        ProviderStatus status;
        DateTime? lastSuccess;
        lock (_lock)
        {
            ResetMonthlyCountersIfNewMonth(DateTime.UtcNow);
            var totalKeys = _keyStatuses.Count;
            var availableKeys = _keyStatuses.Count(k =>
                k.State == ApiKeyState.Available ||
                (k.CooldownUntil.HasValue && k.CooldownUntil.Value <= DateTime.UtcNow));

            status = totalKeys == 0
                ? ProviderStatus.Unavailable
                : availableKeys > 0
                    ? ProviderStatus.Available
                    : ProviderStatus.Degraded;

            lastSuccess = _lastSuccessTime;
        }

        return new TtsProviderInfo
        {
            Name = Name,
            Status = status,
            LastSuccessTime = lastSuccess,
            SupportedVoices = voices
        };
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (_persistSignal != null)
        {
            _persistSignal.Writer.TryComplete();
            _persistCts?.Cancel();
            try
            {
                _persistLoopTask?.Wait(TimeSpan.FromSeconds(2));
            }
            catch
            {
                // best-effort drain on shutdown
            }
            _persistCts?.Dispose();
        }

        if (_ownsHttpClient)
        {
            _httpClient?.Dispose();
        }
    }

    private sealed class ApiKeyStatus
    {
        public int Index { get; init; }
        public required string Name { get; init; }
        public required string ActualKey { get; init; }
        public ApiKeyState State { get; set; } = ApiKeyState.Available;
        public DateTime? CooldownUntil { get; set; }

        public int CounterYear { get; set; } = DateTime.UtcNow.Year;
        public int CounterMonth { get; set; } = DateTime.UtcNow.Month;
        public long MonthlyCharacterCount { get; set; }

        public long TotalSuccesses { get; set; }
        public long TotalFailures { get; set; }
        public int ConsecutiveFailures { get; set; }
        public DateTime? LastSuccessUtc { get; set; }
        public DateTime? LastErrorUtc { get; set; }
        public string? LastErrorReason { get; set; }
    }
}
