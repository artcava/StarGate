namespace StarGate.Infrastructure.Resilience;

using Microsoft.Extensions.Logging;
using Polly;
using Polly.Timeout;

/// <summary>
/// Factory for creating Polly timeout policies.
/// </summary>
public static class TimeoutPolicyFactory
{
    /// <summary>
    /// Creates a timeout policy for HTTP operations.
    /// </summary>
    public static AsyncTimeoutPolicy CreateHttpTimeoutPolicy(
        TimeoutConfiguration config,
        ILogger logger)
    {
        return Policy
            .TimeoutAsync(
                timeout: config.HttpTimeout,
                timeoutStrategy: config.UsePessimisticTimeout
                    ? TimeoutStrategy.Pessimistic
                    : TimeoutStrategy.Optimistic,
                onTimeoutAsync: (context, timespan, task) =>
                {
                    logger.LogError(
                        "HTTP operation timed out: Timeout={Timeout}s, Strategy={Strategy}",
                        timespan.TotalSeconds,
                        config.UsePessimisticTimeout ? "Pessimistic" : "Optimistic");

                    return Task.CompletedTask;
                });
    }

    /// <summary>
    /// Creates a timeout policy for database operations.
    /// </summary>
    public static AsyncTimeoutPolicy CreateDatabaseTimeoutPolicy(
        TimeoutConfiguration config,
        ILogger logger)
    {
        return Policy
            .TimeoutAsync(
                timeout: config.DatabaseTimeout,
                timeoutStrategy: config.UsePessimisticTimeout
                    ? TimeoutStrategy.Pessimistic
                    : TimeoutStrategy.Optimistic,
                onTimeoutAsync: (context, timespan, task) =>
                {
                    logger.LogError(
                        "Database operation timed out: Timeout={Timeout}s, Strategy={Strategy}",
                        timespan.TotalSeconds,
                        config.UsePessimisticTimeout ? "Pessimistic" : "Optimistic");

                    return Task.CompletedTask;
                });
    }

    /// <summary>
    /// Creates a timeout policy for message broker operations.
    /// </summary>
    public static AsyncTimeoutPolicy CreateBrokerTimeoutPolicy(
        TimeoutConfiguration config,
        ILogger logger)
    {
        return Policy
            .TimeoutAsync(
                timeout: config.BrokerTimeout,
                timeoutStrategy: config.UsePessimisticTimeout
                    ? TimeoutStrategy.Pessimistic
                    : TimeoutStrategy.Optimistic,
                onTimeoutAsync: (context, timespan, task) =>
                {
                    logger.LogError(
                        "Broker operation timed out: Timeout={Timeout}s, Strategy={Strategy}",
                        timespan.TotalSeconds,
                        config.UsePessimisticTimeout ? "Pessimistic" : "Optimistic");

                    return Task.CompletedTask;
                });
    }
}
