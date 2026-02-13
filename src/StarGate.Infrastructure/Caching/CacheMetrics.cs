using System.Diagnostics.Metrics;

namespace StarGate.Infrastructure.Caching;

/// <summary>
/// Provides OpenTelemetry metrics for cache operations.
/// Tracks hits, misses, errors, and operation duration for observability.
/// </summary>
public class CacheMetrics
{
    private readonly Counter<long> _cacheHits;
    private readonly Counter<long> _cacheMisses;
    private readonly Counter<long> _cacheErrors;
    private readonly Histogram<double> _cacheOperationDuration;

    /// <summary>
    /// Initializes cache metrics with OpenTelemetry meter.
    /// </summary>
    /// <param name="meterFactory">Meter factory for creating instruments.</param>
    /// <exception cref="ArgumentNullException">If meterFactory is null.</exception>
    public CacheMetrics(IMeterFactory meterFactory)
    {
        ArgumentNullException.ThrowIfNull(meterFactory);

        var meter = meterFactory.Create("StarGate.Cache");

        _cacheHits = meter.CreateCounter<long>(
            "cache.hits",
            description: "Number of cache hits");

        _cacheMisses = meter.CreateCounter<long>(
            "cache.misses",
            description: "Number of cache misses");

        _cacheErrors = meter.CreateCounter<long>(
            "cache.errors",
            description: "Number of cache errors");

        _cacheOperationDuration = meter.CreateHistogram<double>(
            "cache.operation.duration",
            unit: "ms",
            description: "Duration of cache operations in milliseconds");
    }

    /// <summary>
    /// Records a cache hit.
    /// </summary>
    public void RecordHit() => _cacheHits.Add(1);

    /// <summary>
    /// Records a cache miss.
    /// </summary>
    public void RecordMiss() => _cacheMisses.Add(1);

    /// <summary>
    /// Records a cache error.
    /// </summary>
    public void RecordError() => _cacheErrors.Add(1);

    /// <summary>
    /// Records the duration of a cache operation.
    /// </summary>
    /// <param name="milliseconds">Duration in milliseconds.</param>
    public void RecordOperationDuration(double milliseconds) =>
        _cacheOperationDuration.Record(milliseconds);
}
