using FluentAssertions;
using StarGate.Core.Abstractions;
using StarGate.Core.Domain;
using StarGate.Contracts.Requests;
using Xunit;

namespace StarGate.Core.Tests.Abstractions;

/// <summary>
/// Contract tests for IProcessService implementations.
/// Verifies expected behavior of any service implementation.
/// Concrete implementations must inherit this class and implement CreateService() and CleanupAsync().
/// Override test methods with [Fact] attributes to enable test execution.
/// </summary>
public abstract class IProcessServiceTests
{
    /// <summary>
    /// Creates a service instance for testing.
    /// Each test will call this to get a fresh service.
    /// </summary>
    protected abstract IProcessService CreateService();

    /// <summary>
    /// Cleans up test data after each test.
    /// Ensures tests don't interfere with each other.
    /// </summary>
    protected abstract Task CleanupAsync();

    protected virtual async Task SubmitProcessAsync_Should_CreateAndPublishProcess()
    {
        // Arrange
        IProcessService service = CreateService();
        string clientId = "test-client";
        SubmitProcessRequest request = new(
            "client-process-123",
            "order",
            new { OrderId = "ORD-001" },
            "idempotency-key-123");

        // Act
        Process process = await service.SubmitProcessAsync(clientId, request);

        // Assert
        process.Should().NotBeNull();
        process.ProcessId.Should().NotBe(Guid.Empty);
        process.ClientId.Should().Be(clientId);
        process.Status.Should().Be(ProcessStatus.Accepted);

        await CleanupAsync();
    }

    protected virtual async Task SubmitProcessAsync_Should_HandleIdempotency()
    {
        // Arrange
        IProcessService service = CreateService();
        string clientId = "test-client";
        SubmitProcessRequest request = new(
            "client-process-456",
            "order",
            new { OrderId = "ORD-002" },
            "idempotency-key-456");

        // Act
        Process process1 = await service.SubmitProcessAsync(clientId, request);
        Process process2 = await service.SubmitProcessAsync(clientId, request);

        // Assert
        process1.ProcessId.Should().Be(process2.ProcessId);

        await CleanupAsync();
    }

    protected virtual async Task GetProcessByIdAsync_Should_ReturnCachedProcess()
    {
        // Arrange
        IProcessService service = CreateService();
        string clientId = "test-client";
        SubmitProcessRequest request = new(
            "client-process-789",
            "order",
            new { OrderId = "ORD-003" },
            "idempotency-key-789");
        Process created = await service.SubmitProcessAsync(clientId, request);

        // Act
        Process? retrieved = await service.GetProcessByIdAsync(created.ProcessId);

        // Assert
        retrieved.Should().NotBeNull();
        retrieved!.ProcessId.Should().Be(created.ProcessId);

        await CleanupAsync();
    }

    protected virtual async Task UpdateProcessStatusAsync_Should_UpdateAndInvalidateCache()
    {
        // Arrange
        IProcessService service = CreateService();
        string clientId = "test-client";
        SubmitProcessRequest request = new(
            "client-process-abc",
            "order",
            new { OrderId = "ORD-004" },
            "idempotency-key-abc");
        Process created = await service.SubmitProcessAsync(clientId, request);

        // Act
        Process updated = await service.UpdateProcessStatusAsync(
            created.ProcessId,
            ProcessStatus.Completed,
            progress: 100,
            result: new { Status = "Success" });

        // Assert
        updated.Status.Should().Be(ProcessStatus.Completed);
        updated.Progress.Should().Be(100);
        updated.CompletedAt.Should().NotBeNull();

        await CleanupAsync();
    }
}
