using Microsoft.Extensions.Logging;
using Polly;
using Polly.CircuitBreaker;

namespace StarGate.Infrastructure.Resilience;

/// <summary>
/// Factory for creating Polly circuit breaker policies.
/// </summary>
public static class CircuitBreakerFactory
{
    /// <summary>
    /// Creates a circuit breaker policy for HTTP operations.
    /// </summary>
    /// <param name="config">Circuit breaker configuration.</param>
    /// <param name="logger">Logger instance.</param>
    /// <returns>Configured async circuit breaker policy for HTTP responses.</returns>
    public static AsyncCircuitBreakerPolicy<HttpResponseMessage> CreateHttpCircuitBreaker(
        CircuitBreakerConfiguration config,
        ILogger logger)
    {
        return Policy
            .HandleResult<HttpResponseMessage>(r => !r.IsSuccessStatusCode)
            .Or<HttpRequestException>()
            .Or<TimeoutException>()
            .AdvancedCircuitBreakerAsync(
                failureThreshold: config.FailureRateThreshold,
                samplingDuration: config.SamplingDuration,
                minimumThroughput: config.MinimumThroughput,
                durationOfBreak: config.BreakDuration,
                onBreak: (outcome, breakDuration, context) =>
                {
                    var statusCode = outcome.Result?.StatusCode.ToString() ?? "N/A";
                    var exception = outcome.Exception?.GetType().Name ?? "None";

                    logger.LogError(
                        "HTTP circuit breaker opened: StatusCode={StatusCode}, Exception={Exception}, BreakDuration={BreakDuration}s",
                        statusCode,
                        exception,
                        breakDuration.TotalSeconds);
                },
                onReset: context =>
                {
                    logger.LogInformation("HTTP circuit breaker reset: Circuit closed");
                },
                onHalfOpen: () =>
                {
                    logger.LogWarning("HTTP circuit breaker half-open: Testing recovery");
                });
    }

    /// <summary>
    /// Creates a circuit breaker policy for database operations.
    /// </summary>
    /// <param name="config">Circuit breaker configuration.</param>
    /// <param name="logger">Logger instance.</param>
    /// <returns>Configured async circuit breaker policy for database operations.</returns>
    public static AsyncCircuitBreakerPolicy CreateDatabaseCircuitBreaker(
        CircuitBreakerConfiguration config,
        ILogger logger)
    {
        return Policy
            .Handle<TimeoutException>()
            .Or<IOException>()
            .Or<InvalidOperationException>(ex => ex.Message.Contains("connection", StringComparison.OrdinalIgnoreCase))
            .AdvancedCircuitBreakerAsync(
                failureThreshold: config.FailureRateThreshold,
                samplingDuration: config.SamplingDuration,
                minimumThroughput: config.MinimumThroughput,
                durationOfBreak: config.BreakDuration,
                onBreak: (exception, breakDuration, context) =>
                {
                    logger.LogError(
                        exception,
                        "Database circuit breaker opened: Exception={Exception}, BreakDuration={BreakDuration}s",
                        exception.GetType().Name,
                        breakDuration.TotalSeconds);
                },
                onReset: context =>
                {
                    logger.LogInformation("Database circuit breaker reset: Circuit closed");
                },
                onHalfOpen: () =>
                {
                    logger.LogWarning("Database circuit breaker half-open: Testing recovery");
                });
    }

    /// <summary>
    /// Creates a circuit breaker policy for message broker operations.
    /// </summary>
    /// <param name="config">Circuit breaker configuration.</param>
    /// <param name="logger">Logger instance.</param>
    /// <returns>Configured async circuit breaker policy for broker operations.</returns>
    public static AsyncCircuitBreakerPolicy CreateBrokerCircuitBreaker(
        CircuitBreakerConfiguration config,
        ILogger logger)
    {
        return Policy
            .Handle<TimeoutException>()
            .Or<IOException>()
            .Or<InvalidOperationException>(ex => ex.Message.Contains("connection", StringComparison.OrdinalIgnoreCase))
            .AdvancedCircuitBreakerAsync(
                failureThreshold: config.FailureRateThreshold,
                samplingDuration: config.SamplingDuration,
                minimumThroughput: config.MinimumThroughput,
                durationOfBreak: config.BreakDuration,
                onBreak: (exception, breakDuration, context) =>
                {
                    logger.LogError(
                        exception,
                        "Broker circuit breaker opened: Exception={Exception}, BreakDuration={BreakDuration}s",
                        exception.GetType().Name,
                        breakDuration.TotalSeconds);
                },
                onReset: context =>
                {
                    logger.LogInformation("Broker circuit breaker reset: Circuit closed");
                },
                onHalfOpen: () =>
                {
                    logger.LogWarning("Broker circuit breaker half-open: Testing recovery");
                });
    }
}
