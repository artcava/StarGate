using FluentAssertions;
using StarGate.Core.Abstractions;
using StarGate.Core.Domain;
using Xunit;

namespace StarGate.Core.Tests.Abstractions;

/// <summary>
/// Contract tests for IProcessRepository implementations.
/// These tests verify the expected behavior of any repository implementation.
/// Concrete repository implementations (MongoDB, in-memory, etc.) must inherit this class
/// and implement CreateRepository() and CleanupAsync() methods.
/// </summary>
[Trait("Category", "ContractTest")]
public abstract class IProcessRepositoryTests
{
    /// <summary>
    /// Creates a repository instance for testing.
    /// Each test will call this to get a fresh repository.
    /// </summary>
    protected abstract IProcessRepository CreateRepository();

    /// <summary>
    /// Cleans up test data after each test.
    /// Ensures tests don't interfere with each other.
    /// </summary>
    protected abstract Task CleanupAsync();

    [Fact]
    public async Task CreateAsync_Should_PersistProcess()
    {
        // Arrange
        var repository = CreateRepository();
        var process = CreateValidProcess();

        // Act
        var created = await repository.CreateAsync(process);
        var retrieved = await repository.GetByIdAsync(process.ProcessId);

        // Assert
        created.ProcessId.Should().Be(process.ProcessId);
        retrieved.Should().NotBeNull();
        retrieved!.ProcessId.Should().Be(process.ProcessId);
        retrieved.ClientProcessId.Should().Be(process.ClientProcessId);
        retrieved.Status.Should().Be(ProcessStatus.Accepted);

        await CleanupAsync();
    }

    [Fact]
    public async Task GetByIdAsync_Should_ReturnNull_WhenNotFound()
    {
        // Arrange
        var repository = CreateRepository();
        var nonExistentId = Guid.NewGuid();

        // Act
        var result = await repository.GetByIdAsync(nonExistentId);

        // Assert
        result.Should().BeNull();

        await CleanupAsync();
    }

    [Fact]
    public async Task GetByClientProcessIdAsync_Should_EnableIdempotency()
    {
        // Arrange
        var repository = CreateRepository();
        var process = CreateValidProcess();
        await repository.CreateAsync(process);

        // Act
        var retrieved = await repository.GetByClientProcessIdAsync(
            process.ClientId,
            process.ClientProcessId);

        // Assert
        retrieved.Should().NotBeNull();
        retrieved!.ProcessId.Should().Be(process.ProcessId);
        retrieved.ClientId.Should().Be(process.ClientId);
        retrieved.ClientProcessId.Should().Be(process.ClientProcessId);

        await CleanupAsync();
    }

    [Fact]
    public async Task GetByClientProcessIdAsync_Should_ReturnNull_WhenNotFound()
    {
        // Arrange
        var repository = CreateRepository();

        // Act
        var result = await repository.GetByClientProcessIdAsync(
            "non-existent-client",
            "non-existent-process");

        // Assert
        result.Should().BeNull();

        await CleanupAsync();
    }

    [Fact]
    public async Task UpdateAsync_Should_ModifyExistingProcess()
    {
        // Arrange
        var repository = CreateRepository();
        var process = CreateValidProcess();
        await repository.CreateAsync(process);

        // Act
        var updated = process with
        {
            Status = ProcessStatus.Completed,
            Progress = 100,
            CompletedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        await repository.UpdateAsync(updated);
        var retrieved = await repository.GetByIdAsync(process.ProcessId);

        // Assert
        retrieved.Should().NotBeNull();
        retrieved!.Status.Should().Be(ProcessStatus.Completed);
        retrieved.Progress.Should().Be(100);
        retrieved.CompletedAt.Should().NotBeNull();
        retrieved.CompletedAt.Should().BeCloseTo(updated.CompletedAt!.Value, TimeSpan.FromSeconds(1));

        await CleanupAsync();
    }

    [Fact]
    public async Task GetByStatusAsync_Should_ReturnProcessesWithMatchingStatus()
    {
        // Arrange
        var repository = CreateRepository();
        var acceptedProcess = CreateValidProcess() with { Status = ProcessStatus.Accepted };
        var processingProcess = CreateValidProcess() with { Status = ProcessStatus.Processing };
        var completedProcess = CreateValidProcess() with { Status = ProcessStatus.Completed };

        await repository.CreateAsync(acceptedProcess);
        await repository.CreateAsync(processingProcess);
        await repository.CreateAsync(completedProcess);

        // Act
        var acceptedResults = await repository.GetByStatusAsync(ProcessStatus.Accepted);
        var processingResults = await repository.GetByStatusAsync(ProcessStatus.Processing);

        // Assert
        acceptedResults.Should().ContainSingle(p => p.ProcessId == acceptedProcess.ProcessId);
        processingResults.Should().ContainSingle(p => p.ProcessId == processingProcess.ProcessId);

        await CleanupAsync();
    }

    [Fact]
    public async Task GetByClientIdAsync_Should_ReturnClientProcesses()
    {
        // Arrange
        var repository = CreateRepository();
        var clientId = "test-client-123";
        var otherClientId = "other-client-456";

        var clientProcess1 = CreateValidProcess() with { ClientId = clientId };
        var clientProcess2 = CreateValidProcess() with { ClientId = clientId };
        var otherClientProcess = CreateValidProcess() with { ClientId = otherClientId };

        await repository.CreateAsync(clientProcess1);
        await repository.CreateAsync(clientProcess2);
        await repository.CreateAsync(otherClientProcess);

        // Act
        var results = await repository.GetByClientIdAsync(clientId);

        // Assert
        results.Should().HaveCount(2);
        results.Should().AllSatisfy(p => p.ClientId.Should().Be(clientId));
        results.Should().Contain(p => p.ProcessId == clientProcess1.ProcessId);
        results.Should().Contain(p => p.ProcessId == clientProcess2.ProcessId);

        await CleanupAsync();
    }

    [Fact]
    public async Task CountActiveProcessesAsync_Should_ReturnCorrectCount()
    {
        // Arrange
        var repository = CreateRepository();
        var clientId = "test-client";
        var processType = "order";

        var acceptedProcess = CreateValidProcess() with
        {
            ClientId = clientId,
            ProcessType = processType,
            Status = ProcessStatus.Accepted
        };
        var processingProcess = CreateValidProcess() with
        {
            ClientId = clientId,
            ProcessType = processType,
            Status = ProcessStatus.Processing
        };
        var completedProcess = CreateValidProcess() with
        {
            ClientId = clientId,
            ProcessType = processType,
            Status = ProcessStatus.Completed
        };
        var failedProcess = CreateValidProcess() with
        {
            ClientId = clientId,
            ProcessType = processType,
            Status = ProcessStatus.Failed
        };

        await repository.CreateAsync(acceptedProcess);
        await repository.CreateAsync(processingProcess);
        await repository.CreateAsync(completedProcess);
        await repository.CreateAsync(failedProcess);

        // Act
        var count = await repository.CountActiveProcessesAsync(clientId, processType);

        // Assert
        count.Should().Be(2); // Only Accepted and Processing are active

        await CleanupAsync();
    }

    [Fact]
    public async Task CountActiveProcessesAsync_Should_FilterByProcessType()
    {
        // Arrange
        var repository = CreateRepository();
        var clientId = "test-client";

        var orderProcess = CreateValidProcess() with
        {
            ClientId = clientId,
            ProcessType = "order",
            Status = ProcessStatus.Processing
        };
        var shippingProcess = CreateValidProcess() with
        {
            ClientId = clientId,
            ProcessType = "shipping",
            Status = ProcessStatus.Processing
        };

        await repository.CreateAsync(orderProcess);
        await repository.CreateAsync(shippingProcess);

        // Act
        var orderCount = await repository.CountActiveProcessesAsync(clientId, "order");
        var shippingCount = await repository.CountActiveProcessesAsync(clientId, "shipping");

        // Assert
        orderCount.Should().Be(1);
        shippingCount.Should().Be(1);

        await CleanupAsync();
    }

    /// <summary>
    /// Creates a valid process instance for testing.
    /// Each call returns a unique process to avoid conflicts.
    /// </summary>
    private static Process CreateValidProcess() => new()
    {
        ProcessId = Guid.NewGuid(),
        ClientProcessId = $"client-{Guid.NewGuid()}",
        ProcessType = "order",
        ClientId = "test-client",
        Status = ProcessStatus.Accepted,
        Progress = 0,
        CreatedAt = DateTime.UtcNow,
        UpdatedAt = DateTime.UtcNow,
        IdempotencyKey = Guid.NewGuid().ToString(),
        Retryable = true
    };
}
