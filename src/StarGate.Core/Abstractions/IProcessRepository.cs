using StarGate.Core.Domain;

namespace StarGate.Core.Abstractions;

/// <summary>
/// Repository for process persistence operations.
/// Provides data access abstraction following the Repository Pattern.
/// Implementations handle persistence to MongoDB or other data stores.
/// </summary>
public interface IProcessRepository
{
    /// <summary>
    /// Creates a new process in the repository.
    /// Validates uniqueness constraints (ProcessId, IdempotencyKey, ClientId+ClientProcessId).
    /// </summary>
    /// <param name="process">Process to create.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Created process.</returns>
    /// <exception cref="InvalidOperationException">If process with same ID already exists.</exception>
    /// <exception cref="ArgumentNullException">If process is null.</exception>
    Task<Process> CreateAsync(Process process, CancellationToken ct = default);

    /// <summary>
    /// Retrieves a process by its unique identifier.
    /// Primary lookup method for process retrieval.
    /// </summary>
    /// <param name="processId">Process identifier.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Process if found, null otherwise.</returns>
    Task<Process?> GetByIdAsync(Guid processId, CancellationToken ct = default);

    /// <summary>
    /// Retrieves a process by client ID and client process ID.
    /// Used for idempotency checks to prevent duplicate process submissions.
    /// Enforces unique constraint: (ClientId, ClientProcessId) pair must be unique.
    /// </summary>
    /// <param name="clientId">Client identifier.</param>
    /// <param name="clientProcessId">Client-provided process identifier.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Process if found, null otherwise.</returns>
    Task<Process?> GetByClientProcessIdAsync(
        string clientId,
        string clientProcessId,
        CancellationToken ct = default);

    /// <summary>
    /// Updates an existing process.
    /// Uses optimistic concurrency based on UpdatedAt timestamp.
    /// </summary>
    /// <param name="process">Process with updated values.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Updated process.</returns>
    /// <exception cref="InvalidOperationException">If process not found or concurrency conflict.</exception>
    /// <exception cref="ArgumentNullException">If process is null.</exception>
    Task<Process> UpdateAsync(Process process, CancellationToken ct = default);

    /// <summary>
    /// Retrieves processes by status.
    /// Used by background workers to find processes ready for execution.
    /// Results ordered by CreatedAt ascending (FIFO processing).
    /// </summary>
    /// <param name="status">Status to filter by.</param>
    /// <param name="limit">Maximum number of results (default 100, max 1000).</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>List of processes with specified status.</returns>
    Task<IReadOnlyList<Process>> GetByStatusAsync(
        ProcessStatus status,
        int limit = 100,
        CancellationToken ct = default);

    /// <summary>
    /// Retrieves processes for a specific client.
    /// Supports pagination for client-facing APIs.
    /// Results ordered by CreatedAt descending (most recent first).
    /// </summary>
    /// <param name="clientId">Client identifier.</param>
    /// <param name="skip">Number of records to skip (for pagination).</param>
    /// <param name="limit">Maximum number of results (default 100, max 1000).</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>List of processes for the client.</returns>
    Task<IReadOnlyList<Process>> GetByClientIdAsync(
        string clientId,
        int skip = 0,
        int limit = 100,
        CancellationToken ct = default);

    /// <summary>
    /// Counts active processes for a client and process type.
    /// Used for concurrency limit enforcement (MaxConcurrentProcesses policy).
    /// Active = Accepted or Processing status.
    /// </summary>
    /// <param name="clientId">Client identifier.</param>
    /// <param name="processType">Process type.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Number of active processes.</returns>
    Task<int> CountActiveProcessesAsync(
        string clientId,
        string processType,
        CancellationToken ct = default);
}
