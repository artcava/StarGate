namespace StarGate.IntegrationTests.Resilience;

using System.Diagnostics;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Polly.CircuitBreaker;
using Polly.Timeout;
using StarGate.Infrastructure.Resilience;
using Xunit;

/// <summary>
/// Integration tests for resilience policies.
/// </summary>
public class ResilienceIntegrationTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public ResilienceIntegrationTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Should_RetryOnTransientFailures()
    {
        // Arrange
        using var scope = _factory.Services.CreateScope();
        var retryConfig = new RetryPolicyConfiguration
        {
            MaxRetryAttempts = 3,
            InitialDelaySeconds = 0.1,
            UseJitter = false
        };
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<RetryPolicyConfiguration>>();
        var policy = RetryPolicyFactory.CreateDatabaseRetryPolicy(retryConfig, logger);

        var attemptCount = 0;
        var maxAttempts = 2;

        // Act
        await policy.ExecuteAsync(async () =>
        {
            attemptCount++;
            if (attemptCount < maxAttempts)
            {
                await Task.CompletedTask;
                throw new TimeoutException("Simulated transient failure");
            }
            await Task.CompletedTask;
        });

        // Assert
        attemptCount.Should().Be(maxAttempts);
    }

    [Fact]
    public async Task Should_OpenCircuitAfterThreshold()
    {
        // Arrange
        using var scope = _factory.Services.CreateScope();
        var circuitConfig = new CircuitBreakerConfiguration
        {
            FailureThreshold = 3,
            FailureRateThreshold = 0.5,
            MinimumThroughput = 5,
            BreakDurationSeconds = 1.0,
            SamplingDurationSeconds = 10.0
        };
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<CircuitBreakerConfiguration>>();
        var circuitBreaker = CircuitBreakerFactory.CreateDatabaseCircuitBreaker(circuitConfig, logger);

        // Act - Cause failures to open circuit
        for (int i = 0; i < 10; i++)
        {
            try
            {
                await circuitBreaker.ExecuteAsync(async () =>
                {
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

        // Assert - Circuit should be open
        var act = async () => await circuitBreaker.ExecuteAsync(async () =>
        {
            await Task.CompletedTask;
        });

        await act.Should().ThrowAsync();
    }

    [Fact]
    public async Task Should_TimeoutSlowOperations()
    {
        // Arrange
        using var scope = _factory.Services.CreateScope();
        var timeoutConfig = new TimeoutConfiguration
        {
            DatabaseTimeoutSeconds = 0.5,
            UsePessimisticTimeout = true
        };
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<TimeoutConfiguration>>();
        var policy = TimeoutPolicyFactory.CreateDatabaseTimeoutPolicy(timeoutConfig, logger);

        // Act
        var stopwatch = Stopwatch.StartNew();
        var act = async () => await policy.ExecuteAsync(async (ct) =>
        {
            await Task.Delay(TimeSpan.FromSeconds(2), ct);
        });

        // Assert
        await act.Should().ThrowAsync();
        stopwatch.Stop();
        stopwatch.ElapsedMilliseconds.Should().BeLessThan(1000); // Should timeout before 1 second
    }

    [Fact]
    public async Task Should_CombineAllPoliciesCorrectly()
    {
        // Arrange
        using var scope = _factory.Services.CreateScope();
        var timeoutConfig = new TimeoutConfiguration
        {
            DatabaseTimeoutSeconds = 2.0,
            UsePessimisticTimeout = true
        };
        var retryConfig = new RetryPolicyConfiguration
        {
            MaxRetryAttempts = 2,
            InitialDelaySeconds = 0.1,
            UseJitter = false
        };
        var circuitConfig = new CircuitBreakerConfiguration
        {
            FailureThreshold = 5,
            FailureRateThreshold = 0.5,
            MinimumThroughput = 3,
            BreakDurationSeconds = 1.0,
            SamplingDurationSeconds = 10.0
        };
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<RetryPolicyConfiguration>>();
        var completePolicy = ResiliencePolicyWrapper.CreateCompleteDatabaseResiliencePolicy(
            timeoutConfig, retryConfig, circuitConfig, logger);

        var attemptCount = 0;

        // Act - Transient failures should be retried
        await completePolicy.ExecuteAsync(async () =>
        {
            attemptCount++;
            if (attemptCount < 2)
            {
                await Task.CompletedTask;
                throw new TimeoutException("Transient failure");
            }
            await Task.CompletedTask;
        });

        // Assert
        attemptCount.Should().Be(2); // 1 initial attempt + 1 retry
    }

    [Fact]
    public async Task Should_TimeoutEntireOperationIncludingRetries()
    {
        // Arrange
        using var scope = _factory.Services.CreateScope();
        var timeoutConfig = new TimeoutConfiguration
        {
            DatabaseTimeoutSeconds = 1.0,
            UsePessimisticTimeout = true
        };
        var retryConfig = new RetryPolicyConfiguration
        {
            MaxRetryAttempts = 5,
            InitialDelaySeconds = 0.3,
            UseJitter = false
        };
        var circuitConfig = new CircuitBreakerConfiguration
        {
            FailureThreshold = 10,
            FailureRateThreshold = 0.9,
            MinimumThroughput = 20,
            BreakDurationSeconds = 10.0,
            SamplingDurationSeconds = 60.0
        };
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<RetryPolicyConfiguration>>();
        var completePolicy = ResiliencePolicyWrapper.CreateCompleteDatabaseResiliencePolicy(
            timeoutConfig, retryConfig, circuitConfig, logger);

        // Act - Operation that would retry multiple times but timeout should prevent it
        var stopwatch = Stopwatch.StartNew();
        var act = async () => await completePolicy.ExecuteAsync(async () =>
        {
            await Task.Delay(TimeSpan.FromSeconds(0.5));
            throw new TimeoutException("Always failing");
        });

        // Assert
        await act.Should().ThrowAsync();
        stopwatch.Stop();
        stopwatch.ElapsedMilliseconds.Should().BeLessThan(1500); // Should timeout around 1 second
    }
}
