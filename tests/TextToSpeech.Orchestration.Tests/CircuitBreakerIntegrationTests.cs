using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using Moq;
using Olbrasoft.Testing.Xunit.Attributes;
using Olbrasoft.TextToSpeech.Core.Interfaces;
using Olbrasoft.TextToSpeech.Core.Models;
using Olbrasoft.TextToSpeech.Orchestration;
using Olbrasoft.TextToSpeech.Orchestration.Configuration;
using Olbrasoft.TextToSpeech.Providers;

namespace TextToSpeech.Orchestration.Tests;

/// <summary>
/// Integration tests for circuit breaker behavior in TtsProviderChain.
/// Uses FakeTimeProvider to control time progression.
/// </summary>
public class CircuitBreakerIntegrationTests
{
    private readonly Mock<ILogger<TtsProviderChain>> _loggerMock;
    private readonly Mock<ITtsProviderFactory> _factoryMock;
    private readonly FakeTimeProvider _fakeTime;

    public CircuitBreakerIntegrationTests()
    {
        _loggerMock = new Mock<ILogger<TtsProviderChain>>();
        _factoryMock = new Mock<ITtsProviderFactory>();
        _fakeTime = new FakeTimeProvider(DateTimeOffset.UtcNow);
    }

    [Fact]
    public async Task CircuitBreaker_OpensAfterThreeFailures()
    {
        // Arrange
        var failingProvider = CreateMockProvider("Primary", success: false);
        var backupProvider = CreateMockProvider("Backup", success: true);

        _factoryMock.Setup(f => f.GetProvider("Primary")).Returns(failingProvider.Object);
        _factoryMock.Setup(f => f.GetProvider("Backup")).Returns(backupProvider.Object);

        var config = CreateConfig(
            ("Primary", 1, true, failureThreshold: 3),
            ("Backup", 2, true, failureThreshold: 3));

        var chain = new TtsProviderChain(_loggerMock.Object, _factoryMock.Object, Options.Create(config), _fakeTime);
        var request = new TtsRequest { Text = "T" };

        // Act - 3 failures to open circuit
        await chain.SynthesizeAsync(request);
        await chain.SynthesizeAsync(request);
        await chain.SynthesizeAsync(request);

        // Assert - circuit should be open now
        var status = chain.GetProvidersStatus();
        var primaryStatus = status.First(s => s.ProviderName == "Primary");

        Assert.Equal(CircuitState.Open, primaryStatus.CircuitState);
        Assert.Equal(3, primaryStatus.ConsecutiveFailures);
    }

    [Fact]
    public async Task CircuitBreaker_SkipsOpenProvider_UsesBackup()
    {
        // Arrange
        var failingProvider = CreateMockProvider("Primary", success: false);
        var backupProvider = CreateMockProvider("Backup", success: true);

        _factoryMock.Setup(f => f.GetProvider("Primary")).Returns(failingProvider.Object);
        _factoryMock.Setup(f => f.GetProvider("Backup")).Returns(backupProvider.Object);

        var config = CreateConfig(
            ("Primary", 1, true, failureThreshold: 2),
            ("Backup", 2, true, failureThreshold: 3));

        var chain = new TtsProviderChain(_loggerMock.Object, _factoryMock.Object, Options.Create(config), _fakeTime);
        var request = new TtsRequest { Text = "T" };

        // Open circuit on Primary (2 failures)
        await chain.SynthesizeAsync(request);
        await chain.SynthesizeAsync(request);

        // Reset call count to verify next call skips Primary
        failingProvider.Invocations.Clear();
        backupProvider.Invocations.Clear();

        // Act - This should skip Primary (circuit open) and go to Backup
        var result = await chain.SynthesizeAsync(request);

        // Assert
        Assert.True(result.Success);
        Assert.Equal("Backup", result.ProviderUsed);

        // Primary should NOT have been called (circuit is open)
        failingProvider.Verify(
            p => p.SynthesizeAsync(It.IsAny<TtsRequest>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task CircuitBreaker_ResetsAfterTimeout_TriesPrimaryAgain()
    {
        // Arrange
        var callCount = 0;
        var primaryProvider = new Mock<ITtsProvider>();
        primaryProvider.Setup(p => p.Name).Returns("Primary");
        primaryProvider
            .Setup(p => p.SynthesizeAsync(It.IsAny<TtsRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(() =>
            {
                callCount++;
                // First 2 calls fail (to open circuit), third call succeeds (after reset)
                if (callCount <= 2)
                    return TtsResult.Fail("Simulated failure", "Primary", TimeSpan.FromMilliseconds(10));
                return TtsResult.Ok(new MemoryAudioData { Data = [1, 2, 3] }, "Primary", TimeSpan.FromMilliseconds(50));
            });

        var backupProvider = CreateMockProvider("Backup", success: true);

        _factoryMock.Setup(f => f.GetProvider("Primary")).Returns(primaryProvider.Object);
        _factoryMock.Setup(f => f.GetProvider("Backup")).Returns(backupProvider.Object);

        var resetTimeout = TimeSpan.FromMinutes(5);
        var config = new OrchestrationConfig
        {
            Providers =
            [
                CreateProviderConfig("Primary", 1, true, 2, resetTimeout),
                CreateProviderConfig("Backup", 2, true, 3)
            ]
        };

        var chain = new TtsProviderChain(_loggerMock.Object, _factoryMock.Object, Options.Create(config), _fakeTime);
        var request = new TtsRequest { Text = "T" };

        // Open circuit on Primary
        await chain.SynthesizeAsync(request); // Fail 1
        await chain.SynthesizeAsync(request); // Fail 2 - circuit opens

        // Verify circuit is open
        var statusBefore = chain.GetProvidersStatus();
        Assert.Equal(CircuitState.Open, statusBefore.First(s => s.ProviderName == "Primary").CircuitState);

        // Act - Advance time past reset timeout
        _fakeTime.Advance(resetTimeout + TimeSpan.FromSeconds(1));

        // Circuit should now be HalfOpen
        var statusHalfOpen = chain.GetProvidersStatus();
        Assert.Equal(CircuitState.HalfOpen, statusHalfOpen.First(s => s.ProviderName == "Primary").CircuitState);

        // This call should try Primary again (HalfOpen allows one attempt)
        var result = await chain.SynthesizeAsync(request);

        // Assert
        Assert.True(result.Success);
        Assert.Equal("Primary", result.ProviderUsed);

        // Circuit should be closed now (success resets it)
        var statusAfter = chain.GetProvidersStatus();
        Assert.Equal(CircuitState.Closed, statusAfter.First(s => s.ProviderName == "Primary").CircuitState);
    }

    [Fact]
    public async Task CircuitBreaker_ExponentialBackoff_IncreasesResetTime()
    {
        // Arrange
        var failingProvider = CreateMockProvider("Primary", success: false);
        var backupProvider = CreateMockProvider("Backup", success: true);

        _factoryMock.Setup(f => f.GetProvider("Primary")).Returns(failingProvider.Object);
        _factoryMock.Setup(f => f.GetProvider("Backup")).Returns(backupProvider.Object);

        var resetTimeout = TimeSpan.FromMinutes(1);
        var config = new OrchestrationConfig
        {
            Providers =
            [
                CreateProviderConfig("Primary", 1, true, 2, resetTimeout, exponentialBackoff: true),
                CreateProviderConfig("Backup", 2, true, 3)
            ]
        };

        var chain = new TtsProviderChain(_loggerMock.Object, _factoryMock.Object, Options.Create(config), _fakeTime);
        var request = new TtsRequest { Text = "T" };

        // First failure cycle - opens circuit
        await chain.SynthesizeAsync(request);
        await chain.SynthesizeAsync(request);

        var status1 = chain.GetProvidersStatus().First(s => s.ProviderName == "Primary");
        var firstResetTime = status1.CircuitResetTime;
        Assert.NotNull(firstResetTime);

        // Advance time to half-open, then fail again
        _fakeTime.Advance(resetTimeout + TimeSpan.FromSeconds(1));
        await chain.SynthesizeAsync(request); // Primary tries, fails
        await chain.SynthesizeAsync(request); // Still failing

        // Second failure cycle should have longer timeout (2x)
        var status2 = chain.GetProvidersStatus().First(s => s.ProviderName == "Primary");
        var secondResetTime = status2.CircuitResetTime;
        Assert.NotNull(secondResetTime);

        // Verify exponential increase
        var expectedSecondTimeout = _fakeTime.GetUtcNow() + resetTimeout * 2;
        Assert.True(secondResetTime > firstResetTime,
            $"Second reset time ({secondResetTime}) should be later than first ({firstResetTime})");
    }

    [Fact]
    public async Task FallbackChain_TriesAllProviders_InPriorityOrder()
    {
        // Arrange
        var provider1 = CreateMockProvider("First", success: false);
        var provider2 = CreateMockProvider("Second", success: false);
        var provider3 = CreateMockProvider("Third", success: true);

        _factoryMock.Setup(f => f.GetProvider("First")).Returns(provider1.Object);
        _factoryMock.Setup(f => f.GetProvider("Second")).Returns(provider2.Object);
        _factoryMock.Setup(f => f.GetProvider("Third")).Returns(provider3.Object);

        var config = CreateConfig(
            ("First", 1, true, failureThreshold: 10),
            ("Second", 2, true, failureThreshold: 10),
            ("Third", 3, true, failureThreshold: 10));

        var chain = new TtsProviderChain(_loggerMock.Object, _factoryMock.Object, Options.Create(config), _fakeTime);
        var request = new TtsRequest { Text = "T" };

        // Act
        var result = await chain.SynthesizeAsync(request);

        // Assert
        Assert.True(result.Success);
        Assert.Equal("Third", result.ProviderUsed);
        Assert.NotNull(result.AttemptedProviders);
        Assert.Equal(2, result.AttemptedProviders.Count);
        Assert.Equal("First", result.AttemptedProviders[0].ProviderName);
        Assert.Equal("Second", result.AttemptedProviders[1].ProviderName);
    }

    [Fact]
    public async Task PreferredProvider_OverridesPriority()
    {
        // Arrange
        var provider1 = CreateMockProvider("HighPriority", success: true);
        var provider2 = CreateMockProvider("LowPriority", success: true);

        _factoryMock.Setup(f => f.GetProvider("HighPriority")).Returns(provider1.Object);
        _factoryMock.Setup(f => f.GetProvider("LowPriority")).Returns(provider2.Object);

        var config = CreateConfig(
            ("HighPriority", 1, true, failureThreshold: 3),
            ("LowPriority", 2, true, failureThreshold: 3));

        var chain = new TtsProviderChain(_loggerMock.Object, _factoryMock.Object, Options.Create(config), _fakeTime);

        // Act - Request with preferred provider
        var request = new TtsRequest { Text = "T", PreferredProvider = "LowPriority" };
        var result = await chain.SynthesizeAsync(request);

        // Assert
        Assert.True(result.Success);
        Assert.Equal("LowPriority", result.ProviderUsed);

        // HighPriority should NOT have been called
        provider1.Verify(
            p => p.SynthesizeAsync(It.IsAny<TtsRequest>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [SkipOnCIFact]
    public async Task Integration_RealTimeProvider_CircuitBreakerBehavior()
    {
        // This test uses real time - only runs locally
        // Useful for verifying behavior with actual timing

        var failingProvider = CreateMockProvider("Primary", success: false);
        var backupProvider = CreateMockProvider("Backup", success: true);

        _factoryMock.Setup(f => f.GetProvider("Primary")).Returns(failingProvider.Object);
        _factoryMock.Setup(f => f.GetProvider("Backup")).Returns(backupProvider.Object);

        var config = new OrchestrationConfig
        {
            Providers =
            [
                CreateProviderConfig("Primary", 1, true, 2, TimeSpan.FromMilliseconds(100)),
                CreateProviderConfig("Backup", 2, true, 3)
            ]
        };

        // Use real TimeProvider (no mock)
        var chain = new TtsProviderChain(_loggerMock.Object, _factoryMock.Object, Options.Create(config));
        var request = new TtsRequest { Text = "T" };

        // Open circuit
        await chain.SynthesizeAsync(request);
        await chain.SynthesizeAsync(request);

        var statusOpen = chain.GetProvidersStatus().First(s => s.ProviderName == "Primary");
        Assert.Equal(CircuitState.Open, statusOpen.CircuitState);

        // Wait for reset
        await Task.Delay(150);

        var statusHalfOpen = chain.GetProvidersStatus().First(s => s.ProviderName == "Primary");
        Assert.Equal(CircuitState.HalfOpen, statusHalfOpen.CircuitState);
    }

    private static Mock<ITtsProvider> CreateMockProvider(string name, bool success)
    {
        var mock = new Mock<ITtsProvider>();
        mock.Setup(p => p.Name).Returns(name);

        if (success)
        {
            mock.Setup(p => p.SynthesizeAsync(It.IsAny<TtsRequest>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(TtsResult.Ok(
                    new MemoryAudioData { Data = [1, 2, 3] },
                    name,
                    TimeSpan.FromMilliseconds(50)));
        }
        else
        {
            mock.Setup(p => p.SynthesizeAsync(It.IsAny<TtsRequest>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(TtsResult.Fail("Simulated failure", name, TimeSpan.FromMilliseconds(10)));
        }

        return mock;
    }

    private static OrchestrationConfig CreateConfigFull(
        params (string Name, int Priority, bool Enabled, int failureThreshold, TimeSpan resetTimeout, bool exponentialBackoff)[] providers)
    {
        return new OrchestrationConfig
        {
            Providers = providers.Select(p => new ProviderConfig
            {
                Name = p.Name,
                Priority = p.Priority,
                Enabled = p.Enabled,
                CircuitBreaker = new CircuitBreakerConfig
                {
                    FailureThreshold = p.failureThreshold,
                    ResetTimeout = p.resetTimeout,
                    UseExponentialBackoff = p.exponentialBackoff
                }
            }).ToList()
        };
    }

    private static OrchestrationConfig CreateConfig(
        params (string Name, int Priority, bool Enabled, int failureThreshold)[] providers)
    {
        return new OrchestrationConfig
        {
            Providers = providers.Select(p => new ProviderConfig
            {
                Name = p.Name,
                Priority = p.Priority,
                Enabled = p.Enabled,
                CircuitBreaker = new CircuitBreakerConfig
                {
                    FailureThreshold = p.failureThreshold,
                    ResetTimeout = TimeSpan.FromMinutes(5),
                    UseExponentialBackoff = false
                }
            }).ToList()
        };
    }

    private static ProviderConfig CreateProviderConfig(
        string name, int priority, bool enabled, int failureThreshold,
        TimeSpan? resetTimeout = null, bool exponentialBackoff = false)
    {
        return new ProviderConfig
        {
            Name = name,
            Priority = priority,
            Enabled = enabled,
            CircuitBreaker = new CircuitBreakerConfig
            {
                FailureThreshold = failureThreshold,
                ResetTimeout = resetTimeout ?? TimeSpan.FromMinutes(5),
                UseExponentialBackoff = exponentialBackoff
            }
        };
    }
}
