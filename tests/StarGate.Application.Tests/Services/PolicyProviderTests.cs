namespace StarGate.Application.Tests.Services;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using StarGate.Application.Services;
using StarGate.Core.Abstractions;
using StarGate.Core.Domain.Configuration;

/// <summary>
/// Unit tests for PolicyProvider service.
/// Covers caching, override resolution, and fallback scenarios.
/// </summary>
public class PolicyProviderTests
{
    private readonly Mock<IPolicyRepository> _repositoryMock;
    private readonly Mock<ICacheStore> _cacheStoreMock;
    private readonly Mock<ILogger<PolicyProvider>> _loggerMock;
    private readonly PolicyProviderOptions _options;
    private readonly PolicyProvider _sut;

    public PolicyProviderTests()
    {
        _repositoryMock = new Mock<IPolicyRepository>();
        _cacheStoreMock = new Mock<ICacheStore>();
        _loggerMock = new Mock<ILogger<PolicyProvider>>();
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
            _loggerMock.Object);
    }

    [Fact]
    public async Task GetTimeoutAsync_WithValidData_ReturnsTimeout()
    {
        // Arrange
        const string clientId = "client-123";
        const string processType = "order";
        var expectedPolicy = CreateDefaultPolicy(processType);

        _cacheStoreMock
            .Setup(x => x.GetAsync<ProcessTypePolicy>(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((ProcessTypePolicy?)null);

        _repositoryMock
            .Setup(x => x.GetByProcessTypeAsync(processType, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedPolicy);

        _repositoryMock
            .Setup(x => x.GetClientOverrideAsync(clientId, processType, It.IsAny<CancellationToken>()))
            .ReturnsAsync((ClientPolicyOverride?)null);

        // Act
        var result = await _sut.GetTimeoutAsync(clientId, processType);

        // Assert
        result.Should().Be(expectedPolicy.Timeout);
    }

    [Fact]
    public async Task GetEffectivePolicyAsync_WithNoOverride_ReturnsTypeDefault()
    {
        // Arrange
        const string clientId = "client-123";
        const string processType = "order";
        var expectedPolicy = CreateDefaultPolicy(processType);

        _cacheStoreMock
            .Setup(x => x.GetAsync<ProcessTypePolicy>(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((ProcessTypePolicy?)null);

        _repositoryMock
            .Setup(x => x.GetByProcessTypeAsync(processType, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedPolicy);

        _repositoryMock
            .Setup(x => x.GetClientOverrideAsync(clientId, processType, It.IsAny<CancellationToken>()))
            .ReturnsAsync((ClientPolicyOverride?)null);

        // Act
        var result = await _sut.GetEffectivePolicyAsync(clientId, processType);

        // Assert
        result.Should().NotBeNull();
        result.ProcessType.Should().Be(processType);
        result.ClientId.Should().Be(clientId);
        result.Timeout.Should().Be(expectedPolicy.Timeout);
        result.RetryPolicy.MaxAttempts.Should().Be(expectedPolicy.RetryPolicy.MaxAttempts);
        result.Source.TimeoutFromOverride.Should().BeFalse();
    }

    [Fact]
    public async Task GetEffectivePolicyAsync_WithClientOverride_AppliesOverride()
    {
        // Arrange
        const string clientId = "client-123";
        const string processType = "order";
        var typePolicy = CreateDefaultPolicy(processType);
        var customTimeout = TimeSpan.FromMinutes(10);
        var clientOverride = new ClientPolicyOverride
        {
            ClientId = clientId,
            ProcessType = processType,
            Timeout = customTimeout,
            UpdatedAt = DateTime.UtcNow
        };

        _cacheStoreMock
            .Setup(x => x.GetAsync<ProcessTypePolicy>(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((ProcessTypePolicy?)null);

        _cacheStoreMock
            .Setup(x => x.GetAsync<ClientPolicyOverride>(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((ClientPolicyOverride?)null);

        _repositoryMock
            .Setup(x => x.GetByProcessTypeAsync(processType, It.IsAny<CancellationToken>()))
            .ReturnsAsync(typePolicy);

        _repositoryMock
            .Setup(x => x.GetClientOverrideAsync(clientId, processType, It.IsAny<CancellationToken>()))
            .ReturnsAsync(clientOverride);

        // Act
        var result = await _sut.GetEffectivePolicyAsync(clientId, processType);

        // Assert
        result.Timeout.Should().Be(customTimeout);
        result.Source.TimeoutFromOverride.Should().BeTrue();
        result.Source.RetryPolicyFromOverride.Should().BeFalse();
    }

    [Fact]
    public async Task GetEffectivePolicyAsync_WhenTypePolicyNotFound_UsesFallback()
    {
        // Arrange
        const string clientId = "client-123";
        const string processType = "order";

        _cacheStoreMock
            .Setup(x => x.GetAsync<ProcessTypePolicy>(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((ProcessTypePolicy?)null);

        _repositoryMock
            .Setup(x => x.GetByProcessTypeAsync(processType, It.IsAny<CancellationToken>()))
            .ReturnsAsync((ProcessTypePolicy?)null);

        _repositoryMock
            .Setup(x => x.GetClientOverrideAsync(clientId, processType, It.IsAny<CancellationToken>()))
            .ReturnsAsync((ClientPolicyOverride?)null);

        // Act
        var result = await _sut.GetEffectivePolicyAsync(clientId, processType);

        // Assert
        result.Should().NotBeNull();
        result.Timeout.Should().Be(TimeSpan.FromSeconds(_options.DefaultTimeoutSeconds));
        result.RetryPolicy.MaxAttempts.Should().Be(_options.DefaultMaxRetryAttempts);
    }

    [Theory]
    [InlineData(null, "order")]
    [InlineData("", "order")]
    [InlineData(" ", "order")]
    [InlineData("client-123", null)]
    [InlineData("client-123", "")]
    [InlineData("client-123", " ")]
    public async Task GetTimeoutAsync_WithInvalidParameters_ThrowsArgumentException(
        string? clientId,
        string? processType)
    {
        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(
            () => _sut.GetTimeoutAsync(clientId!, processType!));
    }

    private static ProcessTypePolicy CreateDefaultPolicy(string processType)
    {
        return new ProcessTypePolicy
        {
            ProcessType = processType,
            Timeout = TimeSpan.FromMinutes(5),
            RetryPolicy = new RetryPolicy
            {
                MaxAttempts = 3,
                InitialDelay = TimeSpan.FromSeconds(5),
                BackoffStrategy = BackoffStrategy.Exponential
            },
            ResultRetention = TimeSpan.FromDays(30),
            MaxConcurrentProcesses = 10,
            UpdatedAt = DateTime.UtcNow
        };
    }
}
