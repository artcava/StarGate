namespace StarGate.Core.Domain;

/// <summary>
/// Represents the lifecycle status of a process.
/// State transitions follow a defined workflow:
/// Pending → (Accepted | Rejected)
/// Accepted → (Processing | Failed | Rejected)
/// Processing → (Completed | Failed | Retrying)
/// Retrying → (Processing | Failed)
/// Completed, Failed, Rejected are terminal states.
/// </summary>
public enum ProcessStatus
{
    /// <summary>
    /// Process has been submitted and is awaiting initial validation.
    /// Initial state after submission before acceptance decision.
    /// Transition: Can move to Accepted (validation passed) or Rejected (validation failed).
    /// </summary>
    Pending = 0,

    /// <summary>
    /// Process has been accepted and queued for processing.
    /// State after successful submission and validation.
    /// Transition: Can move to Processing when picked up by a worker, Failed, or Rejected.
    /// </summary>
    Accepted = 1,

    /// <summary>
    /// Process is currently being executed by a worker.
    /// Active processing is in progress.
    /// Transition: Can move to Completed (success), Failed (error), or Retrying (recoverable error).
    /// </summary>
    Processing = 2,

    /// <summary>
    /// Process has completed successfully.
    /// Final state - Result property will contain the output data.
    /// Transition: Terminal state, no further transitions.
    /// </summary>
    Completed = 3,

    /// <summary>
    /// Process has failed permanently due to an error.
    /// Error property will contain failure details.
    /// Terminal state when retry limit exceeded or non-retryable error.
    /// Transition: Terminal state, no further transitions.
    /// </summary>
    Failed = 4,

    /// <summary>
    /// Process is waiting to be retried after a recoverable failure.
    /// Indicates the process can be retried and has not exceeded retry limits.
    /// Transition: Can move back to Processing (retry attempt) or Failed (retry limit exceeded).
    /// </summary>
    Retrying = 5,

    /// <summary>
    /// Process has been rejected due to validation failure.
    /// Occurs when business rules or validation constraints are not met.
    /// Transition: Terminal state, no further transitions.
    /// </summary>
    Rejected = 6
}
