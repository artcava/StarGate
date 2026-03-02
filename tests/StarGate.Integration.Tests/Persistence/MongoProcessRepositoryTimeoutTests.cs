using FluentAssertions;
using StarGate.Core.Domain;
using Xunit;

namespace StarGate.Integration.Tests.Persistence;

/// <summary>
/// Integration tests for MongoProcessRepository timeout-related methods.
/// Tests GetTimedOutProcessesAsync with real MongoDB instance.
/// </summary>
[Collection("MongoDB")]
public class MongoProcessRepositoryTimeoutTests : MongoRepositoryTestBase
{
    [Fact]
    public async Task GetTimedOutProcessesAsync_Should_ReturnProcesses_WhenTimeoutExceeded()
    {
        // Arrange
        var timedOutProcess = CreateTestProcess(
            processType: "test-order",
            status: ProcessStatus.Processing);
        timedOutProcess.TimeoutAt = DateTime.UtcNow.AddMinutes(-5); // Timed out 5 minutes ago

        var activeProcess = CreateTestProcess(
            processType: "test-order",
            status: ProcessStatus.Processing);
        activeProcess.TimeoutAt = DateTime.UtcNow.AddHours(1); // Still has time

        await Repository.CreateAsync(timedOutProcess);
        await Repository.CreateAsync(activeProcess);

        // Act
        var result = await Repository.GetTimedOutProcessesAsync();

        // Assert
        result.Should().ContainSingle()
            .Which.ProcessId.Should().Be(timedOutProcess.ProcessId);
    }

    [Fact]
    public async Task GetTimedOutProcessesAsync_Should_NotReturnCompletedProcesses()
    {
        // Arrange
        var completedProcess = CreateTestProcess(
            processType: "test-order",
            status: ProcessStatus.Completed);
        completedProcess.TimeoutAt = DateTime.UtcNow.AddMinutes(-5);

        await Repository.CreateAsync(completedProcess);

        // Act
        var result = await Repository.GetTimedOutProcessesAsync();

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task GetTimedOutProcessesAsync_Should_NotReturnFailedProcesses()
    {
        // Arrange
        var failedProcess = CreateTestProcess(
            processType: "test-order",
            status: ProcessStatus.Failed);
        failedProcess.TimeoutAt = DateTime.UtcNow.AddMinutes(-5);

        await Repository.CreateAsync(failedProcess);

        // Act
        var result = await Repository.GetTimedOutProcessesAsync();

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task GetTimedOutProcessesAsync_Should_ReturnAcceptedTimedOutProcesses()
    {
        // Arrange
        var acceptedProcess = CreateTestProcess(
            processType: "test-order",
            status: ProcessStatus.Accepted);
        acceptedProcess.TimeoutAt = DateTime.UtcNow.AddMinutes(-1);

        await Repository.CreateAsync(acceptedProcess);

        // Act
        var result = await Repository.GetTimedOutProcessesAsync();

        // Assert
        result.Should().ContainSingle()
            .Which.ProcessId.Should().Be(acceptedProcess.ProcessId);
    }

    [Fact]
    public async Task GetTimedOutProcessesAsync_Should_ReturnRetryingTimedOutProcesses()
    {
        // Arrange
        var retryingProcess = CreateTestProcess(
            processType: "test-order",
            status: ProcessStatus.Retrying);
        retryingProcess.TimeoutAt = DateTime.UtcNow.AddMinutes(-2);

        await Repository.CreateAsync(retryingProcess);

        // Act
        var result = await Repository.GetTimedOutProcessesAsync();

        // Assert
        result.Should().ContainSingle()
            .Which.ProcessId.Should().Be(retryingProcess.ProcessId);
    }

    [Fact]
    public async Task GetTimedOutProcessesAsync_Should_NotReturnProcesses_WhenTimeoutNotSet()
    {
        // Arrange
        var processWithoutTimeout = CreateTestProcess(
            processType: "test-order",
            status: ProcessStatus.Processing);
        processWithoutTimeout.TimeoutAt = null;

        await Repository.CreateAsync(processWithoutTimeout);

        // Act
        var result = await Repository.GetTimedOutProcessesAsync();

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task GetTimedOutProcessesAsync_Should_NotReturnProcesses_WhenTimeoutNotExceeded()
    {
        // Arrange
        var futureTimeout = CreateTestProcess(
            processType: "test-order",
            status: ProcessStatus.Processing);
        futureTimeout.TimeoutAt = DateTime.UtcNow.AddMinutes(10);

        await Repository.CreateAsync(futureTimeout);

        // Act
        var result = await Repository.GetTimedOutProcessesAsync();

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task GetTimedOutProcessesAsync_Should_LimitResults_To100()
    {
        // Arrange - Create 150 timed-out processes
        for (int i = 0; i < 150; i++)
        {
            var process = CreateTestProcess(
                processType: "test-order",
                status: ProcessStatus.Processing);
            process.TimeoutAt = DateTime.UtcNow.AddMinutes(-5);

            await Repository.CreateAsync(process);
        }

        // Act
        var result = await Repository.GetTimedOutProcessesAsync();

        // Assert
        result.Should().HaveCount(100);
    }

    [Fact]
    public async Task GetTimedOutProcessesAsync_Should_ReturnEmpty_WhenNoTimedOutProcesses()
    {
        // Arrange - Create only active processes with future timeouts
        var activeProcess1 = CreateTestProcess(
            processType: "test-order",
            status: ProcessStatus.Processing);
        activeProcess1.TimeoutAt = DateTime.UtcNow.AddHours(1);

        var activeProcess2 = CreateTestProcess(
            processType: "test-order",
            status: ProcessStatus.Accepted);
        activeProcess2.TimeoutAt = DateTime.UtcNow.AddMinutes(30);

        await Repository.CreateAsync(activeProcess1);
        await Repository.CreateAsync(activeProcess2);

        // Act
        var result = await Repository.GetTimedOutProcessesAsync();

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task GetTimedOutProcessesAsync_Should_ReturnMultipleStatuses()
    {
        // Arrange
        var acceptedTimedOut = CreateTestProcess(
            processType: "test-order",
            status: ProcessStatus.Accepted);
        acceptedTimedOut.TimeoutAt = DateTime.UtcNow.AddMinutes(-1);

        var processingTimedOut = CreateTestProcess(
            processType: "test-order",
            status: ProcessStatus.Processing);
        processingTimedOut.TimeoutAt = DateTime.UtcNow.AddMinutes(-2);

        var retryingTimedOut = CreateTestProcess(
            processType: "test-order",
            status: ProcessStatus.Retrying);
        retryingTimedOut.TimeoutAt = DateTime.UtcNow.AddMinutes(-3);

        await Repository.CreateAsync(acceptedTimedOut);
        await Repository.CreateAsync(processingTimedOut);
        await Repository.CreateAsync(retryingTimedOut);

        // Act
        var result = await Repository.GetTimedOutProcessesAsync();

        // Assert
        result.Should().HaveCount(3);
        result.Should().Contain(p => p.ProcessId == acceptedTimedOut.ProcessId);
        result.Should().Contain(p => p.ProcessId == processingTimedOut.ProcessId);
        result.Should().Contain(p => p.ProcessId == retryingTimedOut.ProcessId);
    }
}
