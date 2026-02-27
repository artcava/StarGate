namespace StarGate.Core.Abstractions;

/// <summary>
/// Service for managing idempotency of operations.
/// Provides caching and storage mechanisms to prevent duplicate process submissions.
/// </summary>
public interface IIdempotencyService
{
    /// <summary>
    /// Checks if an idempotency key has been used and returns the associated process ID.
    /// </summary>
    /// <param name="clientId">The client identifier.</param>
    /// <param name="idempotencyKey">The unique idempotency key.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The process ID if the key exists, null otherwise.</returns>
    public Task<Guid?> GetProcessIdByIdempotencyKeyAsync(
        string clientId,
        string idempotencyKey,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Stores an idempotency key association with a process.
    /// This reserves the key and associates it with the given process ID.
    /// </summary>
    /// <param name="clientId">The client identifier.</param>
    /// <param name="idempotencyKey">The unique idempotency key.</param>
    /// <param name="processId">The process ID to associate with the key.</param>
    /// <param name="expiration">Optional expiration time (defaults to 24 hours).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public Task StoreIdempotencyKeyAsync(
        string clientId,
        string idempotencyKey,
        Guid processId,
        TimeSpan? expiration = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Removes an idempotency key (used for cleanup or rollback scenarios).
    /// </summary>
    /// <param name="clientId">The client identifier.</param>
    /// <param name="idempotencyKey">The unique idempotency key.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public Task RemoveIdempotencyKeyAsync(
        string clientId,
        string idempotencyKey,
        CancellationToken cancellationToken = default);
}
