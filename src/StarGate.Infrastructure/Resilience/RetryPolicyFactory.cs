using Microsoft.Extensions.Logging;
using Polly;
using Polly.Retry;

namespace StarGate.Infrastructure.Resilience;

/// <summary>
/// Factory for creating Polly retry policies.
/// </summary>
public static class RetryPolicyFactory
{
    /// <summary>
    /// Creates a retry policy for HTTP operations.
    /// </summary>
    /// <param name="config">Retry policy configuration.</param>
    /// <param name="logger">Logger instance.</param>
    /// <returns>Configured async retry policy for HTTP responses.</returns>
    public static AsyncRetryPolicy<HttpResponseMessage> CreateHttpRetryPolicy(
        RetryPolicyConfiguration config,
        ILogger logger)
    {
        return Policy
            .HandleResult<HttpResponseMessage>(r => !r.IsSuccessStatusCode)
            .Or<HttpRequestException>()
            .Or<TimeoutException>()
            .WaitAndRetryAsync(
                retryCount: config.MaxRetryAttempts,
                sleepDurationProvider: retryAttempt => config.CalculateDelay(retryAttempt),
                onRetry: (outcome, timespan, retryAttempt, context) =>
                {
                    var statusCode = outcome.Result?.StatusCode.ToString() ?? "N/A";
                    var exception = outcome.Exception?.GetType().Name ?? "None";

                    logger.LogWarning(
                        "HTTP retry attempt {RetryAttempt}/{MaxRetries}: StatusCode={StatusCode}, Exception={Exception}, Delay={Delay}ms",
                        retryAttempt,
                        config.MaxRetryAttempts,
                        statusCode,
                        exception,
                        timespan.TotalMilliseconds);
                });
    }

    /// <summary>
    /// Creates a retry policy for database operations.
    /// </summary>
    /// <param name="config">Retry policy configuration.</param>
    /// <param name="logger">Logger instance.</param>
    /// <returns>Configured async retry policy for database operations.</returns>
    public static AsyncRetryPolicy CreateDatabaseRetryPolicy(
        RetryPolicyConfiguration config,
        ILogger logger)
    {
        return Policy
            .Handle<TimeoutException>()
            .Or<IOException>()
            .Or<InvalidOperationException>(ex => ex.Message.Contains("connection", StringComparison.OrdinalIgnoreCase))
            .WaitAndRetryAsync(
                retryCount: config.MaxRetryAttempts,
                sleepDurationProvider: retryAttempt => config.CalculateDelay(retryAttempt),
                onRetry: (exception, timespan, retryAttempt, context) =>
                {
                    logger.LogWarning(
                        exception,
                        "Database retry attempt {RetryAttempt}/{MaxRetries}: Exception={Exception}, Delay={Delay}ms",
                        retryAttempt,
                        config.MaxRetryAttempts,
                        exception.GetType().Name,
                        timespan.TotalMilliseconds);
                });
    }

    /// <summary>
    /// Creates a retry policy for message broker operations.
    /// </summary>
    /// <param name="config">Retry policy configuration.</param>
    /// <param name="logger">Logger instance.</param>
    /// <returns>Configured async retry policy for broker operations.</returns>
    public static AsyncRetryPolicy CreateBrokerRetryPolicy(
        RetryPolicyConfiguration config,
        ILogger logger)
    {
        return Policy
            .Handle<TimeoutException>()
            .Or<IOException>()
            .Or<InvalidOperationException>(ex => ex.Message.Contains("connection", StringComparison.OrdinalIgnoreCase))
            .WaitAndRetryAsync(
                retryCount: config.MaxRetryAttempts,
                sleepDurationProvider: retryAttempt => config.CalculateDelay(retryAttempt),
                onRetry: (exception, timespan, retryAttempt, context) =>
                {
                    logger.LogWarning(
                        exception,
                        "Broker retry attempt {RetryAttempt}/{MaxRetries}: Exception={Exception}, Delay={Delay}ms",
                        retryAttempt,
                        config.MaxRetryAttempts,
                        exception.GetType().Name,
                        timespan.TotalMilliseconds);
                });
    }

    /// <summary>
    /// Creates a generic retry policy for any async operation.
    /// </summary>
    /// <param name="config">Retry policy configuration.</param>
    /// <param name="logger">Logger instance.</param>
    /// <returns>Configured async retry policy for generic operations.</returns>
    public static AsyncRetryPolicy CreateGenericRetryPolicy(
        RetryPolicyConfiguration config,
        ILogger logger)
    {
        return Policy
            .Handle<Exception>(ex => IsTransientException(ex))
            .WaitAndRetryAsync(
                retryCount: config.MaxRetryAttempts,
                sleepDurationProvider: retryAttempt => config.CalculateDelay(retryAttempt),
                onRetry: (exception, timespan, retryAttempt, context) =>
                {
                    logger.LogWarning(
                        exception,
                        "Generic retry attempt {RetryAttempt}/{MaxRetries}: Exception={Exception}, Delay={Delay}ms",
                        retryAttempt,
                        config.MaxRetryAttempts,
                        exception.GetType().Name,
                        timespan.TotalMilliseconds);
                });
    }

    private static bool IsTransientException(Exception ex)
    {
        return ex is TimeoutException
            || ex is HttpRequestException
            || ex is IOException
            || (ex is InvalidOperationException && ex.Message.Contains("connection", StringComparison.OrdinalIgnoreCase));
    }
}
