namespace StarGate.Infrastructure.Messaging;

using StarGate.Core.Abstractions;

/// <summary>
/// Factory for creating message envelopes with consistent metadata.
/// </summary>
public static class MessageEnvelopeFactory
{
    /// <summary>
    /// Creates a message envelope with automatic metadata population.
    /// </summary>
    /// <typeparam name="T">Type of the message payload.</typeparam>
    /// <param name="payload">The message payload.</param>
    /// <param name="correlationId">Optional correlation ID.</param>
    /// <param name="metadata">Optional additional metadata.</param>
    /// <returns>A new message envelope.</returns>
    /// <exception cref="ArgumentNullException">Thrown when payload is null.</exception>
    public static MessageEnvelope<T> Create<T>(
        T payload,
        string? correlationId = null,
        Dictionary<string, object>? metadata = null) where T : class
    {
        ArgumentNullException.ThrowIfNull(payload);

        return new MessageEnvelope<T>
        {
            MessageId = Guid.NewGuid().ToString(),
            CorrelationId = correlationId,
            MessageType = typeof(T).FullName ?? typeof(T).Name,
            Timestamp = DateTime.UtcNow,
            Payload = payload,
            Metadata = metadata
        };
    }
}
