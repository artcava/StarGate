# Redis Integration Tests

Integration tests for Redis caching functionality using real Redis instances in Docker containers.

## Overview

These tests validate the Redis caching layer with actual Redis instances managed by [Testcontainers for .NET](https://dotnet.testcontainers.org/). They ensure that caching operations, serialization, TTL behavior, and connection resilience work correctly in real-world scenarios.

## Test Structure

### Fixtures

**RedisFixture** (`tests/StarGate.Integration.Tests/Fixtures/RedisFixture.cs`)
- Manages Redis test container lifecycle
- Uses `redis:7.0-alpine` Docker image
- Provides `RedisStateStore` and `IConnectionMultiplexer` instances
- Configured with 30-second TTL for efficient testing
- Implements `IAsyncLifetime` for proper setup/teardown

### Test Classes

**RedisStateStoreIntegrationTests**
- 14 comprehensive tests covering all RedisStateStore operations
- Tests cache-aside pattern, TTL expiration, serialization
- Validates concurrent operations and race condition handling
- Each test cleans up with `FLUSHDB` for isolation

**RedisHealthCheckIntegrationTests**
- Validates Redis health check implementation
- Ensures correct status reporting and metadata

## Prerequisites

### Docker

**Required:** Docker must be installed and running to execute these tests.

```bash
# Verify Docker is running
docker ps
```

Testcontainers will automatically:
- Pull the `redis:7.0-alpine` image (if not present)
- Start Redis container on a random available port
- Stop and remove container after tests complete

### .NET SDK

- .NET 8.0 SDK or later

## Running Tests

### All Redis Integration Tests

```bash
# Run all caching integration tests
dotnet test tests/StarGate.Integration.Tests --filter "FullyQualifiedName~Caching"
```

### Specific Test Classes

```bash
# Run only RedisStateStore tests
dotnet test tests/StarGate.Integration.Tests --filter "FullyQualifiedName~RedisStateStoreIntegrationTests"

# Run only RedisHealthCheck tests
dotnet test tests/StarGate.Integration.Tests --filter "FullyQualifiedName~RedisHealthCheckIntegrationTests"
```

### With Verbose Output

```bash
# Detailed logging for debugging
dotnet test tests/StarGate.Integration.Tests \
  --logger "console;verbosity=detailed" \
  --filter "FullyQualifiedName~Caching"
```

### Generate Test Report

```bash
# Generate TRX report for CI/CD
dotnet test tests/StarGate.Integration.Tests \
  --logger "trx;LogFileName=redis-test-results.trx" \
  --filter "FullyQualifiedName~Caching"
```

## Test Coverage

### RedisStateStoreIntegrationTests

| Test | Description | Duration |
|------|-------------|----------|
| `SetProcessAsync_Should_CacheProcess_InRedis` | Basic cache write and read | ~100ms |
| `GetProcessAsync_Should_ReturnNull_WhenNotCached` | Cache miss behavior | ~50ms |
| `InvalidateAsync_Should_RemoveFromCache` | Cache invalidation | ~100ms |
| `ExistsAsync_Should_ReturnTrue_WhenCached` | Cache key existence check | ~100ms |
| `ExistsAsync_Should_ReturnFalse_WhenNotCached` | Non-existent key check | ~50ms |
| `TTL_Should_ExpireCache_AfterConfiguredTime` | TTL expiration (30s + buffer) | **~35s** |
| `SetProcessAsync_Should_SerializeComplexData` | Complex object serialization | ~150ms |
| `SetProcessAsync_Should_HandleError_Serialization` | ProcessError serialization | ~100ms |
| `ConcurrentOperations_Should_NotCorruptCache` | 10 concurrent writes/reads | ~200ms |
| `ConcurrentInvalidation_Should_NotThrow` | 10 concurrent invalidations | ~150ms |
| `UpdateProcess_Should_RequireInvalidation` | Cache-aside pattern | ~150ms |
| `LargePayload_Should_BeCached` | 1000 items (~100KB) | ~300ms |
| `RapidCacheMissAndSet_Should_NotCauseRaceCondition` | 5 concurrent cache-aside | ~200ms |

**Total Estimated Duration:** ~37 seconds (dominated by TTL test)

### RedisHealthCheckIntegrationTests

| Test | Description | Duration |
|------|-------------|----------|
| `CheckHealthAsync_Should_ReturnHealthy_WhenRedisConnected` | Health check validation | ~100ms |

## Test Patterns

### Cache-Aside Pattern

Multiple tests validate the cache-aside pattern used in StarGate:

```csharp
// 1. Check cache
var cached = await stateStore.GetProcessAsync(processId);
if (cached != null)
    return cached;

// 2. Load from database
var process = await database.GetProcessAsync(processId);

// 3. Update cache
await stateStore.SetProcessAsync(process);
return process;
```

**Update Pattern:**

```csharp
// 1. Invalidate cache first
await stateStore.InvalidateAsync(processId);

// 2. Update database
await database.UpdateProcessAsync(process);

// 3. Cache will be refreshed on next read (cache-aside)
```

### Test Isolation

Each test uses `IAsyncLifetime.DisposeAsync()` to execute `FLUSHDB` after completion:

```csharp
public async Task DisposeAsync()
{
    await _fixture.FlushDatabaseAsync();
}
```

This ensures:
- No test data pollution between tests
- Tests can run in parallel safely
- Deterministic test results

### Concurrency Testing

Tests validate thread-safety with `Task.WhenAll()`:

```csharp
var tasks = Enumerable.Range(0, 10).Select(_ => 
    stateStore.SetProcessAsync(process));
await Task.WhenAll(tasks);
```

## Troubleshooting

### Docker Not Running

**Error:** `Docker is not running`

**Solution:** Start Docker Desktop or Docker daemon:
```bash
# Linux/macOS
sudo systemctl start docker

# Windows: Start Docker Desktop application
```

### Port Conflicts

Testcontainers automatically assigns random ports, so conflicts are rare. If issues occur:

```bash
# Check for hanging containers
docker ps -a | grep redis

# Remove stale containers
docker rm -f $(docker ps -aq --filter ancestor=redis:7.0-alpine)
```

### TTL Test Timeout

The `TTL_Should_ExpireCache_AfterConfiguredTime` test waits 35 seconds. This is intentional to validate expiration behavior. To skip this test during rapid development:

```bash
# Skip TTL test
dotnet test tests/StarGate.Integration.Tests \
  --filter "FullyQualifiedName~Caching&FullyQualifiedName!~TTL"
```

### Container Cleanup

Testcontainers should clean up automatically, but manual cleanup if needed:

```bash
# Stop all test containers
docker stop $(docker ps -q --filter label=org.testcontainers.session-id)

# Remove all test containers
docker rm $(docker ps -aq --filter label=org.testcontainers.session-id)
```

## CI/CD Integration

These tests are designed for CI/CD pipelines:

### GitHub Actions Example

```yaml
steps:
  - name: Run Redis Integration Tests
    run: |
      dotnet test tests/StarGate.Integration.Tests \
        --filter "FullyQualifiedName~Caching" \
        --logger "trx;LogFileName=redis-tests.trx" \
        --logger "console;verbosity=normal"
    
  - name: Upload Test Results
    uses: actions/upload-artifact@v3
    if: always()
    with:
      name: test-results
      path: '**/redis-tests.trx'
```

### Docker-in-Docker

If running in containerized CI (e.g., GitLab CI), ensure Docker-in-Docker is available:

```yaml
services:
  - docker:dind

variables:
  DOCKER_HOST: tcp://docker:2375
  DOCKER_DRIVER: overlay2
```

## Performance Considerations

### Container Startup Time

- First run: ~10-15s (image pull + container start)
- Subsequent runs: ~2-3s (container start only)

### Test Parallelization

xUnit runs test classes in parallel by default. With `IClassFixture<RedisFixture>`, all tests in `RedisStateStoreIntegrationTests` share the same container, reducing overhead.

**Trade-off:** Tests within a class run sequentially (due to shared fixture + FLUSHDB cleanup).

### Optimization Tips

1. **Skip TTL test during rapid development** (saves 35s)
2. **Use `--filter` to run specific test subsets**
3. **Keep Docker image cached** (don't delete `redis:7.0-alpine`)

## Best Practices

### Writing New Redis Tests

1. **Use `IClassFixture<RedisFixture>`** to share container
2. **Implement `IAsyncLifetime.DisposeAsync()`** with `FlushDatabaseAsync()`
3. **Test real Redis behavior**, not mocked behavior
4. **Use realistic data** (complex objects, large payloads)
5. **Validate concurrency** when applicable

### What to Test vs. What to Mock

**Integration Tests (Real Redis):**
- Serialization/deserialization behavior
- TTL and expiration
- Connection resilience
- Concurrent operations
- Large payloads

**Unit Tests (Mocked Redis):**
- Business logic that uses cache
- Error handling for Redis failures
- Retry logic
- Metrics and logging

## References

- [Testcontainers for .NET Documentation](https://dotnet.testcontainers.org/)
- [Redis Testing Best Practices](https://redis.io/docs/getting-started/testing/)
- [xUnit Documentation](https://xunit.net/)
- [StarGate CODING-CONVENTIONS.md](../../../docs/CODING-CONVENTIONS.md)
- [StarGate TECHNICAL-ANALYSIS.md - Redis Cache](../../../docs/TECHNICAL-ANALYSIS.md#redis-cache-implementation)

## Related Issues

- Issue #24: RedisStateStore implementation
- Issue #25: Cache invalidation logic
- Issue #26: Connection pooling configuration
- Issue #27: Unit tests for cache
- Issue #28: Integration tests with Redis container (this implementation)
