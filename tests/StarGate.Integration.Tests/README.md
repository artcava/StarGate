# StarGate Integration Tests

This project contains integration tests for StarGate that verify the system's behavior with real external dependencies.

## Overview

Integration tests verify:
- **MongoDB Repository Operations**: Real database operations with MongoDB containers
- **Index Creation and Constraints**: Unique indexes and constraint violations
- **Data Serialization**: BSON serialization/deserialization with complex objects
- **Concurrent Operations**: Race conditions and idempotency
- **Error Handling**: Exception propagation and error persistence

## Prerequisites

### Required Software

- **.NET 8.0 SDK** or later
- **Docker Desktop** (or Docker Engine on Linux)
  - Must be running before executing integration tests
  - Required by Testcontainers to spin up MongoDB instances

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
│   └── MongoDbFixture.cs          # MongoDB test container lifecycle
├── Persistence/
│   ├── MongoProcessRepositoryIntegrationTests.cs
│   └── MongoPolicyRepositoryIntegrationTests.cs
├── StarGate.Integration.Tests.csproj
├── Usings.cs
└── README.md (this file)
```

### MongoDbFixture

Manages the MongoDB test container lifecycle:
- Spins up a MongoDB 7.0 container using Testcontainers
- Creates necessary indexes for testing
- Provides database reset functionality between tests
- Automatically disposes container after test execution

## Running Tests

### Run All Integration Tests

```bash
# From repository root
dotnet test tests/StarGate.Integration.Tests

# With verbose output
dotnet test tests/StarGate.Integration.Tests --logger "console;verbosity=detailed"
```

### Run Specific Test Class

```bash
# MongoProcessRepository tests only
dotnet test tests/StarGate.Integration.Tests --filter "FullyQualifiedName~MongoProcessRepositoryIntegrationTests"

# MongoPolicyRepository tests only
dotnet test tests/StarGate.Integration.Tests --filter "FullyQualifiedName~MongoPolicyRepositoryIntegrationTests"
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

## Test Isolation

Each test class:
- Uses `IClassFixture<MongoDbFixture>` for shared container across test class
- Implements `IAsyncLifetime` for per-test cleanup
- Calls `ResetDatabaseAsync()` in `DisposeAsync()` to clean data between tests
- Tests can run in parallel without interference

## Performance Considerations

- **First Test Run**: ~10-15 seconds (container startup)
- **Subsequent Tests**: ~1-2 seconds per test (container reused)
- **Container Cleanup**: Automatic on test completion

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
1. Check for running MongoDB containers: `docker ps | grep mongo`
2. Stop conflicting containers: `docker stop <container_id>`

### Tests Timeout

```
Error: Test timed out after 30 seconds
```

**Solution**:
1. Check Docker resource limits (CPU, Memory)
2. Ensure Docker has internet access to pull mongo:7.0 image
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

1. **Always Clean Up**: Use `DisposeAsync()` to reset database state
2. **Unique Data**: Generate unique IDs (GUIDs) for test data
3. **Explicit Assertions**: Verify both success and error conditions
4. **Test Isolation**: Don't depend on execution order
5. **Real Dependencies**: Use actual MongoDB, not mocks

## References

- [Testcontainers for .NET Documentation](https://dotnet.testcontainers.org/)
- [MongoDB C# Driver Documentation](https://www.mongodb.com/docs/drivers/csharp/current/)
- [xUnit Documentation](https://xunit.net/)
- [FluentAssertions Documentation](https://fluentassertions.com/)

## Related Documentation

- [TECHNICAL-ANALYSIS.md](../../docs/TECHNICAL-ANALYSIS.md) - MongoDB Schema Design
- [CODING-CONVENTIONS.md](../../docs/CODING-CONVENTIONS.md) - Project Coding Standards
- [Issue #23](https://github.com/artcava/StarGate/issues/23) - Integration Tests Implementation
