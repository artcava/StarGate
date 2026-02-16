using Microsoft.Extensions.Logging;
using StarGate.Core.Abstractions;

namespace StarGate.Infrastructure.Messaging;

/// <summary>
/// Null object implementation of <see cref="IMessageConsumer"/>.
/// Used when RabbitMQ is disabled or unavailable.
/// Logs warnings and ignores all consumption requests.
/// </summary>
public sealed class NullMessageConsumer : IMessageConsumer
{
    private readonly ILogger<NullMessageConsumer> _logger;
    private bool _disposed;

    public NullMessageConsumer(ILogger<NullMessageConsumer> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _logger.LogWarning("NullMessageConsumer is active - no messages will be consumed");
    }

    public Task StartConsumingAsync<T>(
        Func<T, MessageContext, Task> messageHandler,
        CancellationToken ct = default)
        where T : class
    {
        ArgumentNullException.ThrowIfNull(messageHandler);
        ObjectDisposedException.ThrowIf(_disposed, this);

        var queueName = $"stargate.{typeof(T).Name.ToLowerInvariant()}";

        _logger.LogWarning(
            "NullMessageConsumer: Ignoring consume request for queue {Queue}",
            queueName);

        return Task.CompletedTask;
    }

    public Task StopConsumingAsync()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        return Task.CompletedTask;
    }

    public ValueTask DisposeAsync()
    {
        if (!_disposed)
        {
            _logger.LogInformation("NullMessageConsumer disposed");
            _disposed = true;
        }

        return ValueTask.CompletedTask;
    }
}
