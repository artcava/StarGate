namespace StarGate.Infrastructure.Messaging.RabbitMQ;

using Microsoft.Extensions.Logging;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using System.Threading;

/// <summary>
/// Factory for creating and configuring RabbitMQ connections.
/// </summary>
public static class RabbitMqConnectionFactory
{
    private static int _recoveryAttempts = 0;

    /// <summary>
    /// Creates a RabbitMQ connection with automatic recovery.
    /// </summary>
    public static IConnection CreateConnection(
        RabbitMqOptions options,
        ILogger logger)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(logger);

        logger.LogInformation(
            "Creating RabbitMQ connection to {HostName}:{Port}",
            options.HostName,
            options.Port);

        var factory = new ConnectionFactory
        {
            HostName = options.HostName,
            Port = options.Port,
            UserName = options.UserName,
            Password = options.Password,
            VirtualHost = options.VirtualHost,
            
            // Automatic recovery settings
            AutomaticRecoveryEnabled = true,
            NetworkRecoveryInterval = TimeSpan.FromSeconds(options.NetworkRecoveryIntervalSeconds),
            TopologyRecoveryEnabled = true,
            
            // Connection timeouts and heartbeats
            RequestedHeartbeat = TimeSpan.FromSeconds(options.HeartbeatSeconds),
            RequestedConnectionTimeout = TimeSpan.FromSeconds(options.ConnectionTimeoutSeconds),
            HandshakeContinuationTimeout = TimeSpan.FromSeconds(10),
            ContinuationTimeout = TimeSpan.FromSeconds(20),
            
            // Async consumer dispatch
            DispatchConsumersAsync = true,
            ConsumerDispatchConcurrency = 1,
            
            // Client identification
            ClientProvidedName = "StarGate-API"
        };

        var connection = factory.CreateConnection();

        RegisterConnectionEvents(connection, logger);
        RegisterRecoveryEvents(connection, options, logger);

        logger.LogInformation(
            "RabbitMQ connection established: {Endpoint}",
            connection.Endpoint);

        return connection;
    }

    private static void RegisterConnectionEvents(
        IConnection connection,
        ILogger logger)
    {
        connection.ConnectionShutdown += (sender, args) =>
        {
            logger.LogWarning(
                "RabbitMQ connection shutdown: {ReplyCode} - {ReplyText}, Initiator: {Initiator}",
                args.ReplyCode,
                args.ReplyText,
                args.Initiator);
        };

        connection.ConnectionBlocked += (sender, args) =>
        {
            logger.LogWarning(
                "RabbitMQ connection blocked: {Reason}",
                args.Reason);
        };

        connection.ConnectionUnblocked += (sender, args) =>
        {
            logger.LogInformation("RabbitMQ connection unblocked");
        };

        connection.CallbackException += (sender, args) =>
        {
            logger.LogError(
                args.Exception,
                "RabbitMQ callback exception: {Detail}",
                args.Detail);
        };
    }

    private static void RegisterRecoveryEvents(
        IConnection connection,
        RabbitMqOptions options,
        ILogger logger)
    {
        if (connection is not IAutorecoveringConnection autoRecovering)
        {
            return;
        }

        autoRecovering.RecoverySucceeded += (sender, args) =>
        {
            Interlocked.Exchange(ref _recoveryAttempts, 0); // Reset counter on success (thread-safe)
            logger.LogInformation("RabbitMQ connection recovery succeeded");
        };

        autoRecovering.ConnectionRecoveryError += (sender, args) =>
        {
            var attempts = Interlocked.Increment(ref _recoveryAttempts);

            if (attempts >= options.MaxRecoveryAttempts)
            {
                logger.LogCritical(
                    args.Exception,
                    "RabbitMQ connection recovery failed after {Attempts} attempts. Manual intervention required.",
                    attempts);
            }
            else
            {
                logger.LogError(
                    args.Exception,
                    "RabbitMQ connection recovery error (attempt {Attempt}/{MaxAttempts})",
                    attempts,
                    options.MaxRecoveryAttempts);
            }
        };

        // ConsumerTagChangeAfterRecovery is available but with limited properties in some versions
        autoRecovering.ConsumerTagChangeAfterRecovery += (sender, args) =>
        {
            logger.LogInformation(
                "RabbitMQ consumer tag changed after recovery for consumer");
        };
    }
}
