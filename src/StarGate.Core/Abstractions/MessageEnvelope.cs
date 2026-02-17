namespace StarGate.Core.Abstractions;

/// <summary>
/// Envelope wrapper for all messages published to the message broker.
/// Contains metadata and the actual message payload.
/// </summary>
/// <typeparam name="T">The type of the message payload.</typeparam>
public class MessageEnvelope<T> where T : class
{
    /// <summary>
    /// Unique message identifier.
    /// </summary>
    public required string MessageId { get; init; }

    /// <summary>
    /// Correlation identifier for tracking related messages.
    /// </summary>
    public string? CorrelationId { get; init; }

    /// <summary>
    /// Message type name for deserialization routing.
    /// </summary>
    public required string MessageType { get; init; }

    /// <summary>
    /// Timestamp when the message was created.
    /// </summary>
    public required DateTime Timestamp { get; init; }

    /// <summary>
    /// The actual message payload.
    /// </summary>
    public required T Payload { get; init; }

    /// <summary>
    /// Optional metadata for additional context.
    /// </summary>
    public Dictionary<string, object>? Metadata { get; init; }
}

/// <summary>
/// Non-generic version for deserialization scenarios where type is unknown.
/// </summary>
public class MessageEnvelope
{
    /// <summary>
    /// Unique message identifier.
    /// </summary>
    public required string MessageId { get; init; }

    /// <summary>
    /// Correlation identifier for tracking related messages.
    /// </summary>
    public string? CorrelationId { get; init; }

    /// <summary>
    /// Message type name for deserialization routing.
    /// </summary>
    public required string MessageType { get; init; }

    /// <summary>
    /// Timestamp when the message was created.
    /// </summary>
    public required DateTime Timestamp { get; init; }

    /// <summary>
    /// The actual message payload.
    /// </summary>
    public required object Payload { get; init; }

    /// <summary>
    /// Optional metadata for additional context.
    /// </summary>
    public Dictionary<string, object>? Metadata { get; init; }
}
