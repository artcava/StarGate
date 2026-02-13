namespace StarGate.Infrastructure.Messaging.RabbitMQ;

using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using Microsoft.Extensions.Logging;

/// <summary>
/// Factory for creating and configuring RabbitMQ connections.
/// </summary>
public static class RabbitMqConnectionFactory
{
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
            NetworkRecoveryInterval = TimeSpan.FromSeconds(10),
            TopologyRecoveryEnabled = true,
            
            // Connection settings
            RequestedHeartbeat = TimeSpan.FromSeconds(60),
            RequestedConnectionTimeout = TimeSpan.FromSeconds(30),
            
            // Dispatch settings
            DispatchConsumersAsync = true,
            
            // Client properties
            ClientProvidedName = "StarGate-API"
        };

        var connection = factory.CreateConnection();

        RegisterConnectionEvents(connection, logger);

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
                "RabbitMQ connection shutdown: {ReplyCode} - {ReplyText}",
                args.ReplyCode,
                args.ReplyText);
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

        if (connection is IAutorecoveringConnection autoRecovering)
        {
            autoRecovering.RecoverySucceeded += (sender, args) =>
            {
                logger.LogInformation("RabbitMQ connection recovery succeeded");
            };

            autoRecovering.ConnectionRecoveryError += (sender, args) =>
            {
                logger.LogError(
                    args.Exception,
                    "RabbitMQ connection recovery error");
            };
        }
    }
}
