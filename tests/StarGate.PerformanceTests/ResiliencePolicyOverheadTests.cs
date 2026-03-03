namespace StarGate.PerformanceTests;

using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Running;
using Microsoft.Extensions.Logging.Abstractions;
using StarGate.Infrastructure.Resilience;

/// <summary>
/// Performance tests to measure overhead of resilience policies.
/// Run with: dotnet run -c Release
/// </summary>
[MemoryDiagnoser]
[SimpleJob(warmupCount: 3, targetCount: 10)]
public class ResiliencePolicyOverheadTests
{
    private readonly TimeoutConfiguration _timeoutConfig;
    private readonly RetryPolicyConfiguration _retryConfig;
    private readonly CircuitBreakerConfiguration _circuitConfig;
    private readonly NullLogger _logger;

    public ResiliencePolicyOverheadTests()
    {
        _timeoutConfig = new TimeoutConfiguration
        {
            DatabaseTimeoutSeconds = 10.0,
            UsePessimisticTimeout = true
        };

        _retryConfig = new RetryPolicyConfiguration
        {
            MaxRetryAttempts = 3,
            InitialDelaySeconds = 1.0,
            UseJitter = false
        };

        _circuitConfig = new CircuitBreakerConfiguration
        {
            FailureThreshold = 5,
            FailureRateThreshold = 0.5,
            MinimumThroughput = 10,
            BreakDurationSeconds = 30.0,
            SamplingDurationSeconds = 60.0
        };

        _logger = NullLogger.Instance;
    }

    [Benchmark(Baseline = true)]
    public async Task Operation_WithoutPolicies()
    {
        // Measure baseline performance
        await Task.Delay(10);
    }

    [Benchmark]
    public async Task Operation_WithRetryPolicy()
    {
        // Measure overhead with retry policy
        var policy = RetryPolicyFactory.CreateDatabaseRetryPolicy(_retryConfig, _logger);
        await policy.ExecuteAsync(async () =>
        {
            await Task.Delay(10);
        });
    }

    [Benchmark]
    public async Task Operation_WithCircuitBreaker()
    {
        // Measure overhead with circuit breaker
        var policy = CircuitBreakerFactory.CreateDatabaseCircuitBreaker(_circuitConfig, _logger);
        await policy.ExecuteAsync(async () =>
        {
            await Task.Delay(10);
        });
    }

    [Benchmark]
    public async Task Operation_WithTimeout()
    {
        // Measure overhead with timeout
        var policy = TimeoutPolicyFactory.CreateDatabaseTimeoutPolicy(_timeoutConfig, _logger);
        await policy.ExecuteAsync(async (ct) =>
        {
            await Task.Delay(10, ct);
        });
    }

    [Benchmark]
    public async Task Operation_WithRetryAndCircuitBreaker()
    {
        // Measure overhead with retry + circuit breaker
        var policy = ResiliencePolicyWrapper.CreateDatabaseResiliencePolicy(
            _retryConfig, _circuitConfig, _logger);
        await policy.ExecuteAsync(async () =>
        {
            await Task.Delay(10);
        });
    }

    [Benchmark]
    public async Task Operation_WithAllPolicies()
    {
        // Measure overhead with complete policy stack (timeout + circuit breaker + retry)
        var policy = ResiliencePolicyWrapper.CreateCompleteDatabaseResiliencePolicy(
            _timeoutConfig, _retryConfig, _circuitConfig, _logger);
        await policy.ExecuteAsync(async () =>
        {
            await Task.Delay(10);
        });
    }
}

/// <summary>
/// Program entry point for BenchmarkDotNet.
/// </summary>
public class Program
{
    public static void Main(string[] args)
    {
        var summary = BenchmarkRunner.Run<ResiliencePolicyOverheadTests>();
    }
}
