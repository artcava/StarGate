using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace StarGate.Infrastructure.Persistence;

/// <summary>
/// MongoDB document for process type default policies.
/// Maps to the 'processTypePolicies' collection.
/// Uses ProcessType as natural key (_id).
/// </summary>
public class ProcessTypePolicyDocument
{
    /// <summary>
    /// Process type identifier (used as _id in MongoDB).
    /// </summary>
    [BsonId]
    [BsonElement("_id")]
    public required string ProcessType { get; set; }

    /// <summary>
    /// Maximum execution time allowed for this process type.
    /// </summary>
    [BsonElement("timeout")]
    [BsonRequired]
    public required TimeSpan Timeout { get; set; }

    /// <summary>
    /// Retry policy configuration.
    /// </summary>
    [BsonElement("retryPolicy")]
    [BsonRequired]
    public required RetryPolicyDocument RetryPolicy { get; set; }

    /// <summary>
    /// How long to retain completed process results.
    /// </summary>
    [BsonElement("resultRetention")]
    [BsonRequired]
    public required TimeSpan ResultRetention { get; set; }

    /// <summary>
    /// Maximum number of concurrent processes allowed (optional).
    /// </summary>
    [BsonElement("maxConcurrentProcesses")]
    public int? MaxConcurrentProcesses { get; set; }

    /// <summary>
    /// Timestamp when policy was last updated (UTC).
    /// </summary>
    [BsonElement("updatedAt")]
    [BsonRequired]
    public required DateTime UpdatedAt { get; set; }
}

/// <summary>
/// MongoDB document for client-specific policy overrides.
/// Maps to the 'clientPolicyOverrides' collection.
/// Uses composite key (clientId:processType) as _id.
/// </summary>
public class ClientPolicyOverrideDocument
{
    /// <summary>
    /// Composite key: "{clientId}:{processType}" (managed by repository).
    /// This provides natural client+processType uniqueness and better query performance.
    /// </summary>
    [BsonId]
    public required string Id { get; set; }

    /// <summary>
    /// Client identifier this override applies to.
    /// </summary>
    [BsonElement("clientId")]
    [BsonRequired]
    public required string ClientId { get; set; }

    /// <summary>
    /// Process type this override applies to.
    /// </summary>
    [BsonElement("processType")]
    [BsonRequired]
    public required string ProcessType { get; set; }

    /// <summary>
    /// Override timeout (null means use default).
    /// </summary>
    [BsonElement("timeout")]
    public TimeSpan? Timeout { get; set; }

    /// <summary>
    /// Override retry policy (null means use default).
    /// </summary>
    [BsonElement("retryPolicy")]
    public RetryPolicyDocument? RetryPolicy { get; set; }

    /// <summary>
    /// Override result retention (null means use default).
    /// </summary>
    [BsonElement("resultRetention")]
    public TimeSpan? ResultRetention { get; set; }

    /// <summary>
    /// Override max concurrent processes (null means use default).
    /// </summary>
    [BsonElement("maxConcurrentProcesses")]
    public int? MaxConcurrentProcesses { get; set; }

    /// <summary>
    /// Timestamp when override was last updated (UTC).
    /// </summary>
    [BsonElement("updatedAt")]
    [BsonRequired]
    public required DateTime UpdatedAt { get; set; }
}

/// <summary>
/// MongoDB document for retry policy configuration.
/// Nested document within ProcessTypePolicyDocument and ClientPolicyOverrideDocument.
/// </summary>
public class RetryPolicyDocument
{
    /// <summary>
    /// Whether retry is enabled for this policy.
    /// </summary>
    [BsonElement("enabled")]
    [BsonRequired]
    public required bool Enabled { get; set; }

    /// <summary>
    /// Maximum number of retry attempts.
    /// </summary>
    [BsonElement("maxAttempts")]
    [BsonRequired]
    public required int MaxAttempts { get; set; }

    /// <summary>
    /// Initial delay before first retry.
    /// </summary>
    [BsonElement("initialDelay")]
    [BsonRequired]
    public required TimeSpan InitialDelay { get; set; }

    /// <summary>
    /// Backoff strategy (stored as string enum value).
    /// </summary>
    [BsonElement("backoffStrategy")]
    [BsonRequired]
    public required string BackoffStrategy { get; set; }

    /// <summary>
    /// Maximum delay between retries.
    /// </summary>
    [BsonElement("maxDelay")]
    [BsonRequired]
    public required TimeSpan MaxDelay { get; set; }
}
