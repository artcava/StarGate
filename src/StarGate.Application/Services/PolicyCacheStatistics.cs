namespace StarGate.Application.Services;

using System.Collections.Concurrent;

/// <summary>
/// Tracks cache hit/miss statistics for policy caching.
/// </summary>
public class PolicyCacheStatistics
{
    private long _hits;
    private long _misses;
    private long _evictions;
    private readonly ConcurrentDictionary<string, long> _hitsByKey;
    private readonly ConcurrentDictionary<string, long> _missesByKey;

    /// <summary>
    /// Initializes a new instance of the <see cref="PolicyCacheStatistics"/> class.
    /// </summary>
    public PolicyCacheStatistics()
    {
        _hitsByKey = new ConcurrentDictionary<string, long>();
        _missesByKey = new ConcurrentDictionary<string, long>();
    }

    /// <summary>
    /// Gets the total number of cache hits.
    /// </summary>
    public long Hits => Interlocked.Read(ref _hits);

    /// <summary>
    /// Gets the total number of cache misses.
    /// </summary>
    public long Misses => Interlocked.Read(ref _misses);

    /// <summary>
    /// Gets the total number of cache evictions.
    /// </summary>
    public long Evictions => Interlocked.Read(ref _evictions);

    /// <summary>
    /// Gets the total number of cache requests (hits + misses).
    /// </summary>
    public long TotalRequests => Hits + Misses;

    /// <summary>
    /// Gets the cache hit ratio (0.0 to 1.0).
    /// </summary>
    public double HitRatio => TotalRequests > 0
        ? (double)Hits / TotalRequests
        : 0.0;

    /// <summary>
    /// Records a cache hit for the specified key.
    /// </summary>
    /// <param name="cacheKey">The cache key that was hit.</param>
    public void RecordHit(string cacheKey)
    {
        Interlocked.Increment(ref _hits);
        _hitsByKey.AddOrUpdate(cacheKey, 1, (_, count) => count + 1);
    }

    /// <summary>
    /// Records a cache miss for the specified key.
    /// </summary>
    /// <param name="cacheKey">The cache key that was missed.</param>
    public void RecordMiss(string cacheKey)
    {
        Interlocked.Increment(ref _misses);
        _missesByKey.AddOrUpdate(cacheKey, 1, (_, count) => count + 1);
    }

    /// <summary>
    /// Records a cache eviction.
    /// </summary>
    public void RecordEviction()
    {
        Interlocked.Increment(ref _evictions);
    }

    /// <summary>
    /// Resets all statistics to zero.
    /// </summary>
    public void Reset()
    {
        Interlocked.Exchange(ref _hits, 0);
        Interlocked.Exchange(ref _misses, 0);
        Interlocked.Exchange(ref _evictions, 0);
        _hitsByKey.Clear();
        _missesByKey.Clear();
    }

    /// <summary>
    /// Gets detailed statistics for all cache keys.
    /// </summary>
    /// <returns>A dictionary of cache key statistics.</returns>
    public Dictionary<string, CacheKeyStatistics> GetKeyStatistics()
    {
        var stats = new Dictionary<string, CacheKeyStatistics>();

        var allKeys = _hitsByKey.Keys.Union(_missesByKey.Keys).Distinct();

        foreach (var key in allKeys)
        {
            _hitsByKey.TryGetValue(key, out var hits);
            _missesByKey.TryGetValue(key, out var misses);

            stats[key] = new CacheKeyStatistics
            {
                Key = key,
                Hits = hits,
                Misses = misses,
                TotalRequests = hits + misses,
                HitRatio = (hits + misses) > 0 ? (double)hits / (hits + misses) : 0.0
            };
        }

        return stats;
    }
}

/// <summary>
/// Represents cache statistics for a specific key.
/// </summary>
public class CacheKeyStatistics
{
    /// <summary>
    /// Gets or initializes the cache key.
    /// </summary>
    public required string Key { get; init; }

    /// <summary>
    /// Gets or initializes the number of hits for this key.
    /// </summary>
    public long Hits { get; init; }

    /// <summary>
    /// Gets or initializes the number of misses for this key.
    /// </summary>
    public long Misses { get; init; }

    /// <summary>
    /// Gets or initializes the total number of requests for this key.
    /// </summary>
    public long TotalRequests { get; init; }

    /// <summary>
    /// Gets or initializes the hit ratio for this key (0.0 to 1.0).
    /// </summary>
    public double HitRatio { get; init; }
}
