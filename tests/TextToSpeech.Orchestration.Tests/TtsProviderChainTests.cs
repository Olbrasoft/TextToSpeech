using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Olbrasoft.TextToSpeech.Core.Interfaces;
using Olbrasoft.TextToSpeech.Core.Models;
using Olbrasoft.TextToSpeech.Orchestration;
using Olbrasoft.TextToSpeech.Orchestration.Configuration;
using Olbrasoft.TextToSpeech.Providers;

namespace TextToSpeech.Orchestration.Tests;

public class TtsProviderChainTests
{
    private readonly Mock<ILogger<TtsProviderChain>> _loggerMock;
    private readonly Mock<ITtsProviderFactory> _factoryMock;

    public TtsProviderChainTests()
    {
        _loggerMock = new Mock<ILogger<TtsProviderChain>>();
        _factoryMock = new Mock<ITtsProviderFactory>();
    }

    [Fact]
    public async Task SynthesizeAsync_FirstProviderSucceeds_ReturnsResult()
    {
        // Arrange
        var provider1 = CreateMockProvider("Provider1", success: true);
        _factoryMock.Setup(f => f.GetProvider("Provider1")).Returns(provider1.Object);

        var config = CreateConfig(new[] { ("Provider1", 1, true) });
        var chain = new TtsProviderChain(_loggerMock.Object, _factoryMock.Object, Options.Create(config));

        var request = new TtsRequest { Text = "Test" };

        // Act
        var result = await chain.SynthesizeAsync(request);

        // Assert
        Assert.True(result.Success);
        Assert.Equal("Provider1", result.ProviderUsed);
    }

    [Fact]
    public async Task SynthesizeAsync_FirstProviderFails_TriesSecond()
    {
        // Arrange
        var provider1 = CreateMockProvider("Provider1", success: false);
        var provider2 = CreateMockProvider("Provider2", success: true);
        _factoryMock.Setup(f => f.GetProvider("Provider1")).Returns(provider1.Object);
        _factoryMock.Setup(f => f.GetProvider("Provider2")).Returns(provider2.Object);

        var config = CreateConfig(new[] { ("Provider1", 1, true), ("Provider2", 2, true) });
        var chain = new TtsProviderChain(_loggerMock.Object, _factoryMock.Object, Options.Create(config));

        var request = new TtsRequest { Text = "Test" };

        // Act
        var result = await chain.SynthesizeAsync(request);

        // Assert
        Assert.True(result.Success);
        Assert.Equal("Provider2", result.ProviderUsed);
        Assert.NotNull(result.AttemptedProviders);
        Assert.Single(result.AttemptedProviders);
    }

    [Fact]
    public async Task SynthesizeAsync_AllProvidersFail_ReturnsFailure()
    {
        // Arrange
        var provider1 = CreateMockProvider("Provider1", success: false);
        var provider2 = CreateMockProvider("Provider2", success: false);
        _factoryMock.Setup(f => f.GetProvider("Provider1")).Returns(provider1.Object);
        _factoryMock.Setup(f => f.GetProvider("Provider2")).Returns(provider2.Object);

        var config = CreateConfig(new[] { ("Provider1", 1, true), ("Provider2", 2, true) });
        var chain = new TtsProviderChain(_loggerMock.Object, _factoryMock.Object, Options.Create(config));

        var request = new TtsRequest { Text = "Test" };

        // Act
        var result = await chain.SynthesizeAsync(request);

        // Assert
        Assert.False(result.Success);
        Assert.Contains("All", result.ErrorMessage);
        Assert.NotNull(result.AttemptedProviders);
        Assert.Equal(2, result.AttemptedProviders.Count);
    }

    [Fact]
    public async Task SynthesizeAsync_WithPreferredProvider_TriesPreferredFirst()
    {
        // Arrange
        var provider1 = CreateMockProvider("Provider1", success: true);
        var provider2 = CreateMockProvider("Provider2", success: true);
        _factoryMock.Setup(f => f.GetProvider("Provider1")).Returns(provider1.Object);
        _factoryMock.Setup(f => f.GetProvider("Provider2")).Returns(provider2.Object);

        // Provider1 has higher priority (1), Provider2 has lower (2)
        var config = CreateConfig(new[] { ("Provider1", 1, true), ("Provider2", 2, true) });
        var chain = new TtsProviderChain(_loggerMock.Object, _factoryMock.Object, Options.Create(config));

        // Request prefers Provider2
        var request = new TtsRequest { Text = "Test", PreferredProvider = "Provider2" };

        // Act
        var result = await chain.SynthesizeAsync(request);

        // Assert
        Assert.True(result.Success);
        Assert.Equal("Provider2", result.ProviderUsed);
    }

    [Fact]
    public void GetProvidersStatus_ReturnsAllProviders()
    {
        // Arrange
        var config = CreateConfig(new[] { ("Provider1", 1, true), ("Provider2", 2, false) });
        var chain = new TtsProviderChain(_loggerMock.Object, _factoryMock.Object, Options.Create(config));

        // Act
        var statuses = chain.GetProvidersStatus();

        // Assert
        Assert.Equal(2, statuses.Count);
        Assert.Contains(statuses, s => s.ProviderName == "Provider1" && s.Enabled);
        Assert.Contains(statuses, s => s.ProviderName == "Provider2" && !s.Enabled);
    }

    [Fact]
    public async Task SynthesizeAsync_DisabledProvider_IsSkipped()
    {
        // Arrange
        var provider1 = CreateMockProvider("Provider1", success: true);
        _factoryMock.Setup(f => f.GetProvider("Provider1")).Returns(provider1.Object);

        // Provider1 disabled, Provider2 doesn't exist in factory
        var config = CreateConfig(new[] { ("Provider1", 1, false), ("Provider2", 2, true) });
        var chain = new TtsProviderChain(_loggerMock.Object, _factoryMock.Object, Options.Create(config));

        var request = new TtsRequest { Text = "Test" };

        // Act
        var result = await chain.SynthesizeAsync(request);

        // Assert
        Assert.False(result.Success); // Provider2 not in factory, Provider1 disabled
        provider1.Verify(p => p.SynthesizeAsync(It.IsAny<TtsRequest>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    private static Mock<ITtsProvider> CreateMockProvider(string name, bool success)
    {
        var mock = new Mock<ITtsProvider>();
        mock.Setup(p => p.Name).Returns(name);

        if (success)
        {
            mock.Setup(p => p.SynthesizeAsync(It.IsAny<TtsRequest>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(TtsResult.Ok(
                    new MemoryAudioData { Data = new byte[] { 1, 2, 3 } },
                    name,
                    TimeSpan.FromMilliseconds(50)));
        }
        else
        {
            mock.Setup(p => p.SynthesizeAsync(It.IsAny<TtsRequest>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(TtsResult.Fail("Test failure", name, TimeSpan.FromMilliseconds(10)));
        }

        return mock;
    }

    private static OrchestrationConfig CreateConfig((string Name, int Priority, bool Enabled)[] providers)
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
                    FailureThreshold = 3,
                    ResetTimeout = TimeSpan.FromMinutes(5)
                }
            }).ToList()
        };
    }
}
