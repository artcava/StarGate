using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Polly.CircuitBreaker;
using StarGate.Infrastructure.Resilience;
using Xunit;

namespace StarGate.Infrastructure.Tests.Resilience;

/// <summary>
/// Unit tests for circuit breaker functionality.
/// </summary>
public class CircuitBreakerTests
{
    private readonly CircuitBreakerConfiguration _config;
    private readonly NullLogger _logger;

    public CircuitBreakerTests()
    {
        _config = new CircuitBreakerConfiguration
        {
            FailureThreshold = 3,
            FailureRateThreshold = 0.5,
            MinimumThroughput = 5,
            BreakDurationSeconds = 1.0,
            SamplingDurationSeconds = 10.0
        };
        _logger = NullLogger.Instance;
    }

    [Fact]
    public async Task CircuitBreaker_Should_OpenAfterThresholdExceeded()
    {
        // Arrange
        var circuitBreaker = CircuitBreakerFactory.CreateDatabaseCircuitBreaker(_config, _logger);
        var failures = 0;

        // Act - Execute until circuit opens
        for (int i = 0; i < 10; i++)
        {
            try
            {
                await circuitBreaker.ExecuteAsync(async () =>
                {
                    failures++;
                    await Task.CompletedTask;
                    throw new TimeoutException("Simulated failure");
                });
            }
            catch (TimeoutException)
            {
                // Expected
            }
            catch (BrokenCircuitException)
            {
                // Circuit opened
                break;
            }
        }

        // Assert - Circuit should be open after threshold reached
        var act = async () => await circuitBreaker.ExecuteAsync(async () =>
        {
            await Task.CompletedTask;
        });

        await act.Should().ThrowAsync<BrokenCircuitException>();
        failures.Should().BeGreaterThanOrEqualTo(5); // MinimumThroughput
    }

    [Fact]
    public async Task CircuitBreaker_Should_ResetAfterBreakDuration()
    {
        // Arrange
        var config = new CircuitBreakerConfiguration
        {
            FailureThreshold = 2,
            FailureRateThreshold = 0.5,
            MinimumThroughput = 3,
            BreakDurationSeconds = 0.5,
            SamplingDurationSeconds = 10.0
        };
        var circuitBreaker = CircuitBreakerFactory.CreateDatabaseCircuitBreaker(config, _logger);

        // Act - Cause circuit to open
        for (int i = 0; i < 5; i++)
        {
            try
            {
                await circuitBreaker.ExecuteAsync(async () =>
                {
                    await Task.CompletedTask;
                    throw new TimeoutException();
                });
            }
            catch { }
        }

        // Verify circuit is open
        var actWhileOpen = async () => await circuitBreaker.ExecuteAsync(async () =>
        {
            await Task.CompletedTask;
        });
        await actWhileOpen.Should().ThrowAsync<BrokenCircuitException>();

        // Wait for break duration
        await Task.Delay(TimeSpan.FromSeconds(1));

        // Act - Execute successful operation (half-open -> closed)
        await circuitBreaker.ExecuteAsync(async () =>
        {
            await Task.CompletedTask;
        });

        // Assert - Circuit should be closed
        await circuitBreaker.ExecuteAsync(async () =>
        {
            await Task.CompletedTask;
        });
    }

    [Fact]
    public async Task CircuitBreaker_Should_FailFast_When_Open()
    {
        // Arrange
        var config = new CircuitBreakerConfiguration
        {
            FailureThreshold = 2,
            FailureRateThreshold = 0.5,
            MinimumThroughput = 3,
            BreakDurationSeconds = 10.0,
            SamplingDurationSeconds = 10.0
        };
        var circuitBreaker = CircuitBreakerFactory.CreateDatabaseCircuitBreaker(config, _logger);

        // Act - Open the circuit
        for (int i = 0; i < 5; i++)
        {
            try
            {
                await circuitBreaker.ExecuteAsync(async () =>
                {
                    await Task.CompletedTask;
                    throw new TimeoutException();
                });
            }
            catch { }
        }

        // Assert - Next call should fail fast (much faster than retry delays)
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        var act = async () => await circuitBreaker.ExecuteAsync(async () =>
        {
            await Task.CompletedTask;
        });
        
        await act.Should().ThrowAsync<BrokenCircuitException>();
        stopwatch.Stop();
        
        // Should fail fast (< 500ms) vs retry delays (1s, 2s, 4s = 7s total)
        // This validates fail-fast behavior while accounting for test overhead
        stopwatch.ElapsedMilliseconds.Should().BeLessThan(500);
    }

    [Fact]
    public void CircuitBreakerStateService_Should_TrackStates()
    {
        // Arrange
        var service = new CircuitBreakerStateService();

        // Act
        service.RecordStateChange("database", CircuitState.Closed);
        service.RecordStateChange("broker", CircuitState.Open);

        // Assert
        service.GetState("database").Should().Be(CircuitState.Closed);
        service.GetState("broker").Should().Be(CircuitState.Open);
        service.HasOpenCircuit().Should().BeTrue();
    }

    [Fact]
    public void CircuitBreakerStateService_Should_UpdateExistingState()
    {
        // Arrange
        var service = new CircuitBreakerStateService();
        service.RecordStateChange("database", CircuitState.Closed);

        // Act
        service.RecordStateChange("database", CircuitState.Open);

        // Assert
        service.GetState("database").Should().Be(CircuitState.Open);
    }

    [Fact]
    public void CircuitBreakerStateService_Should_ReturnAllStates()
    {
        // Arrange
        var service = new CircuitBreakerStateService();
        service.RecordStateChange("database", CircuitState.Closed);
        service.RecordStateChange("broker", CircuitState.HalfOpen);
        service.RecordStateChange("http", CircuitState.Open);

        // Act
        var allStates = service.GetAllStates();

        // Assert
        allStates.Should().HaveCount(3);
        allStates["database"].Should().Be(CircuitState.Closed);
        allStates["broker"].Should().Be(CircuitState.HalfOpen);
        allStates["http"].Should().Be(CircuitState.Open);
    }

    [Fact]
    public void CircuitBreakerStateService_Should_ReturnNull_For_UnknownCircuit()
    {
        // Arrange
        var service = new CircuitBreakerStateService();

        // Act
        var state = service.GetState("unknown");

        // Assert
        state.Should().BeNull();
    }

    [Fact]
    public void CircuitBreakerStateService_Should_ReturnFalse_When_NoOpenCircuits()
    {
        // Arrange
        var service = new CircuitBreakerStateService();
        service.RecordStateChange("database", CircuitState.Closed);
        service.RecordStateChange("broker", CircuitState.Closed);

        // Act
        var hasOpen = service.HasOpenCircuit();

        // Assert
        hasOpen.Should().BeFalse();
    }
}
