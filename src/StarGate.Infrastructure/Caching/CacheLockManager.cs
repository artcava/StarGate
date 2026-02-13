using System.Collections.Concurrent;

namespace StarGate.Infrastructure.Caching;

/// <summary>
/// Prevents cache stampede by ensuring only one thread fetches data on cache miss.
/// Uses semaphores to coordinate concurrent access to the same resource.
/// Automatically cleans up semaphores when no longer needed.
/// </summary>
public class CacheLockManager
{
    private readonly ConcurrentDictionary<Guid, SemaphoreSlim> _locks = new();

    /// <summary>
    /// Executes a function with exclusive lock on the specified key.
    /// Only one thread can execute the function for a given key at a time.
    /// Other threads will wait until the first completes.
    /// </summary>
    /// <typeparam name="T">Return type of the fetch function.</typeparam>
    /// <param name="key">Unique key to lock on (typically process ID).</param>
    /// <param name="fetchFunction">Function to execute while holding the lock.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Result of the fetch function.</returns>
    /// <exception cref="ArgumentNullException">If fetchFunction is null.</exception>
    public async Task<T> ExecuteWithLockAsync<T>(
        Guid key,
        Func<Task<T>> fetchFunction,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(fetchFunction);

        // Get or create semaphore for this key
        var semaphore = _locks.GetOrAdd(key, _ => new SemaphoreSlim(1, 1));

        await semaphore.WaitAsync(ct);
        try
        {
            return await fetchFunction();
        }
        finally
        {
            semaphore.Release();

            // Clean up semaphore if no longer needed
            // CurrentCount == 1 means no threads are waiting
            if (semaphore.CurrentCount == 1)
            {
                if (_locks.TryRemove(key, out var removedSemaphore))
                {
                    // Only dispose if we successfully removed it
                    if (ReferenceEquals(removedSemaphore, semaphore))
                    {
                        removedSemaphore.Dispose();
                    }
                }
            }
        }
    }

    /// <summary>
    /// Gets the current number of active locks.
    /// Useful for monitoring and diagnostics.
    /// </summary>
    /// <returns>Number of currently active locks.</returns>
    public int GetActiveLockCount() => _locks.Count;
}
