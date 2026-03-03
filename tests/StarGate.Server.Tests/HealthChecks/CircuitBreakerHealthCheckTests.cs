using FluentAssertions;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Polly.CircuitBreaker;
using StarGate.Infrastructure.Resilience;
using StarGate.Server.HealthChecks;
using Xunit;

namespace StarGate.Server.Tests.HealthChecks;

/// <summary>
/// Unit tests for CircuitBreakerHealthCheck.
/// </summary>
public class CircuitBreakerHealthCheckTests
{
    [Fact]
    public async Task CheckHealthAsync_Should_ReturnHealthy_When_NoCircuits()
    {
        // Arrange
        var stateService = new CircuitBreakerStateService();
        var healthCheck = new CircuitBreakerHealthCheck(stateService);
        var context = new HealthCheckContext();

        // Act
        var result = await healthCheck.CheckHealthAsync(context);

        // Assert
        result.Status.Should().Be(HealthStatus.Healthy);
        result.Description.Should().Be("No circuit breakers configured");
    }

    [Fact]
    public async Task CheckHealthAsync_Should_ReturnHealthy_When_AllCircuitsClosed()
    {
        // Arrange
        var stateService = new CircuitBreakerStateService();
        stateService.RecordStateChange("database", CircuitState.Closed);
        stateService.RecordStateChange("broker", CircuitState.Closed);
        
        var healthCheck = new CircuitBreakerHealthCheck(stateService);
        var context = new HealthCheckContext();

        // Act
        var result = await healthCheck.CheckHealthAsync(context);

        // Assert
        result.Status.Should().Be(HealthStatus.Healthy);
        result.Description.Should().Be("All circuit breakers closed");
        result.Data.Should().ContainKey("database");
        result.Data.Should().ContainKey("broker");
        result.Data["database"].Should().Be("Closed");
        result.Data["broker"].Should().Be("Closed");
    }

    [Fact]
    public async Task CheckHealthAsync_Should_ReturnDegraded_When_CircuitsHalfOpen()
    {
        // Arrange
        var stateService = new CircuitBreakerStateService();
        stateService.RecordStateChange("database", CircuitState.Closed);
        stateService.RecordStateChange("broker", CircuitState.HalfOpen);
        
        var healthCheck = new CircuitBreakerHealthCheck(stateService);
        var context = new HealthCheckContext();

        // Act
        var result = await healthCheck.CheckHealthAsync(context);

        // Assert
        result.Status.Should().Be(HealthStatus.Degraded);
        result.Description.Should().Contain("Circuit breakers half-open");
        result.Description.Should().Contain("broker");
        result.Data["broker"].Should().Be("HalfOpen");
    }

    [Fact]
    public async Task CheckHealthAsync_Should_ReturnUnhealthy_When_CircuitsOpen()
    {
        // Arrange
        var stateService = new CircuitBreakerStateService();
        stateService.RecordStateChange("database", CircuitState.Open);
        stateService.RecordStateChange("broker", CircuitState.Closed);
        
        var healthCheck = new CircuitBreakerHealthCheck(stateService);
        var context = new HealthCheckContext();

        // Act
        var result = await healthCheck.CheckHealthAsync(context);

        // Assert
        result.Status.Should().Be(HealthStatus.Unhealthy);
        result.Description.Should().Contain("Circuit breakers open");
        result.Description.Should().Contain("database");
        result.Data["database"].Should().Be("Open");
    }

    [Fact]
    public async Task CheckHealthAsync_Should_ReturnUnhealthy_When_MultipleCircuitsOpen()
    {
        // Arrange
        var stateService = new CircuitBreakerStateService();
        stateService.RecordStateChange("database", CircuitState.Open);
        stateService.RecordStateChange("broker", CircuitState.Open);
        stateService.RecordStateChange("http", CircuitState.Closed);
        
        var healthCheck = new CircuitBreakerHealthCheck(stateService);
        var context = new HealthCheckContext();

        // Act
        var result = await healthCheck.CheckHealthAsync(context);

        // Assert
        result.Status.Should().Be(HealthStatus.Unhealthy);
        result.Description.Should().Contain("database");
        result.Description.Should().Contain("broker");
    }

    [Fact]
    public async Task CheckHealthAsync_Should_PrioritizeUnhealthy_Over_Degraded()
    {
        // Arrange
        var stateService = new CircuitBreakerStateService();
        stateService.RecordStateChange("database", CircuitState.Open);
        stateService.RecordStateChange("broker", CircuitState.HalfOpen);
        
        var healthCheck = new CircuitBreakerHealthCheck(stateService);
        var context = new HealthCheckContext();

        // Act
        var result = await healthCheck.CheckHealthAsync(context);

        // Assert
        result.Status.Should().Be(HealthStatus.Unhealthy);
        result.Description.Should().Contain("Circuit breakers open");
    }

    [Fact]
    public void Constructor_Should_ThrowArgumentNullException_When_StateServiceIsNull()
    {
        // Act
        Action act = () => new CircuitBreakerHealthCheck(null!);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("stateService");
    }
}
