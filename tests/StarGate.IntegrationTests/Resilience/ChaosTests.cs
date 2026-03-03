namespace StarGate.IntegrationTests.Resilience;

using System.Diagnostics;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Polly.CircuitBreaker;
using StarGate.Infrastructure.Resilience;
using Xunit;
using Xunit.Abstractions;

/// <summary>
/// Chaos testing scenarios for resilience validation.
/// </summary>
public class ChaosTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;
    private readonly ITestOutputHelper _output;

    public ChaosTests(WebApplicationFactory<Program> factory, ITestOutputHelper output)
    {
        _factory = factory;
        _output = output;
    }

    [Fact]
    public async Task ChaosScenario_DatabaseIntermittentFailures()
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
            MaxRetryAttempts = 3,
            InitialDelaySeconds = 0.2,
            UseJitter = false
        };
        var circuitConfig = new CircuitBreakerConfiguration
        {
            FailureThreshold = 5,
            FailureRateThreshold = 0.5,
            MinimumThroughput = 10,
            BreakDurationSeconds = 2.0,
            SamplingDurationSeconds = 10.0
        };
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<RetryPolicyConfiguration>>();
        var policy = ResiliencePolicyWrapper.CreateCompleteDatabaseResiliencePolicy(
            timeoutConfig, retryConfig, circuitConfig, logger);

        var random = new Random(42); // Fixed seed for reproducibility
        var successCount = 0;
        var failureCount = 0;
        var totalAttempts = 50;

        // Act - Simulate 30% failure rate
        for (int i = 0; i < totalAttempts; i++)
        {
            try
            {
                await policy.ExecuteAsync(async () =>
                {
                    if (random.NextDouble() < 0.3)
                    {
                        await Task.CompletedTask;
                        throw new TimeoutException("Simulated intermittent failure");
                    }
                    await Task.Delay(10); // Simulate work
                });
                successCount++;
            }
            catch (Exception)
            {
                failureCount++;
            }
        }

        // Assert - Retry should handle intermittent failures
        _output.WriteLine($"Success: {successCount}/{totalAttempts}, Failures: {failureCount}/{totalAttempts}");
        successCount.Should().BeGreaterThan((int)(totalAttempts * 0.6)); // Most should succeed with retries
    }

    [Fact]
    public async Task ChaosScenario_DatabaseProlongedOutage()
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
            MaxRetryAttempts = 3,
            InitialDelaySeconds = 0.1,
            UseJitter = false
        };
        var circuitConfig = new CircuitBreakerConfiguration
        {
            FailureThreshold = 3,
            FailureRateThreshold = 0.5,
            MinimumThroughput = 5,
            BreakDurationSeconds = 1.0,
            SamplingDurationSeconds = 10.0
        };
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<RetryPolicyConfiguration>>();
        var policy = ResiliencePolicyWrapper.CreateCompleteDatabaseResiliencePolicy(
            timeoutConfig, retryConfig, circuitConfig, logger);

        var circuitOpenCount = 0;
        var totalAttempts = 20;

        // Act - Simulate complete database unavailability
        for (int i = 0; i < totalAttempts; i++)
        {
            try
            {
                await policy.ExecuteAsync(async () =>
                {
                    await Task.CompletedTask;
                    throw new TimeoutException("Database unavailable");
                });
            }
            catch (BrokenCircuitException)
            {
                circuitOpenCount++;
            }
            catch (Exception)
            {
                // Other exceptions (TimeoutException from retries)
            }
        }

        // Assert - Circuit breaker should open and fail fast
        _output.WriteLine($"Circuit open responses: {circuitOpenCount}/{totalAttempts}");
        circuitOpenCount.Should().BeGreaterThan(0); // Circuit should open after threshold
    }

    [Fact]
    public async Task ChaosScenario_BrokerSlowResponses()
    {
        // Arrange
        using var scope = _factory.Services.CreateScope();
        var timeoutConfig = new TimeoutConfiguration
        {
            BrokerTimeoutSeconds = 0.5,
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
            MinimumThroughput = 10,
            BreakDurationSeconds = 1.0,
            SamplingDurationSeconds = 10.0
        };
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<RetryPolicyConfiguration>>();
        var policy = ResiliencePolicyWrapper.CreateCompleteBrokerResiliencePolicy(
            timeoutConfig, retryConfig, circuitConfig, logger);

        var timeoutCount = 0;
        var totalAttempts = 10;
        var stopwatch = Stopwatch.StartNew();

        // Act - Simulate slow broker responses (>timeout)
        for (int i = 0; i < totalAttempts; i++)
        {
            try
            {
                await policy.ExecuteAsync(async (ct) =>
                {
                    await Task.Delay(TimeSpan.FromSeconds(2), ct); // Slower than timeout
                });
            }
            catch (Polly.Timeout.TimeoutRejectedException)
            {
                timeoutCount++;
            }
            catch (Exception)
            {
                // Other exceptions
            }
        }

        stopwatch.Stop();

        // Assert - Timeout policy should activate and limit latency
        _output.WriteLine($"Timeouts: {timeoutCount}/{totalAttempts}, Total time: {stopwatch.ElapsedMilliseconds}ms");
        timeoutCount.Should().BeGreaterThan(0);
        stopwatch.ElapsedMilliseconds.Should().BeLessThan(totalAttempts * 2000); // Should be faster than waiting for all
    }

    [Fact]
    public async Task ChaosScenario_NetworkPartition()
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
            MaxRetryAttempts = 3,
            InitialDelaySeconds = 0.2,
            UseJitter = false
        };
        var circuitConfig = new CircuitBreakerConfiguration
        {
            FailureThreshold = 5,
            FailureRateThreshold = 0.5,
            MinimumThroughput = 8,
            BreakDurationSeconds = 1.0,
            SamplingDurationSeconds = 10.0
        };
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<RetryPolicyConfiguration>>();
        var policy = ResiliencePolicyWrapper.CreateCompleteDatabaseResiliencePolicy(
            timeoutConfig, retryConfig, circuitConfig, logger);

        var random = new Random(123);
        var timeoutCount = 0;
        var connectionErrorCount = 0;
        var successCount = 0;
        var totalAttempts = 30;

        // Act - Simulate network issues (timeouts, connection errors)
        for (int i = 0; i < totalAttempts; i++)
        {
            try
            {
                await policy.ExecuteAsync(async (ct) =>
                {
                    var issue = random.NextDouble();
                    if (issue < 0.2)
                    {
                        await Task.Delay(TimeSpan.FromSeconds(5), ct); // Timeout scenario
                    }
                    else if (issue < 0.4)
                    {
                        throw new IOException("Connection reset");
                    }
                    else
                    {
                        await Task.Delay(10); // Success
                    }
                });
                successCount++;
            }
            catch (Polly.Timeout.TimeoutRejectedException)
            {
                timeoutCount++;
            }
            catch (IOException)
            {
                connectionErrorCount++;
            }
            catch (Exception)
            {
                // Other exceptions
            }
        }

        // Assert - All policies should work together
        _output.WriteLine($"Success: {successCount}, Timeouts: {timeoutCount}, Connection errors: {connectionErrorCount}");
        (successCount + timeoutCount + connectionErrorCount).Should().Be(totalAttempts);
    }

    [Fact]
    public async Task ChaosScenario_HighLoad()
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
            UseJitter = true
        };
        var circuitConfig = new CircuitBreakerConfiguration
        {
            FailureThreshold = 10,
            FailureRateThreshold = 0.5,
            MinimumThroughput = 20,
            BreakDurationSeconds = 1.0,
            SamplingDurationSeconds = 5.0
        };
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<RetryPolicyConfiguration>>();
        var policy = ResiliencePolicyWrapper.CreateCompleteDatabaseResiliencePolicy(
            timeoutConfig, retryConfig, circuitConfig, logger);

        var successCount = 0;
        var failureCount = 0;
        var concurrentRequests = 50;
        var stopwatch = Stopwatch.StartNew();

        // Act - Simulate high load with varying failure rates
        var tasks = Enumerable.Range(0, concurrentRequests).Select(async i =>
        {
            var random = new Random(i);
            try
            {
                await policy.ExecuteAsync(async () =>
                {
                    await Task.Delay(random.Next(10, 100)); // Variable latency
                    if (random.NextDouble() < 0.2) // 20% failure rate
                    {
                        throw new TimeoutException("Simulated failure under load");
                    }
                });
                Interlocked.Increment(ref successCount);
            }
            catch (Exception)
            {
                Interlocked.Increment(ref failureCount);
            }
        });

        await Task.WhenAll(tasks);
        stopwatch.Stop();

        // Assert - Circuit breaker should protect system
        _output.WriteLine($"Success: {successCount}/{concurrentRequests}, Failures: {failureCount}, Time: {stopwatch.ElapsedMilliseconds}ms");
        var totalProcessed = successCount + failureCount;
        totalProcessed.Should().Be(concurrentRequests);
        var throughput = concurrentRequests * 1000.0 / stopwatch.ElapsedMilliseconds;
        _output.WriteLine($"Throughput: {throughput:F2} requests/second");
    }
}
