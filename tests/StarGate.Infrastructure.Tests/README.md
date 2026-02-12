# StarGate.Infrastructure.Tests

## Overview

This project contains **unit tests** for the StarGate Infrastructure layer. These tests use **mocking** (via Moq) to isolate repository logic from external dependencies like MongoDB.

**Important**: These are PURE UNIT TESTS with NO actual database connection. All MongoDB operations are mocked.

## Test Categories

### Unit Tests (This Project)
- ✅ **MongoPolicyRepositoryTests**: Tests for policy repository with mocked MongoDB driver
- 🔜 **MongoProcessRepositoryTests**: Tests for process repository (to be added)
- Uses **Moq** to mock `IMongoDatabase` and `IMongoCollection`
- Uses **FluentAssertions** for readable test assertions
- **NO Docker or MongoDB installation required**

### Integration Tests (Separate Project - Issue #23)
- Tests with **real MongoDB** running in Docker container
- Uses **Testcontainers** to spin up MongoDB
- Validates actual database operations
- Requires Docker to be running

## Running Tests

### Run All Unit Tests
```bash
dotnet test tests/StarGate.Infrastructure.Tests
```

### Run Specific Test Class
```bash
# Run only MongoPolicyRepositoryTests
dotnet test tests/StarGate.Infrastructure.Tests --filter "FullyQualifiedName~MongoPolicyRepositoryTests"
```

### Run with Detailed Output
```bash
dotnet test tests/StarGate.Infrastructure.Tests --verbosity detailed
```

## Code Coverage

### Generate Coverage Report
```bash
# Run tests with coverage collection
dotnet test tests/StarGate.Infrastructure.Tests --collect:"XPlat Code Coverage"

# Install ReportGenerator tool (one-time)
dotnet tool install -g dotnet-reportgenerator-globaltool

# Generate HTML report
reportgenerator -reports:"**/coverage.cobertura.xml" -targetdir:"coverage-report" -reporttypes:Html

# Open the report
open coverage-report/index.html  # macOS
start coverage-report/index.html # Windows
```

### Coverage Goals
- **Target**: >90% code coverage for repository classes
- **Current**: Check `coverage-report/index.html` after running coverage

## Test Structure

All tests follow the **AAA pattern**:
- **Arrange**: Setup test data and mocks
- **Act**: Execute the method under test
- **Assert**: Verify the results using FluentAssertions

### Example Test
```csharp
[Fact]
public async Task GetProcessTypePolicyAsync_Should_ReturnPolicy_WhenExists()
{
    // Arrange
    var processType = "order";
    var document = CreateValidPolicyDocument() with { ProcessType = processType };
    SetupMockCursor(document);

    // Act
    var result = await _repository.GetProcessTypePolicyAsync(processType);

    // Assert
    result.Should().NotBeNull();
    result.ProcessType.Should().Be(processType);
}
```

## Dependencies

- **xUnit**: Test framework
- **Moq**: Mocking library for MongoDB driver
- **FluentAssertions**: Assertion library
- **MongoDB.Driver**: Types for mocking
- **Microsoft.Extensions.Logging.Abstractions**: For NullLogger

## Notes

- Tests are **isolated** - no shared state between tests
- Each test class creates fresh mocks in constructor
- **No cleanup required** - all mocks are disposed after test execution
- Tests run **fast** (~seconds) since no I/O operations

## Related Issues

- Issue #22: Unit Tests for Repositories (this project)
- Issue #23: Integration Tests with MongoDB Container (separate project)
- Issue #20: MongoPolicyRepository implementation
- Issue #19: MongoProcessRepository implementation
