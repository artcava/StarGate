# Cache Invalidation Strategy

## Overview

This document describes the cache invalidation strategies implemented in StarGate to ensure data consistency between Redis cache and MongoDB database.

## Patterns Implemented

### 1. Cache-Aside (Lazy Loading)

**Used in**: `ProcessService.GetProcessByIdAsync`

```csharp
public async Task<Process?> GetProcessByIdAsync(Guid processId)
{
    // 1. Try cache first
    var cached = await _cache.GetProcessAsync(processId);
    if (cached is not null)
        return cached;

    // 2. Cache miss - fetch from database
    var process = await _repository.GetByIdAsync(processId);

    // 3. Populate cache for future requests
    if (process is not null)
        await _cache.SetProcessAsync(process);

    return process;
}
```

**Benefits**:
- Cache only what's actually requested
- No stale data on startup
- Natural cache warming

**Trade-offs**:
- First request slower (cache miss)
- Potential cache stampede on popular items

### 2. Write-Through Caching

**Used in**: `ProcessService.SubmitProcessAsync`

```csharp
public async Task<Process> SubmitProcessAsync(...)
{
    var process = new Process { ... };

    // 1. Write to database first (source of truth)
    await _repository.CreateAsync(process);

    // 2. Then cache (write-through)
    await _cache.SetProcessAsync(process);

    // 3. Publish to message broker
    await _messageBroker.PublishAsync(...);

    return process;
}
```

**Benefits**:
- New entities immediately available in cache
- Fast subsequent reads
- Consistent data

**Trade-offs**:
- Slight write latency increase
- Cache overhead even for rarely-read items

### 3. Write-Invalidate (Cache Invalidation on Update)

**Used in**: `ProcessService.UpdateProcessStatusAsync`

```csharp
public async Task<Process> UpdateProcessStatusAsync(...)
{
    var updated = process with { Status = status, ... };

    // 1. Write to database first
    await _repository.UpdateAsync(updated);

    // 2. Invalidate cache
    await _cache.InvalidateAsync(processId);

    return updated;
}
```

**Benefits**:
- Guarantees fresh data on next read
- Simpler than cache update
- No risk of cache/DB inconsistency

**Trade-offs**:
- Next read is slower (cache miss)
- More database load for frequently updated items

**Alternative**: Write-through update (cache the updated value immediately)

## Cache Stampede Prevention

### Problem

When cache expires for a popular item, multiple concurrent requests try to fetch from database simultaneously, overwhelming it.

### Solution: CacheLockManager

```csharp
public class EnhancedProcessService
{
    private readonly CacheLockManager _lockManager;

    public async Task<Process?> GetProcessByIdAsync(Guid processId)
    {
        // Try cache first
        var cached = await _cache.GetProcessAsync(processId);
        if (cached is not null)
            return cached;

        // Use lock to ensure only one thread fetches
        return await _lockManager.ExecuteWithLockAsync(
            processId,
            async () =>
            {
                // Double-check cache (another thread may have populated it)
                var rechecked = await _cache.GetProcessAsync(processId);
                if (rechecked is not null)
                    return rechecked;

                // Fetch from database
                var process = await _repository.GetByIdAsync(processId);
                
                // Populate cache
                if (process is not null)
                    await _cache.SetProcessAsync(process);

                return process;
            });
    }
}
```

**How it works**:
1. First request acquires semaphore, fetches from DB
2. Concurrent requests wait on semaphore
3. When first completes, others read from cache
4. Semaphore auto-disposed when no longer needed

## Batch Operations

### Batch Invalidation

**Use case**: Invalidate multiple related processes

```csharp
using StarGate.Infrastructure.Caching;

public async Task InvalidateClientProcessesAsync(string clientId)
{
    var processes = await _repository.GetByClientIdAsync(clientId);
    var processIds = processes.Select(p => p.ProcessId);

    // Invalidate all in parallel
    await _cache.InvalidateBatchAsync(processIds, _logger);
}
```

**Features**:
- Parallel execution with `Task.WhenAll`
- Error handling per item (doesn't fail entire batch)
- Optional logging

### Batch Existence Check

**Use case**: Check which processes are cached before fetching

```csharp
public async Task<List<Process>> GetProcessesBatchAsync(List<Guid> processIds)
{
    // Check which are cached
    var existenceMap = await _cache.ExistsBatchAsync(processIds);

    var cached = new List<Process>();
    var toFetch = new List<Guid>();

    foreach (var id in processIds)
    {
        if (existenceMap[id])
        {
            var process = await _cache.GetProcessAsync(id);
            if (process is not null)
                cached.Add(process);
            else
                toFetch.Add(id); // Race condition - fetch from DB
        }
        else
        {
            toFetch.Add(id);
        }
    }

    // Fetch missing from database
    var fetched = await _repository.GetByIdsAsync(toFetch);
    
    // Cache fetched items
    foreach (var process in fetched)
        await _cache.SetProcessAsync(process);

    return cached.Concat(fetched).ToList();
}
```

## Cache Metrics

### Available Metrics

```csharp
public class CacheMetrics
{
    public void RecordHit();              // cache.hits counter
    public void RecordMiss();             // cache.misses counter
    public void RecordError();            // cache.errors counter
    public void RecordOperationDuration(double ms); // cache.operation.duration histogram
}
```

### Metrics Collection

Metrics are automatically collected by `RedisStateStore` on every operation:

```csharp
public async Task<Process?> GetProcessAsync(Guid processId)
{
    var stopwatch = Stopwatch.StartNew();
    try
    {
        var cached = await db.StringGetAsync(key);
        
        if (!cached.HasValue)
        {
            _metrics?.RecordMiss();
            return null;
        }

        _metrics?.RecordHit();
        return JsonSerializer.Deserialize<Process>(cached);
    }
    catch (Exception)
    {
        _metrics?.RecordError();
        throw;
    }
    finally
    {
        _metrics?.RecordOperationDuration(stopwatch.Elapsed.TotalMilliseconds);
    }
}
```

### Monitoring with Prometheus

```promql
# Cache hit rate
rate(cache_hits[5m]) / (rate(cache_hits[5m]) + rate(cache_misses[5m]))

# Cache error rate
rate(cache_errors[5m])

# P95 operation duration
histogram_quantile(0.95, rate(cache_operation_duration_bucket[5m]))
```

### Grafana Dashboard Example

```json
{
  "title": "Cache Performance",
  "panels": [
    {
      "title": "Hit Rate",
      "targets": [
        {
          "expr": "rate(cache_hits[5m]) / (rate(cache_hits[5m]) + rate(cache_misses[5m]))"
        }
      ]
    },
    {
      "title": "Operation Duration (P95)",
      "targets": [
        {
          "expr": "histogram_quantile(0.95, rate(cache_operation_duration_bucket[5m]))"
        }
      ]
    }
  ]
}
```

## Best Practices

### 1. Always Invalidate on Update

```csharp
// ❌ BAD: Update DB but not cache
await _repository.UpdateAsync(process);

// ✅ GOOD: Invalidate cache after update
await _repository.UpdateAsync(process);
await _cache.InvalidateAsync(process.ProcessId);
```

### 2. Handle Cache Failures Gracefully

Cache operations should never break application flow:

```csharp
// ❌ BAD: Let cache errors propagate
var cached = await _cache.GetProcessAsync(processId); // May throw

// ✅ GOOD: Cache implementation handles errors internally
public async Task<Process?> GetProcessAsync(Guid processId)
{
    try
    {
        // Cache operation
    }
    catch (RedisException ex)
    {
        _logger.LogError(ex, "Cache error");
        return null; // Fail gracefully
    }
}
```

### 3. Use Appropriate TTL

```csharp
// Different TTL for different data types
var ttl = process.Status switch
{
    ProcessStatus.Accepted or ProcessStatus.Processing => TimeSpan.FromMinutes(5),
    ProcessStatus.Completed or ProcessStatus.Failed => TimeSpan.FromHours(1),
    _ => TimeSpan.FromMinutes(15)
};
```

### 4. Batch Operations for Efficiency

```csharp
// ❌ BAD: Sequential invalidation
foreach (var id in processIds)
    await _cache.InvalidateAsync(id);

// ✅ GOOD: Parallel batch invalidation
await _cache.InvalidateBatchAsync(processIds, _logger);
```

### 5. Monitor Cache Performance

```csharp
// Always register metrics for observability
services.AddSingleton<CacheMetrics>();

// Check metrics regularly
// - Hit rate should be > 80%
// - Miss rate spike = potential issue
// - Error rate should be near 0%
// - P95 duration should be < 10ms
```

## Troubleshooting

### Issue: Cache Hit Rate Low (<50%)

**Possible causes**:
1. TTL too short
2. Frequent updates causing invalidations
3. Access pattern not cache-friendly

**Solutions**:
```csharp
// Increase TTL
var ttl = TimeSpan.FromHours(1); // Was 15 minutes

// Use write-through instead of write-invalidate
await _repository.UpdateAsync(updated);
await _cache.SetProcessAsync(updated); // Update instead of invalidate

// Pre-warm cache for known hot items
var popular = await _repository.GetPopularProcessesAsync();
foreach (var process in popular)
    await _cache.SetProcessAsync(process);
```

### Issue: Cache Stampede Detected

**Symptoms**: DB load spike when cache expires

**Solution**:
```csharp
// Use CacheLockManager
private readonly CacheLockManager _lockManager;

public async Task<Process?> GetProcessByIdAsync(Guid processId)
{
    var cached = await _cache.GetProcessAsync(processId);
    if (cached is not null)
        return cached;

    return await _lockManager.ExecuteWithLockAsync(
        processId,
        async () =>
        {
            // Double-check and fetch
        });
}
```

### Issue: Stale Data in Cache

**Cause**: Missed invalidation after update

**Solution**:
```csharp
// Audit all update paths
public async Task UpdateProcessAsync(Process process)
{
    await _repository.UpdateAsync(process);
    await _cache.InvalidateAsync(process.ProcessId); // Don't forget!
}

// Consider defensive invalidation
public async Task<Process?> GetProcessByIdAsync(Guid processId)
{
    var cached = await _cache.GetProcessAsync(processId);
    if (cached is not null)
    {
        // Optional: Check if stale
        if (DateTime.UtcNow - cached.UpdatedAt > TimeSpan.FromMinutes(5))
        {
            await _cache.InvalidateAsync(processId);
            cached = null; // Force DB fetch
        }
    }
    // ...
}
```

### Issue: High Cache Error Rate

**Possible causes**:
1. Redis connection issues
2. Serialization errors
3. Memory pressure

**Diagnostics**:
```bash
# Check Redis connectivity
redis-cli ping

# Check memory
redis-cli info memory

# Check error logs
grep "cache.errors" application.log
```

**Solutions**:
```csharp
// Add Redis health check
services.AddHealthChecks()
    .AddRedis(redisConnectionString);

// Configure memory limits
redis-cli config set maxmemory 2gb
redis-cli config set maxmemory-policy allkeys-lru

// Add retry policy
services.AddSingleton<IConnectionMultiplexer>(sp =>
{
    var options = ConfigurationOptions.Parse(connectionString);
    options.ConnectRetry = 5;
    options.ReconnectRetryPolicy = new ExponentialRetry(5000);
    return ConnectionMultiplexer.Connect(options);
});
```

## Testing

### Unit Tests

```csharp
[Fact]
public async Task GetProcessByIdAsync_CacheHit_ReturnsCachedProcess()
{
    // Arrange
    var processId = Guid.NewGuid();
    var cachedProcess = new Process { ProcessId = processId };
    _mockCache.Setup(c => c.GetProcessAsync(processId))
        .ReturnsAsync(cachedProcess);

    // Act
    var result = await _service.GetProcessByIdAsync(processId);

    // Assert
    Assert.Equal(cachedProcess, result);
    _mockRepository.Verify(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
}

[Fact]
public async Task UpdateProcessStatusAsync_InvalidatesCache()
{
    // Arrange
    var processId = Guid.NewGuid();
    var existingProcess = new Process { ProcessId = processId };
    _mockRepository.Setup(r => r.GetByIdAsync(processId, default))
        .ReturnsAsync(existingProcess);

    // Act
    await _service.UpdateProcessStatusAsync(processId, ProcessStatus.Completed);

    // Assert
    _mockCache.Verify(c => c.InvalidateAsync(processId), Times.Once);
}
```

### Integration Tests

```csharp
[Fact]
public async Task CacheInvalidation_IntegrationTest()
{
    // Arrange
    var process = await CreateProcessAsync();
    
    // Warm cache
    var cached = await _service.GetProcessByIdAsync(process.ProcessId);
    Assert.NotNull(cached);

    // Act: Update process
    await _service.UpdateProcessStatusAsync(process.ProcessId, ProcessStatus.Completed);

    // Assert: Cache invalidated
    var exists = await _cache.ExistsAsync(process.ProcessId);
    Assert.False(exists);

    // Next read fetches from DB
    var updated = await _service.GetProcessByIdAsync(process.ProcessId);
    Assert.Equal(ProcessStatus.Completed, updated.Status);
}
```

## References

- [Microsoft: Cache-Aside Pattern](https://learn.microsoft.com/en-us/azure/architecture/patterns/cache-aside)
- [Redis: Cache Invalidation Strategies](https://redis.io/docs/manual/patterns/)
- [Martin Fowler: Cache Patterns](https://martinfowler.com/bliki/TwoHardThings.html)
- [StarGate: Redis State Store README](../src/StarGate.Infrastructure/Caching/README.md)
