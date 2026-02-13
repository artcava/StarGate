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
        MessageProperties? properties = null,
        CancellationToken cancellationToken = default) where T : class
    {
        _logger.LogWarning(
            "NullMessageBroker: Ignoring message publication to queue {Queue}",
            queueName);

        return Task.CompletedTask;
    }
}
