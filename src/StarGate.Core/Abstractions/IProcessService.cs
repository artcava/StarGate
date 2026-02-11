using StarGate.Core.Domain;
using StarGate.Contracts.Requests;

namespace StarGate.Core.Abstractions;

/// <summary>
/// Core service for process lifecycle management.
/// Orchestrates process submission, retrieval, and status updates.
/// Implements cache-aside pattern and coordinates with repository and message broker.
/// </summary>
public interface IProcessService
{
    /// <summary>
    /// Submits a new process for asynchronous execution.
    /// Handles idempotency by checking for existing process with same client process ID.
    /// Publishes accepted process to message broker for asynchronous processing.
    /// </summary>
    /// <param name="clientId">Client identifier from authentication.</param>
    /// <param name="request">Process submission request with payload and metadata.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Created process (or existing if idempotent request).</returns>
    /// <exception cref="ArgumentNullException">If clientId or request is null.</exception>
    /// <exception cref="InvalidOperationException">If concurrency limit exceeded.</exception>
    public Task<Process> SubmitProcessAsync(
        string clientId,
        SubmitProcessRequest request,
        CancellationToken ct = default);

    /// <summary>
    /// Retrieves a process by its unique identifier.
    /// Checks cache first (IStateStore), then falls back to repository.
    /// Implements cache-aside pattern for performance.
    /// </summary>
    /// <param name="processId">Process identifier.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Process if found, null otherwise.</returns>
    /// <exception cref="ArgumentException">If processId is empty.</exception>
    public Task<Process?> GetProcessByIdAsync(Guid processId, CancellationToken ct = default);

    /// <summary>
    /// Retrieves a process by client process identifier.
    /// Used for idempotency checks during submission.
    /// Queries repository directly (not cached).
    /// </summary>
    /// <param name="clientId">Client identifier.</param>
    /// <param name="clientProcessId">Client-provided process identifier.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Process if found, null otherwise.</returns>
    /// <exception cref="ArgumentNullException">If clientId or clientProcessId is null.</exception>
    public Task<Process?> GetProcessByClientProcessIdAsync(
        string clientId,
        string clientProcessId,
        CancellationToken ct = default);

    /// <summary>
    /// Updates process status and related fields.
    /// Invalidates cache and persists changes to repository.
    /// Sets CompletedAt timestamp for terminal states (Completed, Failed, Cancelled).
    /// </summary>
    /// <param name="processId">Process identifier.</param>
    /// <param name="status">New status.</param>
    /// <param name="progress">Progress percentage (0-100). Default is 0.</param>
    /// <param name="currentStep">Current processing step description. Null if not applicable.</param>
    /// <param name="result">Process result (for completed processes). Null if not completed.</param>
    /// <param name="error">Error details (for failed processes). Null if no error.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Updated process.</returns>
    /// <exception cref="ArgumentException">If processId is empty or progress is out of range.</exception>
    /// <exception cref="InvalidOperationException">If process not found.</exception>
    public Task<Process> UpdateProcessStatusAsync(
        Guid processId,
        ProcessStatus status,
        int progress = 0,
        string? currentStep = null,
        object? result = null,
        ProcessError? error = null,
        CancellationToken ct = default);

    /// <summary>
    /// Lists processes for a specific client.
    /// Supports filtering by status and pagination.
    /// Results are ordered by creation date (newest first).
    /// </summary>
    /// <param name="clientId">Client identifier.</param>
    /// <param name="status">Optional status filter. Null returns all statuses.</param>
    /// <param name="skip">Number of records to skip (for pagination). Default is 0.</param>
    /// <param name="limit">Maximum number of results (1-1000). Default is 100.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>List of processes matching criteria.</returns>
    /// <exception cref="ArgumentNullException">If clientId is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException">If skip is negative or limit is out of range.</exception>
    public Task<IReadOnlyList<Process>> ListProcessesAsync(
        string clientId,
        ProcessStatus? status = null,
        int skip = 0,
        int limit = 100,
        CancellationToken ct = default);

    /// <summary>
    /// Checks if client can submit new process based on concurrency limits.
    /// Queries active processes (Accepted, Processing) for client and type.
    /// Compares count against policy concurrency limit.
    /// </summary>
    /// <param name="clientId">Client identifier.</param>
    /// <param name="processType">Process type.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>True if submission is allowed, false if limit exceeded.</returns>
    /// <exception cref="ArgumentNullException">If clientId or processType is null.</exception>
    public Task<bool> CanSubmitProcessAsync(
        string clientId,
        string processType,
        CancellationToken ct = default);
}
