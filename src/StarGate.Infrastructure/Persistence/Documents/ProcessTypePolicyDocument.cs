using MongoDB.Bson.Serialization.Attributes;

namespace StarGate.Infrastructure.Persistence.Documents;

/// <summary>
/// MongoDB document for ProcessTypePolicy configuration.
/// </summary>
public record ProcessTypePolicyDocument
{
    /// <summary>
    /// Process type identifier (used as document _id).
    /// </summary>
    [BsonId]
    [BsonElement("_id")]
    public required string ProcessType { get; init; }

    /// <summary>
    /// Maximum execution timeout.
    /// </summary>
    [BsonElement("timeout")]
    [BsonTimeSpanOptions(BsonType.Int64, TimeSpanUnits.Seconds)]
    public required TimeSpan Timeout { get; init; }

    /// <summary>
    /// Retry policy configuration.
    /// </summary>
    [BsonElement("retryPolicy")]
    public required RetryPolicyDocument RetryPolicy { get; init; }

    /// <summary>
    /// Result retention period.
    /// </summary>
    [BsonElement("resultRetention")]
    [BsonTimeSpanOptions(BsonType.Int64, TimeSpanUnits.Days)]
    public required TimeSpan ResultRetention { get; init; }

    /// <summary>
    /// Maximum concurrent processes allowed.
    /// </summary>
    [BsonElement("maxConcurrentProcesses")]
    [BsonIgnoreIfNull]
    public int? MaxConcurrentProcesses { get; init; }

    /// <summary>
    /// Last update timestamp.
    /// </summary>
    [BsonElement("updatedAt")]
    [BsonDateTimeOptions(Kind = DateTimeKind.Utc)]
    public required DateTime UpdatedAt { get; init; }
}

/// <summary>
/// MongoDB embedded document for RetryPolicy configuration.
/// </summary>
public record RetryPolicyDocument
{
    /// <summary>
    /// Whether retry mechanism is enabled.
    /// </summary>
    [BsonElement("enabled")]
    public required bool Enabled { get; init; }

    /// <summary>
    /// Maximum number of retry attempts.
    /// </summary>
    [BsonElement("maxAttempts")]
    public required int MaxAttempts { get; init; }

    /// <summary>
    /// Initial retry delay.
    /// </summary>
    [BsonElement("initialDelay")]
    [BsonTimeSpanOptions(BsonType.Int64, TimeSpanUnits.Seconds)]
    public required TimeSpan InitialDelay { get; init; }

    /// <summary>
    /// Backoff strategy (Linear, Exponential).
    /// </summary>
    [BsonElement("backoffStrategy")]
    [BsonRepresentation(BsonType.String)]
    public required string BackoffStrategy { get; init; }

    /// <summary>
    /// Maximum retry delay.
    /// </summary>
    [BsonElement("maxDelay")]
    [BsonTimeSpanOptions(BsonType.Int64, TimeSpanUnits.Seconds)]
    public required TimeSpan MaxDelay { get; init; }
}
