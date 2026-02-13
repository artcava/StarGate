using Microsoft.Extensions.Logging;
using StarGate.Core.Abstractions;

namespace StarGate.Infrastructure.Caching;

/// <summary>
/// Extension methods for IStateStore.
/// Provides batch operations and utility methods for cache management.
/// </summary>
public static class StateStoreExtensions
{
    /// <summary>
    /// Invalidates multiple process caches in parallel.
    /// Errors are logged but don't stop the batch operation.
    /// </summary>
    /// <param name="stateStore">State store instance.</param>
    /// <param name="processIds">Collection of process IDs to invalidate.</param>
    /// <param name="logger">Optional logger for error tracking.</param>
    /// <returns>Task representing the async operation.</returns>
    /// <exception cref="ArgumentNullException">If stateStore or processIds is null.</exception>
    public static async Task InvalidateBatchAsync(
        this IStateStore stateStore,
        IEnumerable<Guid> processIds,
        ILogger? logger = null)
    {
        ArgumentNullException.ThrowIfNull(stateStore);
        ArgumentNullException.ThrowIfNull(processIds);

        var ids = processIds.ToList();
        if (ids.Count == 0)
        {
            logger?.LogDebug("No process IDs provided for batch invalidation");
            return;
        }

        var tasks = ids.Select(async id =>
        {
            try
            {
                await stateStore.InvalidateAsync(id);
            }
            catch (Exception ex)
            {
                logger?.LogError(
                    ex,
                    "Error invalidating cache for process {ProcessId}",
                    id);
            }
        });

        await Task.WhenAll(tasks);

        logger?.LogInformation(
            "Batch cache invalidation completed for {Count} processes",
            ids.Count);
    }

    /// <summary>
    /// Checks if multiple processes are cached.
    /// Returns a dictionary mapping process IDs to their cache existence status.
    /// </summary>
    /// <param name="stateStore">State store instance.</param>
    /// <param name="processIds">Collection of process IDs to check.</param>
    /// <returns>Dictionary mapping process ID to existence status (true if cached, false otherwise).</returns>
    /// <exception cref="ArgumentNullException">If stateStore or processIds is null.</exception>
    public static async Task<Dictionary<Guid, bool>> ExistsBatchAsync(
        this IStateStore stateStore,
        IEnumerable<Guid> processIds)
    {
        ArgumentNullException.ThrowIfNull(stateStore);
        ArgumentNullException.ThrowIfNull(processIds);

        var ids = processIds.ToList();
        if (ids.Count == 0)
        {
            return new Dictionary<Guid, bool>();
        }

        var tasks = ids.Select(async id =>
        {
            try
            {
                var exists = await stateStore.ExistsAsync(id);
                return (id: id, exists: exists);
            }
            catch (Exception)
            {
                // On error, assume not cached (fail gracefully)
                return (id: id, exists: false);
            }
        });

        var results = await Task.WhenAll(tasks);

        return results.ToDictionary(r => r.id, r => r.exists);
    }
}
