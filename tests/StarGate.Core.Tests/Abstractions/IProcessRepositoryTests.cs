using FluentAssertions;
using StarGate.Core.Abstractions;
using StarGate.Core.Domain;

namespace StarGate.Core.Tests.Abstractions;

/// <summary>
/// Contract tests for IProcessRepository implementations.
/// These tests verify the expected behavior of any repository implementation.
/// Concrete repository implementations (MongoDB, in-memory, etc.) must inherit this class,
/// implement CreateRepository() and CleanupAsync() methods, and override test methods
/// with [Fact] attributes to enable test execution.
/// </summary>
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

    protected virtual async Task CreateAsync_Should_PersistProcess()
    {
        // Arrange
        IProcessRepository repository = CreateRepository();
        Process process = CreateValidProcess();

        // Act
        Process created = await repository.CreateAsync(process);
        Process? retrieved = await repository.GetByIdAsync(process.ProcessId);

        // Assert
        created.ProcessId.Should().Be(process.ProcessId);
        retrieved.Should().NotBeNull();
        retrieved!.ProcessId.Should().Be(process.ProcessId);
        retrieved.ClientProcessId.Should().Be(process.ClientProcessId);
        retrieved.Status.Should().Be(ProcessStatus.Accepted);

        await CleanupAsync();
    }

    protected virtual async Task GetByIdAsync_Should_ReturnNull_WhenNotFound()
    {
        // Arrange
        IProcessRepository repository = CreateRepository();
        Guid nonExistentId = Guid.NewGuid();

        // Act
        Process? result = await repository.GetByIdAsync(nonExistentId);

        // Assert
        result.Should().BeNull();

        await CleanupAsync();
    }

    protected virtual async Task GetByClientProcessIdAsync_Should_EnableIdempotency()
    {
        // Arrange
        IProcessRepository repository = CreateRepository();
        Process process = CreateValidProcess();
        await repository.CreateAsync(process);

        // Act
        Process? retrieved = await repository.GetByClientProcessIdAsync(
            process.ClientId,
            process.ClientProcessId);

        // Assert
        retrieved.Should().NotBeNull();
        retrieved!.ProcessId.Should().Be(process.ProcessId);
        retrieved.ClientId.Should().Be(process.ClientId);
        retrieved.ClientProcessId.Should().Be(process.ClientProcessId);

        await CleanupAsync();
    }

    protected virtual async Task GetByClientProcessIdAsync_Should_ReturnNull_WhenNotFound()
    {
        // Arrange
        IProcessRepository repository = CreateRepository();

        // Act
        Process? result = await repository.GetByClientProcessIdAsync(
            "non-existent-client",
            "non-existent-process");

        // Assert
        result.Should().BeNull();

        await CleanupAsync();
    }

    protected virtual async Task UpdateAsync_Should_ModifyExistingProcess()
    {
        // Arrange
        IProcessRepository repository = CreateRepository();
        Process process = CreateValidProcess();
        await repository.CreateAsync(process);

        // Act
        Process updated = process with
        {
            Status = ProcessStatus.Completed,
            Progress = 100,
            CompletedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        await repository.UpdateAsync(updated);
        Process? retrieved = await repository.GetByIdAsync(process.ProcessId);

        // Assert
        retrieved.Should().NotBeNull();
        retrieved!.Status.Should().Be(ProcessStatus.Completed);
        retrieved.Progress.Should().Be(100);
        retrieved.CompletedAt.Should().NotBeNull();
        retrieved.CompletedAt.Should().BeCloseTo(updated.CompletedAt!.Value, TimeSpan.FromSeconds(1));

        await CleanupAsync();
    }

    protected virtual async Task GetByStatusAsync_Should_ReturnProcessesWithMatchingStatus()
    {
        // Arrange
        IProcessRepository repository = CreateRepository();
        Process acceptedProcess = CreateValidProcess() with { Status = ProcessStatus.Accepted };
        Process processingProcess = CreateValidProcess() with { Status = ProcessStatus.Processing };
        Process completedProcess = CreateValidProcess() with { Status = ProcessStatus.Completed };

        await repository.CreateAsync(acceptedProcess);
        await repository.CreateAsync(processingProcess);
        await repository.CreateAsync(completedProcess);

        // Act
        IReadOnlyList<Process> acceptedResults = await repository.GetByStatusAsync(ProcessStatus.Accepted);
        IReadOnlyList<Process> processingResults = await repository.GetByStatusAsync(ProcessStatus.Processing);

        // Assert
        acceptedResults.Should().ContainSingle(p => p.ProcessId == acceptedProcess.ProcessId);
        processingResults.Should().ContainSingle(p => p.ProcessId == processingProcess.ProcessId);

        await CleanupAsync();
    }

    protected virtual async Task GetByClientIdAsync_Should_ReturnClientProcesses()
    {
        // Arrange
        IProcessRepository repository = CreateRepository();
        string clientId = "test-client-123";
        string otherClientId = "other-client-456";

        Process clientProcess1 = CreateValidProcess() with { ClientId = clientId };
        Process clientProcess2 = CreateValidProcess() with { ClientId = clientId };
        Process otherClientProcess = CreateValidProcess() with { ClientId = otherClientId };

        await repository.CreateAsync(clientProcess1);
        await repository.CreateAsync(clientProcess2);
        await repository.CreateAsync(otherClientProcess);

        // Act
        IReadOnlyList<Process> results = await repository.GetByClientIdAsync(clientId);

        // Assert
        results.Should().HaveCount(2);
        results.Should().AllSatisfy(p => p.ClientId.Should().Be(clientId));
        results.Should().Contain(p => p.ProcessId == clientProcess1.ProcessId);
        results.Should().Contain(p => p.ProcessId == clientProcess2.ProcessId);

        await CleanupAsync();
    }

    protected virtual async Task CountActiveProcessesAsync_Should_ReturnCorrectCount()
    {
        // Arrange
        IProcessRepository repository = CreateRepository();
        string clientId = "test-client";
        string processType = "order";

        Process acceptedProcess = CreateValidProcess() with
        {
            ClientId = clientId,
            ProcessType = processType,
            Status = ProcessStatus.Accepted
        };
        Process processingProcess = CreateValidProcess() with
        {
            ClientId = clientId,
            ProcessType = processType,
            Status = ProcessStatus.Processing
        };
        Process completedProcess = CreateValidProcess() with
        {
            ClientId = clientId,
            ProcessType = processType,
            Status = ProcessStatus.Completed
        };
        Process failedProcess = CreateValidProcess() with
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
        int count = await repository.CountActiveProcessesAsync(clientId, processType);

        // Assert
        count.Should().Be(2); // Only Accepted and Processing are active

        await CleanupAsync();
    }

    protected virtual async Task CountActiveProcessesAsync_Should_FilterByProcessType()
    {
        // Arrange
        IProcessRepository repository = CreateRepository();
        string clientId = "test-client";

        Process orderProcess = CreateValidProcess() with
        {
            ClientId = clientId,
            ProcessType = "order",
            Status = ProcessStatus.Processing
        };
        Process shippingProcess = CreateValidProcess() with
        {
            ClientId = clientId,
            ProcessType = "shipping",
            Status = ProcessStatus.Processing
        };

        await repository.CreateAsync(orderProcess);
        await repository.CreateAsync(shippingProcess);

        // Act
        int orderCount = await repository.CountActiveProcessesAsync(clientId, "order");
        int shippingCount = await repository.CountActiveProcessesAsync(clientId, "shipping");

        // Assert
        orderCount.Should().Be(1);
        shippingCount.Should().Be(1);

        await CleanupAsync();
    }

    /// <summary>
    /// Creates a valid process instance for testing.
    /// Each call returns a unique process to avoid conflicts.
    /// </summary>
    protected static Process CreateValidProcess() => new()
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
