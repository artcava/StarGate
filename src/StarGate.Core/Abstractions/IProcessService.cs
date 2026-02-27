namespace StarGate.Core.Abstractions;

using StarGate.Core.Domain;

/// <summary>
/// Service interface for managing process lifecycle and operations.
/// </summary>
public interface IProcessService
{
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
    /// <exception cref="ArgumentException">If required parameters are null or whitespace.</exception>
    /// <exception cref="DuplicateProcessException">If idempotency key already exists for the client.</exception>
    Task<Process> CreateProcessAsync(
        string clientId,
        string processType,
        string clientProcessId,
        string idempotencyKey,
        Dictionary<string, string>? metadata = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves a process by its unique identifier.
    /// </summary>
    /// <param name="processId">Process identifier (GUID).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The process entity.</returns>
    /// <exception cref="ProcessNotFoundException">If process not found.</exception>
    Task<Process> GetProcessAsync(
        Guid processId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves a process by client identifier and client process identifier.
    /// </summary>
    /// <param name="clientId">Client identifier.</param>
    /// <param name="clientProcessId">Client-provided process identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The process entity.</returns>
    /// <exception cref="ArgumentException">If required parameters are null or whitespace.</exception>
    /// <exception cref="ProcessNotFoundException">If process not found.</exception>
    Task<Process> GetProcessByClientIdAsync(
        string clientId,
        string clientProcessId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates the status of a process with validation of state transitions.
    /// Automatically updates timestamps based on the new status.
    /// </summary>
    /// <param name="processId">Process identifier.</param>
    /// <param name="newStatus">New status to set.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <exception cref="ProcessNotFoundException">If process not found.</exception>
    /// <exception cref="InvalidStateTransitionException">If state transition is not allowed.</exception>
    Task UpdateProcessStatusAsync(
        Guid processId,
        ProcessStatus newStatus,
        CancellationToken cancellationToken = default);
}
