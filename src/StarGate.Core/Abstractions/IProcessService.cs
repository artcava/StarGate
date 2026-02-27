using StarGate.Contracts.Requests;
using StarGate.Core.Domain;

namespace StarGate.Core.Abstractions;

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
}
