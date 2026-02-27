using StarGate.Contracts.Requests;
using StarGate.Core.Domain;

namespace StarGate.Core.Abstractions;

/// <summary>
/// Service interface for managing process lifecycle and operations.
/// </summary>
public interface IProcessService
{
    // ============ Creation ============

    /// <summary>
    /// Creates a new process with automatic GUID generation.
    /// Handles idempotency to prevent duplicate process creation.
    /// </summary>
    /// <param name="clientId">Client identifier.</param>
    /// <param name="processType">Type of process (e.g., "order", "shipping").</param>
    /// <param name="clientProcessId">Client-provided process identifier.</param>
    /// <param name="idempotencyKey">Unique key to prevent duplicate submissions.</param>
    /// <param name="metadata">Optional metadata dictionary.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The created process with generated GUID.</returns>
    /// <exception cref="System.ArgumentException">If required parameters are null or whitespace.</exception>
    /// <exception cref="StarGate.Core.Exceptions.DuplicateProcessException">If idempotency key already exists for the client.</exception>
    public Task<Process> CreateProcessAsync(
        string clientId,
        string processType,
        string clientProcessId,
        string idempotencyKey,
        Dictionary<string, string>? metadata = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Submits a new process (alias for CreateProcessAsync for API compatibility).
    /// </summary>
    /// <param name="clientId">Client identifier.</param>
    /// <param name="request">Process submission request.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The created process with generated GUID.</returns>
    public Task<Process> SubmitProcessAsync(
        string clientId,
        SubmitProcessRequest request,
        CancellationToken cancellationToken = default);

    // ============ Retrieval ============

    /// <summary>
    /// Retrieves a process by its unique identifier.
    /// </summary>
    /// <param name="processId">Process identifier (GUID).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The process entity.</returns>
    /// <exception cref="StarGate.Core.Exceptions.ProcessNotFoundException">If process not found.</exception>
    public Task<Process> GetProcessAsync(
        Guid processId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves a process by its unique identifier (alias for GetProcessAsync for API compatibility).
    /// </summary>
    /// <param name="processId">Process identifier (GUID).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The process entity, or null if not found.</returns>
    public Task<Process?> GetProcessByIdAsync(
        Guid processId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves a process by client identifier and client process identifier.
    /// </summary>
    /// <param name="clientId">Client identifier.</param>
    /// <param name="clientProcessId">Client-provided process identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The process entity.</returns>
    /// <exception cref="System.ArgumentException">If required parameters are null or whitespace.</exception>
    /// <exception cref="StarGate.Core.Exceptions.ProcessNotFoundException">If process not found.</exception>
    public Task<Process> GetProcessByClientIdAsync(
        string clientId,
        string clientProcessId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves a process by client identifier and client process identifier (alias for API compatibility).
    /// </summary>
    /// <param name="clientId">Client identifier.</param>
    /// <param name="clientProcessId">Client-provided process identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The process entity, or null if not found.</returns>
    public Task<Process?> GetProcessByClientProcessIdAsync(
        string clientId,
        string clientProcessId,
        CancellationToken cancellationToken = default);

    // ============ State Management ============

    /// <summary>
    /// Updates the status of a process with validation of state transitions.
    /// Automatically updates timestamps based on the new status.
    /// </summary>
    /// <param name="processId">Process identifier.</param>
    /// <param name="newStatus">New status to set.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <exception cref="StarGate.Core.Exceptions.ProcessNotFoundException">If process not found.</exception>
    /// <exception cref="StarGate.Core.Exceptions.InvalidStateTransitionException">If state transition is not allowed.</exception>
    public Task UpdateProcessStatusAsync(
        Guid processId,
        ProcessStatus newStatus,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Transitions a process to Processing state.
    /// Validates that current state allows this transition.
    /// </summary>
    /// <param name="processId">Process identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <exception cref="StarGate.Core.Exceptions.ProcessNotFoundException">If process not found.</exception>
    /// <exception cref="StarGate.Core.Exceptions.InvalidStateTransitionException">If state transition is not allowed.</exception>
    public Task TransitionToProcessingAsync(
        Guid processId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Completes a process successfully.
    /// Sets status to Completed, progress to 100, and records completion timestamp.
    /// </summary>
    /// <param name="processId">Process identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <exception cref="StarGate.Core.Exceptions.ProcessNotFoundException">If process not found.</exception>
    /// <exception cref="StarGate.Core.Exceptions.InvalidStateTransitionException">If state transition is not allowed.</exception>
    public Task CompleteProcessAsync(
        Guid processId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Fails a process with error details.
    /// Determines whether to transition to Retrying or Failed based on retry policy.
    /// </summary>
    /// <param name="processId">Process identifier.</param>
    /// <param name="errorCode">Error code for categorization.</param>
    /// <param name="errorMessage">Human-readable error message.</param>
    /// <param name="canRetry">Indicates if the error is retryable.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <exception cref="StarGate.Core.Exceptions.ProcessNotFoundException">If process not found.</exception>
    public Task FailProcessAsync(
        Guid processId,
        string errorCode,
        string errorMessage,
        bool canRetry = false,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Rejects a process due to validation failure.
    /// Transitions from Pending to Rejected state.
    /// </summary>
    /// <param name="processId">Process identifier.</param>
    /// <param name="reason">Reason for rejection.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <exception cref="StarGate.Core.Exceptions.ProcessNotFoundException">If process not found.</exception>
    /// <exception cref="StarGate.Core.Exceptions.InvalidStateTransitionException">If state transition is not allowed.</exception>
    public Task RejectProcessAsync(
        Guid processId,
        string reason,
        CancellationToken cancellationToken = default);

    // ============ Progress Tracking ============

    /// <summary>
    /// Updates the progress of a process.
    /// Progress must be between 0 and 100.
    /// </summary>
    /// <param name="processId">Process identifier.</param>
    /// <param name="progress">Progress percentage (0-100).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <exception cref="StarGate.Core.Exceptions.ProcessNotFoundException">If process not found.</exception>
    /// <exception cref="System.ArgumentOutOfRangeException">If progress is not between 0 and 100.</exception>
    public Task UpdateProcessProgressAsync(
        Guid processId,
        int progress,
        CancellationToken cancellationToken = default);

    // ============ Error Handling ============

    /// <summary>
    /// Records an error for a process without changing its status.
    /// Useful for tracking non-fatal errors or warnings.
    /// </summary>
    /// <param name="processId">Process identifier.</param>
    /// <param name="errorCode">Error code for categorization.</param>
    /// <param name="message">Human-readable error message.</param>
    /// <param name="retryable">Indicates if this error is retryable.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <exception cref="StarGate.Core.Exceptions.ProcessNotFoundException">If process not found.</exception>
    public Task RecordProcessErrorAsync(
        Guid processId,
        string errorCode,
        string message,
        bool retryable = false,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Increments the retry count for a process.
    /// </summary>
    /// <param name="processId">Process identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <exception cref="StarGate.Core.Exceptions.ProcessNotFoundException">If process not found.</exception>
    public Task IncrementRetryCountAsync(
        Guid processId,
        CancellationToken cancellationToken = default);

    // ============ Timeout Management ============

    /// <summary>
    /// Checks if a process has timed out and fails it if necessary.
    /// </summary>
    /// <param name="processId">Process identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <exception cref="StarGate.Core.Exceptions.ProcessNotFoundException">If process not found.</exception>
    public Task CheckTimeoutAsync(
        Guid processId,
        CancellationToken cancellationToken = default);
}
