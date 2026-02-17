namespace StarGate.Core.Abstractions;

/// <summary>
/// Context information for a received message.
/// Provides metadata and acknowledgment mechanisms.
/// Used by message handlers to acknowledge or reject messages.
/// </summary>
public record MessageContext
{
    /// <summary>
    /// Unique identifier of the message.
    /// Assigned by the broker.
    /// Used for logging and tracking.
    /// </summary>
    public required string MessageId { get; init; }

    /// <summary>
    /// Correlation identifier for tracking related messages.
    /// Used in request/response patterns to match responses.
    /// Null if not set by publisher.
    /// </summary>
    public string? CorrelationId { get; init; }

    /// <summary>
    /// Timestamp when message was received.
    /// Set by consumer when message is dequeued.
    /// Useful for measuring processing latency.
    /// </summary>
    public required DateTime Timestamp { get; init; }

    /// <summary>
    /// Broker-specific delivery tag for acknowledgment.
    /// Used internally by broker to track message delivery.
    /// Required for acknowledging or rejecting messages.
    /// </summary>
    public required long DeliveryTag { get; init; }

    /// <summary>
    /// Number of times this message has been delivered.
    /// Incremented on each redelivery (after rejection with requeue=true).
    /// Useful for detecting poison messages (high delivery count).
    /// First delivery has count = 1.
    /// </summary>
    public int DeliveryCount { get; init; }

    /// <summary>
    /// Acknowledges successful message processing.
    /// Removes message from queue permanently.
    /// Should be called after handler completes successfully.
    /// </summary>
    /// <returns>Task representing the asynchronous acknowledgment.</returns>
    public required Func<Task> AcknowledgeAsync { get; init; }

    /// <summary>
    /// Rejects message processing.
    /// Delegate accepts a boolean parameter: true to requeue message for retry, false to move to dead-letter queue.
    /// Message can be requeued for retry or moved to dead-letter queue.
    /// Should be called when handler fails and message needs retry.
    /// </summary>
    /// <returns>Task representing the asynchronous rejection.</returns>
    public required Func<bool, Task> RejectAsync { get; init; }

    /// <summary>
    /// Custom headers from the message.
    /// Contains metadata set by publisher via MessageProperties.Headers.
    /// Read-only dictionary for safe access.
    /// Null if no headers were set.
    /// </summary>
    public IReadOnlyDictionary<string, object>? Headers { get; init; }
}
