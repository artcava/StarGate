using Microsoft.Extensions.Logging;
using StackExchange.Redis;

namespace StarGate.Infrastructure.Caching;

/// <summary>
/// Factory for creating Redis connections with proper configuration and resilience.
/// </summary>
public static class RedisConnectionFactory
{
    /// <summary>
    /// Creates a configured Redis connection multiplexer.
    /// </summary>
    /// <param name="connectionString">Redis connection string.</param>
    /// <param name="logger">Logger for connection events.</param>
    /// <returns>Configured connection multiplexer.</returns>
    /// <exception cref="ArgumentException">If connection string is null or empty.</exception>
    /// <exception cref="ArgumentNullException">If logger is null.</exception>
    public static IConnectionMultiplexer CreateConnection(
        string connectionString,
        ILogger logger)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);
        ArgumentNullException.ThrowIfNull(logger);

        var options = ConfigurationOptions.Parse(connectionString);

        // Connection resilience settings
        options.AbortOnConnectFail = false; // Don't throw on initial connection failure
        options.ConnectRetry = 3; // Retry connection 3 times
        options.ConnectTimeout = 5000; // 5 seconds connect timeout
        options.SyncTimeout = 5000; // 5 seconds sync operation timeout
        options.AsyncTimeout = 5000; // 5 seconds async operation timeout
        options.KeepAlive = 60; // Send keepalive every 60 seconds
        options.ReconnectRetryPolicy = new ExponentialRetry(5000); // Exponential backoff starting at 5s

        var connection = ConnectionMultiplexer.Connect(options);

        // Connection event handlers for monitoring
        connection.ConnectionFailed += (sender, args) =>
        {
            logger.LogError(
                "Redis connection failed: {EndPoint} - {FailureType} - {Exception}",
                args.EndPoint,
                args.FailureType,
                args.Exception?.Message ?? "Unknown error");
        };

        connection.ConnectionRestored += (sender, args) =>
        {
            logger.LogInformation(
                "Redis connection restored: {EndPoint}",
                args.EndPoint);
        };

        connection.ErrorMessage += (sender, args) =>
        {
            logger.LogError(
                "Redis error: {Message}",
                args.Message);
        };

        connection.InternalError += (sender, args) =>
        {
            logger.LogError(
                args.Exception,
                "Redis internal error: {Origin}",
                args.Origin);
        };

        logger.LogInformation(
            "Redis connection established to {EndPoint}",
            connection.GetEndPoints().FirstOrDefault());

        return connection;
    }
}
