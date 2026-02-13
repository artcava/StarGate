namespace StarGate.Core.Abstractions;

/// <summary>
/// Properties for message publishing.
/// </summary>
public record MessageProperties
{
    /// <summary>
    /// Unique message identifier.
    /// </summary>
    public string? MessageId { get; init; }

    /// <summary>
    /// Correlation identifier for request/response patterns.
    /// </summary>
    public string? CorrelationId { get; init; }

    /// <summary>
    /// Message priority (0-10, higher is more important).
    /// </summary>
    public int? Priority { get; init; }

    /// <summary>
    /// Message expiration time (TTL).
    /// </summary>
    public TimeSpan? TimeToLive { get; init; }

    /// <summary>
    /// Custom headers for the message.
    /// </summary>
    public Dictionary<string, object>? Headers { get; init; }
}
