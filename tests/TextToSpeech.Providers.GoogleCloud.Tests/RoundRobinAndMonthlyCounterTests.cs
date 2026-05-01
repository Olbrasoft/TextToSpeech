using System.Collections.Concurrent;
using System.Net;
using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Newtonsoft.Json;
using Olbrasoft.TextToSpeech.Core.Models;
using Olbrasoft.TextToSpeech.Providers.GoogleCloud;

namespace TextToSpeech.Providers.GoogleCloud.Tests;

public class RoundRobinAndMonthlyCounterTests
{
    private readonly Mock<ILogger<GoogleCloudMultiKeyTtsProvider>> _logger = new();

    [Fact]
    public async Task RoundRobin_DistributesCallsAcrossAllKeys()
    {
        // Arrange - 3 keys, all healthy. Track which key Google saw on each call.
        var seenKeys = new List<string>();
        var handler = new MockHttpMessageHandler(req =>
        {
            seenKeys.Add(req.RequestUri!.Query.Split("key=")[1]);
            return SuccessResponse();
        });

        var provider = BuildProvider(
            secretValues: ["k-A", "k-B", "k-C"],
            handler: handler,
            enableRoundRobin: true,
            monthlyLimit: 0); // disable cap so it doesn't interfere

        // Act - 6 calls
        for (var i = 0; i < 6; i++)
        {
            var r = await provider.SynthesizeAsync(new TtsRequest { Text = "x" });
            Assert.True(r.Success);
        }

        // Assert - each key used exactly twice (3-key round robin x 2 cycles)
        Assert.Equal(2, seenKeys.Count(k => k == "k-A"));
        Assert.Equal(2, seenKeys.Count(k => k == "k-B"));
        Assert.Equal(2, seenKeys.Count(k => k == "k-C"));
    }

    [Fact]
    public async Task FirstAvailable_LegacyMode_AlwaysHitsFirstKey()
    {
        var seenKeys = new List<string>();
        var handler = new MockHttpMessageHandler(req =>
        {
            seenKeys.Add(req.RequestUri!.Query.Split("key=")[1]);
            return SuccessResponse();
        });

        var provider = BuildProvider(
            secretValues: ["k-A", "k-B", "k-C"],
            handler: handler,
            enableRoundRobin: false,
            monthlyLimit: 0);

        for (var i = 0; i < 4; i++)
        {
            await provider.SynthesizeAsync(new TtsRequest { Text = "x" });
        }

        // All four calls hit the first key.
        Assert.Equal(4, seenKeys.Count(k => k == "k-A"));
        Assert.DoesNotContain("k-B", seenKeys);
    }

    [Fact]
    public async Task MonthlyLimit_KeyTakenOutOfRotation_WhenLimitReached()
    {
        // Two keys, monthly cap = 10 chars. Each call uses 5 chars.
        // After 2 calls per key, the cap (10) is reached and the key drops out.
        var seenKeys = new ConcurrentBag<string>();
        var handler = new MockHttpMessageHandler(req =>
        {
            seenKeys.Add(req.RequestUri!.Query.Split("key=")[1]);
            return SuccessResponse();
        });

        var provider = BuildProvider(
            secretValues: ["k-A", "k-B"],
            handler: handler,
            enableRoundRobin: true,
            monthlyLimit: 10);

        // 4 successful calls fit (2 per key x 5 chars = 10). Fifth must fail.
        for (var i = 0; i < 4; i++)
        {
            var r = await provider.SynthesizeAsync(new TtsRequest { Text = "12345" }); // 5 chars
            Assert.True(r.Success, $"Call {i + 1} expected success, got: {r.ErrorMessage}");
        }

        var exhausted = await provider.SynthesizeAsync(new TtsRequest { Text = "12345" });
        Assert.False(exhausted.Success);
        Assert.Contains("exhausted", exhausted.ErrorMessage, StringComparison.OrdinalIgnoreCase);

        var snapshot = provider.GetKeysUsage();
        Assert.All(snapshot, s => Assert.Equal(ApiKeyState.MonthlyLimitExceeded, s.State));
        Assert.All(snapshot, s => Assert.Equal(10, s.MonthlyCharacterCount));
        Assert.All(snapshot, s => Assert.Equal("MonthlyLimitExceeded", s.LastErrorReason));
    }

    [Fact]
    public async Task MonthlyLimit_RejectsCallThatWouldExceed()
    {
        // Single key, cap 100. After 60 chars used, a 50-char request must fail
        // (60 + 50 > 100), even though the key has 40 chars of headroom.
        var handler = new MockHttpMessageHandler(_ => SuccessResponse());

        var provider = BuildProvider(
            secretValues: ["k-A"],
            handler: handler,
            enableRoundRobin: true,
            monthlyLimit: 100);

        var first = await provider.SynthesizeAsync(new TtsRequest { Text = new string('x', 60) });
        Assert.True(first.Success);

        var oversized = await provider.SynthesizeAsync(new TtsRequest { Text = new string('x', 50) });
        Assert.False(oversized.Success);

        var fits = await provider.SynthesizeAsync(new TtsRequest { Text = new string('x', 40) });
        Assert.True(fits.Success);

        var snap = provider.GetKeysUsage().Single();
        Assert.Equal(100, snap.MonthlyCharacterCount);
    }

    [Fact]
    public async Task GetKeysUsage_ReturnsOrderedSnapshotsWithCounters()
    {
        var handler = new MockHttpMessageHandler(_ => SuccessResponse());
        var provider = BuildProvider(
            secretValues: ["k-A", "k-B"],
            handler: handler,
            enableRoundRobin: true,
            monthlyLimit: 0);

        await provider.SynthesizeAsync(new TtsRequest { Text = "abcde" }); // 5 chars
        await provider.SynthesizeAsync(new TtsRequest { Text = "xy" });    // 2 chars

        var snap = provider.GetKeysUsage();
        Assert.Equal(2, snap.Count);
        Assert.Equal(0, snap[0].Index);
        Assert.Equal("key-1", snap[0].Name);
        Assert.Equal(5, snap[0].MonthlyCharacterCount);
        Assert.Equal(1, snap[0].TotalSuccesses);
        Assert.Equal(2, snap[1].MonthlyCharacterCount);
        Assert.Equal(1, snap[1].TotalSuccesses);
        Assert.All(snap, s => Assert.Equal(ApiKeyState.Available, s.State));
    }

    [Fact]
    public async Task UsageStore_IsCalledAfterEachCall()
    {
        // Deterministic wait: gate releases as soon as a save with the expected
        // counter lands. Background worker is bounded - records can be
        // collapsed - so we look for the final counter value, not call count.
        var saved = new TaskCompletionSource<ApiKeyUsageRecord>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var store = new InMemoryStore(onSave: r =>
        {
            if (r.MonthlyCharacterCount == 5) saved.TrySetResult(r);
        });

        var handler = new MockHttpMessageHandler(_ => SuccessResponse());

        var provider = BuildProvider(
            secretValues: ["k-A"],
            handler: handler,
            enableRoundRobin: true,
            monthlyLimit: 0,
            store: store);

        await provider.SynthesizeAsync(new TtsRequest { Text = "hello" });

        var record = await saved.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal("key-1", record.KeyName);
        Assert.Equal(5, record.MonthlyCharacterCount);
        Assert.Equal(1, record.TotalSuccesses);
    }

    [Fact]
    public async Task UsageStore_HydratesCounterOnStartup_ForCurrentMonth()
    {
        var now = DateTime.UtcNow;
        var store = new InMemoryStore();
        store.Records["key-1"] = new ApiKeyUsageRecord
        {
            KeyName = "key-1",
            Year = now.Year,
            Month = now.Month,
            MonthlyCharacterCount = 999_500,
            TotalSuccesses = 42,
            TotalFailures = 1,
            ConsecutiveFailures = 0,
            LastSuccessUtc = now.AddMinutes(-5)
        };

        var provider = BuildProvider(
            secretValues: ["k-A"],
            handler: new MockHttpMessageHandler(_ => SuccessResponse()),
            enableRoundRobin: true,
            monthlyLimit: 1_000_000,
            store: store);

        await provider.WaitForHydrationAsync().WaitAsync(TimeSpan.FromSeconds(5));
        var snap = provider.GetKeysUsage().Single();
        Assert.Equal(999_500, snap.MonthlyCharacterCount);
        Assert.Equal(42, snap.TotalSuccesses);
    }

    [Fact]
    public async Task UsageStore_DiscardsCounterFromPreviousMonth()
    {
        var now = DateTime.UtcNow;
        var store = new InMemoryStore();
        var prev = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc).AddMonths(-1);
        store.Records["key-1"] = new ApiKeyUsageRecord
        {
            KeyName = "key-1",
            Year = prev.Year,
            Month = prev.Month,
            MonthlyCharacterCount = 999_999,
            TotalSuccesses = 100
        };

        var provider = BuildProvider(
            secretValues: ["k-A"],
            handler: new MockHttpMessageHandler(_ => SuccessResponse()),
            enableRoundRobin: true,
            monthlyLimit: 1_000_000,
            store: store);

        await provider.WaitForHydrationAsync().WaitAsync(TimeSpan.FromSeconds(5));
        var snap = provider.GetKeysUsage().Single();
        Assert.Equal(0, snap.MonthlyCharacterCount); // reset on new month
        Assert.Equal(100, snap.TotalSuccesses);      // lifetime stats kept
    }

    [Fact]
    public async Task UsageStore_HydratesParkedState_AcrossRestart()
    {
        // Persisted snapshot says key-1 was rate-limited and is still in cooldown.
        var now = DateTime.UtcNow;
        var store = new InMemoryStore();
        store.Records["key-1"] = new ApiKeyUsageRecord
        {
            KeyName = "key-1",
            Year = now.Year,
            Month = now.Month,
            MonthlyCharacterCount = 12_345,
            TotalSuccesses = 5,
            TotalFailures = 1,
            ConsecutiveFailures = 1,
            LastErrorUtc = now.AddMinutes(-2),
            LastErrorReason = "429",
            State = ApiKeyState.RateLimited,
            CooldownUntilUtc = now.AddMinutes(30)
        };

        var provider = BuildProvider(
            secretValues: ["k-A"],
            handler: new MockHttpMessageHandler(_ => SuccessResponse()),
            enableRoundRobin: true,
            monthlyLimit: 1_000_000,
            store: store);

        await provider.WaitForHydrationAsync().WaitAsync(TimeSpan.FromSeconds(5));
        var snap = provider.GetKeysUsage().Single();
        Assert.Equal(ApiKeyState.RateLimited, snap.State);
        Assert.NotNull(snap.CooldownUntilUtc);
    }

    [Fact]
    public async Task UsageStore_ExpiredCooldownIsResetOnHydrate()
    {
        // Persisted state says rate-limited but the cooldown window has passed.
        // After hydration the key should be Available again - no need to wait
        // for the next routing pass to discover the timer expired.
        var now = DateTime.UtcNow;
        var store = new InMemoryStore();
        store.Records["key-1"] = new ApiKeyUsageRecord
        {
            KeyName = "key-1",
            Year = now.Year,
            Month = now.Month,
            MonthlyCharacterCount = 100,
            State = ApiKeyState.RateLimited,
            CooldownUntilUtc = now.AddMinutes(-1) // expired
        };

        var provider = BuildProvider(
            secretValues: ["k-A"],
            handler: new MockHttpMessageHandler(_ => SuccessResponse()),
            enableRoundRobin: true,
            monthlyLimit: 1_000_000,
            store: store);

        await provider.WaitForHydrationAsync().WaitAsync(TimeSpan.FromSeconds(5));
        var snap = provider.GetKeysUsage().Single();
        Assert.Equal(ApiKeyState.Available, snap.State);
        Assert.Null(snap.CooldownUntilUtc);
    }

    [Fact]
    public async Task PersistAsync_CollapsesBurstsToOneSavePerKey()
    {
        // Determinism trick: hold SaveAsync on a TaskCompletionSource so the
        // worker can't drain while we're enqueuing. Push N synthesis calls;
        // by the time we release the gate the persistence channel has
        // accumulated duplicates that the worker collapses. Without bounded
        // last-write-wins this would be ~callCount saves; with it, a handful.
        var saveCount = 0;
        var saveGate = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var store = new InMemoryStore(onSave: _ => Interlocked.Increment(ref saveCount))
        {
            BeforeSave = () => saveGate.Task
        };

        var provider = BuildProvider(
            secretValues: ["k-A", "k-B", "k-C"],
            handler: new MockHttpMessageHandler(_ => SuccessResponse()),
            enableRoundRobin: true,
            monthlyLimit: 0,
            store: store);

        await provider.WaitForHydrationAsync().WaitAsync(TimeSpan.FromSeconds(5));

        const int callCount = 60;
        for (var i = 0; i < callCount; i++)
        {
            await provider.SynthesizeAsync(new TtsRequest { Text = "x" });
        }

        saveGate.SetResult(true);

        // Wait for the queue to settle: poll until saveCount stops climbing.
        var prev = -1;
        for (var i = 0; i < 100 && saveCount != prev; i++)
        {
            prev = saveCount;
            await Task.Delay(20);
        }

        Assert.True(saveCount > 0, "Expected at least one save");
        // Tight upper bound: 3 keys, even allowing for a handful of drain
        // cycles, total saves must be << callCount. Anything close to 60
        // means the channel didn't collapse anything.
        Assert.True(saveCount <= 12,
            $"Save count {saveCount} should be <= 12 (3 keys x ~few drains); channel must collapse bursts. callCount={callCount}");
    }

    [Fact]
    public async Task RoundRobin_SkipsKeyInCooldown()
    {
        // 3 keys; key #2 gets rate limited on its first hit. Subsequent calls
        // should rotate between #1 and #3 only.
        var seenKeys = new List<string>();
        var rateLimitOnce = false;

        var handler = new MockHttpMessageHandler(req =>
        {
            var key = req.RequestUri!.Query.Split("key=")[1];
            seenKeys.Add(key);
            if (key == "k-B" && !rateLimitOnce)
            {
                rateLimitOnce = true;
                return new HttpResponseMessage(HttpStatusCode.TooManyRequests)
                { Content = new StringContent("rate limit") };
            }
            return SuccessResponse();
        });

        var provider = BuildProvider(
            secretValues: ["k-A", "k-B", "k-C"],
            handler: handler,
            enableRoundRobin: true,
            monthlyLimit: 0);

        // 5 successful syntheses. First call: A(ok); second: B(429 -> retry C ok);
        // remaining three rotate A, C, A (B is parked, C is the next slot).
        for (var i = 0; i < 5; i++)
        {
            var r = await provider.SynthesizeAsync(new TtsRequest { Text = "x" });
            Assert.True(r.Success);
        }

        // After the rate-limit incident, key B is parked.
        var snap = provider.GetKeysUsage();
        Assert.Equal(ApiKeyState.RateLimited, snap.First(s => s.Name == "key-2").State);

        // Subsequent calls should not have hit B again (only the one rate-limited call).
        Assert.Equal(1, seenKeys.Count(k => k == "k-B"));
    }

    #region helpers

    private GoogleCloudMultiKeyTtsProvider BuildProvider(
        string[] secretValues,
        HttpMessageHandler handler,
        bool enableRoundRobin,
        long monthlyLimit,
        IApiKeyUsageStore? store = null)
    {
        var keyConfigs = secretValues
            .Select((_, i) => new ApiKeyConfig { SecretKey = $"Key{i + 1}", Name = $"key-{i + 1}" })
            .ToList();

        var config = new GoogleCloudMultiKeyConfiguration
        {
            ApiKeySecrets = keyConfigs,
            EnableRoundRobin = enableRoundRobin,
            MonthlyCharacterLimit = monthlyLimit
        };

        var data = secretValues
            .Select((v, i) => new KeyValuePair<string, string?>($"Key{i + 1}", v));
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(data).Build();

        var http = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(30) };
        return new GoogleCloudMultiKeyTtsProvider(
            Options.Create(config),
            configuration,
            _logger.Object,
            http,
            store);
    }

    private static HttpResponseMessage SuccessResponse()
    {
        var body = new { audioContent = Convert.ToBase64String(Encoding.UTF8.GetBytes("audio")) };
        return new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(JsonConvert.SerializeObject(body), Encoding.UTF8, "application/json")
        };
    }

    private sealed class InMemoryStore : IApiKeyUsageStore
    {
        public ConcurrentDictionary<string, ApiKeyUsageRecord> Records { get; } = new();
        public Func<Task>? BeforeSave { get; set; }
        private readonly Action<ApiKeyUsageRecord>? _onSave;

        public InMemoryStore(Action<ApiKeyUsageRecord>? onSave = null) => _onSave = onSave;

        public Task<ApiKeyUsageRecord?> LoadAsync(string keyName, CancellationToken cancellationToken = default)
            => Task.FromResult(Records.TryGetValue(keyName, out var r) ? r : null);

        public async Task SaveAsync(ApiKeyUsageRecord record, CancellationToken cancellationToken = default)
        {
            if (BeforeSave != null) await BeforeSave().ConfigureAwait(false);
            Records[record.KeyName] = record;
            _onSave?.Invoke(record);
        }
    }

    #endregion
}
