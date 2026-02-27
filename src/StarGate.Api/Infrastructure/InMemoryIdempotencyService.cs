using System.Collections.Concurrent;
using StarGate.Core.Abstractions;

namespace StarGate.Api.Infrastructure;

/// <summary>
/// In-memory implementation of idempotency service for testing purposes.
/// NOT suitable for production use (not distributed, not persistent).
/// </summary>
public class InMemoryIdempotencyService : IIdempotencyService
{
    private readonly ConcurrentDictionary<string, (Guid ProcessId, DateTime Expiration)> _store = new();
    private static readonly TimeSpan _defaultExpiration = TimeSpan.FromHours(24);

    public Task<Guid?> GetProcessIdByIdempotencyKeyAsync(
        string clientId,
        string idempotencyKey,
        CancellationToken cancellationToken = default)
    {
        var key = BuildKey(clientId, idempotencyKey);

        if (_store.TryGetValue(key, out var entry))
        {
            // Check expiration
            if (entry.Expiration > DateTime.UtcNow)
            {
                return Task.FromResult<Guid?>(entry.ProcessId);
            }

            // Remove expired entry
            _store.TryRemove(key, out _);
        }

        return Task.FromResult<Guid?>(null);
    }

    public Task StoreIdempotencyKeyAsync(
        string clientId,
        string idempotencyKey,
        Guid processId,
        TimeSpan? expiration = null,
        CancellationToken cancellationToken = default)
    {
        var key = BuildKey(clientId, idempotencyKey);
        var expirationTime = DateTime.UtcNow + (expiration ?? _defaultExpiration);

        _store[key] = (processId, expirationTime);

        return Task.CompletedTask;
    }

    public Task RemoveIdempotencyKeyAsync(
        string clientId,
        string idempotencyKey,
        CancellationToken cancellationToken = default)
    {
        var key = BuildKey(clientId, idempotencyKey);
        _store.TryRemove(key, out _);

        return Task.CompletedTask;
    }

    private static string BuildKey(string clientId, string idempotencyKey)
    {
        return $"{clientId}:{idempotencyKey}";
    }

    /// <summary>
    /// Clears all stored idempotency keys (for testing).
    /// </summary>
    public void Clear()
    {
        _store.Clear();
    }
}
