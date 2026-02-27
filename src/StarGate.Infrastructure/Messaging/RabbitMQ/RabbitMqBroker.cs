using RabbitMQ.Client;
using RabbitMQ.Client.Exceptions;
using StarGate.Core.Abstractions;
using StarGate.Core.Exceptions;
using Microsoft.Extensions.Logging;
using System.Text;
using System.Text.Json;

namespace StarGate.Infrastructure.Messaging.RabbitMQ;

/// <summary>
/// RabbitMQ implementation of IMessageBroker.
/// Publishes messages to RabbitMQ queues with reliable delivery.
/// </summary>
public class RabbitMqBroker : IMessageBroker, IDisposable
{
    private readonly IConnection _connection;
    private readonly IModel _channel;
    private readonly IMessageSerializer _serializer;
    private readonly RabbitMqOptions _options;
    private readonly ILogger<RabbitMqBroker> _logger;
    private bool _disposed;
    private const string ExchangeName = "stargate.processes";
    private const string ExchangeType = "topic";

    public RabbitMqBroker(
        IConnection connection,
        IMessageSerializer serializer,
        RabbitMqOptions options,
        ILogger<RabbitMqBroker> logger)
    {
        _connection = connection ?? throw new ArgumentNullException(nameof(connection));
        _serializer = serializer ?? throw new ArgumentNullException(nameof(serializer));
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        _channel = _connection.CreateModel();

        if (_options.PublisherConfirms)
        {
            _channel.ConfirmSelect();
        }

        DeclareInfrastructure();

        _logger.LogInformation(
            "RabbitMQ broker initialized: Exchange={Exchange}, PublisherConfirms={Confirms}",
            _options.ProcessExchange,
            _options.PublisherConfirms);
    }

    public Task PublishAsync<T>(
        string queueName,
        T message,
        CancellationToken ct = default) where T : class
    {
        return PublishAsync(queueName, message, new MessageProperties(), ct);
    }

    public async Task PublishAsync<T>(
        string queueName,
        T message,
        MessageProperties properties,
        CancellationToken ct = default) where T : class
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(queueName);
        ArgumentNullException.ThrowIfNull(message);
        ArgumentNullException.ThrowIfNull(properties);

        try
        {
            // Ensure queue exists
            DeclareQueue(queueName);

            // Create message envelope
            var envelope = MessageEnvelopeFactory.Create(
                message,
                properties.CorrelationId,
                properties.Headers);

            // Serialize message
            var body = _serializer.Serialize(envelope);

            // Create basic properties
            var basicProperties = _channel.CreateBasicProperties();
            basicProperties.Persistent = true; // Durable messages
            basicProperties.MessageId = properties.MessageId ?? envelope.MessageId;
            basicProperties.CorrelationId = properties.CorrelationId;
            basicProperties.Timestamp = new AmqpTimestamp(DateTimeOffset.UtcNow.ToUnixTimeSeconds());
            basicProperties.ContentType = "application/json";
            basicProperties.ContentEncoding = "utf-8";

            if (properties.Priority.HasValue)
            {
                basicProperties.Priority = (byte)properties.Priority.Value;
            }

            if (properties.TimeToLive.HasValue)
            {
                basicProperties.Expiration = properties.TimeToLive.Value.TotalMilliseconds.ToString("F0");
            }

            // Publish to exchange with routing key = queue name
            _channel.BasicPublish(
                exchange: _options.ProcessExchange,
                routingKey: queueName,
                mandatory: true,
                basicProperties: basicProperties,
                body: body);

            // Wait for confirmation if enabled
            if (_options.PublisherConfirms)
            {
                var confirmed = _channel.WaitForConfirms(
                    TimeSpan.FromMilliseconds(_options.PublisherConfirmTimeoutMs));

                if (!confirmed)
                {
                    throw new BrokerException(
                        $"Failed to confirm message publication to queue '{queueName}'");
                }
            }

            _logger.LogDebug(
                "Published message {MessageId} to queue {Queue}, size: {Size} bytes",
                envelope.MessageId,
                queueName,
                body.Length);

            await Task.CompletedTask; // Satisfy async signature
        }
        catch (BrokerException)
        {
            throw; // Re-throw broker exceptions
        }
        catch (AlreadyClosedException ex)
        {
            _logger.LogError(
                ex,
                "RabbitMQ channel/connection closed while publishing to queue {Queue}",
                queueName);

            throw new BrokerException(
                $"Failed to publish message to queue '{queueName}': Connection closed",
                ex);
        }
        catch (OperationInterruptedException ex)
        {
            _logger.LogError(
                ex,
                "RabbitMQ operation interrupted while publishing to queue {Queue}",
                queueName);

            throw new BrokerException(
                $"Failed to publish message to queue '{queueName}': Operation interrupted",
                ex);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Unexpected error publishing message to queue {Queue}",
                queueName);

            throw new BrokerException(
                $"Failed to publish message to queue '{queueName}'",
                ex);
        }
    }

    public Task PublishAsync<T>(
        T message,
        string routingKey,
        CancellationToken cancellationToken = default) where T : class
    {
        ArgumentNullException.ThrowIfNull(message);
        ArgumentException.ThrowIfNullOrWhiteSpace(routingKey);

        try
        {
            var messageBody = SerializeMessage(message);
            var properties = _channel.CreateBasicProperties();
            properties.Persistent = true;
            properties.ContentType = "application/json";
            properties.Timestamp = new AmqpTimestamp(DateTimeOffset.UtcNow.ToUnixTimeSeconds());

            _channel.BasicPublish(
                exchange: ExchangeName,
                routingKey: routingKey,
                basicProperties: properties,
                body: messageBody);

            _logger.LogDebug(
                "Message published: RoutingKey={RoutingKey}, MessageType={MessageType}",
                routingKey,
                typeof(T).Name);

            return Task.CompletedTask;
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Failed to publish message: RoutingKey={RoutingKey}, MessageType={MessageType}",
                routingKey,
                typeof(T).Name);

            throw;
        }
    }

    public async Task PublishWithDelayAsync<T>(
        T message,
        string routingKey,
        TimeSpan delay,
        CancellationToken cancellationToken = default) where T : class
    {
        ArgumentNullException.ThrowIfNull(message);
        ArgumentException.ThrowIfNullOrWhiteSpace(routingKey);

        try
        {
            var messageBody = SerializeMessage(message);
            var properties = _channel.CreateBasicProperties();
            properties.Persistent = true;
            properties.ContentType = "application/json";
            properties.Expiration = ((int)delay.TotalMilliseconds).ToString();

            // Use delayed exchange or dead-letter exchange pattern
            _channel.BasicPublish(
                exchange: ExchangeName,
                routingKey: routingKey,
                basicProperties: properties,
                body: messageBody);

            _logger.LogDebug(
                "Delayed message published: RoutingKey={RoutingKey}, Delay={Delay}",
                routingKey,
                delay);

            await Task.CompletedTask;
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Failed to publish delayed message: RoutingKey={RoutingKey}",
                routingKey);

            throw;
        }
    }

    public IMessageConsumer CreateConsumer(string queueName, CancellationToken cancellationToken = default)
    {
        // Consumer implementation is part of Phase 3.2
        throw new NotImplementedException("Consumer functionality will be implemented in Phase 3.2");
    }

    private static byte[] SerializeMessage<T>(T message) where T : class
    {
        var json = JsonSerializer.Serialize(message, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = false
        });

        return Encoding.UTF8.GetBytes(json);
    }

    private void DeclareInfrastructure()
    {
        try
        {
            // Declare topic exchange for process routing
            _channel.ExchangeDeclare(
                exchange: ExchangeName,
                type: ExchangeType,
                durable: true,
                autoDelete: false);

            // Declare dead letter exchange
            _channel.ExchangeDeclare(
                exchange: _options.DeadLetterExchange,
                type: RabbitMQ.Client.ExchangeType.Direct,
                durable: true,
                autoDelete: false);

            // Declare dead letter queue
            _channel.QueueDeclare(
                queue: _options.DeadLetterQueue,
                durable: true,
                exclusive: false,
                autoDelete: false,
                arguments: null);

            // Bind DLQ to DLX
            _channel.QueueBind(
                queue: _options.DeadLetterQueue,
                exchange: _options.DeadLetterExchange,
                routingKey: "#");

            // Declare main process exchange
            _channel.ExchangeDeclare(
                exchange: _options.ProcessExchange,
                type: RabbitMQ.Client.ExchangeType.Direct,
                durable: true,
                autoDelete: false);

            _logger.LogInformation(
                "RabbitMQ infrastructure declared: Exchange={Exchange}, TopicExchange={TopicExchange}, DLX={DLX}, DLQ={DLQ}",
                _options.ProcessExchange,
                ExchangeName,
                _options.DeadLetterExchange,
                _options.DeadLetterQueue);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Failed to declare RabbitMQ infrastructure");
            throw;
        }
    }

    private void DeclareQueue(string queueName)
    {
        try
        {
            var arguments = new Dictionary<string, object>
            {
                // Route failed messages to DLX
                ["x-dead-letter-exchange"] = _options.DeadLetterExchange,
                ["x-dead-letter-routing-key"] = queueName,
                // Enable message priority
                ["x-max-priority"] = 10
            };

            _channel.QueueDeclare(
                queue: queueName,
                durable: true,
                exclusive: false,
                autoDelete: false,
                arguments: arguments);

            // Bind queue to exchange
            _channel.QueueBind(
                queue: queueName,
                exchange: _options.ProcessExchange,
                routingKey: queueName);

            _logger.LogDebug(
                "Declared queue {Queue} with DLX support",
                queueName);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Failed to declare queue {Queue}",
                queueName);
            throw;
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        try
        {
            _channel?.Close();
            _channel?.Dispose();

            _logger.LogInformation("RabbitMQ broker disposed");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error disposing RabbitMQ broker");
        }
        finally
        {
            _disposed = true;
        }
    }
}
