using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using StarGate.Core.Domain;
using StarGate.Infrastructure.Persistence;
using StarGate.Integration.Tests.Fixtures;
using Xunit;

namespace StarGate.Integration.Tests.Persistence;

/// <summary>
/// Integration tests for MongoProcessRepository timeout-related methods.
/// Tests GetTimedOutProcessesAsync with real MongoDB instance.
/// </summary>
[Trait("Category", "Integration")]
public class MongoProcessRepositoryTimeoutTests : IClassFixture<MongoDbFixture>, IAsyncLifetime
{
    private readonly MongoDbFixture _fixture;
    private readonly MongoProcessRepository _repository;

    public MongoProcessRepositoryTimeoutTests(MongoDbFixture fixture)
    {
        _fixture = fixture;
        _repository = new MongoProcessRepository(
            _fixture.Database,
            NullLogger<MongoProcessRepository>.Instance);
    }

    public Task InitializeAsync() => Task.CompletedTask;

    public async Task DisposeAsync()
    {
        await _fixture.ResetDatabaseAsync();
    }

    [Fact]
    public async Task GetTimedOutProcessesAsync_Should_ReturnProcesses_WhenTimeoutExceeded()
    {
        // Arrange
        var timedOutProcess = CreateValidProcess();
        timedOutProcess.TimeoutAt = DateTime.UtcNow.AddMinutes(-5); // Timed out 5 minutes ago
        timedOutProcess.Status = ProcessStatus.Processing;

        var activeProcess = CreateValidProcess();
        activeProcess.TimeoutAt = DateTime.UtcNow.AddHours(1); // Still has time
        activeProcess.Status = ProcessStatus.Processing;

        await _repository.CreateAsync(timedOutProcess);
        await _repository.CreateAsync(activeProcess);

        // Act
        var result = await _repository.GetTimedOutProcessesAsync();

        // Assert
        result.Should().ContainSingle()
            .Which.ProcessId.Should().Be(timedOutProcess.ProcessId);
    }

    [Fact]
    public async Task GetTimedOutProcessesAsync_Should_NotReturnCompletedProcesses()
    {
        // Arrange
        var completedProcess = CreateValidProcess();
        completedProcess.TimeoutAt = DateTime.UtcNow.AddMinutes(-5);
        completedProcess.Status = ProcessStatus.Completed;

        await _repository.CreateAsync(completedProcess);

        // Act
        var result = await _repository.GetTimedOutProcessesAsync();

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task GetTimedOutProcessesAsync_Should_NotReturnFailedProcesses()
    {
        // Arrange
        var failedProcess = CreateValidProcess();
        failedProcess.TimeoutAt = DateTime.UtcNow.AddMinutes(-5);
        failedProcess.Status = ProcessStatus.Failed;

        await _repository.CreateAsync(failedProcess);

        // Act
        var result = await _repository.GetTimedOutProcessesAsync();

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task GetTimedOutProcessesAsync_Should_ReturnAcceptedTimedOutProcesses()
    {
        // Arrange
        var acceptedProcess = CreateValidProcess();
        acceptedProcess.TimeoutAt = DateTime.UtcNow.AddMinutes(-1);
        acceptedProcess.Status = ProcessStatus.Accepted;

        await _repository.CreateAsync(acceptedProcess);

        // Act
        var result = await _repository.GetTimedOutProcessesAsync();

        // Assert
        result.Should().ContainSingle()
            .Which.ProcessId.Should().Be(acceptedProcess.ProcessId);
    }

    [Fact]
    public async Task GetTimedOutProcessesAsync_Should_ReturnRetryingTimedOutProcesses()
    {
        // Arrange
        var retryingProcess = CreateValidProcess();
        retryingProcess.TimeoutAt = DateTime.UtcNow.AddMinutes(-2);
        retryingProcess.Status = ProcessStatus.Retrying;

        await _repository.CreateAsync(retryingProcess);

        // Act
        var result = await _repository.GetTimedOutProcessesAsync();

        // Assert
        result.Should().ContainSingle()
            .Which.ProcessId.Should().Be(retryingProcess.ProcessId);
    }

    [Fact]
    public async Task GetTimedOutProcessesAsync_Should_NotReturnProcesses_WhenTimeoutNotSet()
    {
        // Arrange
        var processWithoutTimeout = CreateValidProcess();
        processWithoutTimeout.TimeoutAt = null;
        processWithoutTimeout.Status = ProcessStatus.Processing;

        await _repository.CreateAsync(processWithoutTimeout);

        // Act
        var result = await _repository.GetTimedOutProcessesAsync();

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task GetTimedOutProcessesAsync_Should_NotReturnProcesses_WhenTimeoutNotExceeded()
    {
        // Arrange
        var futureTimeout = CreateValidProcess();
        futureTimeout.TimeoutAt = DateTime.UtcNow.AddMinutes(10);
        futureTimeout.Status = ProcessStatus.Processing;

        await _repository.CreateAsync(futureTimeout);

        // Act
        var result = await _repository.GetTimedOutProcessesAsync();

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task GetTimedOutProcessesAsync_Should_LimitResults_To100()
    {
        // Arrange - Create 150 timed-out processes
        for (int i = 0; i < 150; i++)
        {
            var process = CreateValidProcess();
            process.TimeoutAt = DateTime.UtcNow.AddMinutes(-5);
            process.Status = ProcessStatus.Processing;

            await _repository.CreateAsync(process);
        }

        // Act
        var result = await _repository.GetTimedOutProcessesAsync();

        // Assert
        result.Should().HaveCount(100);
    }

    [Fact]
    public async Task GetTimedOutProcessesAsync_Should_ReturnEmpty_WhenNoTimedOutProcesses()
    {
        // Arrange - Create only active processes with future timeouts
        var activeProcess1 = CreateValidProcess();
        activeProcess1.TimeoutAt = DateTime.UtcNow.AddHours(1);
        activeProcess1.Status = ProcessStatus.Processing;

        var activeProcess2 = CreateValidProcess();
        activeProcess2.TimeoutAt = DateTime.UtcNow.AddMinutes(30);
        activeProcess2.Status = ProcessStatus.Accepted;

        await _repository.CreateAsync(activeProcess1);
        await _repository.CreateAsync(activeProcess2);

        // Act
        var result = await _repository.GetTimedOutProcessesAsync();

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task GetTimedOutProcessesAsync_Should_ReturnMultipleStatuses()
    {
        // Arrange
        var acceptedTimedOut = CreateValidProcess();
        acceptedTimedOut.TimeoutAt = DateTime.UtcNow.AddMinutes(-1);
        acceptedTimedOut.Status = ProcessStatus.Accepted;

        var processingTimedOut = CreateValidProcess();
        processingTimedOut.TimeoutAt = DateTime.UtcNow.AddMinutes(-2);
        processingTimedOut.Status = ProcessStatus.Processing;

        var retryingTimedOut = CreateValidProcess();
        retryingTimedOut.TimeoutAt = DateTime.UtcNow.AddMinutes(-3);
        retryingTimedOut.Status = ProcessStatus.Retrying;

        await _repository.CreateAsync(acceptedTimedOut);
        await _repository.CreateAsync(processingTimedOut);
        await _repository.CreateAsync(retryingTimedOut);

        // Act
        var result = await _repository.GetTimedOutProcessesAsync();

        // Assert
        result.Should().HaveCount(3);
        result.Should().Contain(p => p.ProcessId == acceptedTimedOut.ProcessId);
        result.Should().Contain(p => p.ProcessId == processingTimedOut.ProcessId);
        result.Should().Contain(p => p.ProcessId == retryingTimedOut.ProcessId);
    }

    private static Process CreateValidProcess() => new()
    {
        ProcessId = Guid.NewGuid(),
        ClientProcessId = $"client-{Guid.NewGuid()}",
        ProcessType = "test-order",
        ClientId = "test-client",
        Status = ProcessStatus.Accepted,
        Progress = 0,
        CreatedAt = DateTime.UtcNow,
        UpdatedAt = DateTime.UtcNow,
        IdempotencyKey = Guid.NewGuid().ToString(),
        Retryable = true
    };
}
