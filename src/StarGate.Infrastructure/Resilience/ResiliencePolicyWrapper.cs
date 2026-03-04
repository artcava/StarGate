using Microsoft.Extensions.Logging;
using Polly;
using Polly.Wrap;

namespace StarGate.Infrastructure.Resilience;

/// <summary>
/// Wraps retry and circuit breaker policies together.
/// </summary>
public static class ResiliencePolicyWrapper
{
    /// <summary>
    /// Creates a wrapped policy with retry inside circuit breaker for HTTP.
    /// </summary>
    /// <param name="retryConfig">Retry policy configuration.</param>
    /// <param name="circuitConfig">Circuit breaker configuration.</param>
    /// <param name="logger">Logger instance.</param>
    /// <returns>Wrapped policy with circuit breaker outer and retry inner.</returns>
    public static AsyncPolicyWrap<HttpResponseMessage> CreateHttpResiliencePolicy(
        RetryPolicyConfiguration retryConfig,
        CircuitBreakerConfiguration circuitConfig,
        ILogger logger)
    {
        var retryPolicy = RetryPolicyFactory.CreateHttpRetryPolicy(retryConfig, logger);
        var circuitBreaker = CircuitBreakerFactory.CreateHttpCircuitBreaker(circuitConfig, logger);

        // Wrap: Circuit Breaker (outer) -> Retry (inner)
        return Policy.WrapAsync(circuitBreaker, retryPolicy);
    }

    /// <summary>
    /// Creates a wrapped policy with retry inside circuit breaker for database.
    /// </summary>
    /// <param name="retryConfig">Retry policy configuration.</param>
    /// <param name="circuitConfig">Circuit breaker configuration.</param>
    /// <param name="logger">Logger instance.</param>
    /// <returns>Wrapped policy with circuit breaker outer and retry inner.</returns>
    public static AsyncPolicyWrap CreateDatabaseResiliencePolicy(
        RetryPolicyConfiguration retryConfig,
        CircuitBreakerConfiguration circuitConfig,
        ILogger logger)
    {
        var retryPolicy = RetryPolicyFactory.CreateDatabaseRetryPolicy(retryConfig, logger);
        var circuitBreaker = CircuitBreakerFactory.CreateDatabaseCircuitBreaker(circuitConfig, logger);

        return Policy.WrapAsync(circuitBreaker, retryPolicy);
    }

    /// <summary>
    /// Creates a wrapped policy with retry inside circuit breaker for broker.
    /// </summary>
    /// <param name="retryConfig">Retry policy configuration.</param>
    /// <param name="circuitConfig">Circuit breaker configuration.</param>
    /// <param name="logger">Logger instance.</param>
    /// <returns>Wrapped policy with circuit breaker outer and retry inner.</returns>
    public static AsyncPolicyWrap CreateBrokerResiliencePolicy(
        RetryPolicyConfiguration retryConfig,
        CircuitBreakerConfiguration circuitConfig,
        ILogger logger)
    {
        var retryPolicy = RetryPolicyFactory.CreateBrokerRetryPolicy(retryConfig, logger);
        var circuitBreaker = CircuitBreakerFactory.CreateBrokerCircuitBreaker(circuitConfig, logger);

        return Policy.WrapAsync(circuitBreaker, retryPolicy);
    }

    /// <summary>
    /// Creates a complete resilience policy with timeout, circuit breaker, and retry for HTTP.
    /// Note: Timeout is applied as an outer wrapper via ExecuteAsync pattern.
    /// </summary>
    /// <param name="timeoutConfig">Timeout configuration.</param>
    /// <param name="retryConfig">Retry policy configuration.</param>
    /// <param name="circuitConfig">Circuit breaker configuration.</param>
    /// <param name="logger">Logger instance.</param>
    /// <returns>Wrapped policy combining circuit breaker and retry. Apply timeout via WrapWithTimeoutAsync extension.</returns>
    public static CompleteHttpResiliencePolicy CreateCompleteHttpResiliencePolicy(
        TimeoutConfiguration timeoutConfig,
        RetryPolicyConfiguration retryConfig,
        CircuitBreakerConfiguration circuitConfig,
        ILogger logger)
    {
        var timeoutPolicy = TimeoutPolicyFactory.CreateHttpTimeoutPolicy(timeoutConfig, logger);
        var retryPolicy = RetryPolicyFactory.CreateHttpRetryPolicy(retryConfig, logger);
        var circuitBreaker = CircuitBreakerFactory.CreateHttpCircuitBreaker(circuitConfig, logger);

        // Wrap circuit breaker and retry
        var innerPolicy = Policy.WrapAsync(circuitBreaker, retryPolicy);

        return new CompleteHttpResiliencePolicy(timeoutPolicy, innerPolicy);
    }

    /// <summary>
    /// Creates a complete resilience policy for database operations.
    /// </summary>
    /// <param name="timeoutConfig">Timeout configuration.</param>
    /// <param name="retryConfig">Retry policy configuration.</param>
    /// <param name="circuitConfig">Circuit breaker configuration.</param>
    /// <param name="logger">Logger instance.</param>
    /// <returns>Complete wrapped policy with timeout (outer), circuit breaker, and retry (inner).</returns>
    public static AsyncPolicyWrap CreateCompleteDatabaseResiliencePolicy(
        TimeoutConfiguration timeoutConfig,
        RetryPolicyConfiguration retryConfig,
        CircuitBreakerConfiguration circuitConfig,
        ILogger logger)
    {
        var timeoutPolicy = TimeoutPolicyFactory.CreateDatabaseTimeoutPolicy(timeoutConfig, logger);
        var retryPolicy = RetryPolicyFactory.CreateDatabaseRetryPolicy(retryConfig, logger);
        var circuitBreaker = CircuitBreakerFactory.CreateDatabaseCircuitBreaker(circuitConfig, logger);

        return Policy.WrapAsync(timeoutPolicy, circuitBreaker, retryPolicy);
    }

    /// <summary>
    /// Creates a complete resilience policy for broker operations.
    /// </summary>
    /// <param name="timeoutConfig">Timeout configuration.</param>
    /// <param name="retryConfig">Retry policy configuration.</param>
    /// <param name="circuitConfig">Circuit breaker configuration.</param>
    /// <param name="logger">Logger instance.</param>
    /// <returns>Complete wrapped policy with timeout (outer), circuit breaker, and retry (inner).</returns>
    public static AsyncPolicyWrap CreateCompleteBrokerResiliencePolicy(
        TimeoutConfiguration timeoutConfig,
        RetryPolicyConfiguration retryConfig,
        CircuitBreakerConfiguration circuitConfig,
        ILogger logger)
    {
        var timeoutPolicy = TimeoutPolicyFactory.CreateBrokerTimeoutPolicy(timeoutConfig, logger);
        var retryPolicy = RetryPolicyFactory.CreateBrokerRetryPolicy(retryConfig, logger);
        var circuitBreaker = CircuitBreakerFactory.CreateBrokerCircuitBreaker(circuitConfig, logger);

        return Policy.WrapAsync(timeoutPolicy, circuitBreaker, retryPolicy);
    }
}

/// <summary>
/// Wrapper for complete HTTP resilience policy with timeout, circuit breaker, and retry.
/// </summary>
public class CompleteHttpResiliencePolicy
{
    private readonly Polly.Timeout.AsyncTimeoutPolicy _timeoutPolicy;
    private readonly AsyncPolicyWrap<HttpResponseMessage> _innerPolicy;

    public CompleteHttpResiliencePolicy(
        Polly.Timeout.AsyncTimeoutPolicy timeoutPolicy,
        AsyncPolicyWrap<HttpResponseMessage> innerPolicy)
    {
        _timeoutPolicy = timeoutPolicy ?? throw new ArgumentNullException(nameof(timeoutPolicy));
        _innerPolicy = innerPolicy ?? throw new ArgumentNullException(nameof(innerPolicy));
    }

    /// <summary>
    /// Executes the operation with timeout, circuit breaker, and retry policies.
    /// </summary>
    public async Task<HttpResponseMessage> ExecuteAsync(
        Func<Task<HttpResponseMessage>> operation,
        CancellationToken cancellationToken = default)
    {
        return await _timeoutPolicy.ExecuteAsync(async (ct) =>
        {
            return await _innerPolicy.ExecuteAsync(() => operation());
        }, cancellationToken);
    }

    /// <summary>
    /// Executes the operation with timeout, circuit breaker, and retry policies.
    /// </summary>
    public async Task<HttpResponseMessage> ExecuteAsync(
        Func<CancellationToken, Task<HttpResponseMessage>> operation,
        CancellationToken cancellationToken = default)
    {
        return await _timeoutPolicy.ExecuteAsync(async (ct) =>
        {
            return await _innerPolicy.ExecuteAsync(() => operation(ct));
        }, cancellationToken);
    }
}
