using Microsoft.Extensions.Logging;
using StackExchange.Redis;

namespace StarGate.Infrastructure.Caching;

/// <summary>
/// Factory for creating and configuring Redis connections with pooling.
/// Implements singleton pattern for connection multiplexer reuse.
/// </summary>
public static class RedisConnectionFactory
{
    private static readonly object Lock = new();
    private static IConnectionMultiplexer? _instance;

    /// <summary>
    /// Creates or returns existing singleton Redis connection.
    /// Implements lazy initialization with thread safety.
    /// </summary>
    /// <param name="connectionString">Redis connection string.</param>
    /// <param name="logger">Logger for connection events and diagnostics.</param>
    /// <returns>Configured connection multiplexer instance.</returns>
    /// <exception cref="ArgumentException">If connection string is null or whitespace.</exception>
    /// <exception cref="ArgumentNullException">If logger is null.</exception>
    public static IConnectionMultiplexer GetOrCreateConnection(
        string connectionString,
        ILogger logger)
    {
        if (_instance?.IsConnected == true)
        {
            return _instance;
        }

        lock (Lock)
        {
            if (_instance?.IsConnected == true)
            {
                return _instance;
            }

            _instance?.Dispose();
            _instance = CreateConnection(connectionString, logger);
            return _instance;
        }
    }

    /// <summary>
    /// Creates a new Redis connection with optimized configuration.
    /// Configures connection pooling, resilience, and event monitoring.
    /// </summary>
    /// <param name="connectionString">Redis connection string.</param>
    /// <param name="logger">Logger for connection events and diagnostics.</param>
    /// <returns>Configured connection multiplexer.</returns>
    /// <exception cref="ArgumentException">If connection string is null or whitespace.</exception>
    /// <exception cref="ArgumentNullException">If logger is null.</exception>
    public static IConnectionMultiplexer CreateConnection(
        string connectionString,
        ILogger logger)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);
        ArgumentNullException.ThrowIfNull(logger);

        logger.LogInformation("Creating Redis connection...");

        ConfigurationOptions options = ConfigurationOptions.Parse(connectionString);
        
        // Connection pooling and resilience
        options.AbortOnConnectFail = false;           // Don't fail fast on startup
        options.ConnectRetry = 5;                     // Retry 5 times
        options.ConnectTimeout = 10000;               // 10 seconds
        options.SyncTimeout = 5000;                   // 5 seconds for sync operations
        options.AsyncTimeout = 10000;                 // 10 seconds for async operations
        options.KeepAlive = 60;                       // Keep-alive every 60 seconds
        options.AllowAdmin = false;                   // Disable admin commands for security
        
        // Reconnection strategy
        options.ReconnectRetryPolicy = new ExponentialRetry(
            deltaBackOffMilliseconds: 1000,
            maxDeltaBackOffMilliseconds: 30000);

        // Connection pooling (StackExchange.Redis handles this internally)
        // The multiplexer is thread-safe and should be reused
        options.ClientName = "StarGate";

        // Socket configuration for better performance
        options.SocketManager = SocketManager.Shared;

        logger.LogInformation(
            "Redis configuration: ConnectTimeout={ConnectTimeout}ms, "
            + "SyncTimeout={SyncTimeout}ms, KeepAlive={KeepAlive}s",
            options.ConnectTimeout,
            options.SyncTimeout,
            options.KeepAlive);

        IConnectionMultiplexer connection = ConnectionMultiplexer.Connect(options);

        RegisterConnectionEvents(connection, logger);

        logger.LogInformation(
            "Redis connection established: {Endpoints}, Status={Status}",
            string.Join(", ", connection.GetEndPoints().Select(ep => ep.ToString())),
            connection.IsConnected ? "Connected" : "Disconnected");

        return connection;
    }

    /// <summary>
    /// Registers event handlers for connection monitoring and diagnostics.
    /// </summary>
    /// <param name="connection">Redis connection multiplexer.</param>
    /// <param name="logger">Logger for connection events.</param>
    private static void RegisterConnectionEvents(
        IConnectionMultiplexer connection,
        ILogger logger)
    {
        connection.ConnectionFailed += (sender, args) =>
        {
            logger.LogError(
                "Redis connection failed: EndPoint={EndPoint}, FailureType={FailureType}, Exception={Exception}",
                args.EndPoint,
                args.FailureType,
                args.Exception?.Message ?? "Unknown");
        };

        connection.ConnectionRestored += (sender, args) =>
        {
            logger.LogInformation(
                "Redis connection restored: EndPoint={EndPoint}, FailureType={FailureType}",
                args.EndPoint,
                args.FailureType);
        };

        connection.ErrorMessage += (sender, args) =>
        {
            logger.LogError(
                "Redis error message: {Message}",
                args.Message);
        };

        connection.InternalError += (sender, args) =>
        {
            logger.LogError(
                args.Exception,
                "Redis internal error: Origin={Origin}, ConnectionType={ConnectionType}",
                args.Origin,
                args.ConnectionType);
        };

        connection.ConfigurationChanged += (sender, args) =>
        {
            logger.LogInformation(
                "Redis configuration changed: EndPoint={EndPoint}",
                args.EndPoint);
        };

        connection.ConfigurationChangedBroadcast += (sender, args) =>
        {
            logger.LogInformation(
                "Redis configuration broadcast: EndPoint={EndPoint}",
                args.EndPoint);
        };
    }
}
