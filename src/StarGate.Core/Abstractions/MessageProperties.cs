namespace StarGate.Core.Abstractions;

/// <summary>
/// Additional properties for message publishing.
/// Provides metadata for message routing, priority, expiration, and correlation.
/// Immutable record type ensures thread safety.
/// </summary>
public record MessageProperties
{
    /// <summary>
    /// Correlation identifier for request/response patterns.
    /// Used to match responses with original requests.
    /// Useful for distributed tracing and request tracking.
    /// </summary>
    public string? CorrelationId { get; init; }

    /// <summary>
    /// Unique message identifier.
    /// Auto-generated if not provided by broker implementation.
    /// Used for deduplication and tracking.
    /// </summary>
    public string? MessageId { get; init; }

    /// <summary>
    /// Message priority (0-255, higher is more important).
    /// Brokers may use this for message ordering.
    /// Default is 0 (normal priority).
    /// Not all brokers support priority queues.
    /// </summary>
    public int Priority { get; init; }

    /// <summary>
    /// Time-to-live for the message.
    /// Message expires if not consumed within this duration.
    /// Broker may move expired messages to dead-letter queue.
    /// Null means no expiration.
    /// </summary>
    public TimeSpan? TimeToLive { get; init; }

    /// <summary>
    /// Custom headers for additional metadata.
    /// Used for application-specific routing, filtering, or tracking.
    /// Values can be strings, numbers, booleans, or other serializable types.
    /// Null means no custom headers.
    /// </summary>
    public Dictionary<string, object>? Headers { get; init; }

    /// <summary>
    /// Content type of the message payload.
    /// Typically "application/json" for JSON serialization.
    /// Used by consumers to determine deserialization strategy.
    /// </summary>
    public string? ContentType { get; init; }

    /// <summary>
    /// Content encoding (e.g., "utf-8", "gzip").
    /// Indicates if message body is compressed or encoded.
    /// Null means default encoding (UTF-8).
    /// </summary>
    public string? ContentEncoding { get; init; }
}
