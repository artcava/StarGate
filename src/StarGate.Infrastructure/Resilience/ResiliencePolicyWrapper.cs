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
    /// Creates a complete resilience policy with timeout, circuit breaker, and retry.
    /// </summary>
    /// <param name="timeoutConfig">Timeout configuration.</param>
    /// <param name="retryConfig">Retry policy configuration.</param>
    /// <param name="circuitConfig">Circuit breaker configuration.</param>
    /// <param name="logger">Logger instance.</param>
    /// <returns>Complete wrapped policy with timeout (outer), circuit breaker, and retry (inner).</returns>
    public static AsyncPolicyWrap<HttpResponseMessage> CreateCompleteHttpResiliencePolicy(
        TimeoutConfiguration timeoutConfig,
        RetryPolicyConfiguration retryConfig,
        CircuitBreakerConfiguration circuitConfig,
        ILogger logger)
    {
        var timeoutPolicy = TimeoutPolicyFactory.CreateHttpTimeoutPolicy(timeoutConfig, logger);
        var retryPolicy = RetryPolicyFactory.CreateHttpRetryPolicy(retryConfig, logger);
        var circuitBreaker = CircuitBreakerFactory.CreateHttpCircuitBreaker(circuitConfig, logger);

        // Wrap: Timeout (outer) -> Circuit Breaker -> Retry (inner)
        return Policy.WrapAsync(timeoutPolicy, circuitBreaker, retryPolicy);
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
