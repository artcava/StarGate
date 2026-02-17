# StarGate Integration Tests

This project contains integration tests for StarGate that verify the system's behavior with real external dependencies.

## Overview

Integration tests verify:
- **MongoDB Repository Operations**: Real database operations with MongoDB containers
- **Redis Caching Operations**: Real caching operations with Redis containers
- **Index Creation and Constraints**: Unique indexes and constraint violations
- **Data Serialization**: BSON/JSON serialization/deserialization with complex objects
- **Concurrent Operations**: Race conditions and idempotency
- **Error Handling**: Exception propagation and error persistence
- **Health Checks**: Redis connection health monitoring

## Prerequisites

### Required Software

- **.NET 8.0 SDK** or later
- **Docker Desktop** (or Docker Engine on Linux)
  - Must be running before executing integration tests
  - Required by Testcontainers to spin up MongoDB and Redis instances

### Verify Docker Installation

```bash
# Check Docker is running
docker ps

# Should show container list (can be empty)
# If error: "Cannot connect to the Docker daemon" - start Docker Desktop
```

## Test Structure

```
StarGate.Integration.Tests/
├── Fixtures/
│   ├── MongoDbFixture.cs          # MongoDB test container lifecycle
│   └── RedisFixture.cs            # Redis test container lifecycle
├── Persistence/
│   ├── MongoProcessRepositoryIntegrationTests.cs
│   └── MongoPolicyRepositoryIntegrationTests.cs
├── Caching/
│   ├── RedisStateStoreIntegrationTests.cs
│   ├── RedisHealthCheckIntegrationTests.cs
│   └── README.md                  # Detailed Redis tests documentation
├── StarGate.Integration.Tests.csproj
├── Usings.cs
└── README.md (this file)
```

### Fixtures

**MongoDbFixture**
Manages the MongoDB test container lifecycle:
- Spins up a MongoDB 7.0 container using Testcontainers
- Creates necessary indexes for testing
- Provides database reset functionality between tests
- Automatically disposes container after test execution

**RedisFixture**
Manages the Redis test container lifecycle:
- Spins up a Redis 7.0-alpine container using Testcontainers
- Provides RedisStateStore and IConnectionMultiplexer instances
- Configured with short TTL (30s) for efficient testing
- Implements FLUSHDB for test isolation
- Automatically disposes container after test execution

## Running Tests

### Run All Integration Tests

```bash
# From repository root
dotnet test tests/StarGate.Integration.Tests

# With verbose output
dotnet test tests/StarGate.Integration.Tests --logger "console;verbosity=detailed"
```

### Run Tests by Category

```bash
# MongoDB/Persistence tests only
dotnet test tests/StarGate.Integration.Tests --filter "FullyQualifiedName~Persistence"

# Redis/Caching tests only
dotnet test tests/StarGate.Integration.Tests --filter "FullyQualifiedName~Caching"
```

### Run Specific Test Class

```bash
# MongoProcessRepository tests
dotnet test tests/StarGate.Integration.Tests --filter "FullyQualifiedName~MongoProcessRepositoryIntegrationTests"

# MongoPolicyRepository tests
dotnet test tests/StarGate.Integration.Tests --filter "FullyQualifiedName~MongoPolicyRepositoryIntegrationTests"

# RedisStateStore tests
dotnet test tests/StarGate.Integration.Tests --filter "FullyQualifiedName~RedisStateStoreIntegrationTests"

# RedisHealthCheck tests
dotnet test tests/StarGate.Integration.Tests --filter "FullyQualifiedName~RedisHealthCheckIntegrationTests"
```

### Run Specific Test Method

```bash
# Run a single test
dotnet test tests/StarGate.Integration.Tests --filter "FullyQualifiedName~CreateAsync_Should_PersistProcess_InDatabase"
```

### Generate Test Report

```bash
# Generate TRX report
dotnet test tests/StarGate.Integration.Tests --logger "trx;LogFileName=integration-test-results.trx"

# Results saved to: TestResults/integration-test-results.trx
```

## Test Coverage

### MongoProcessRepositoryIntegrationTests

Tests for process persistence operations:

- ✅ `CreateAsync_Should_PersistProcess_InDatabase` - Basic create and retrieve
- ✅ `CreateAsync_Should_ThrowException_WhenDuplicateProcessId` - Unique ProcessId constraint
- ✅ `CreateAsync_Should_ThrowException_WhenDuplicateClientProcessId` - Unique ClientId+ClientProcessId
- ✅ `CreateAsync_Should_ThrowException_WhenDuplicateIdempotencyKey` - Unique IdempotencyKey
- ✅ `GetByIdAsync_Should_ReturnNull_WhenProcessNotFound` - Not found handling
- ✅ `GetByClientProcessIdAsync_Should_ReturnProcess_WhenExists` - Composite key query
- ✅ `GetByClientProcessIdAsync_Should_ReturnNull_WhenNotFound` - Not found handling
- ✅ `UpdateAsync_Should_ModifyExistingProcess` - Update operations
- ✅ `UpdateAsync_Should_ThrowException_WhenProcessNotFound` - Update validation
- ✅ `CreateAsync_Should_SerializeComplexData_Correctly` - Complex JSON serialization
- ✅ `UpdateAsync_Should_HandleError_Correctly` - ProcessError persistence
- ✅ `ConcurrentCreates_Should_HandleRaceCondition_WithIdempotencyKey` - Concurrency control

### MongoPolicyRepositoryIntegrationTests

Tests for policy retrieval operations:

- ✅ `GetProcessTypePolicyAsync_Should_ReturnPolicy_WhenExists` - Policy retrieval
- ✅ `GetProcessTypePolicyAsync_Should_ThrowException_WhenNotFound` - Missing policy handling
- ✅ `GetClientOverrideAsync_Should_ReturnOverride_WhenExists` - Override retrieval
- ✅ `GetClientOverrideAsync_Should_ReturnNull_WhenNotFound` - Missing override handling
- ✅ `GetProcessTypePolicyAsync_Should_HandleComplexRetryPolicy` - Complex policy data
- ✅ `GetClientOverrideAsync_Should_HandleNullableFields` - Nullable field handling
- ✅ `GetProcessTypePolicyAsync_Should_HandleDisabledRetryPolicy` - Disabled retry scenarios

### RedisStateStoreIntegrationTests

Tests for Redis caching operations (14 tests):

- ✅ Basic CRUD operations (Set, Get, Invalidate, Exists)
- ✅ TTL expiration behavior (30s + 5s buffer)
- ✅ Complex data serialization (nested objects, arrays, decimals)
- ✅ ProcessError serialization with metadata
- ✅ Concurrent write operations (10 parallel)
- ✅ Concurrent invalidation (10 parallel)
- ✅ Cache-aside pattern with updates
- ✅ Large payload caching (1000 items, ~100KB)
- ✅ Race condition handling in cache-aside pattern

**See [Caching/README.md](Caching/README.md) for detailed Redis test documentation.**

### RedisHealthCheckIntegrationTests

Tests for Redis health check monitoring:

- ✅ `CheckHealthAsync_Should_ReturnHealthy_WhenRedisConnected` - Health status validation

## Test Isolation

### MongoDB Tests
Each test class:
- Uses `IClassFixture<MongoDbFixture>` for shared container across test class
- Implements `IAsyncLifetime` for per-test cleanup
- Calls `ResetDatabaseAsync()` in `DisposeAsync()` to clean data between tests
- Tests can run in parallel without interference

### Redis Tests
Each test class:
- Uses `IClassFixture<RedisFixture>` for shared container across test class
- Implements `IAsyncLifetime` for per-test cleanup
- Calls `FlushDatabaseAsync()` in `DisposeAsync()` to execute FLUSHDB
- Tests can run in parallel without interference

## Performance Considerations

### MongoDB Tests
- **First Test Run**: ~10-15 seconds (container startup)
- **Subsequent Tests**: ~1-2 seconds per test (container reused)

### Redis Tests
- **First Test Run**: ~10-15 seconds (container startup + image pull)
- **Subsequent Tests**: ~100-300ms per test (container reused)
- **TTL Test**: ~35 seconds (intentional wait for expiration validation)

### Overall
- **Container Cleanup**: Automatic on test completion
- **Parallel Execution**: xUnit runs test classes in parallel by default

## Troubleshooting

### Docker Not Running

```
Error: Cannot connect to the Docker daemon at unix:///var/run/docker.sock
```

**Solution**: Start Docker Desktop or Docker Engine

### Port Already in Use

```
Error: Bind for 0.0.0.0:27017 failed: port is already allocated
```

**Solution**: Testcontainers automatically assigns random ports. If this occurs:
1. Check for running containers: `docker ps | grep mongo` or `docker ps | grep redis`
2. Stop conflicting containers: `docker stop <container_id>`
3. Remove stale containers: `docker rm <container_id>`

### Tests Timeout

```
Error: Test timed out after 30 seconds
```

**Solution**:
1. Check Docker resource limits (CPU, Memory)
2. Ensure Docker has internet access to pull images (mongo:7.0, redis:7.0-alpine)
3. Check Docker logs: `docker logs <container_id>`

### Permission Denied (Linux)

```
Error: permission denied while trying to connect to Docker daemon
```

**Solution**: Add user to docker group
```bash
sudo usermod -aG docker $USER
# Log out and log back in
```

### Redis TTL Test Takes Too Long

The `TTL_Should_ExpireCache_AfterConfiguredTime` test intentionally waits 35 seconds.

**Solution for rapid development**: Skip this specific test
```bash
dotnet test tests/StarGate.Integration.Tests \
  --filter "FullyQualifiedName~Caching&FullyQualifiedName!~TTL"
```

## CI/CD Integration

These tests are designed to run in CI/CD pipelines:

```yaml
# Example GitHub Actions
steps:
  - uses: actions/checkout@v3
  - uses: actions/setup-dotnet@v3
    with:
      dotnet-version: '8.0.x'
  - name: Run Integration Tests
    run: dotnet test tests/StarGate.Integration.Tests
```

Docker is typically available in most CI environments by default.

## Best Practices

1. **Always Clean Up**: Use `DisposeAsync()` to reset database/cache state
2. **Unique Data**: Generate unique IDs (GUIDs) for test data
3. **Explicit Assertions**: Verify both success and error conditions
4. **Test Isolation**: Don't depend on execution order
5. **Real Dependencies**: Use actual MongoDB/Redis, not mocks
6. **Realistic Data**: Test with complex objects and large payloads
7. **Concurrency Testing**: Validate thread-safety with parallel operations

## References

- [Testcontainers for .NET Documentation](https://dotnet.testcontainers.org/)
- [MongoDB C# Driver Documentation](https://www.mongodb.com/docs/drivers/csharp/current/)
- [StackExchange.Redis Documentation](https://stackexchange.github.io/StackExchange.Redis/)
- [xUnit Documentation](https://xunit.net/)
- [FluentAssertions Documentation](https://fluentassertions.com/)

## Related Documentation

- [TECHNICAL-ANALYSIS.md](../../docs/TECHNICAL-ANALYSIS.md) - MongoDB Schema & Redis Cache Design
- [CODING-CONVENTIONS.md](../../docs/CODING-CONVENTIONS.md) - Project Coding Standards
- [Caching/README.md](Caching/README.md) - Detailed Redis Integration Tests Documentation
- [Issue #23](https://github.com/artcava/StarGate/issues/23) - MongoDB Integration Tests Implementation
- [Issue #28](https://github.com/artcava/StarGate/issues/28) - Redis Integration Tests Implementation
