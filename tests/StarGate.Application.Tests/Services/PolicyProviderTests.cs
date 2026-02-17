using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using StarGate.Application.Services;
using StarGate.Core.Abstractions;
using StarGate.Core.Domain.Configuration;

namespace StarGate.Application.Tests.Services;

/// <summary>
/// Unit tests for PolicyProvider service.
/// Covers caching, override resolution, fallback scenarios, and validation.
/// </summary>
public class PolicyProviderTests : IAsyncDisposable
{
    private readonly Mock<IPolicyRepository> _repositoryMock;
    private readonly Mock<ICacheStore> _cacheStoreMock;
    private readonly Mock<PolicyResolutionService> _resolutionServiceMock;
    private readonly PolicyCacheStatistics _cacheStatistics;
    private readonly PolicyProviderOptions _options;
    private readonly PolicyProvider _sut;

    public PolicyProviderTests()
    {
        _repositoryMock = new Mock<IPolicyRepository>();
        _cacheStoreMock = new Mock<ICacheStore>();
        
        // Create mock for PolicyResolutionService
        _resolutionServiceMock = new Mock<PolicyResolutionService>(
            NullLogger<PolicyResolutionService>.Instance);
        
        _cacheStatistics = new PolicyCacheStatistics();
        
        _options = new PolicyProviderOptions
        {
            CacheTtlMinutes = 60,
            DefaultTimeoutSeconds = 300,
            DefaultMaxRetryAttempts = 3,
            DefaultRetryDelaySeconds = 5,
            DefaultRetentionDays = 30,
            DefaultMaxConcurrentProcesses = 10,
            DefaultBackoffStrategy = "Exponential"
        };

        _sut = new PolicyProvider(
            _repositoryMock.Object,
            _cacheStoreMock.Object,
            Options.Create(_options),
            _resolutionServiceMock.Object,
            _cacheStatistics,
            NullLogger<PolicyProvider>.Instance);
    }

    #region Memory Cache Tests

    [Fact]
    public async Task GetEffectivePolicyAsync_Should_ReturnFromMemoryCache_WhenCached()
    {
        // Arrange
        const string processType = "order";
        const string clientId = "client-123";
        var policy = CreateDefaultPolicy(processType);

        // Setup L2 cache miss
        _cacheStoreMock
            .Setup(x => x.GetAsync<ProcessTypePolicy>(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((ProcessTypePolicy?)null);

        _cacheStoreMock
            .Setup(x => x.GetAsync<ClientPolicyOverride>(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((ClientPolicyOverride?)null);

        _repositoryMock
            .Setup(x => x.GetProcessTypePolicyAsync(processType, It.IsAny<CancellationToken>()))
            .ReturnsAsync(policy);

        _repositoryMock
            .Setup(x => x.GetClientOverrideAsync(clientId, processType, It.IsAny<CancellationToken>()))
            .ReturnsAsync((ClientPolicyOverride?)null);

        _resolutionServiceMock
            .Setup(x => x.ValidatePolicy(It.IsAny<ProcessTypePolicy>()))
            .Returns(new PolicyValidationResult { IsValid = true, Errors = new List<string>() });

        // First call to populate memory cache
        await _sut.GetEffectivePolicyAsync(clientId, processType);

        _repositoryMock.Reset();

        // Act - Second call should use memory cache
        var result = await _sut.GetEffectivePolicyAsync(clientId, processType);

        // Assert
        result.Should().NotBeNull();
        result.ProcessType.Should().Be(processType);
        _repositoryMock.Verify(
            x => x.GetProcessTypePolicyAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
        _cacheStatistics.Hits.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task GetEffectivePolicyAsync_Should_LoadFromRepository_OnCacheMiss()
    {
        // Arrange
        const string processType = "order";
        const string clientId = "client-123";
        var policy = CreateDefaultPolicy(processType);

        _cacheStoreMock
            .Setup(x => x.GetAsync<ProcessTypePolicy>(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((ProcessTypePolicy?)null);

        _cacheStoreMock
            .Setup(x => x.GetAsync<ClientPolicyOverride>(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((ClientPolicyOverride?)null);

        _repositoryMock
            .Setup(x => x.GetProcessTypePolicyAsync(processType, It.IsAny<CancellationToken>()))
            .ReturnsAsync(policy);

        _repositoryMock
            .Setup(x => x.GetClientOverrideAsync(clientId, processType, It.IsAny<CancellationToken>()))
            .ReturnsAsync((ClientPolicyOverride?)null);

        _resolutionServiceMock
            .Setup(x => x.ValidatePolicy(It.IsAny<ProcessTypePolicy>()))
            .Returns(new PolicyValidationResult { IsValid = true, Errors = new List<string>() });

        // Act
        var result = await _sut.GetEffectivePolicyAsync(clientId, processType);

        // Assert
        result.Should().NotBeNull();
        result.ProcessType.Should().Be(processType);
        _repositoryMock.Verify(
            x => x.GetProcessTypePolicyAsync(processType, It.IsAny<CancellationToken>()),
            Times.Once);
        _cacheStatistics.Misses.Should().BeGreaterThan(0);
    }

    #endregion

    #region Client Override Tests

    [Fact]
    public async Task GetEffectivePolicyAsync_Should_ApplyClientOverride_WhenProvided()
    {
        // Arrange
        const string processType = "order";
        const string clientId = "client-123";
        var typeDefault = CreateDefaultPolicy(processType);
        var customTimeout = TimeSpan.FromMinutes(10);
        var clientOverride = new ClientPolicyOverride
        {
            ClientId = clientId,
            ProcessType = processType,
            Timeout = customTimeout,
            UpdatedAt = DateTime.UtcNow
        };
        var resolvedPolicy = typeDefault with { Timeout = customTimeout };

        _cacheStoreMock
            .Setup(x => x.GetAsync<ProcessTypePolicy>(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((ProcessTypePolicy?)null);

        _cacheStoreMock
            .Setup(x => x.GetAsync<ClientPolicyOverride>(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((ClientPolicyOverride?)null);

        _repositoryMock
            .Setup(x => x.GetProcessTypePolicyAsync(processType, It.IsAny<CancellationToken>()))
            .ReturnsAsync(typeDefault);

        _repositoryMock
            .Setup(x => x.GetClientOverrideAsync(clientId, processType, It.IsAny<CancellationToken>()))
            .ReturnsAsync(clientOverride);

        _resolutionServiceMock
            .Setup(x => x.ValidateClientOverride(clientOverride))
            .Returns(new PolicyValidationResult { IsValid = true, Errors = new List<string>() });

        _resolutionServiceMock
            .Setup(x => x.ResolvePolicy(typeDefault, clientOverride))
            .Returns(resolvedPolicy);

        _resolutionServiceMock
            .Setup(x => x.ValidatePolicy(resolvedPolicy))
            .Returns(new PolicyValidationResult { IsValid = true, Errors = new List<string>() });

        // Act
        var result = await _sut.GetEffectivePolicyAsync(clientId, processType);

        // Assert
        result.Timeout.Should().Be(customTimeout);
        result.Source.TimeoutFromOverride.Should().BeTrue();
        _resolutionServiceMock.Verify(
            x => x.ResolvePolicy(typeDefault, clientOverride),
            Times.Once);
    }

    [Fact]
    public async Task GetEffectivePolicyAsync_Should_UseTypeDefault_WhenNoOverride()
    {
        // Arrange
        const string processType = "order";
        const string clientId = "client-123";
        var typeDefault = CreateDefaultPolicy(processType);

        _cacheStoreMock
            .Setup(x => x.GetAsync<ProcessTypePolicy>(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((ProcessTypePolicy?)null);

        _cacheStoreMock
            .Setup(x => x.GetAsync<ClientPolicyOverride>(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((ClientPolicyOverride?)null);

        _repositoryMock
            .Setup(x => x.GetProcessTypePolicyAsync(processType, It.IsAny<CancellationToken>()))
            .ReturnsAsync(typeDefault);

        _repositoryMock
            .Setup(x => x.GetClientOverrideAsync(clientId, processType, It.IsAny<CancellationToken>()))
            .ReturnsAsync((ClientPolicyOverride?)null);

        _resolutionServiceMock
            .Setup(x => x.ValidatePolicy(It.IsAny<ProcessTypePolicy>()))
            .Returns(new PolicyValidationResult { IsValid = true, Errors = new List<string>() });

        // Act
        var result = await _sut.GetEffectivePolicyAsync(clientId, processType);

        // Assert
        result.Should().NotBeNull();
        result.Timeout.Should().Be(typeDefault.Timeout);
        result.RetryPolicy.Should().BeEquivalentTo(typeDefault.RetryPolicy);
        result.Source.TimeoutFromOverride.Should().BeFalse();
        result.Source.RetryPolicyFromOverride.Should().BeFalse();
    }

    #endregion

    #region Fallback Tests

    [Fact]
    public async Task GetEffectivePolicyAsync_Should_ReturnFallback_WhenPolicyNotFound()
    {
        // Arrange
        const string processType = "order";
        const string clientId = "client-123";

        _cacheStoreMock
            .Setup(x => x.GetAsync<ProcessTypePolicy>(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((ProcessTypePolicy?)null);

        _cacheStoreMock
            .Setup(x => x.GetAsync<ClientPolicyOverride>(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((ClientPolicyOverride?)null);

        _repositoryMock
            .Setup(x => x.GetProcessTypePolicyAsync(processType, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new KeyNotFoundException());

        _repositoryMock
            .Setup(x => x.GetClientOverrideAsync(clientId, processType, It.IsAny<CancellationToken>()))
            .ReturnsAsync((ClientPolicyOverride?)null);

        _resolutionServiceMock
            .Setup(x => x.ValidatePolicy(It.IsAny<ProcessTypePolicy>()))
            .Returns(new PolicyValidationResult { IsValid = true, Errors = new List<string>() });

        // Act
        var result = await _sut.GetEffectivePolicyAsync(clientId, processType);

        // Assert
        result.Should().NotBeNull();
        result.ProcessType.Should().Be(processType);
        result.Timeout.Should().Be(TimeSpan.FromSeconds(_options.DefaultTimeoutSeconds));
        result.RetryPolicy.MaxAttempts.Should().Be(_options.DefaultMaxRetryAttempts);
    }

    #endregion

    #region Cache Invalidation Tests

    [Fact]
    public async Task RefreshPoliciesAsync_Should_ClearMemoryCache()
    {
        // Arrange
        const string processType = "order";
        const string clientId = "client-123";
        var policy = CreateDefaultPolicy(processType);

        _cacheStoreMock
            .Setup(x => x.GetAsync<ProcessTypePolicy>(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((ProcessTypePolicy?)null);

        _cacheStoreMock
            .Setup(x => x.GetAsync<ClientPolicyOverride>(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((ClientPolicyOverride?)null);

        _repositoryMock
            .Setup(x => x.GetProcessTypePolicyAsync(processType, It.IsAny<CancellationToken>()))
            .ReturnsAsync(policy);

        _repositoryMock
            .Setup(x => x.GetClientOverrideAsync(clientId, processType, It.IsAny<CancellationToken>()))
            .ReturnsAsync((ClientPolicyOverride?)null);

        _resolutionServiceMock
            .Setup(x => x.ValidatePolicy(It.IsAny<ProcessTypePolicy>()))
            .Returns(new PolicyValidationResult { IsValid = true, Errors = new List<string>() });

        // Populate cache
        await _sut.GetEffectivePolicyAsync(clientId, processType);

        // Act
        var clearedCount = await _sut.RefreshPoliciesAsync();

        // Assert
        clearedCount.Should().BeGreaterThan(0);
        
        // Verify cache was cleared by checking repository is called again
        await _sut.GetEffectivePolicyAsync(clientId, processType);
        _repositoryMock.Verify(
            x => x.GetProcessTypePolicyAsync(processType, It.IsAny<CancellationToken>()),
            Times.Exactly(2));
    }

    [Fact]
    public async Task InvalidatePolicyAsync_Should_RemoveFromMemoryCache()
    {
        // Arrange
        const string processType = "order";
        const string clientId = "client-123";
        var policy = CreateDefaultPolicy(processType);

        _cacheStoreMock
            .Setup(x => x.GetAsync<ProcessTypePolicy>(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((ProcessTypePolicy?)null);

        _cacheStoreMock
            .Setup(x => x.GetAsync<ClientPolicyOverride>(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((ClientPolicyOverride?)null);

        _repositoryMock
            .Setup(x => x.GetProcessTypePolicyAsync(processType, It.IsAny<CancellationToken>()))
            .ReturnsAsync(policy);

        _repositoryMock
            .Setup(x => x.GetClientOverrideAsync(clientId, processType, It.IsAny<CancellationToken>()))
            .ReturnsAsync((ClientPolicyOverride?)null);

        _resolutionServiceMock
            .Setup(x => x.ValidatePolicy(It.IsAny<ProcessTypePolicy>()))
            .Returns(new PolicyValidationResult { IsValid = true, Errors = new List<string>() });

        // Populate cache
        await _sut.GetEffectivePolicyAsync(clientId, processType);

        // Act
        await _sut.InvalidatePolicyAsync(processType);

        // Assert
        _cacheStatistics.Evictions.Should().BeGreaterThan(0);
    }

    #endregion

    #region Validation Tests

    [Fact]
    public async Task GetEffectivePolicyAsync_Should_FallbackToTypeDefault_WhenOverrideValidationFails()
    {
        // Arrange
        const string processType = "order";
        const string clientId = "client-123";
        var typeDefault = CreateDefaultPolicy(processType);
        var invalidOverride = new ClientPolicyOverride
        {
            ClientId = clientId,
            ProcessType = processType,
            Timeout = TimeSpan.FromSeconds(-100), // Invalid
            UpdatedAt = DateTime.UtcNow
        };

        _cacheStoreMock
            .Setup(x => x.GetAsync<ProcessTypePolicy>(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((ProcessTypePolicy?)null);

        _cacheStoreMock
            .Setup(x => x.GetAsync<ClientPolicyOverride>(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((ClientPolicyOverride?)null);

        _repositoryMock
            .Setup(x => x.GetProcessTypePolicyAsync(processType, It.IsAny<CancellationToken>()))
            .ReturnsAsync(typeDefault);

        _repositoryMock
            .Setup(x => x.GetClientOverrideAsync(clientId, processType, It.IsAny<CancellationToken>()))
            .ReturnsAsync(invalidOverride);

        _resolutionServiceMock
            .Setup(x => x.ValidateClientOverride(invalidOverride))
            .Returns(new PolicyValidationResult
            {
                IsValid = false,
                Errors = new List<string> { "Timeout must be positive" }
            });

        _resolutionServiceMock
            .Setup(x => x.ValidatePolicy(typeDefault))
            .Returns(new PolicyValidationResult { IsValid = true, Errors = new List<string>() });

        // Act
        var result = await _sut.GetEffectivePolicyAsync(clientId, processType);

        // Assert
        result.Timeout.Should().Be(typeDefault.Timeout); // Should use type default
        _resolutionServiceMock.Verify(
            x => x.ResolvePolicy(It.IsAny<ProcessTypePolicy>(), It.IsAny<ClientPolicyOverride>()),
            Times.Never);
    }

    #endregion

    #region Edge Cases and Error Handling

    [Fact]
    public async Task GetEffectivePolicyAsync_Should_ThrowArgumentException_WhenClientIdEmpty()
    {
        // Act
        Func<Task> act = async () => await _sut.GetEffectivePolicyAsync("", "order");

        // Assert
        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task GetEffectivePolicyAsync_Should_ThrowArgumentException_WhenProcessTypeEmpty()
    {
        // Act
        Func<Task> act = async () => await _sut.GetEffectivePolicyAsync("client-123", "");

        // Assert
        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task GetTimeoutAsync_Should_ReturnTimeout_FromEffectivePolicy()
    {
        // Arrange
        const string processType = "order";
        const string clientId = "client-123";
        var policy = CreateDefaultPolicy(processType);

        _cacheStoreMock
            .Setup(x => x.GetAsync<ProcessTypePolicy>(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((ProcessTypePolicy?)null);

        _cacheStoreMock
            .Setup(x => x.GetAsync<ClientPolicyOverride>(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((ClientPolicyOverride?)null);

        _repositoryMock
            .Setup(x => x.GetProcessTypePolicyAsync(processType, It.IsAny<CancellationToken>()))
            .ReturnsAsync(policy);

        _repositoryMock
            .Setup(x => x.GetClientOverrideAsync(clientId, processType, It.IsAny<CancellationToken>()))
            .ReturnsAsync((ClientPolicyOverride?)null);

        _resolutionServiceMock
            .Setup(x => x.ValidatePolicy(It.IsAny<ProcessTypePolicy>()))
            .Returns(new PolicyValidationResult { IsValid = true, Errors = new List<string>() });

        // Act
        var result = await _sut.GetTimeoutAsync(clientId, processType);

        // Assert
        result.Should().Be(policy.Timeout);
    }

    [Fact]
    public async Task GetRetryPolicyAsync_Should_ReturnRetryPolicy_FromEffectivePolicy()
    {
        // Arrange
        const string processType = "order";
        const string clientId = "client-123";
        var policy = CreateDefaultPolicy(processType);

        _cacheStoreMock
            .Setup(x => x.GetAsync<ProcessTypePolicy>(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((ProcessTypePolicy?)null);

        _cacheStoreMock
            .Setup(x => x.GetAsync<ClientPolicyOverride>(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((ClientPolicyOverride?)null);

        _repositoryMock
            .Setup(x => x.GetProcessTypePolicyAsync(processType, It.IsAny<CancellationToken>()))
            .ReturnsAsync(policy);

        _repositoryMock
            .Setup(x => x.GetClientOverrideAsync(clientId, processType, It.IsAny<CancellationToken>()))
            .ReturnsAsync((ClientPolicyOverride?)null);

        _resolutionServiceMock
            .Setup(x => x.ValidatePolicy(It.IsAny<ProcessTypePolicy>()))
            .Returns(new PolicyValidationResult { IsValid = true, Errors = new List<string>() });

        // Act
        var result = await _sut.GetRetryPolicyAsync(clientId, processType);

        // Assert
        result.Should().BeEquivalentTo(policy.RetryPolicy);
    }

    [Fact]
    public void GetCacheStatistics_Should_ReturnStatistics()
    {
        // Act
        var stats = _sut.GetCacheStatistics();

        // Assert
        stats.Should().NotBeNull();
        stats.Should().BeSameAs(_cacheStatistics);
    }

    #endregion

    #region Helper Methods

    private static ProcessTypePolicy CreateDefaultPolicy(string processType) => new()
    {
        ProcessType = processType,
        Timeout = TimeSpan.FromMinutes(5),
        RetryPolicy = new RetryPolicy
        {
            Enabled = true,
            MaxAttempts = 3,
            InitialDelay = TimeSpan.FromSeconds(5),
            BackoffStrategy = BackoffStrategy.Exponential,
            MaxDelay = TimeSpan.FromMinutes(5)
        },
        ResultRetention = TimeSpan.FromDays(30),
        MaxConcurrentProcesses = 10,
        UpdatedAt = DateTime.UtcNow
    };

    #endregion

    public async ValueTask DisposeAsync()
    {
        await Task.CompletedTask;
    }
}
