using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using StarGate.Application.Services;
using StarGate.Core.Abstractions;
using StarGate.Core.Domain;
using StarGate.Core.Domain.Configuration;
using StarGate.Core.Exceptions;
using Xunit;

namespace StarGate.Application.Tests.Services;

public class ProcessServiceIntegrationTests
{
    private readonly Mock<IProcessRepository> _repositoryMock;
    private readonly Mock<IIdempotencyService> _idempotencyMock;
    private readonly Mock<IMessageBroker> _brokerMock;
    private readonly Mock<IPolicyProvider> _policyMock;
    private readonly ProcessService _service;

    public ProcessServiceIntegrationTests()
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

    [Fact]
    public async Task CompleteProcessLifecycle_Should_TransitionThroughAllStates_Successfully()
    {
        // Arrange
        var clientId = "test-client";
        var processType = "order";
        var clientProcessId = "order-123";
        var idempotencyKey = "key-123";

        SetupSuccessfulCreation();

        // Act & Assert - Create
        var process = await _service.CreateProcessAsync(
            clientId,
            processType,
            clientProcessId,
            idempotencyKey);

        process.Status.Should().Be(ProcessStatus.Accepted);
        process.Progress.Should().Be(0);

        // Act & Assert - Transition to Processing
        _repositoryMock
            .Setup(r => r.GetByIdAsync(process.ProcessId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(process);

        _repositoryMock
            .Setup(r => r.UpdateAsync(It.IsAny<Process>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Process p, CancellationToken ct) => p);

        var processingProcess = await _service.TransitionToProcessingAsync(process.ProcessId);
        processingProcess.Status.Should().Be(ProcessStatus.Processing);

        // Act & Assert - Update Progress
        _repositoryMock
            .Setup(r => r.GetByIdAsync(process.ProcessId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(processingProcess);

        var progressProcess = await _service.UpdateProcessProgressAsync(process.ProcessId, 50);
        progressProcess.Progress.Should().Be(50);

        // Act & Assert - Complete
        _repositoryMock
            .Setup(r => r.GetByIdAsync(process.ProcessId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(progressProcess);

        var completedProcess = await _service.CompleteProcessAsync(process.ProcessId);
        completedProcess.Status.Should().Be(ProcessStatus.Completed);
        completedProcess.Progress.Should().Be(100);
        completedProcess.CompletedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task ProcessWithRetry_Should_TransitionToRetrying_ThenProcessing_ThenComplete()
    {
        // Arrange
        var clientId = "test-client";
        var processType = "order";
        var clientProcessId = "order-123";
        var idempotencyKey = "key-123";

        SetupSuccessfulCreation();

        // Create process
        var process = await _service.CreateProcessAsync(
            clientId,
            processType,
            clientProcessId,
            idempotencyKey);

        _repositoryMock
            .Setup(r => r.GetByIdAsync(process.ProcessId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(process);

        _repositoryMock
            .Setup(r => r.UpdateAsync(It.IsAny<Process>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Process p, CancellationToken ct) => p);

        // Transition to Processing
        var processingProcess = await _service.TransitionToProcessingAsync(process.ProcessId);

        // Fail with retry
        _repositoryMock
            .Setup(r => r.GetByIdAsync(process.ProcessId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(processingProcess);

        var retryingProcess = await _service.FailProcessAsync(
            process.ProcessId,
            "TEMPORARY_ERROR",
            "Temporary error occurred",
            canRetry: true);

        retryingProcess.Status.Should().Be(ProcessStatus.Retrying);
        retryingProcess.RetryCount.Should().Be(1);
        retryingProcess.Errors.Should().HaveCount(1);

        // Retry - Transition back to Processing
        _repositoryMock
            .Setup(r => r.GetByIdAsync(process.ProcessId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(retryingProcess);

        var reprocessingProcess = await _service.TransitionToProcessingAsync(process.ProcessId);
        reprocessingProcess.Status.Should().Be(ProcessStatus.Processing);

        // Complete successfully
        _repositoryMock
            .Setup(r => r.GetByIdAsync(process.ProcessId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(reprocessingProcess);

        var completedProcess = await _service.CompleteProcessAsync(process.ProcessId);
        completedProcess.Status.Should().Be(ProcessStatus.Completed);
        completedProcess.RetryCount.Should().Be(1);
    }

    [Fact]
    public async Task ProcessWithMaxRetriesExceeded_Should_TransitionToFailed()
    {
        // Arrange
        var clientId = "test-client";
        var processType = "order";
        var clientProcessId = "order-123";
        var idempotencyKey = "key-123";

        SetupSuccessfulCreation();

        var process = await _service.CreateProcessAsync(
            clientId,
            processType,
            clientProcessId,
            idempotencyKey);

        _repositoryMock
            .Setup(r => r.UpdateAsync(It.IsAny<Process>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Process p, CancellationToken ct) => p);

        // Transition to Processing
        _repositoryMock
            .Setup(r => r.GetByIdAsync(process.ProcessId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(process);

        var processingProcess = await _service.TransitionToProcessingAsync(process.ProcessId);

        // Simulate 3 failures with retries
        Process currentProcess = processingProcess;
        for (int i = 1; i <= 3; i++)
        {
            _repositoryMock
                .Setup(r => r.GetByIdAsync(process.ProcessId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(currentProcess);

            currentProcess = await _service.FailProcessAsync(
                process.ProcessId,
                "TEMPORARY_ERROR",
                $"Attempt {i} failed",
                canRetry: true);

            if (i < 3)
            {
                // First two attempts should transition to Retrying
                currentProcess.Status.Should().Be(ProcessStatus.Retrying);
                currentProcess.RetryCount.Should().Be(i);

                // Transition back to processing for next attempt
                _repositoryMock
                    .Setup(r => r.GetByIdAsync(process.ProcessId, It.IsAny<CancellationToken>()))
                    .ReturnsAsync(currentProcess);

                currentProcess = await _service.TransitionToProcessingAsync(process.ProcessId);
                currentProcess.Status.Should().Be(ProcessStatus.Processing);
            }
            else
            {
                // After 3rd failure with RetryCount=3 and MaxRetries=3, should be Failed
                currentProcess.Status.Should().Be(ProcessStatus.Failed);
                currentProcess.RetryCount.Should().Be(3);
                currentProcess.FailedAt.Should().NotBeNull();
            }
        }

        currentProcess.Errors.Should().HaveCount(3);
    }

    [Fact]
    public async Task ProcessTimeout_Should_TransitionToRetrying_WithTimeoutError()
    {
        // Arrange
        var clientId = "test-client";
        var processType = "order";
        var clientProcessId = "order-123";
        var idempotencyKey = "key-123";

        SetupSuccessfulCreation();

        var process = await _service.CreateProcessAsync(
            clientId,
            processType,
            clientProcessId,
            idempotencyKey);

        // Transition to Processing first (required for timeout to work)
        _repositoryMock
            .Setup(r => r.GetByIdAsync(process.ProcessId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(process);

        _repositoryMock
            .Setup(r => r.UpdateAsync(It.IsAny<Process>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Process p, CancellationToken ct) => p);

        var processingProcess = await _service.TransitionToProcessingAsync(process.ProcessId);

        // Simulate timeout by setting TimeoutAt in the past
        var timedOutProcess = processingProcess with
        {
            TimeoutAt = DateTime.UtcNow.AddHours(-1)
        };

        _repositoryMock
            .Setup(r => r.GetByIdAsync(process.ProcessId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(timedOutProcess);

        // Act
        var result = await _service.CheckTimeoutAsync(process.ProcessId);

        // Assert
        result.Status.Should().Be(ProcessStatus.Retrying);
        result.Errors.Should().HaveCount(1);
        result.Errors![0].ErrorCode.Should().Be("PROCESS_TIMEOUT");
        result.Errors[0].Retryable.Should().BeTrue();
    }

    [Fact]
    public async Task ProcessRejection_Should_SetRejectedStatus_AndAddError()
    {
        // Arrange
        var clientId = "test-client";
        var processType = "order";
        var clientProcessId = "order-123";
        var idempotencyKey = "key-123";

        SetupSuccessfulCreation();

        var process = await _service.CreateProcessAsync(
            clientId,
            processType,
            clientProcessId,
            idempotencyKey);

        _repositoryMock
            .Setup(r => r.GetByIdAsync(process.ProcessId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(process);

        _repositoryMock
            .Setup(r => r.UpdateAsync(It.IsAny<Process>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Process p, CancellationToken ct) => p);

        // Act
        var rejectedProcess = await _service.RejectProcessAsync(
            process.ProcessId,
            "Invalid order data");

        // Assert
        rejectedProcess.Status.Should().Be(ProcessStatus.Rejected);
        rejectedProcess.Errors.Should().HaveCount(1);
        rejectedProcess.Errors![0].ErrorCode.Should().Be("PROCESS_REJECTED");
        rejectedProcess.Errors[0].Message.Should().Be("Invalid order data");
        rejectedProcess.Errors[0].Retryable.Should().BeFalse();
    }

    [Fact]
    public async Task MultipleProcessCreation_Should_GenerateUniqueIds_AndPreserveIndependence()
    {
        // Arrange
        var clientId = "test-client";
        var processType = "order";

        SetupSuccessfulCreation();

        // Act
        var process1 = await _service.CreateProcessAsync(
            clientId,
            processType,
            "order-1",
            "key-1");

        var process2 = await _service.CreateProcessAsync(
            clientId,
            processType,
            "order-2",
            "key-2");

        var process3 = await _service.CreateProcessAsync(
            clientId,
            processType,
            "order-3",
            "key-3");

        // Assert
        var processIds = new[] { process1.ProcessId, process2.ProcessId, process3.ProcessId };
        processIds.Should().OnlyHaveUniqueItems();

        process1.ClientProcessId.Should().Be("order-1");
        process2.ClientProcessId.Should().Be("order-2");
        process3.ClientProcessId.Should().Be("order-3");

        process1.IdempotencyKey.Should().Be("key-1");
        process2.IdempotencyKey.Should().Be("key-2");
        process3.IdempotencyKey.Should().Be("key-3");
    }

    [Fact]
    public async Task ProcessWithMultipleErrors_Should_AccumulateErrorList()
    {
        // Arrange
        var clientId = "test-client";
        var processType = "order";
        var clientProcessId = "order-123";
        var idempotencyKey = "key-123";

        SetupSuccessfulCreation();

        var process = await _service.CreateProcessAsync(
            clientId,
            processType,
            clientProcessId,
            idempotencyKey);

        _repositoryMock
            .Setup(r => r.UpdateAsync(It.IsAny<Process>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Process p, CancellationToken ct) => p);

        // Record first error
        _repositoryMock
            .Setup(r => r.GetByIdAsync(process.ProcessId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(process);

        var processWithError1 = await _service.RecordProcessErrorAsync(
            process.ProcessId,
            "ERROR_1",
            "First error",
            false);

        // Record second error
        _repositoryMock
            .Setup(r => r.GetByIdAsync(process.ProcessId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(processWithError1);

        var processWithError2 = await _service.RecordProcessErrorAsync(
            process.ProcessId,
            "ERROR_2",
            "Second error",
            true);

        // Record third error
        _repositoryMock
            .Setup(r => r.GetByIdAsync(process.ProcessId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(processWithError2);

        var processWithError3 = await _service.RecordProcessErrorAsync(
            process.ProcessId,
            "ERROR_3",
            "Third error",
            false);

        // Assert
        processWithError3.Errors.Should().HaveCount(3);
        processWithError3.Errors![0].ErrorCode.Should().Be("ERROR_1");
        processWithError3.Errors[1].ErrorCode.Should().Be("ERROR_2");
        processWithError3.Errors[2].ErrorCode.Should().Be("ERROR_3");
    }

    [Fact]
    public async Task ProcessWithProgressUpdates_Should_TrackProgressThroughLifecycle()
    {
        // Arrange
        var clientId = "test-client";
        var processType = "order";
        var clientProcessId = "order-123";
        var idempotencyKey = "key-123";

        SetupSuccessfulCreation();

        var process = await _service.CreateProcessAsync(
            clientId,
            processType,
            clientProcessId,
            idempotencyKey);

        _repositoryMock
            .Setup(r => r.UpdateAsync(It.IsAny<Process>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Process p, CancellationToken ct) => p);

        // Transition to Processing
        _repositoryMock
            .Setup(r => r.GetByIdAsync(process.ProcessId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(process);

        var processingProcess = await _service.TransitionToProcessingAsync(process.ProcessId);

        // Update progress incrementally
        var progressValues = new[] { 25, 50, 75 };
        Process currentProcess = processingProcess;

        foreach (var progress in progressValues)
        {
            _repositoryMock
                .Setup(r => r.GetByIdAsync(process.ProcessId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(currentProcess);

            currentProcess = await _service.UpdateProcessProgressAsync(process.ProcessId, progress);
            currentProcess.Progress.Should().Be(progress);
        }

        // Complete sets progress to 100
        _repositoryMock
            .Setup(r => r.GetByIdAsync(process.ProcessId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(currentProcess);

        var completedProcess = await _service.CompleteProcessAsync(process.ProcessId);
        completedProcess.Progress.Should().Be(100);
    }

    [Fact]
    public async Task TerminalStates_Should_PreventFurtherTransitions()
    {
        // Arrange
        var clientId = "test-client";
        var processType = "order";
        var clientProcessId = "order-123";
        var idempotencyKey = "key-123";

        SetupSuccessfulCreation();

        var process = await _service.CreateProcessAsync(
            clientId,
            processType,
            clientProcessId,
            idempotencyKey);

        _repositoryMock
            .Setup(r => r.UpdateAsync(It.IsAny<Process>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Process p, CancellationToken ct) => p);

        // Complete the process (terminal state)
        _repositoryMock
            .Setup(r => r.GetByIdAsync(process.ProcessId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(process);

        var processingProcess = await _service.TransitionToProcessingAsync(process.ProcessId);

        _repositoryMock
            .Setup(r => r.GetByIdAsync(process.ProcessId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(processingProcess);

        var completedProcess = await _service.CompleteProcessAsync(process.ProcessId);

        // Try to transition from completed (should fail)
        _repositoryMock
            .Setup(r => r.GetByIdAsync(process.ProcessId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(completedProcess);

        var act = async () => await _service.TransitionToProcessingAsync(process.ProcessId);

        // Assert
        await act.Should().ThrowAsync<InvalidStateTransitionException>()
            .Where(ex => ex.CurrentStatus == ProcessStatus.Completed);
    }

    [Fact]
    public async Task GetProcessByDifferentMethods_Should_ReturnSameProcess()
    {
        // Arrange
        var clientId = "test-client";
        var processType = "order";
        var clientProcessId = "order-123";
        var idempotencyKey = "key-123";

        SetupSuccessfulCreation();

        var createdProcess = await _service.CreateProcessAsync(
            clientId,
            processType,
            clientProcessId,
            idempotencyKey);

        // Setup both retrieval methods to return the same process
        _repositoryMock
            .Setup(r => r.GetByIdAsync(createdProcess.ProcessId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(createdProcess);

        _repositoryMock
            .Setup(r => r.GetByClientProcessIdAsync(clientId, clientProcessId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(createdProcess);

        // Act
        var processByGuid = await _service.GetProcessAsync(createdProcess.ProcessId);
        var processByClientId = await _service.GetProcessByClientIdAsync(clientId, clientProcessId);

        // Assert
        processByGuid.ProcessId.Should().Be(processByClientId.ProcessId);
        processByGuid.ClientId.Should().Be(processByClientId.ClientId);
        processByGuid.ClientProcessId.Should().Be(processByClientId.ClientProcessId);
        processByGuid.Status.Should().Be(processByClientId.Status);
    }

    private void SetupSuccessfulCreation()
    {
        var policy = new EffectivePolicy
        {
            ProcessType = "order",
            ClientId = "test-client",
            Timeout = TimeSpan.FromHours(1),
            RetryPolicy = new RetryPolicy
            {
                Enabled = true,
                MaxAttempts = 3,
                InitialDelay = TimeSpan.FromSeconds(5),
                MaxDelay = TimeSpan.FromMinutes(5),
                BackoffStrategy = BackoffStrategy.Exponential
            },
            ResultRetention = TimeSpan.FromDays(30),
            MaxConcurrentProcesses = null,
            Source = new PolicySource
            {
                TimeoutFromOverride = false,
                RetryPolicyFromOverride = false,
                ResultRetentionFromOverride = false,
                ConcurrencyLimitFromOverride = false
            }
        };

        _policyMock
            .Setup(p => p.GetEffectivePolicyAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(policy);

        _repositoryMock
            .Setup(r => r.CountActiveProcessesAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(0);

        _idempotencyMock
            .Setup(i => i.GetProcessIdByIdempotencyKeyAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((Guid?)null);

        _repositoryMock
            .Setup(r => r.GetByIdempotencyKeyAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((Process?)null);

        _idempotencyMock
            .Setup(i => i.StoreIdempotencyKeyAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<Guid>(),
                It.IsAny<TimeSpan?>(),
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _repositoryMock
            .Setup(r => r.CreateAsync(It.IsAny<Process>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Process p, CancellationToken ct) => p);

        _brokerMock
            .Setup(b => b.PublishAsync(
                It.IsAny<object>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
    }
}
