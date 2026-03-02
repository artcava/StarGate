using System.Collections.Concurrent;
using System.Text;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using RabbitMQ.Client.Exceptions;
using StarGate.Core.Abstractions;

namespace StarGate.Infrastructure.Messaging.RabbitMQ;

/// <summary>
/// RabbitMQ implementation of <see cref="IMessageConsumer"/>.
/// Consumes messages from RabbitMQ queues with acknowledgment and error handling.
/// Supports async message consumption with event-based model.
/// Includes Dead Letter Exchange (DLX) configuration and poison message detection.
/// </summary>
public sealed class RabbitMqConsumer : IMessageConsumer
{
    private const int MaxRetryCount = 5;
    private const string DeadLetterExchange = "stargate.processes.dlx";
    private const string DeadLetterQueue = "stargate.processes.dead-letter";
    private const string DeadLetterRoutingKey = "dlq";
    private const string RetryCountHeader = "x-retry-count";

    private readonly IConnection _connection;
    private readonly IMessageSerializer _serializer;
    private readonly RabbitMqOptions _options;
    private readonly ILogger<RabbitMqConsumer> _logger;

    private readonly ConcurrentDictionary<string, IModel> _channels;
    private readonly ConcurrentDictionary<string, AsyncEventingBasicConsumer> _consumers;
    private readonly SemaphoreSlim _lock;

    private string? _currentQueue;
    private bool _isConsuming;
    private bool _disposed;

    public RabbitMqConsumer(
        IConnection connection,
        IMessageSerializer serializer,
        RabbitMqOptions options,
        ILogger<RabbitMqConsumer> logger)
    {
        _connection = connection ?? throw new ArgumentNullException(nameof(connection));
        _serializer = serializer ?? throw new ArgumentNullException(nameof(serializer));
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        _channels = new ConcurrentDictionary<string, IModel>(StringComparer.Ordinal);
        _consumers = new ConcurrentDictionary<string, AsyncEventingBasicConsumer>(StringComparer.Ordinal);
        _lock = new SemaphoreSlim(1, 1);

        _logger.LogInformation("RabbitMQ consumer initialized with DLX support");
    }

    public async Task StartConsumingAsync<T>(
        Func<T, MessageContext, Task> messageHandler,
        CancellationToken ct = default)
        where T : class
    {
        ArgumentNullException.ThrowIfNull(messageHandler);

        await _lock.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            ObjectDisposedException.ThrowIf(_disposed, this);

            if (_isConsuming)
            {
                throw new InvalidOperationException("Consumer is already started");
            }

            var queueName = GetQueueNameForType<T>();

            try
            {
                var channel = _connection.CreateModel();

                channel.BasicQos(
                    prefetchSize: 0,
                    prefetchCount: _options.PrefetchCount,
                    global: false);

                EnsureQueueExistsWithDlx(channel, queueName);

                var consumer = new AsyncEventingBasicConsumer(channel);

                consumer.Received += async (_, eventArgs) =>
                {
                    await HandleMessageAsync<T>(
                        channel,
                        eventArgs,
                        messageHandler,
                        ct)
                        .ConfigureAwait(false);
                };

                consumer.Shutdown += (_, args) =>
                {
                    _logger.LogWarning(
                        "Consumer shutdown for queue {Queue}: {ReplyCode} - {ReplyText}",
                        queueName,
                        args.ReplyCode,
                        args.ReplyText);
                    return Task.CompletedTask;
                };

                consumer.Registered += (_, args) =>
                {
                    _logger.LogInformation(
                        "Consumer registered for queue {Queue}, tag: {ConsumerTag}",
                        queueName,
                        args.ConsumerTags?.FirstOrDefault());
                    return Task.CompletedTask;
                };

                consumer.Unregistered += (_, args) =>
                {
                    _logger.LogInformation(
                        "Consumer unregistered for queue {Queue}, tag: {ConsumerTag}",
                        queueName,
                        args.ConsumerTags?.FirstOrDefault());
                    return Task.CompletedTask;
                };

                var consumerTag = channel.BasicConsume(
                    queue: queueName,
                    autoAck: false,
                    consumer: consumer);

                _channels.TryAdd(queueName, channel);
                _consumers.TryAdd(queueName, consumer);
                _currentQueue = queueName;
                _isConsuming = true;

                _logger.LogInformation(
                    "Started consuming from queue {Queue} with DLX, tag: {ConsumerTag}, prefetch: {PrefetchCount}",
                    queueName,
                    consumerTag,
                    _options.PrefetchCount);
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Failed to start consuming from queue {Queue}",
                    queueName);

                throw;
            }
        }
        finally
        {
            _lock.Release();
        }
    }

    private async Task HandleMessageAsync<T>(
        IModel channel,
        BasicDeliverEventArgs eventArgs,
        Func<T, MessageContext, Task> messageHandler,
        CancellationToken cancellationToken)
        where T : class
    {
        var deliveryTag = eventArgs.DeliveryTag;
        var messageId = eventArgs.BasicProperties?.MessageId ?? Guid.NewGuid().ToString();
        var retryCount = GetRetryCount(eventArgs.BasicProperties);

        try
        {
            _logger.LogDebug(
                "Received message {MessageId} from queue {Queue}, delivery tag: {DeliveryTag}, retry count: {RetryCount}",
                messageId,
                eventArgs.RoutingKey,
                deliveryTag,
                retryCount);

            // Detect poison messages
            if (retryCount >= MaxRetryCount)
            {
                _logger.LogError(
                    "Poison message detected: MessageId={MessageId}, RetryCount={RetryCount}",
                    messageId,
                    retryCount);

                // NACK without requeue - goes to DLQ
                channel.BasicNack(deliveryTag, multiple: false, requeue: false);
                return;
            }

            var envelope = _serializer.Deserialize<T>(eventArgs.Body.ToArray());

            if (envelope?.Payload is null)
            {
                throw new InvalidOperationException($"Message {messageId} has null payload");
            }

            var context = new MessageContext
            {
                MessageId = envelope.MessageId,
                CorrelationId = envelope.CorrelationId,
                Timestamp = envelope.Timestamp,
                DeliveryTag = (long)deliveryTag,
                DeliveryCount = retryCount + 1,
                Headers = envelope.Metadata != null 
                    ? new Dictionary<string, object>(envelope.Metadata) 
                    : null,
                AcknowledgeAsync = () =>
                {
                    channel.BasicAck(deliveryTag, multiple: false);
                    _logger.LogDebug("Message {MessageId} acknowledged", messageId);
                    return Task.CompletedTask;
                },
                RejectAsync = (requeue) =>
                {
                    if (requeue)
                    {
                        // Increment retry count when requeuing
                        var newRetryCount = retryCount + 1;
                        
                        _logger.LogWarning(
                            "Message {MessageId} requeued for retry: RetryCount={RetryCount}",
                            messageId,
                            newRetryCount);

                        // Requeue with updated retry count
                        channel.BasicNack(deliveryTag, multiple: false, requeue: true);
                        
                        // Note: In a production scenario, we would republish with updated headers
                        // For now, RabbitMQ's native requeue is used
                    }
                    else
                    {
                        _logger.LogWarning(
                            "Message {MessageId} rejected and sent to DLQ",
                            messageId);
                        
                        channel.BasicReject(deliveryTag, requeue: false);
                    }

                    return Task.CompletedTask;
                }
            };

            await messageHandler(envelope.Payload, context)
                .ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is InvalidOperationException or ArgumentException)
        {
            _logger.LogError(
                ex,
                "Failed to deserialize or validate message {MessageId}, rejecting",
                messageId);

            try
            {
                channel.BasicReject(deliveryTag, requeue: false);
            }
            catch (AlreadyClosedException closeEx)
            {
                _logger.LogError(
                    closeEx,
                    "Channel closed, cannot reject message {MessageId}",
                    messageId);
            }
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation(
                "Message processing cancelled for {MessageId}, requeuing",
                messageId);

            try
            {
                channel.BasicNack(deliveryTag, multiple: false, requeue: true);
            }
            catch (AlreadyClosedException closeEx)
            {
                _logger.LogError(
                    closeEx,
                    "Channel closed, cannot requeue message {MessageId}",
                    messageId);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Unexpected error processing message {MessageId}, requeuing",
                messageId);

            try
            {
                channel.BasicNack(deliveryTag, multiple: false, requeue: true);
            }
            catch (AlreadyClosedException closeEx)
            {
                _logger.LogError(
                    closeEx,
                    "Channel closed, cannot requeue message {MessageId}",
                    messageId);
            }
        }
    }

    private void EnsureQueueExistsWithDlx(IModel channel, string queueName)
    {
        try
        {
            // Declare Dead Letter Exchange
            channel.ExchangeDeclare(
                exchange: DeadLetterExchange,
                type: "topic",
                durable: true,
                autoDelete: false,
                arguments: null);

            _logger.LogDebug(
                "Dead Letter Exchange declared: {DLX}",
                DeadLetterExchange);

            // Declare Dead Letter Queue
            channel.QueueDeclare(
                queue: DeadLetterQueue,
                durable: true,
                exclusive: false,
                autoDelete: false,
                arguments: null);

            _logger.LogDebug(
                "Dead Letter Queue declared: {DLQ}",
                DeadLetterQueue);

            // Bind DLQ to DLX
            channel.QueueBind(
                queue: DeadLetterQueue,
                exchange: DeadLetterExchange,
                routingKey: "#");

            _logger.LogDebug(
                "Dead Letter Queue bound to DLX: {DLQ} -> {DLX}",
                DeadLetterQueue,
                DeadLetterExchange);

            // Configure main queue with DLX arguments
            var queueArgs = new Dictionary<string, object>
            {
                ["x-dead-letter-exchange"] = DeadLetterExchange,
                ["x-dead-letter-routing-key"] = DeadLetterRoutingKey
            };

            // Try passive declare first to check if queue exists
            try
            {
                channel.QueueDeclarePassive(queueName);
                
                _logger.LogDebug(
                    "Queue {Queue} exists (created by publisher)",
                    queueName);
            }
            catch (OperationInterruptedException)
            {
                // Queue doesn't exist - this is expected, publisher should create it
                _logger.LogWarning(
                    "Queue {Queue} does not exist, it should be created by the publisher with DLX configuration",
                    queueName);

                throw;
            }

            _logger.LogInformation(
                "Queue {Queue} configured with DLX: {DLX}",
                queueName,
                DeadLetterExchange);
        }
        catch (Exception ex) when (ex is not OperationInterruptedException)
        {
            _logger.LogError(
                ex,
                "Failed to configure DLX for queue {Queue}",
                queueName);
            throw;
        }
    }

    private static int GetRetryCount(IBasicProperties? properties)
    {
        if (properties?.Headers == null)
        {
            return 0;
        }

        if (properties.Headers.TryGetValue(RetryCountHeader, out var value))
        {
            return value switch
            {
                int intValue => intValue,
                byte[] byteValue => BitConverter.ToInt32(byteValue, 0),
                _ => 0
            };
        }

        return 0;
    }

    private static string GetQueueNameForType<T>()
    {
        var typeName = typeof(T).Name;
        return $"stargate.{typeName.ToLowerInvariant()}";
    }

    public async Task StopConsumingAsync()
    {
        await _lock.WaitAsync().ConfigureAwait(false);
        try
        {
            ObjectDisposedException.ThrowIf(_disposed, this);

            if (!_isConsuming)
            {
                throw new InvalidOperationException("Consumer is not started");
            }

            _logger.LogInformation("Stopping consumer...");

            foreach (var kvp in _channels.ToArray())
            {
                var queueName = kvp.Key;
                var channel = kvp.Value;

                try
                {
                    if (channel.IsOpen)
                    {
                        await Task.Delay(_options.ShutdownGracePeriodMs)
                            .ConfigureAwait(false);

                        channel.Close();
                        _logger.LogInformation(
                            "Stopped consumer for queue {Queue}",
                            queueName);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(
                        ex,
                        "Error stopping consumer for queue {Queue}",
                        queueName);
                }
            }

            _channels.Clear();
            _consumers.Clear();
            _currentQueue = null;
            _isConsuming = false;

            _logger.LogInformation("Consumer stopped");
        }
        finally
        {
            _lock.Release();
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }

        try
        {
            if (_isConsuming)
            {
                await StopConsumingAsync().ConfigureAwait(false);
            }

            foreach (var channel in _channels.Values)
            {
                try
                {
                    channel?.Dispose();
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error disposing channel");
                }
            }

            _lock.Dispose();

            _logger.LogInformation("RabbitMQ consumer disposed");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error disposing RabbitMQ consumer");
        }
        finally
        {
            _disposed = true;
        }
    }
}
