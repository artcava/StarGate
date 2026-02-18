using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace StarGate.Infrastructure.Persistence;

/// <summary>
/// MongoDB document representation of a Process.
/// Maps to the 'processes' collection.
/// </summary>
public class ProcessDocument
{
    /// <summary>
    /// Unique process identifier (GUID).
    /// Mapped to _id via BsonClassMap (not via attribute).
    /// </summary>
    public required Guid ProcessId { get; set; }

    /// <summary>
    /// Client-provided unique identifier for correlation.
    /// </summary>
    [BsonElement("clientProcessId")]
    [BsonRequired]
    public required string ClientProcessId { get; set; }

    /// <summary>
    /// Type of process (e.g., "order", "shipping").
    /// </summary>
    [BsonElement("processType")]
    [BsonRequired]
    public required string ProcessType { get; set; }

    /// <summary>
    /// Client identifier from authentication token.
    /// </summary>
    [BsonElement("clientId")]
    [BsonRequired]
    public required string ClientId { get; set; }

    /// <summary>
    /// Current status of the process (stored as string).
    /// </summary>
    [BsonElement("status")]
    [BsonRequired]
    public required string Status { get; set; }

    /// <summary>
    /// Progress percentage (0-100).
    /// </summary>
    [BsonElement("progress")]
    public int Progress { get; set; }

    /// <summary>
    /// Current processing step (optional).
    /// </summary>
    [BsonElement("currentStep")]
    public string? CurrentStep { get; set; }

    /// <summary>
    /// Input data for the process as a BSON document.
    /// </summary>
    [BsonElement("data")]
    public BsonDocument? Data { get; set; }

    /// <summary>
    /// Process result (populated when completed).
    /// </summary>
    [BsonElement("result")]
    public BsonDocument? Result { get; set; }

    /// <summary>
    /// Error details (populated when failed).
    /// </summary>
    [BsonElement("error")]
    public ErrorDocument? Error { get; set; }

    /// <summary>
    /// Timestamp when process was created (UTC).
    /// </summary>
    [BsonElement("createdAt")]
    [BsonRequired]
    public required DateTime CreatedAt { get; set; }

    /// <summary>
    /// Timestamp when process was last updated (UTC).
    /// </summary>
    [BsonElement("updatedAt")]
    [BsonRequired]
    public required DateTime UpdatedAt { get; set; }

    /// <summary>
    /// Timestamp when process completed or failed (UTC).
    /// </summary>
    [BsonElement("completedAt")]
    public DateTime? CompletedAt { get; set; }

    /// <summary>
    /// Idempotency key to prevent duplicate submissions.
    /// </summary>
    [BsonElement("idempotencyKey")]
    [BsonRequired]
    public required string IdempotencyKey { get; set; }

    /// <summary>
    /// Indicates if the process can be retried after failure.
    /// </summary>
    [BsonElement("retryable")]
    public bool Retryable { get; set; }

    /// <summary>
    /// Timestamp when the process times out (calculated from policy).
    /// </summary>
    [BsonElement("timeoutAt")]
    public DateTime? TimeoutAt { get; set; }

    /// <summary>
    /// Current retry count.
    /// </summary>
    [BsonElement("retryCount")]
    public int RetryCount { get; set; }

    /// <summary>
    /// Maximum retry attempts (from policy at creation time).
    /// </summary>
    [BsonElement("maxRetries")]
    public int MaxRetries { get; set; }

    /// <summary>
    /// Timestamp when the process expires and can be deleted (calculated from retention policy).
    /// </summary>
    [BsonElement("retentionExpiresAt")]
    public DateTime? RetentionExpiresAt { get; set; }
}

/// <summary>
/// MongoDB document representation of a ProcessError.
/// Nested document within ProcessDocument.
/// </summary>
public class ErrorDocument
{
    /// <summary>
    /// Error code for categorization.
    /// </summary>
    [BsonElement("code")]
    public required string Code { get; set; }

    /// <summary>
    /// Human-readable error message.
    /// </summary>
    [BsonElement("message")]
    public required string Message { get; set; }

    /// <summary>
    /// Additional error context as BSON document.
    /// </summary>
    [BsonElement("details")]
    public BsonDocument? Details { get; set; }
}
