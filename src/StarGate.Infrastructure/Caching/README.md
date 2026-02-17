# Redis State Store

## Overview

RedisStateStore provides a high-performance caching layer for StarGate process data using Redis. It implements the `IStateStore` interface and follows fail-safe patterns to ensure cache failures don't disrupt application operations.

## Architecture

### Components

```
┌─────────────────────────────────────────┐
│         Application Layer               │
│    (Uses IStateStore interface)         │
└──────────────┬──────────────────────────┘
               │
               ▼
┌─────────────────────────────────────────┐
│         RedisStateStore                 │
│  - GetProcessAsync()                    │
│  - SetProcessAsync()                    │
│  - InvalidateAsync()                    │
│  - ExistsAsync()                        │
│  - TrySetStatusAsync()                  │
└──────────────┬──────────────────────────┘
               │
               ▼
┌─────────────────────────────────────────┐
│    IConnectionMultiplexer               │
│    (StackExchange.Redis)                │
└──────────────┬──────────────────────────┘
               │
               ▼
         [ Redis Server ]
```

### Design Patterns

#### 1. **Fail-Safe Pattern**
All Redis operations are wrapped in try-catch blocks. Failures are logged but don't throw exceptions:
- **Cache miss** → Caller fetches from repository
- **Cache set failure** → Operation continues without caching
- **Cache invalidation failure** → Logged but not critical

#### 2. **Cache-Aside Pattern**
- **Read**: Check cache → Cache miss → Fetch from DB → Cache result
- **Write**: Update DB → Invalidate cache → Next read repopulates

#### 3. **Null Object Pattern**
`NullStateStore` provides a no-op implementation when Redis is disabled, avoiding null checks throughout the codebase.

#### 4. **Optimistic Locking**
`TrySetStatusAsync` uses version-based concurrency control with Lua scripts for atomic operations.

## Configuration

### appsettings.json

```json
{
  "Redis": {
    "ConnectionString": "localhost:6379",
    "DefaultTtlSeconds": 3600,
    "Enabled": true
  }
}
```

### Configuration Options

| Option | Type | Default | Description |
|--------|------|---------|-------------|
| `ConnectionString` | string | required | Redis connection string. Format: `host:port` or `host:port,password=xxx,ssl=true` |
| `DefaultTtlSeconds` | int | 3600 | Default TTL for cached items in seconds (1 hour) |
| `Enabled` | bool | true | Enable/disable Redis caching. When false, uses NullStateStore |

### Connection String Formats

```
# Simple local development
localhost:6379

# With password
localhost:6379,password=mySecretPassword

# SSL/TLS enabled
redis.example.com:6380,ssl=true,password=xxx

# Azure Redis Cache
mycache.redis.cache.windows.net:6380,password=xxx,ssl=true,abortConnect=false

# AWS ElastiCache
my-cluster.xxxxxx.0001.use1.cache.amazonaws.com:6379,ssl=true
```

## Usage Examples

### Basic Cache Operations

```csharp
public class ProcessService
{
    private readonly IStateStore _cache;
    private readonly IProcessRepository _repository;

    public async Task<Process?> GetProcessAsync(Guid processId)
    {
        // Try cache first
        var cached = await _cache.GetProcessAsync(processId);
        if (cached != null)
            return cached;

        // Cache miss - fetch from database
        var process = await _repository.GetByIdAsync(processId);
        if (process != null)
        {
            // Populate cache for next request
            await _cache.SetProcessAsync(process);
        }

        return process;
    }

    public async Task UpdateProcessAsync(Process process)
    {
        // Update database first
        await _repository.UpdateAsync(process);

        // Invalidate cache to maintain consistency
        await _cache.InvalidateAsync(process.ProcessId);
    }
}
```

### Optimistic Locking for Status Updates

```csharp
public async Task<bool> UpdateStatusAsync(
    Guid processId,
    ProcessStatus newStatus,
    long currentVersion)
{
    // Try to update status with version check
    var success = await _cache.TrySetStatusAsync(
        processId,
        newStatus,
        currentVersion);

    if (!success)
    {
        // Version mismatch - another process updated concurrently
        // Retry with fresh version or handle conflict
        _logger.LogWarning(
            "Status update conflict for process {ProcessId}",
            processId);
        return false;
    }

    return true;
}
```

### Idempotency Check

```csharp
public async Task<bool> IsProcessCached(Guid processId)
{
    // Lightweight existence check without deserializing full process
    return await _cache.ExistsAsync(processId);
}
```

## Key Design

### Key Patterns

```
process:{processId}               → Full process JSON
process:{processId}:status        → Process status (for optimistic locking)
process:{processId}:version       → Version number (for optimistic locking)
```

### Examples

```
process:550e8400-e29b-41d4-a716-446655440000
process:550e8400-e29b-41d4-a716-446655440000:status
process:550e8400-e29b-41d4-a716-446655440000:version
```

## Error Handling

### Redis Connection Failures

```csharp
// All methods handle RedisException gracefully
try
{
    var cached = await db.StringGetAsync(key);
    // ...
}
catch (RedisException ex)
{
    _logger.LogError(ex, "Redis error while getting process {ProcessId}", processId);
    return null; // Fail gracefully - caller will fetch from repository
}
```

### JSON Serialization Errors

```csharp
catch (JsonException ex)
{
    _logger.LogError(ex, "JSON deserialization error for process {ProcessId}", processId);
    // Invalidate corrupted cache entry
    await InvalidateAsync(processId);
    return null;
}
```

## Connection Resilience

### Settings

```csharp
var options = ConfigurationOptions.Parse(connectionString);
options.AbortOnConnectFail = false;              // Don't throw on initial failure
options.ConnectRetry = 3;                        // Retry 3 times
options.ConnectTimeout = 5000;                   // 5 seconds
options.KeepAlive = 60;                          // Send keepalive every 60s
options.ReconnectRetryPolicy = new ExponentialRetry(5000); // Exponential backoff
```

### Connection Events

The factory registers event handlers for monitoring:

- **ConnectionFailed**: Logged as Error
- **ConnectionRestored**: Logged as Information
- **ErrorMessage**: Logged as Error
- **InternalError**: Logged as Error with exception

## Performance Considerations

### TTL Strategy

- **Active processes** (Accepted/Processing): Short TTL (5-15 minutes)
- **Completed processes**: Longer TTL (1-24 hours)
- **Failed processes**: Medium TTL (1 hour)

Current implementation uses a single configurable TTL. Future enhancement can implement dynamic TTL based on process status.

### Memory Management

Redis uses LRU (Least Recently Used) eviction by default:

```bash
# Set max memory and eviction policy in redis.conf
maxmemory 2gb
maxmemory-policy allkeys-lru
```

### Serialization

Uses `System.Text.Json` for high performance:
- Fast serialization/deserialization
- Supports JsonDocument for complex nested data
- No reflection overhead

## Monitoring

### Logging Levels

- **Debug**: Cache hits, misses, sets, invalidations
- **Information**: Connection established, restored
- **Error**: Connection failures, Redis errors, JSON errors

### Key Metrics to Monitor

```bash
# Cache hit rate
log_cache_hits / (log_cache_hits + log_cache_misses)

# Connection failures
count(log_level="Error" AND message contains "Redis connection failed")

# Serialization errors
count(log_level="Error" AND message contains "JSON")
```

## Troubleshooting

### Issue: Cache always returns null

**Possible causes**:
1. Redis server not running
2. Connection string incorrect
3. Firewall blocking connection

**Solution**:
```bash
# Test Redis connectivity
telnet localhost 6379
redis-cli ping  # Should return PONG

# Check logs for connection errors
grep "Redis connection failed" application.log
```

### Issue: High memory usage

**Possible causes**:
1. TTL too long
2. Too many processes cached
3. Large process data

**Solution**:
```bash
# Check Redis memory usage
redis-cli info memory

# Reduce TTL in appsettings.json
"DefaultTtlSeconds": 1800  # 30 minutes instead of 1 hour

# Configure maxmemory policy
redis-cli config set maxmemory 2gb
redis-cli config set maxmemory-policy allkeys-lru
```

### Issue: Version conflicts frequent

**Possible causes**:
1. High concurrent updates
2. Long-running operations

**Solution**:
- Implement retry logic with exponential backoff
- Fetch fresh version before retry
- Consider reducing cache TTL for active processes

## Disabling Redis

To disable Redis caching:

```json
{
  "Redis": {
    "Enabled": false
  }
}
```

This switches to `NullStateStore`, allowing the application to run without Redis while maintaining the same interface.

## Testing

### Local Development

```bash
# Start Redis with Docker
docker run -d -p 6379:6379 --name redis-dev redis:7.0

# Verify connection
redis-cli ping

# Monitor operations
redis-cli monitor

# Check keys
redis-cli keys "process:*"
```

### Integration Tests

See `tests/StarGate.Infrastructure.Tests/Caching/RedisStateStoreTests.cs` for comprehensive test examples using Testcontainers.

## References

- [StackExchange.Redis Documentation](https://stackexchange.github.io/StackExchange.Redis/)
- [Redis Best Practices](https://redis.io/docs/manual/patterns/)
- [TECHNICAL-ANALYSIS.md - Redis Cache](https://github.com/artcava/StarGate/blob/main/docs/TECHNICAL-ANALYSIS.md)
