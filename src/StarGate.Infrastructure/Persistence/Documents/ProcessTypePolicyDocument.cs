namespace StarGate.Infrastructure.Persistence.Documents;

using MongoDB.Bson.Serialization.Attributes;

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
    /// Maximum execution timeout in seconds.
    /// </summary>
    [BsonElement("timeoutSeconds")]
    public required int TimeoutSeconds { get; init; }

    /// <summary>
    /// Whether retry mechanism is enabled.
    /// </summary>
    [BsonElement("retryEnabled")]
    public required bool RetryEnabled { get; init; }

    /// <summary>
    /// Maximum number of retry attempts.
    /// </summary>
    [BsonElement("retryMaxAttempts")]
    public required int RetryMaxAttempts { get; init; }

    /// <summary>
    /// Initial retry delay in seconds.
    /// </summary>
    [BsonElement("retryInitialDelaySeconds")]
    public required int RetryInitialDelaySeconds { get; init; }

    /// <summary>
    /// Backoff strategy (Linear, Exponential, Constant).
    /// </summary>
    [BsonElement("retryBackoffStrategy")]
    public required string RetryBackoffStrategy { get; init; }

    /// <summary>
    /// Maximum retry delay in seconds.
    /// </summary>
    [BsonElement("retryMaxDelaySeconds")]
    public required int RetryMaxDelaySeconds { get; init; }

    /// <summary>
    /// Result retention period in days.
    /// </summary>
    [BsonElement("resultRetentionDays")]
    public required int ResultRetentionDays { get; init; }

    /// <summary>
    /// Maximum concurrent processes allowed.
    /// </summary>
    [BsonElement("maxConcurrentProcesses")]
    public required int MaxConcurrentProcesses { get; init; }

    /// <summary>
    /// Last update timestamp.
    /// </summary>
    [BsonElement("updatedAt")]
    [BsonDateTimeOptions(Kind = DateTimeKind.Utc)]
    public required DateTime UpdatedAt { get; init; }
}
