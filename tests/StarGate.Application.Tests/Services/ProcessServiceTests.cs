namespace StarGate.Application.Tests.Services;

using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using StarGate.Application.Services;
using StarGate.Core.Abstractions;
using StarGate.Core.Domain;
using StarGate.Core.Exceptions;
using Xunit;

public class ProcessServiceTests
{
    private readonly Mock<IProcessRepository> _repositoryMock;
    private readonly ProcessService _service;

    public ProcessServiceTests()
    {
        _repositoryMock = new Mock<IProcessRepository>();
        _service = new ProcessService(
            _repositoryMock.Object,
            NullLogger<ProcessService>.Instance);
    }

    [Fact]
    public async Task CreateProcessAsync_Should_GenerateGuid_WhenCreatingNewProcess()
    {
        // Arrange
        var clientId = "test-client";
        var processType = "order";
        var clientProcessId = "order-123";
        var idempotencyKey = "idempotency-123";

        _repositoryMock
            .Setup(r => r.GetByIdempotencyKeyAsync(clientId, idempotencyKey, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Process?)null);

        _repositoryMock
            .Setup(r => r.CreateAsync(It.IsAny<Process>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Process p, CancellationToken ct) => p);

        // Act
        var result = await _service.CreateProcessAsync(
            clientId,
            processType,
            clientProcessId,
            idempotencyKey,
            null);

        // Assert
        result.ProcessId.Should().NotBe(Guid.Empty);
        result.ClientId.Should().Be(clientId);
        result.ProcessType.Should().Be(processType);
        result.ClientProcessId.Should().Be(clientProcessId);
        result.IdempotencyKey.Should().Be(idempotencyKey);
        result.Status.Should().Be(ProcessStatus.Pending);
        result.Progress.Should().Be(0);
        result.Retryable.Should().BeTrue();

        _repositoryMock.Verify(
            r => r.CreateAsync(It.IsAny<Process>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task CreateProcessAsync_Should_ThrowDuplicateException_WhenIdempotencyKeyExists()
    {
        // Arrange
        var clientId = "test-client";
        var idempotencyKey = "idempotency-123";
        var existingProcess = CreateTestProcess();

        _repositoryMock
            .Setup(r => r.GetByIdempotencyKeyAsync(clientId, idempotencyKey, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingProcess);

        // Act
        var act = async () => await _service.CreateProcessAsync(
            clientId,
            "order",
            "order-123",
            idempotencyKey,
            null);

        // Assert
        await act.Should().ThrowAsync<DuplicateProcessException>()
            .WithMessage($"*{idempotencyKey}*");

        _repositoryMock.Verify(
            r => r.CreateAsync(It.IsAny<Process>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task GetProcessAsync_Should_ReturnProcess_WhenExists()
    {
        // Arrange
        var processId = Guid.NewGuid();
        var expectedProcess = CreateTestProcess();
        expectedProcess.ProcessId = processId;

        _repositoryMock
            .Setup(r => r.GetByIdAsync(processId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedProcess);

        // Act
        var result = await _service.GetProcessAsync(processId);

        // Assert
        result.Should().NotBeNull();
        result.ProcessId.Should().Be(processId);
    }

    [Fact]
    public async Task GetProcessAsync_Should_ThrowNotFoundException_WhenNotExists()
    {
        // Arrange
        var processId = Guid.NewGuid();

        _repositoryMock
            .Setup(r => r.GetByIdAsync(processId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Process?)null);

        // Act
        var act = async () => await _service.GetProcessAsync(processId);

        // Assert
        await act.Should().ThrowAsync<ProcessNotFoundException>()
            .WithMessage($"*{processId}*");
    }

    [Fact]
    public async Task GetProcessByClientIdAsync_Should_ReturnProcess_WhenExists()
    {
        // Arrange
        var clientId = "test-client";
        var clientProcessId = "order-123";
        var expectedProcess = CreateTestProcess();
        expectedProcess.ClientId = clientId;
        expectedProcess.ClientProcessId = clientProcessId;

        _repositoryMock
            .Setup(r => r.GetByClientProcessIdAsync(clientId, clientProcessId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedProcess);

        // Act
        var result = await _service.GetProcessByClientIdAsync(clientId, clientProcessId);

        // Assert
        result.Should().NotBeNull();
        result.ClientId.Should().Be(clientId);
        result.ClientProcessId.Should().Be(clientProcessId);
    }

    [Fact]
    public async Task GetProcessByClientIdAsync_Should_ThrowNotFoundException_WhenNotExists()
    {
        // Arrange
        var clientId = "test-client";
        var clientProcessId = "order-123";

        _repositoryMock
            .Setup(r => r.GetByClientProcessIdAsync(clientId, clientProcessId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Process?)null);

        // Act
        var act = async () => await _service.GetProcessByClientIdAsync(clientId, clientProcessId);

        // Assert
        await act.Should().ThrowAsync<ProcessNotFoundException>()
            .WithMessage($"*{clientId}*")
            .WithMessage($"*{clientProcessId}*");
    }

    [Theory]
    [InlineData(ProcessStatus.Pending, ProcessStatus.Accepted)]
    [InlineData(ProcessStatus.Pending, ProcessStatus.Rejected)]
    [InlineData(ProcessStatus.Accepted, ProcessStatus.Processing)]
    [InlineData(ProcessStatus.Processing, ProcessStatus.Completed)]
    [InlineData(ProcessStatus.Processing, ProcessStatus.Failed)]
    public async Task UpdateProcessStatusAsync_Should_UpdateStatus_WhenValidTransition(
        ProcessStatus currentStatus,
        ProcessStatus newStatus)
    {
        // Arrange
        var processId = Guid.NewGuid();
        var process = CreateTestProcess();
        process.ProcessId = processId;
        process.Status = currentStatus;

        _repositoryMock
            .Setup(r => r.GetByIdAsync(processId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(process);

        _repositoryMock
            .Setup(r => r.UpdateAsync(It.IsAny<Process>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Process p, CancellationToken ct) => p);

        // Act
        await _service.UpdateProcessStatusAsync(processId, newStatus);

        // Assert
        _repositoryMock.Verify(
            r => r.UpdateAsync(
                It.Is<Process>(p => p.ProcessId == processId && p.Status == newStatus),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Theory]
    [InlineData(ProcessStatus.Completed, ProcessStatus.Processing)]
    [InlineData(ProcessStatus.Rejected, ProcessStatus.Processing)]
    [InlineData(ProcessStatus.Pending, ProcessStatus.Completed)]
    public async Task UpdateProcessStatusAsync_Should_ThrowException_WhenInvalidTransition(
        ProcessStatus currentStatus,
        ProcessStatus newStatus)
    {
        // Arrange
        var processId = Guid.NewGuid();
        var process = CreateTestProcess();
        process.ProcessId = processId;
        process.Status = currentStatus;

        _repositoryMock
            .Setup(r => r.GetByIdAsync(processId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(process);

        // Act
        var act = async () => await _service.UpdateProcessStatusAsync(processId, newStatus);

        // Assert
        await act.Should().ThrowAsync<InvalidStateTransitionException>()
            .Where(ex => ex.CurrentStatus == currentStatus && ex.NewStatus == newStatus);

        _repositoryMock.Verify(
            r => r.UpdateAsync(It.IsAny<Process>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task UpdateProcessStatusAsync_Should_SetCompletedAt_WhenStatusIsCompleted()
    {
        // Arrange
        var processId = Guid.NewGuid();
        var process = CreateTestProcess();
        process.ProcessId = processId;
        process.Status = ProcessStatus.Processing;

        _repositoryMock
            .Setup(r => r.GetByIdAsync(processId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(process);

        _repositoryMock
            .Setup(r => r.UpdateAsync(It.IsAny<Process>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Process p, CancellationToken ct) => p);

        // Act
        await _service.UpdateProcessStatusAsync(processId, ProcessStatus.Completed);

        // Assert
        _repositoryMock.Verify(
            r => r.UpdateAsync(
                It.Is<Process>(p => 
                    p.ProcessId == processId && 
                    p.Status == ProcessStatus.Completed &&
                    p.CompletedAt.HasValue &&
                    p.Progress == 100),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task UpdateProcessStatusAsync_Should_SetFailedAt_WhenStatusIsFailed()
    {
        // Arrange
        var processId = Guid.NewGuid();
        var process = CreateTestProcess();
        process.ProcessId = processId;
        process.Status = ProcessStatus.Processing;

        _repositoryMock
            .Setup(r => r.GetByIdAsync(processId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(process);

        _repositoryMock
            .Setup(r => r.UpdateAsync(It.IsAny<Process>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Process p, CancellationToken ct) => p);

        // Act
        await _service.UpdateProcessStatusAsync(processId, ProcessStatus.Failed);

        // Assert
        _repositoryMock.Verify(
            r => r.UpdateAsync(
                It.Is<Process>(p => 
                    p.ProcessId == processId && 
                    p.Status == ProcessStatus.Failed &&
                    p.FailedAt.HasValue),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    private static Process CreateTestProcess() => new()
    {
        ProcessId = Guid.NewGuid(),
        ClientId = "test-client",
        ProcessType = "order",
        ClientProcessId = "order-123",
        IdempotencyKey = "idempotency-123",
        Status = ProcessStatus.Pending,
        Progress = 0,
        Retryable = true,
        Metadata = new Dictionary<string, string>(),
        CreatedAt = DateTime.UtcNow,
        UpdatedAt = DateTime.UtcNow
    };
}
