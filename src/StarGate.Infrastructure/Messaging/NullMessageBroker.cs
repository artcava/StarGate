using StarGate.Core.Abstractions;
using Microsoft.Extensions.Logging;

namespace StarGate.Infrastructure.Messaging;

/// <summary>
/// Null object implementation of IMessageBroker.
/// Used when RabbitMQ is disabled or unavailable.
/// </summary>
public class NullMessageBroker : IMessageBroker
{
    private readonly ILogger<NullMessageBroker> _logger;

    public NullMessageBroker(ILogger<NullMessageBroker> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _logger.LogWarning("NullMessageBroker is active - messages will not be published");
    }

    public Task PublishAsync<T>(
        string queueName,
        T message,
        CancellationToken ct = default) where T : class
    {
        _logger.LogWarning(
            "NullMessageBroker: Ignoring message publication to queue {Queue}",
            queueName);

        return Task.CompletedTask;
    }

    public Task PublishAsync<T>(
        string queueName,
        T message,
        MessageProperties properties,
        CancellationToken ct = default) where T : class
    {
        _logger.LogWarning(
            "NullMessageBroker: Ignoring message publication to queue {Queue}",
            queueName);

        return Task.CompletedTask;
    }

    public Task PublishAsync<T>(
        T message,
        string routingKey,
        CancellationToken cancellationToken = default) where T : class
    {
        _logger.LogWarning(
            "NullMessageBroker: Ignoring message publication with routing key {RoutingKey}",
            routingKey);

        return Task.CompletedTask;
    }

    public Task PublishWithDelayAsync<T>(
        T message,
        string routingKey,
        TimeSpan delay,
        CancellationToken cancellationToken = default) where T : class
    {
        _logger.LogWarning(
            "NullMessageBroker: Ignoring delayed message publication with routing key {RoutingKey}, delay {Delay}",
            routingKey,
            delay);

        return Task.CompletedTask;
    }

    public IMessageConsumer CreateConsumer(string queueName, CancellationToken cancellationToken = default)
    {
        _logger.LogWarning(
            "NullMessageBroker: Cannot create consumer for queue {Queue}",
            queueName);

        throw new NotSupportedException("NullMessageBroker does not support message consumers");
    }
}
