# Policy Integration Tests

This directory contains integration tests for the policy enforcement system in StarGate.

## Overview

These tests verify end-to-end policy enforcement across the following components:
- **PolicyProvider**: Policy resolution and caching
- **PolicyRepository**: Policy persistence and retrieval
- **ProcessService**: Policy application during process creation
- **ProcessWorker**: Policy enforcement during execution (tested separately)

## Test Suites

### PolicyProviderIntegrationTests

Tests for policy resolution and caching behavior:

- **Policy Resolution**: Verifies correct policy loading from repository
- **Client Overrides**: Tests client-specific policy overrides
- **Caching**: Validates policy caching and cache invalidation
- **Validation**: Tests handling of invalid policy configurations

**Key Test Cases:**
- `GetPolicyAsync_Should_ReturnTypeDefaultPolicy_WhenNoClientOverride`
- `GetPolicyAsync_Should_ApplyClientOverride_WhenConfigured`
- `GetPolicyAsync_Should_CacheResolvedPolicy`
- `GetPolicyAsync_Should_InvalidateCache_WhenPolicyUpdated`
- `GetPolicyAsync_Should_ReturnNull_WhenProcessTypeNotFound`
- `GetPolicyAsync_Should_IgnoreInvalidClientOverride`

### PolicyEnforcementIntegrationTests

Tests for end-to-end policy enforcement:

- **Policy Application**: Verifies policies are correctly applied during process creation
- **Timeout Configuration**: Tests timeout policy loading
- **Priority Handling**: Validates priority-based client overrides
- **Multi-Type Support**: Tests multiple process types with different policies

**Key Test Cases:**
- `CreateProcessAsync_Should_ApplyPolicyFromProvider`
- `ProcessExecution_Should_EnforceTimeout`
- `ProcessExecution_Should_RespectClientOverridePriority`
- `ProcessExecution_Should_HandleMultipleProcessTypes_WithDifferentPolicies`

## Test Infrastructure

### PolicyIntegrationFixture

Shared fixture providing:
- **MongoDB container**: For policy and process persistence
- **RabbitMQ container**: For message broker simulation
- **Service provider**: Fully configured DI container
- **Seeded policies**: Default test policies for "order" and "payment" types

**Default Policies:**
```csharp
// Order Policy
TimeoutSeconds: 300
MaxRetryAttempts: 3
MaxConcurrentExecutions: 10
Priority: 5

// Payment Policy
TimeoutSeconds: 60
MaxRetryAttempts: 5
MaxConcurrentExecutions: 20
Priority: 8
```

## Running the Tests

### Run All Policy Tests
```bash
dotnet test tests/StarGate.Integration.Tests --filter "FullyQualifiedName~Policy"
```

### Run Provider Tests Only
```bash
dotnet test tests/StarGate.Integration.Tests --filter "FullyQualifiedName~PolicyProvider"
```

### Run Enforcement Tests Only
```bash
dotnet test tests/StarGate.Integration.Tests --filter "FullyQualifiedName~PolicyEnforcement"
```

### Run with Verbose Output
```bash
dotnet test tests/StarGate.Integration.Tests --logger "console;verbosity=detailed" --filter "FullyQualifiedName~Policy"
```

### Run in Parallel
```bash
dotnet test tests/StarGate.Integration.Tests --parallel --filter "FullyQualifiedName~Policy"
```

## Test Isolation

Each test class implements `IAsyncLifetime` to ensure proper cleanup:

- **InitializeAsync**: No-op (fixture handles initialization)
- **DisposeAsync**: Clears policies and processes after each test

### Isolation Guarantees

1. **Unique IDs**: Each test uses unique client IDs and process IDs
2. **Container Isolation**: Testcontainers uses random ports to avoid conflicts
3. **Database Separation**: Each fixture uses a separate database (`stargate_test`)
4. **Cleanup**: Automatic cleanup in `DisposeAsync()` prevents test pollution

## Dependencies

Required NuGet packages (already in `.csproj`):
- `Testcontainers` (4.0.0)
- `Testcontainers.MongoDb` (3.10.0)
- `Testcontainers.RabbitMq` (4.0.0)
- `FluentAssertions` (7.0.0)
- `xunit` (2.6.6)

## Implementation Notes

### Scope

**In Scope:**
- Policy loading from repository
- Policy resolution (type default + client override)
- Policy caching behavior
- Policy validation enforcement
- End-to-end policy application in ProcessService

**Out of Scope:**
- Actual process execution timeouts (tested in ProcessWorker unit tests)
- Retry logic execution (tested in ProcessWorker unit tests)
- Concurrency limit enforcement (tested separately)

### Performance Considerations

These integration tests use real containers and can be slower than unit tests:
- Container startup: ~2-5 seconds per fixture
- Test execution: ~100-500ms per test
- Total suite time: ~10-20 seconds

For faster feedback during development, run unit tests first.

## Troubleshooting

### Container Startup Failures

If containers fail to start:
```bash
# Check Docker is running
docker ps

# Check available ports
netstat -an | grep LISTEN

# Manually cleanup containers
docker container prune
```

### MongoDB Connection Issues

If MongoDB tests fail with connection errors:
- Ensure Docker has sufficient memory (at least 2GB)
- Check firewall settings for port binding
- Verify MongoDB driver version matches Infrastructure project

### Test Flakiness

If tests are flaky:
- Check for shared state between tests
- Verify `DisposeAsync()` is properly cleaning up
- Ensure unique IDs for each test
- Consider adding delays for async operations

## Related Documentation

- [TECHNICAL-ANALYSIS.md - Configuration Management](https://github.com/artcava/StarGate/blob/main/docs/TECHNICAL-ANALYSIS.md#configuration-management)
- [CODING-CONVENTIONS.md](https://github.com/artcava/StarGate/blob/main/docs/CODING-CONVENTIONS.md)
- [Issue #65: Policy Integration Tests](https://github.com/artcava/StarGate/issues/65)

## Contributing

When adding new policy integration tests:

1. Use the existing `PolicyIntegrationFixture`
2. Implement `IAsyncLifetime` for cleanup
3. Use unique client/process IDs
4. Add descriptive test names following the pattern: `Method_Should_Behavior_When_Condition`
5. Use FluentAssertions for readable assertions
6. Add XML documentation for complex test scenarios
