using FluentAssertions;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using StarGate.Api.HealthChecks;
using StarGate.Core.Abstractions;
using StarGate.Core.Domain.Configuration;
using Xunit;

namespace StarGate.Api.Tests.HealthChecks;

public class PolicyProviderHealthCheckTests
{
    private readonly Mock<IPolicyProvider> _policyProviderMock;
    private readonly PolicyProviderHealthCheck _healthCheck;

    public PolicyProviderHealthCheckTests()
    {
        _policyProviderMock = new Mock<IPolicyProvider>();
        _healthCheck = new PolicyProviderHealthCheck(
            _policyProviderMock.Object,
            NullLogger<PolicyProviderHealthCheck>.Instance);
    }

    [Fact]
    public async Task CheckHealthAsync_Should_ReturnHealthy_WhenPolicyProviderIsAccessible()
    {
        // Arrange
        EffectivePolicy? nullPolicy = null;
        _policyProviderMock
            .Setup(p => p.GetPolicyAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(nullPolicy);

        var context = new HealthCheckContext();

        // Act
        var result = await _healthCheck.CheckHealthAsync(context);

        // Assert
        result.Status.Should().Be(HealthStatus.Healthy);
        result.Description.Should().Be("PolicyProvider is operational");
    }

    [Fact]
    public async Task CheckHealthAsync_Should_ReturnHealthy_WhenPolicyProviderReturnsPolicy()
    {
        // Arrange
        var policy = new EffectivePolicy
        {
            ProcessType = "test",
            ClientId = "test-client",
            Timeout = TimeSpan.FromSeconds(300),
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
            Source = new PolicySource
            {
                TimeoutFromOverride = false,
                RetryPolicyFromOverride = false,
                ResultRetentionFromOverride = false,
                ConcurrencyLimitFromOverride = false
            }
        };

        _policyProviderMock
            .Setup(p => p.GetPolicyAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(policy);

        var context = new HealthCheckContext();

        // Act
        var result = await _healthCheck.CheckHealthAsync(context);

        // Assert
        result.Status.Should().Be(HealthStatus.Healthy);
        result.Description.Should().Be("PolicyProvider is operational");
    }

    [Fact]
    public async Task CheckHealthAsync_Should_ReturnUnhealthy_WhenPolicyProviderThrowsException()
    {
        // Arrange
        _policyProviderMock
            .Setup(p => p.GetPolicyAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Policy repository unavailable"));

        var context = new HealthCheckContext();

        // Act
        var result = await _healthCheck.CheckHealthAsync(context);

        // Assert
        result.Status.Should().Be(HealthStatus.Unhealthy);
        result.Description.Should().Be("PolicyProvider is not operational");
        result.Exception.Should().NotBeNull();
        result.Data.Should().ContainKey("error");
        result.Data.Should().ContainKey("type");
    }

    [Fact]
    public async Task CheckHealthAsync_Should_ReturnUnhealthy_WhenCancelled()
    {
        // Arrange
        var cts = new CancellationTokenSource();
        cts.Cancel();

        _policyProviderMock
            .Setup(p => p.GetPolicyAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new OperationCanceledException());

        var context = new HealthCheckContext();

        // Act
        var result = await _healthCheck.CheckHealthAsync(context, cts.Token);

        // Assert
        result.Status.Should().Be(HealthStatus.Unhealthy);
        result.Description.Should().Contain("cancelled");
    }

    [Fact]
    public void Constructor_Should_ThrowArgumentNullException_WhenPolicyProviderIsNull()
    {
        // Act
        var act = () => new PolicyProviderHealthCheck(
            null!,
            NullLogger<PolicyProviderHealthCheck>.Instance);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("policyProvider");
    }

    [Fact]
    public void Constructor_Should_ThrowArgumentNullException_WhenLoggerIsNull()
    {
        // Act
        var act = () => new PolicyProviderHealthCheck(
            _policyProviderMock.Object,
            null!);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("logger");
    }
}
