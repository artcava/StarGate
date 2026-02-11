using StarGate.Core.Domain;

namespace StarGate.Core.Abstractions;

/// <summary>
/// State store for caching process data.
/// Provides fast read access to frequently accessed process information.
/// Typically implemented with Redis or other in-memory data stores.
/// Complements IProcessRepository for read-heavy workloads.
/// </summary>
public interface IStateStore
{
    /// <summary>
    /// Retrieves a process from cache.
    /// Returns null for cache miss - caller should fallback to repository.
    /// </summary>
    /// <param name="processId">Process identifier.</param>
    /// <returns>Cached process if found, null otherwise.</returns>
    Task<Process?> GetProcessAsync(Guid processId);

    /// <summary>
    /// Stores a process in cache.
    /// Sets appropriate TTL based on process status:
    /// - Active (Accepted/Processing): short TTL (5-15 minutes)
    /// - Completed/Failed: longer TTL (1-24 hours)
    /// </summary>
    /// <param name="process">Process to cache.</param>
    /// <returns>Task representing the asynchronous operation.</returns>
    Task SetProcessAsync(Process process);

    /// <summary>
    /// Invalidates cached process data.
    /// Called after process updates to maintain cache consistency.
    /// Follows cache-aside pattern: invalidate on write, populate on read.
    /// </summary>
    /// <param name="processId">Process identifier.</param>
    /// <returns>Task representing the asynchronous operation.</returns>
    Task InvalidateAsync(Guid processId);

    /// <summary>
    /// Checks if a process exists in cache.
    /// Lightweight operation for existence checks without deserializing full process.
    /// Useful for fast idempotency checks.
    /// </summary>
    /// <param name="processId">Process identifier.</param>
    /// <returns>True if process is cached, false otherwise.</returns>
    Task<bool> ExistsAsync(Guid processId);

    /// <summary>
    /// Sets process status in cache with optimistic concurrency.
    /// Uses version-based optimistic locking to prevent race conditions.
    /// Returns false if version mismatch (expected version != actual version).
    /// Caller should retry operation with fresh version on conflict.
    /// </summary>
    /// <param name="processId">Process identifier.</param>
    /// <param name="status">New status.</param>
    /// <param name="expectedVersion">Expected version for optimistic locking.</param>
    /// <returns>True if update succeeded, false if version mismatch.</returns>
    Task<bool> TrySetStatusAsync(
        Guid processId,
        ProcessStatus status,
        long expectedVersion);
}
