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
    /// Indicates the lifecycle state: Pending, Accepted, Processing, Completed, Failed, Retrying, or Rejected.
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
    /// Error details (populated when failed) - DEPRECATED.
    /// Use Errors list for comprehensive error tracking.
    /// Maintained for backward compatibility.
    /// </summary>
    public ProcessError? Error { get; init; }

    /// <summary>
    /// List of errors encountered during process execution.
    /// Tracks all errors including retryable and non-retryable failures.
    /// </summary>
    public List<ProcessErrorEntry>? Errors { get; init; }

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
    /// Timestamp when process completed successfully (UTC).
    /// Records the completion time for successful processes.
    /// </summary>
    public DateTime? CompletedAt { get; init; }

    /// <summary>
    /// Timestamp when process failed permanently (UTC).
    /// Records the failure time for permanently failed processes.
    /// </summary>
    public DateTime? FailedAt { get; init; }

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

    /// <summary>
    /// Timestamp when the process times out (calculated from policy).
    /// Derived from CreatedAt + policy.TimeoutSeconds.
    /// Used to identify processes that have exceeded their execution time limit.
    /// </summary>
    public DateTime? TimeoutAt { get; init; }

    /// <summary>
    /// Current retry count.
    /// Tracks how many times this process has been retried after failure.
    /// Used to enforce MaxRetries policy constraint.
    /// </summary>
    public int RetryCount { get; init; }

    /// <summary>
    /// Maximum retry attempts (from policy at creation time).
    /// Snapshot of the policy value when the process was created.
    /// Ensures consistent retry behavior even if policy changes later.
    /// </summary>
    public int MaxRetries { get; init; }

    /// <summary>
    /// Timestamp when the process expires and can be deleted (calculated from retention policy).
    /// Derived from CreatedAt + policy.RetentionDays.
    /// Used by cleanup jobs to identify processes eligible for deletion.
    /// Only applies to terminal states: Completed, Failed, Rejected.
    /// </summary>
    public DateTime? RetentionExpiresAt { get; init; }

    /// <summary>
    /// Checks if the process has exceeded its retry limit.
    /// True when RetryCount has reached or exceeded MaxRetries.
    /// </summary>
    public bool IsRetryLimitExceeded => RetryCount >= MaxRetries;

    /// <summary>
    /// Checks if the process has timed out.
    /// True when current UTC time has passed the TimeoutAt threshold.
    /// </summary>
    public bool IsTimedOut => TimeoutAt.HasValue && DateTime.UtcNow > TimeoutAt.Value;

    /// <summary>
    /// Checks if the process is in a terminal state.
    /// Terminal states: Completed, Failed, Rejected - no further transitions possible.
    /// </summary>
    public bool IsTerminal => Status is ProcessStatus.Completed or ProcessStatus.Failed or ProcessStatus.Rejected;

    /// <summary>
    /// Checks if the process is active (can be executed).
    /// Active states: Accepted, Processing, Retrying.
    /// </summary>
    public bool IsActive => Status is ProcessStatus.Accepted or ProcessStatus.Processing or ProcessStatus.Retrying;

    /// <summary>
    /// Adds an error to the process.
    /// Note: Since Process is immutable, this returns a new instance with the error added.
    /// </summary>
    /// <param name="errorCode">Error code for categorization.</param>
    /// <param name="message">Human-readable error message.</param>
    /// <param name="retryable">Indicates if this error is retryable.</param>
    /// <returns>New Process instance with the error added.</returns>
    public Process AddError(string errorCode, string message, bool retryable)
    {
        var errorList = new List<ProcessErrorEntry>(Errors ?? new List<ProcessErrorEntry>())
        {
            new ProcessErrorEntry
            {
                ErrorCode = errorCode,
                Message = message,
                Retryable = retryable,
                Timestamp = DateTime.UtcNow
            }
        };

        return this with { Errors = errorList };
    }
}

/// <summary>
/// Represents a single error entry in the process error history.
/// </summary>
public class ProcessErrorEntry
{
    /// <summary>
    /// Error code for categorization (e.g., "TIMEOUT", "VALIDATION_ERROR").
    /// </summary>
    public required string ErrorCode { get; init; }

    /// <summary>
    /// Human-readable error message.
    /// </summary>
    public required string Message { get; init; }

    /// <summary>
    /// Indicates if this error is retryable.
    /// </summary>
    public required bool Retryable { get; init; }

    /// <summary>
    /// Timestamp when this error occurred (UTC).
    /// </summary>
    public required DateTime Timestamp { get; init; }
}
