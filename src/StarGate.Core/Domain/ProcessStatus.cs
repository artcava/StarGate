namespace StarGate.Core.Domain;

/// <summary>
/// Represents the lifecycle status of a process.
/// State transitions follow a defined workflow: Accepted → Processing → (Completed | Failed).
/// </summary>
public enum ProcessStatus
{
    /// <summary>
    /// Process has been accepted and queued for processing.
    /// Initial state after successful submission and validation.
    /// Transition: Can move to Processing when picked up by a worker.
    /// </summary>
    Accepted = 0,

    /// <summary>
    /// Process is currently being executed by a worker.
    /// Active processing is in progress.
    /// Transition: Can move to Completed (success) or Failed (error).
    /// </summary>
    Processing = 1,

    /// <summary>
    /// Process has completed successfully.
    /// Final state - Result property will contain the output data.
    /// Transition: Terminal state, no further transitions.
    /// </summary>
    Completed = 2,

    /// <summary>
    /// Process has failed due to an error.
    /// Error property will contain failure details.
    /// Transition: May be retried (back to Processing) if Retryable is true.
    /// </summary>
    Failed = 3
}
