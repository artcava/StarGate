using System.Collections.Concurrent;
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
/// </summary>
public sealed class RabbitMqConsumer : IMessageConsumer
{
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

        _logger.LogInformation("RabbitMQ consumer initialized");
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

            // Derive queue name from type T
            var queueName = GetQueueNameForType<T>();

            try
            {
                // Create dedicated channel for this consumer
                var channel = _connection.CreateModel();

                // Configure QoS - prefetch count for better throughput control
                channel.BasicQos(
                    prefetchSize: 0,
                    prefetchCount: _options.PrefetchCount,
                    global: false);

                // Ensure queue exists
                EnsureQueueExists(channel, queueName);

                // Create async consumer
                var consumer = new AsyncEventingBasicConsumer(channel);

                consumer.Received += async (_, eventArgs) =>
                {
                    await HandleMessageAsync(
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
                };

                consumer.Registered += (_, args) =>
                {
                    _logger.LogInformation(
                        "Consumer registered for queue {Queue}, tag: {ConsumerTag}",
                        queueName,
                        args.ConsumerTags?.FirstOrDefault());
                };

                consumer.Unregistered += (_, args) =>
                {
                    _logger.LogInformation(
                        "Consumer unregistered for queue {Queue}, tag: {ConsumerTag}",
                        queueName,
                        args.ConsumerTags?.FirstOrDefault());
                };

                // Start consuming
                var consumerTag = channel.BasicConsume(
                    queue: queueName,
                    autoAck: false, // Manual acknowledgment
                    consumer: consumer);

                _channels.TryAdd(queueName, channel);
                _consumers.TryAdd(queueName, consumer);
                _currentQueue = queueName;
                _isConsuming = true;

                _logger.LogInformation(
                    "Started consuming from queue {Queue}, tag: {ConsumerTag}, prefetch: {PrefetchCount}",
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
        var correlationId = eventArgs.BasicProperties?.CorrelationId;

        try
        {
            _logger.LogDebug(
                "Received message {MessageId} from queue {Queue}, delivery tag: {DeliveryTag}",
                messageId,
                eventArgs.RoutingKey,
                deliveryTag);

            // Deserialize message envelope
            var envelope = _serializer.Deserialize<MessageEnvelope<T>>(eventArgs.Body.ToArray());

            if (envelope?.Payload is null)
            {
                throw new InvalidOperationException($"Message {messageId} has null payload");
            }

            // Build message context with acknowledgment delegates
            var context = new MessageContext
            {
                MessageId = messageId,
                CorrelationId = correlationId,
                Timestamp = DateTime.UtcNow,
                DeliveryTag = deliveryTag,
                DeliveryCount = eventArgs.Redelivered ? 2 : 1, // Simplified delivery count
                Headers = envelope.Properties?.Headers,
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
                        channel.BasicNack(deliveryTag, multiple: false, requeue: true);
                        _logger.LogWarning("Message {MessageId} requeued for retry", messageId);
                    }
                    else
                    {
                        channel.BasicReject(deliveryTag, requeue: false);
                        _logger.LogWarning("Message {MessageId} rejected and sent to DLQ", messageId);
                    }

                    return Task.CompletedTask;
                }
            };

            // Invoke message handler
            await messageHandler(envelope.Payload, context)
                .ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is InvalidOperationException or ArgumentException)
        {
            _logger.LogError(
                ex,
                "Failed to deserialize or validate message {MessageId}, rejecting",
                messageId);

            // Can't deserialize or invalid - reject permanently
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

            // Operation cancelled - requeue for another worker
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

            // Unexpected error - requeue for retry
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

    private void EnsureQueueExists(IModel channel, string queueName)
    {
        try
        {
            // Passive declare to check if queue exists
            channel.QueueDeclarePassive(queueName);

            _logger.LogDebug(
                "Queue {Queue} exists",
                queueName);
        }
        catch (OperationInterruptedException)
        {
            _logger.LogWarning(
                "Queue {Queue} does not exist, it should be created by the publisher",
                queueName);

            throw;
        }
    }

    private static string GetQueueNameForType<T>()
    {
        // Convention: queue name based on type name in lowercase with dots
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
                        // Grace period for pending messages
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
