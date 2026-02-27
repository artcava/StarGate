using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using StarGate.Application.Services;
using StarGate.Core.Abstractions;
using StarGate.Core.Domain;
using StarGate.Core.Exceptions;

namespace StarGate.Application.Tests.Services;

/// <summary>
/// Comprehensive test suite for ProcessService state transitions.
/// Tests all valid and invalid state transitions, retry logic, progress tracking,
/// error handling, and timeout management.
/// </summary>
public class ProcessServiceStateTransitionTests
{
    private readonly Mock<IProcessRepository> _repositoryMock;
    private readonly Mock<IIdempotencyService> _idempotencyMock;
    private readonly Mock<IMessageBroker> _brokerMock;
    private readonly Mock<IPolicyProvider> _policyMock;
    private readonly ProcessService _service;

    public ProcessServiceStateTransitionTests()
    {
        _repositoryMock = new Mock<IProcessRepository>();
        _idempotencyMock = new Mock<IIdempotencyService>();
        _brokerMock = new Mock<IMessageBroker>();
        _policyMock = new Mock<IPolicyProvider>();
        _service = new ProcessService(
            _repositoryMock.Object,
            _idempotencyMock.Object,
            _brokerMock.Object,
            _policyMock.Object,
            NullLogger<ProcessService>.Instance);
    }

    #region Valid Transitions Tests

    [Theory]
    [InlineData(ProcessStatus.Pending, ProcessStatus.Accepted)]
    [InlineData(ProcessStatus.Pending, ProcessStatus.Rejected)]
    [InlineData(ProcessStatus.Accepted, ProcessStatus.Processing)]
    [InlineData(ProcessStatus.Processing, ProcessStatus.Completed)]
    [InlineData(ProcessStatus.Processing, ProcessStatus.Failed)]
    [InlineData(ProcessStatus.Processing, ProcessStatus.Retrying)]
    [InlineData(ProcessStatus.Retrying, ProcessStatus.Processing)]
    [InlineData(ProcessStatus.Retrying, ProcessStatus.Failed)]
    public async Task UpdateProcessStatusAsync_Should_AllowValidTransition(
        ProcessStatus from,
        ProcessStatus to)
    {
        // Arrange
        var process = CreateTestProcess(status: from);
        _repositoryMock
            .Setup(r => r.GetByIdAsync(process.ProcessId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(process);

        // Act
        var result = await _service.UpdateProcessStatusAsync(process.ProcessId, to);

        // Assert
        result.Should().NotBeNull();
        result.ProcessId.Should().Be(process.ProcessId);
        result.Status.Should().Be(to);
        _repositoryMock.Verify(
            r => r.UpdateAsync(
                It.Is<Process>(p => p.ProcessId == process.ProcessId && p.Status == to),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    #endregion

    #region Invalid Transitions Tests

    [Theory]
    [InlineData(ProcessStatus.Completed, ProcessStatus.Processing)]
    [InlineData(ProcessStatus.Failed, ProcessStatus.Processing)]
    [InlineData(ProcessStatus.Rejected, ProcessStatus.Processing)]
    [InlineData(ProcessStatus.Pending, ProcessStatus.Completed)]
    [InlineData(ProcessStatus.Pending, ProcessStatus.Failed)]
    [InlineData(ProcessStatus.Pending, ProcessStatus.Processing)]
    [InlineData(ProcessStatus.Accepted, ProcessStatus.Completed)]
    [InlineData(ProcessStatus.Accepted, ProcessStatus.Retrying)]
    public async Task UpdateProcessStatusAsync_Should_RejectInvalidTransition(
        ProcessStatus from,
        ProcessStatus to)
    {
        // Arrange
        var process = CreateTestProcess(status: from);
        _repositoryMock
            .Setup(r => r.GetByIdAsync(process.ProcessId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(process);

        // Act
        var act = async () => await _service.UpdateProcessStatusAsync(process.ProcessId, to);

        // Assert
        await act.Should().ThrowAsync<InvalidStateTransitionException>();
    }

    #endregion

    #region Complete Process Tests

    [Fact]
    public async Task CompleteProcessAsync_Should_SetCompletedAtAndProgress100()
    {
        // Arrange
        var process = CreateTestProcess(status: ProcessStatus.Processing);

        _repositoryMock
            .Setup(r => r.GetByIdAsync(process.ProcessId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(process);

        // Act
        var result = await _service.CompleteProcessAsync(process.ProcessId);

        // Assert
        result.Should().NotBeNull();
        result.Status.Should().Be(ProcessStatus.Completed);
        result.Progress.Should().Be(100);
        result.CompletedAt.Should().NotBeNull();
        result.CompletedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task CompleteProcessAsync_Should_ThrowException_WhenNotInProcessingState()
    {
        // Arrange
        var process = CreateTestProcess(status: ProcessStatus.Accepted);
        _repositoryMock
            .Setup(r => r.GetByIdAsync(process.ProcessId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(process);

        // Act
        var act = async () => await _service.CompleteProcessAsync(process.ProcessId);

        // Assert
        await act.Should().ThrowAsync<InvalidStateTransitionException>();
    }

    #endregion

    #region Fail Process Tests

    [Fact]
    public async Task FailProcessAsync_Should_TransitionToRetrying_WhenCanRetryAndNotExceeded()
    {
        // Arrange
        var process = CreateTestProcess(
            status: ProcessStatus.Processing,
            maxRetries: 3,
            retryCount: 1);

        _repositoryMock
            .Setup(r => r.GetByIdAsync(process.ProcessId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(process);

        // Act
        var result = await _service.FailProcessAsync(
            process.ProcessId,
            "TEST_ERROR",
            "Test error message",
            canRetry: true);

        // Assert
        result.Should().NotBeNull();
        result.Status.Should().Be(ProcessStatus.Retrying);
        result.RetryCount.Should().Be(2);
        result.Errors.Should().ContainSingle()
            .Which.ErrorCode.Should().Be("TEST_ERROR");
    }

    [Fact]
    public async Task FailProcessAsync_Should_TransitionToFailed_WhenRetryLimitExceeded()
    {
        // Arrange
        var process = CreateTestProcess(
            status: ProcessStatus.Processing,
            maxRetries: 3,
            retryCount: 3); // At limit

        _repositoryMock
            .Setup(r => r.GetByIdAsync(process.ProcessId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(process);

        // Act
        var result = await _service.FailProcessAsync(
            process.ProcessId,
            "TEST_ERROR",
            "Test error message",
            canRetry: true);

        // Assert
        result.Should().NotBeNull();
        result.Status.Should().Be(ProcessStatus.Failed);
        result.FailedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task FailProcessAsync_Should_TransitionToFailed_WhenNotRetryable()
    {
        // Arrange
        var process = CreateTestProcess(
            status: ProcessStatus.Processing,
            retryable: false);

        _repositoryMock
            .Setup(r => r.GetByIdAsync(process.ProcessId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(process);

        // Act
        var result = await _service.FailProcessAsync(
            process.ProcessId,
            "FATAL_ERROR",
            "Fatal error",
            canRetry: true);

        // Assert
        result.Should().NotBeNull();
        result.Status.Should().Be(ProcessStatus.Failed);
    }

    [Fact]
    public async Task FailProcessAsync_Should_TransitionToFailed_WhenCanRetryIsFalse()
    {
        // Arrange
        var process = CreateTestProcess(
            status: ProcessStatus.Processing,
            maxRetries: 3,
            retryCount: 0);

        _repositoryMock
            .Setup(r => r.GetByIdAsync(process.ProcessId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(process);

        // Act
        var result = await _service.FailProcessAsync(
            process.ProcessId,
            "NON_RETRYABLE_ERROR",
            "Non-retryable error",
            canRetry: false);

        // Assert
        result.Should().NotBeNull();
        result.Status.Should().Be(ProcessStatus.Failed);
        result.FailedAt.Should().NotBeNull();
    }

    #endregion

    #region Reject Process Tests

    [Fact]
    public async Task RejectProcessAsync_Should_TransitionToRejected()
    {
        // Arrange
        var process = CreateTestProcess(status: ProcessStatus.Pending);

        _repositoryMock
            .Setup(r => r.GetByIdAsync(process.ProcessId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(process);

        // Act
        var result = await _service.RejectProcessAsync(process.ProcessId, "Validation failed");

        // Assert
        result.Should().NotBeNull();
        result.Status.Should().Be(ProcessStatus.Rejected);
        result.Errors.Should().ContainSingle()
            .Which.ErrorCode.Should().Be("PROCESS_REJECTED");
    }

    [Fact]
    public async Task RejectProcessAsync_Should_ThrowException_WhenNotInPendingState()
    {
        // Arrange
        var process = CreateTestProcess(status: ProcessStatus.Processing);
        _repositoryMock
            .Setup(r => r.GetByIdAsync(process.ProcessId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(process);

        // Act
        var act = async () => await _service.RejectProcessAsync(process.ProcessId, "Rejected");

        // Assert
        await act.Should().ThrowAsync<InvalidStateTransitionException>();
    }

    #endregion

    #region Progress Tracking Tests

    [Theory]
    [InlineData(-1)]
    [InlineData(101)]
    [InlineData(-100)]
    [InlineData(150)]
    public async Task UpdateProcessProgressAsync_Should_ThrowException_WhenProgressOutOfRange(int invalidProgress)
    {
        // Arrange
        var processId = Guid.NewGuid();

        // Act
        var act = async () => await _service.UpdateProcessProgressAsync(processId, invalidProgress);

        // Assert
        await act.Should().ThrowAsync<ArgumentOutOfRangeException>();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(25)]
    [InlineData(50)]
    [InlineData(75)]
    [InlineData(100)]
    public async Task UpdateProcessProgressAsync_Should_UpdateProgress_WhenValidRange(int validProgress)
    {
        // Arrange
        var process = CreateTestProcess(status: ProcessStatus.Processing);

        _repositoryMock
            .Setup(r => r.GetByIdAsync(process.ProcessId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(process);

        // Act
        var result = await _service.UpdateProcessProgressAsync(process.ProcessId, validProgress);

        // Assert
        result.Should().NotBeNull();
        result.Progress.Should().Be(validProgress);
    }

    #endregion

    #region Timeout Tests

    [Fact]
    public async Task CheckTimeoutAsync_Should_FailProcess_WhenTimedOut()
    {
        // Arrange
        var process = CreateTestProcess(
            status: ProcessStatus.Processing,
            timeoutAt: DateTime.UtcNow.AddSeconds(-10)); // Already timed out

        _repositoryMock
            .Setup(r => r.GetByIdAsync(process.ProcessId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(process);

        // Act
        var result = await _service.CheckTimeoutAsync(process.ProcessId);

        // Assert
        result.Should().NotBeNull();
        result.Errors.Should().ContainSingle()
            .Which.ErrorCode.Should().Be("PROCESS_TIMEOUT");
    }

    [Fact]
    public async Task CheckTimeoutAsync_Should_ReturnUnchanged_WhenNotTimedOut()
    {
        // Arrange
        var process = CreateTestProcess(
            status: ProcessStatus.Processing,
            timeoutAt: DateTime.UtcNow.AddHours(1)); // Not timed out

        _repositoryMock
            .Setup(r => r.GetByIdAsync(process.ProcessId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(process);

        // Act
        var result = await _service.CheckTimeoutAsync(process.ProcessId);

        // Assert
        result.Should().NotBeNull();
        result.ProcessId.Should().Be(process.ProcessId);
        result.Status.Should().Be(ProcessStatus.Processing);
        _repositoryMock.Verify(
            r => r.UpdateAsync(It.IsAny<Process>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task CheckTimeoutAsync_Should_ReturnUnchanged_WhenTimeoutAtIsNull()
    {
        // Arrange
        var process = CreateTestProcess(
            status: ProcessStatus.Processing,
            timeoutAt: null); // No timeout

        _repositoryMock
            .Setup(r => r.GetByIdAsync(process.ProcessId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(process);

        // Act
        var result = await _service.CheckTimeoutAsync(process.ProcessId);

        // Assert
        result.Should().NotBeNull();
        result.ProcessId.Should().Be(process.ProcessId);
        _repositoryMock.Verify(
            r => r.UpdateAsync(It.IsAny<Process>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    #endregion

    #region Error Recording Tests

    [Fact]
    public async Task RecordProcessErrorAsync_Should_AddErrorToProcess()
    {
        // Arrange
        var process = CreateTestProcess(status: ProcessStatus.Processing);

        _repositoryMock
            .Setup(r => r.GetByIdAsync(process.ProcessId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(process);

        // Act
        var result = await _service.RecordProcessErrorAsync(
            process.ProcessId,
            "ERROR_CODE",
            "Error message",
            retryable: true);

        // Assert
        result.Should().NotBeNull();
        result.Errors.Should().ContainSingle();
        result.Errors![0].ErrorCode.Should().Be("ERROR_CODE");
        result.Errors[0].Message.Should().Be("Error message");
        result.Errors[0].Retryable.Should().BeTrue();
    }

    [Fact]
    public async Task RecordProcessErrorAsync_Should_AddMultipleErrors()
    {
        // Arrange
        var process = CreateTestProcess(status: ProcessStatus.Processing);
        var processWithError1 = process.AddError("ERROR_1", "First error", true);
        var processWithError2 = processWithError1.AddError("ERROR_2", "Second error", false);

        _repositoryMock
            .SetupSequence(r => r.GetByIdAsync(process.ProcessId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(process)
            .ReturnsAsync(processWithError1)
            .ReturnsAsync(processWithError2);

        // Act
        var result1 = await _service.RecordProcessErrorAsync(process.ProcessId, "ERROR_1", "First error", true);
        var result2 = await _service.RecordProcessErrorAsync(process.ProcessId, "ERROR_2", "Second error", false);
        var result3 = await _service.RecordProcessErrorAsync(process.ProcessId, "ERROR_3", "Third error", true);

        // Assert
        result3.Errors.Should().HaveCount(3);
        result3.Errors![0].ErrorCode.Should().Be("ERROR_1");
        result3.Errors[1].ErrorCode.Should().Be("ERROR_2");
        result3.Errors[2].ErrorCode.Should().Be("ERROR_3");
    }

    #endregion

    #region Retry Count Tests

    [Fact]
    public async Task IncrementRetryCountAsync_Should_IncrementCounter()
    {
        // Arrange
        var process = CreateTestProcess(
            status: ProcessStatus.Retrying,
            retryCount: 2);

        _repositoryMock
            .Setup(r => r.GetByIdAsync(process.ProcessId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(process);

        // Act
        var result = await _service.IncrementRetryCountAsync(process.ProcessId);

        // Assert
        result.Should().NotBeNull();
        result.RetryCount.Should().Be(3);
    }

    #endregion

    #region Transition to Processing Tests

    [Fact]
    public async Task TransitionToProcessingAsync_Should_TransitionFromAccepted()
    {
        // Arrange
        var process = CreateTestProcess(status: ProcessStatus.Accepted);

        _repositoryMock
            .Setup(r => r.GetByIdAsync(process.ProcessId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(process);

        // Act
        var result = await _service.TransitionToProcessingAsync(process.ProcessId);

        // Assert
        result.Should().NotBeNull();
        result.Status.Should().Be(ProcessStatus.Processing);
    }

    [Fact]
    public async Task TransitionToProcessingAsync_Should_TransitionFromRetrying()
    {
        // Arrange
        var process = CreateTestProcess(status: ProcessStatus.Retrying);

        _repositoryMock
            .Setup(r => r.GetByIdAsync(process.ProcessId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(process);

        // Act
        var result = await _service.TransitionToProcessingAsync(process.ProcessId);

        // Assert
        result.Should().NotBeNull();
        result.Status.Should().Be(ProcessStatus.Processing);
    }

    #endregion

    #region Helper Methods

    /// <summary>
    /// Creates a test process with configurable properties.
    /// </summary>
    private Process CreateTestProcess(
        ProcessStatus status = ProcessStatus.Pending,
        int maxRetries = 3,
        int retryCount = 0,
        bool retryable = true,
        DateTime? timeoutAt = null)
    {
        return new Process
        {
            ProcessId = Guid.NewGuid(),
            ClientId = "test-client",
            ProcessType = "order",
            ClientProcessId = "order-123",
            IdempotencyKey = "idempotency-123",
            Status = status,
            Progress = 0,
            Retryable = retryable,
            MaxRetries = maxRetries,
            RetryCount = retryCount,
            TimeoutAt = timeoutAt,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
    }

    #endregion
}
