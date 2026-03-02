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
    public Task<Process> CreateAsync(Process process, CancellationToken ct = default);

    /// <summary>
    /// Retrieves a process by its unique identifier.
    /// Primary lookup method for process retrieval.
    /// </summary>
    /// <param name="processId">Process identifier.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Process if found, null otherwise.</returns>
    public Task<Process?> GetByIdAsync(Guid processId, CancellationToken ct = default);

    /// <summary>
    /// Retrieves a process by client ID and idempotency key.
    /// Used for idempotency checks to prevent duplicate process submissions.
    /// Idempotency keys are scoped per client (same key from different clients = different processes).
    /// </summary>
    /// <param name="clientId">Client identifier.</param>
    /// <param name="idempotencyKey">Idempotency key.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Process if found, null otherwise.</returns>
    public Task<Process?> GetByIdempotencyKeyAsync(
        string clientId,
        string idempotencyKey,
        CancellationToken ct = default);

    /// <summary>
    /// Retrieves a process by client ID and client process ID.
    /// Used for idempotency checks to prevent duplicate process submissions.
    /// Enforces unique constraint: (ClientId, ClientProcessId) pair must be unique.
    /// </summary>
    /// <param name="clientId">Client identifier.</param>
    /// <param name="clientProcessId">Client-provided process identifier.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Process if found, null otherwise.</returns>
    public Task<Process?> GetByClientProcessIdAsync(
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
    public Task<Process> UpdateAsync(Process process, CancellationToken ct = default);

    /// <summary>
    /// Retrieves processes by status.
    /// Used by background workers to find processes ready for execution.
    /// Results ordered by CreatedAt ascending (FIFO processing).
    /// </summary>
    /// <param name="status">Status to filter by.</param>
    /// <param name="limit">Maximum number of results (default 100, max 1000).</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>List of processes with specified status.</returns>
    public Task<IReadOnlyList<Process>> GetByStatusAsync(
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
    public Task<IReadOnlyList<Process>> GetByClientIdAsync(
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
    public Task<int> CountActiveProcessesAsync(
        string clientId,
        string processType,
        CancellationToken ct = default);

    /// <summary>
    /// Counts the number of running processes for a specific type and client.
    /// Running processes include those in Accepted or Running status.
    /// Used to enforce the MaxConcurrentExecutions policy constraint.
    /// </summary>
    /// <param name="processType">Process type to count.</param>
    /// <param name="clientId">Client identifier.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Number of running processes for the specified type and client.</returns>
    public Task<int> CountRunningProcessesByTypeAsync(
        string processType,
        string clientId,
        CancellationToken ct = default);

    /// <summary>
    /// Gets processes that have expired based on their retention policy.
    /// Returns processes in terminal states (Completed, Failed, Cancelled) where
    /// RetentionExpiresAt is less than or equal to the specified expiration date.
    /// Results are limited to prevent excessive memory usage.
    /// </summary>
    /// <param name="expirationDate">The expiration cutoff date (typically DateTime.UtcNow).</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>List of expired processes ready for cleanup (max 1000 per call).</returns>
    public Task<IReadOnlyList<Process>> GetExpiredProcessesAsync(
        DateTime expirationDate,
        CancellationToken ct = default);

    /// <summary>
    /// Gets all active processes that have exceeded their timeout.
    /// Active processes include Accepted, Processing, and Retrying states.
    /// Used by TimeoutScannerWorker to identify processes that need timeout enforcement.
    /// Results are limited to 100 per call for batch processing efficiency.
    /// </summary>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>List of timed-out processes (max 100 per call).</returns>
    public Task<IReadOnlyList<Process>> GetTimedOutProcessesAsync(
        CancellationToken ct = default);
}
