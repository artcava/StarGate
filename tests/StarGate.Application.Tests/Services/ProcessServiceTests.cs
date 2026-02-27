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

public class ProcessServiceTests
{
    private readonly Mock<IProcessRepository> _repositoryMock;
    private readonly Mock<IIdempotencyService> _idempotencyMock;
    private readonly Mock<IMessageBroker> _brokerMock;
    private readonly Mock<IPolicyProvider> _policyMock;
    private readonly ProcessService _service;

    public ProcessServiceTests()
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
    public async Task CreateProcessAsync_Should_GenerateUniqueGuid()
    {
        // Arrange
        var clientId = "test-client";
        var processType = "order";
        var createdProcessIds = new List<Guid>();

        SetupSuccessfulCreation();

        _repositoryMock
            .Setup(r => r.CreateAsync(It.IsAny<Process>(), It.IsAny<CancellationToken>()))
            .Callback<Process, CancellationToken>((p, ct) => createdProcessIds.Add(p.ProcessId))
            .ReturnsAsync((Process p, CancellationToken ct) => p);

        // Act
        await _service.CreateProcessAsync(clientId, processType, "order-1", "key-1", null);
        await _service.CreateProcessAsync(clientId, processType, "order-2", "key-2", null);
        await _service.CreateProcessAsync(clientId, processType, "order-3", "key-3", null);

        // Assert
        createdProcessIds.Should().HaveCount(3);
        createdProcessIds.Should().OnlyHaveUniqueItems();
        createdProcessIds.Should().AllSatisfy(id => id.Should().NotBeEmpty());
    }

    [Fact]
    public async Task CreateProcessAsync_Should_SetInitialStatus_ToAccepted()
    {
        // Arrange
        Process? capturedProcess = null;
        SetupSuccessfulCreation();

        _repositoryMock
            .Setup(r => r.CreateAsync(It.IsAny<Process>(), It.IsAny<CancellationToken>()))
            .Callback<Process, CancellationToken>((p, ct) => capturedProcess = p)
            .ReturnsAsync((Process p, CancellationToken ct) => p);

        // Act
        await _service.CreateProcessAsync("test-client", "order", "order-123", "key-1", null);

        // Assert
        capturedProcess.Should().NotBeNull();
        capturedProcess!.Status.Should().Be(ProcessStatus.Accepted);
    }

    [Fact]
    public async Task CreateProcessAsync_Should_SetCreatedAtAndUpdatedAt()
    {
        // Arrange
        Process? capturedProcess = null;
        SetupSuccessfulCreation();

        _repositoryMock
            .Setup(r => r.CreateAsync(It.IsAny<Process>(), It.IsAny<CancellationToken>()))
            .Callback<Process, CancellationToken>((p, ct) => capturedProcess = p)
            .ReturnsAsync((Process p, CancellationToken ct) => p);

        var beforeCreation = DateTime.UtcNow;

        // Act
        await _service.CreateProcessAsync("test-client", "order", "order-123", "key-1", null);

        var afterCreation = DateTime.UtcNow;

        // Assert
        capturedProcess.Should().NotBeNull();
        capturedProcess!.CreatedAt.Should().BeAfter(beforeCreation.AddSeconds(-1))
            .And.BeBefore(afterCreation.AddSeconds(1));
        capturedProcess.UpdatedAt.Should().BeAfter(beforeCreation.AddSeconds(-1))
            .And.BeBefore(afterCreation.AddSeconds(1));
    }

    [Fact]
    public async Task CreateProcessAsync_Should_SetTimeoutAndRetentionFromPolicy()
    {
        // Arrange
        Process? capturedProcess = null;
        SetupSuccessfulCreation();

        _repositoryMock
            .Setup(r => r.CreateAsync(It.IsAny<Process>(), It.IsAny<CancellationToken>()))
            .Callback<Process, CancellationToken>((p, ct) => capturedProcess = p)
            .ReturnsAsync((Process p, CancellationToken ct) => p);

        var beforeCreation = DateTime.UtcNow;

        // Act
        await _service.CreateProcessAsync("test-client", "order", "order-123", "key-1", null);

        // Assert
        capturedProcess.Should().NotBeNull();
        capturedProcess!.TimeoutAt.Should().NotBeNull();
        capturedProcess.TimeoutAt!.Value.Should().BeAfter(beforeCreation.AddHours(1).AddSeconds(-1));
        capturedProcess.RetentionExpiresAt.Should().NotBeNull();
        capturedProcess.RetentionExpiresAt!.Value.Should().BeAfter(beforeCreation.AddDays(30).AddSeconds(-1));
    }

    [Fact]
    public async Task CreateProcessAsync_Should_SetRetryableFromPolicy()
    {
        // Arrange
        Process? capturedProcess = null;
        SetupSuccessfulCreation();

        _repositoryMock
            .Setup(r => r.CreateAsync(It.IsAny<Process>(), It.IsAny<CancellationToken>()))
            .Callback<Process, CancellationToken>((p, ct) => capturedProcess = p)
            .ReturnsAsync((Process p, CancellationToken ct) => p);

        // Act
        await _service.CreateProcessAsync("test-client", "order", "order-123", "key-1", null);

        // Assert
        capturedProcess.Should().NotBeNull();
        capturedProcess!.Retryable.Should().BeTrue();
        capturedProcess.MaxRetries.Should().Be(3);
        capturedProcess.RetryCount.Should().Be(0);
    }

    [Fact]
    public async Task CreateProcessAsync_Should_InitializeProgressToZero()
    {
        // Arrange
        Process? capturedProcess = null;
        SetupSuccessfulCreation();

        _repositoryMock
            .Setup(r => r.CreateAsync(It.IsAny<Process>(), It.IsAny<CancellationToken>()))
            .Callback<Process, CancellationToken>((p, ct) => capturedProcess = p)
            .ReturnsAsync((Process p, CancellationToken ct) => p);

        // Act
        await _service.CreateProcessAsync("test-client", "order", "order-123", "key-1", null);

        // Assert
        capturedProcess.Should().NotBeNull();
        capturedProcess!.Progress.Should().Be(0);
    }

    [Fact]
    public async Task GetProcessAsync_Should_ReturnProcess_WhenExists()
    {
        // Arrange
        var expectedProcess = CreateTestProcess();
        _repositoryMock
            .Setup(r => r.GetByIdAsync(expectedProcess.ProcessId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedProcess);

        // Act
        var result = await _service.GetProcessAsync(expectedProcess.ProcessId);

        // Assert
        result.Should().BeSameAs(expectedProcess);
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
            .Where(ex => ex.ProcessId == processId);
    }

    [Fact]
    public async Task GetProcessByIdAsync_Should_ReturnProcess_WhenExists()
    {
        // Arrange
        var expectedProcess = CreateTestProcess();
        _repositoryMock
            .Setup(r => r.GetByIdAsync(expectedProcess.ProcessId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedProcess);

        // Act
        var result = await _service.GetProcessByIdAsync(expectedProcess.ProcessId);

        // Assert
        result.Should().BeSameAs(expectedProcess);
    }

    [Fact]
    public async Task GetProcessByIdAsync_Should_ReturnNull_WhenNotExists()
    {
        // Arrange
        var processId = Guid.NewGuid();
        _repositoryMock
            .Setup(r => r.GetByIdAsync(processId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Process?)null);

        // Act
        var result = await _service.GetProcessByIdAsync(processId);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task GetProcessByClientIdAsync_Should_ReturnProcess_WhenExists()
    {
        // Arrange
        var clientId = "test-client";
        var clientProcessId = "order-123";
        var expectedProcess = CreateTestProcess();

        _repositoryMock
            .Setup(r => r.GetByClientProcessIdAsync(clientId, clientProcessId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedProcess);

        // Act
        var result = await _service.GetProcessByClientIdAsync(clientId, clientProcessId);

        // Assert
        result.Should().BeSameAs(expectedProcess);
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

    [Fact]
    public async Task GetProcessByClientIdAsync_Should_ThrowArgumentException_WhenClientIdIsEmpty()
    {
        // Act
        var act = async () => await _service.GetProcessByClientIdAsync("", "order-123");

        // Assert
        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task GetProcessByClientIdAsync_Should_ThrowArgumentException_WhenClientProcessIdIsEmpty()
    {
        // Act
        var act = async () => await _service.GetProcessByClientIdAsync("test-client", "");

        // Assert
        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task GetProcessByClientProcessIdAsync_Should_ReturnProcess_WhenExists()
    {
        // Arrange
        var clientId = "test-client";
        var clientProcessId = "order-123";
        var expectedProcess = CreateTestProcess();

        _repositoryMock
            .Setup(r => r.GetByClientProcessIdAsync(clientId, clientProcessId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedProcess);

        // Act
        var result = await _service.GetProcessByClientProcessIdAsync(clientId, clientProcessId);

        // Assert
        result.Should().BeSameAs(expectedProcess);
    }

    [Fact]
    public async Task GetProcessByClientProcessIdAsync_Should_ReturnNull_WhenNotExists()
    {
        // Arrange
        var clientId = "test-client";
        var clientProcessId = "order-123";

        _repositoryMock
            .Setup(r => r.GetByClientProcessIdAsync(clientId, clientProcessId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Process?)null);

        // Act
        var result = await _service.GetProcessByClientProcessIdAsync(clientId, clientProcessId);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task UpdateProcessProgressAsync_Should_UpdateProgress_WhenValid()
    {
        // Arrange
        var process = CreateTestProcess();
        _repositoryMock
            .Setup(r => r.GetByIdAsync(process.ProcessId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(process);

        Process? updatedProcess = null;
        _repositoryMock
            .Setup(r => r.UpdateAsync(It.IsAny<Process>(), It.IsAny<CancellationToken>()))
            .Callback<Process, CancellationToken>((p, ct) => updatedProcess = p)
            .ReturnsAsync((Process p, CancellationToken ct) => p);

        // Act
        var result = await _service.UpdateProcessProgressAsync(process.ProcessId, 50);

        // Assert
        result.Progress.Should().Be(50);
        updatedProcess.Should().NotBeNull();
        updatedProcess!.Progress.Should().Be(50);
    }

    [Fact]
    public async Task UpdateProcessProgressAsync_Should_ThrowArgumentOutOfRangeException_WhenProgressIsNegative()
    {
        // Arrange
        var process = CreateTestProcess();
        _repositoryMock
            .Setup(r => r.GetByIdAsync(process.ProcessId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(process);

        // Act
        var act = async () => await _service.UpdateProcessProgressAsync(process.ProcessId, -1);

        // Assert
        await act.Should().ThrowAsync<ArgumentOutOfRangeException>()
            .WithParameterName("progress");
    }

    [Fact]
    public async Task UpdateProcessProgressAsync_Should_ThrowArgumentOutOfRangeException_WhenProgressExceeds100()
    {
        // Arrange
        var process = CreateTestProcess();
        _repositoryMock
            .Setup(r => r.GetByIdAsync(process.ProcessId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(process);

        // Act
        var act = async () => await _service.UpdateProcessProgressAsync(process.ProcessId, 101);

        // Assert
        await act.Should().ThrowAsync<ArgumentOutOfRangeException>()
            .WithParameterName("progress");
    }

    [Fact]
    public async Task RecordProcessErrorAsync_Should_AddErrorToProcess()
    {
        // Arrange
        var process = CreateTestProcess();
        _repositoryMock
            .Setup(r => r.GetByIdAsync(process.ProcessId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(process);

        Process? updatedProcess = null;
        _repositoryMock
            .Setup(r => r.UpdateAsync(It.IsAny<Process>(), It.IsAny<CancellationToken>()))
            .Callback<Process, CancellationToken>((p, ct) => updatedProcess = p)
            .ReturnsAsync((Process p, CancellationToken ct) => p);

        // Act
        var result = await _service.RecordProcessErrorAsync(
            process.ProcessId,
            "TEST_ERROR",
            "Test error message",
            false);

        // Assert
        result.Errors.Should().NotBeNull().And.HaveCount(1);
        result.Errors![0].ErrorCode.Should().Be("TEST_ERROR");
        result.Errors[0].Message.Should().Be("Test error message");
        result.Errors[0].Retryable.Should().BeFalse();
    }

    [Fact]
    public async Task IncrementRetryCountAsync_Should_IncrementRetryCount()
    {
        // Arrange
        var process = CreateTestProcess();
        _repositoryMock
            .Setup(r => r.GetByIdAsync(process.ProcessId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(process);

        Process? updatedProcess = null;
        _repositoryMock
            .Setup(r => r.UpdateAsync(It.IsAny<Process>(), It.IsAny<CancellationToken>()))
            .Callback<Process, CancellationToken>((p, ct) => updatedProcess = p)
            .ReturnsAsync((Process p, CancellationToken ct) => p);

        // Act
        var result = await _service.IncrementRetryCountAsync(process.ProcessId);

        // Assert
        result.RetryCount.Should().Be(1);
        updatedProcess.Should().NotBeNull();
        updatedProcess!.RetryCount.Should().Be(1);
    }

    [Fact]
    public void Constructor_Should_ThrowArgumentNullException_WhenRepositoryIsNull()
    {
        // Act
        var act = () => new ProcessService(
            null!,
            _idempotencyMock.Object,
            _brokerMock.Object,
            _policyMock.Object,
            NullLogger<ProcessService>.Instance);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("processRepository");
    }

    [Fact]
    public void Constructor_Should_ThrowArgumentNullException_WhenIdempotencyServiceIsNull()
    {
        // Act
        var act = () => new ProcessService(
            _repositoryMock.Object,
            null!,
            _brokerMock.Object,
            _policyMock.Object,
            NullLogger<ProcessService>.Instance);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("idempotencyService");
    }

    [Fact]
    public void Constructor_Should_ThrowArgumentNullException_WhenMessageBrokerIsNull()
    {
        // Act
        var act = () => new ProcessService(
            _repositoryMock.Object,
            _idempotencyMock.Object,
            null!,
            _policyMock.Object,
            NullLogger<ProcessService>.Instance);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("messageBroker");
    }

    [Fact]
    public void Constructor_Should_ThrowArgumentNullException_WhenPolicyProviderIsNull()
    {
        // Act
        var act = () => new ProcessService(
            _repositoryMock.Object,
            _idempotencyMock.Object,
            _brokerMock.Object,
            null!,
            NullLogger<ProcessService>.Instance);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("policyProvider");
    }

    [Fact]
    public void Constructor_Should_ThrowArgumentNullException_WhenLoggerIsNull()
    {
        // Act
        var act = () => new ProcessService(
            _repositoryMock.Object,
            _idempotencyMock.Object,
            _brokerMock.Object,
            _policyMock.Object,
            null!);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("logger");
    }

    private Process CreateTestProcess()
    {
        return new Process
        {
            ProcessId = Guid.NewGuid(),
            ClientId = "test-client",
            ProcessType = "order",
            ClientProcessId = "order-123",
            IdempotencyKey = "idempotency-123",
            Status = ProcessStatus.Accepted,
            Progress = 0,
            Retryable = true,
            MaxRetries = 3,
            RetryCount = 0,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
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
