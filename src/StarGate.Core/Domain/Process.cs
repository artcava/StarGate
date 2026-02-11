using System.Text.Json;

namespace StarGate.Core.Domain;

/// <summary>
/// Represents a business process submitted through StarGate.
/// Immutable record to ensure data integrity and support event sourcing patterns.
/// </summary>
public record Process
{
    /// <summary>
    /// Server-generated unique identifier (GUID).
    /// This is the primary identifier used internally by StarGate.
    /// </summary>
    public required Guid ProcessId { get; init; }

    /// <summary>
    /// Client-provided unique identifier for correlation.
    /// Used by clients to track and reference their processes.
    /// </summary>
    public required string ClientProcessId { get; init; }

    /// <summary>
    /// Type of process (e.g., "order", "shipping", "invoice").
    /// Used to determine which handler processes this request and which policies apply.
    /// </summary>
    public required string ProcessType { get; init; }

    /// <summary>
    /// Client identifier from authentication token.
    /// Identifies which client system submitted this process.
    /// </summary>
    public required string ClientId { get; init; }

    /// <summary>
    /// Current status of the process.
    /// Indicates the lifecycle state: Accepted, Processing, Completed, or Failed.
    /// </summary>
    public required ProcessStatus Status { get; init; }

    /// <summary>
    /// Progress percentage (0-100).
    /// Provides visibility into long-running process execution.
    /// </summary>
    public int Progress { get; init; }

    /// <summary>
    /// Current processing step (optional).
    /// Human-readable description of the current operation being performed.
    /// </summary>
    public string? CurrentStep { get; init; }

    /// <summary>
    /// Input data for the process as a JSON document.
    /// Contains the business data required for process execution.
    /// Stored as JsonDocument for type-safe access and efficient serialization.
    /// </summary>
    public JsonDocument? Data { get; init; }

    /// <summary>
    /// Process result (populated when completed).
    /// Contains the output data produced by successful process execution.
    /// Stored as JsonDocument for type-safe access and efficient serialization.
    /// </summary>
    public JsonDocument? Result { get; init; }

    /// <summary>
    /// Error details (populated when failed).
    /// Contains structured error information when the process fails.
    /// </summary>
    public ProcessError? Error { get; init; }

    /// <summary>
    /// Timestamp when process was created (UTC).
    /// Records the initial submission time.
    /// </summary>
    public required DateTime CreatedAt { get; init; }

    /// <summary>
    /// Timestamp when process was last updated (UTC).
    /// Updated whenever the process state changes.
    /// </summary>
    public required DateTime UpdatedAt { get; init; }

    /// <summary>
    /// Timestamp when process completed or failed (UTC).
    /// Records the final state transition time.
    /// </summary>
    public DateTime? CompletedAt { get; init; }

    /// <summary>
    /// Idempotency key to prevent duplicate submissions.
    /// Guarantees that retrying the same request doesn't create duplicate processes.
    /// </summary>
    public required string IdempotencyKey { get; init; }

    /// <summary>
    /// Indicates if the process can be retried after failure.
    /// Determined by process type policies and error classification.
    /// </summary>
    public bool Retryable { get; init; }
}
