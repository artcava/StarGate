using Microsoft.Extensions.Logging.Abstractions;
using StarGate.Application.Services;
using StarGate.Core.Abstractions;
using StarGate.Core.Domain;
using StarGate.Core.Domain.Configuration;
using StarGate.Core.Exceptions;
using StarGate.Core.Messages;

namespace StarGate.Application.Tests.Services;

/// <summary>
/// Unit tests for policy enforcement in ProcessService.
/// Validates policy retrieval, concurrency limits, timeout calculation,
/// and retry configuration based on policies.
/// </summary>
public class ProcessServicePolicyTests
{
    private readonly Mock<IProcessRepository> _repositoryMock;
    private readonly Mock<IIdempotencyService> _idempotencyMock;
    private readonly Mock<IMessageBroker> _brokerMock;
    private readonly Mock<IPolicyProvider> _policyMock;
    private readonly ProcessService _service;

    public ProcessServicePolicyTests()
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
    public async Task CreateProcessAsync_Should_RetrievePolicy_BeforeCreation()
    {
        // Arrange
        var clientId = "test-client";
        var processType = "order";
        var policy = CreateTestPolicy();

        SetupSuccessfulCreation(policy);

        // Act
        await _service.CreateProcessAsync(
            clientId,
            processType,
            "order-123",
            "idempotency-123",
            null);

        // Assert
        _policyMock.Verify(
            p => p.GetEffectivePolicyAsync(clientId, processType, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task CreateProcessAsync_Should_ThrowException_WhenPolicyNotFound()
    {
        // Arrange
        var clientId = "test-client";
        var processType = "order";

        _policyMock
            .Setup(p => p.GetEffectivePolicyAsync(clientId, processType, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Policy not found"));

        // Act
        var act = async () => await _service.CreateProcessAsync(
            clientId,
            processType,
            "order-123",
            "idempotency-123",
            null);

        // Assert
        await act.Should().ThrowAsync<PolicyNotFoundException>()
            .Where(ex => ex.ClientId == clientId && ex.ProcessType == processType);
    }

    [Fact]
    public async Task CreateProcessAsync_Should_EnforceConcurrencyLimit()
    {
        // Arrange
        var clientId = "test-client";
        var processType = "order";
        var policy = CreateTestPolicy(maxConcurrent: 5);

        SetupSuccessfulCreation(policy);

        // Act
        await _service.CreateProcessAsync(
            clientId,
            processType,
            "order-123",
            "idempotency-123",
            null);

        // Assert
        _repositoryMock.Verify(
            r => r.CountActiveProcessesAsync(clientId, processType, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task CreateProcessAsync_Should_ThrowException_WhenConcurrencyLimitExceeded()
    {
        // Arrange
        var clientId = "test-client";
        var processType = "order";
        var policy = CreateTestPolicy(maxConcurrent: 5);

        _policyMock
            .Setup(p => p.GetEffectivePolicyAsync(clientId, processType, It.IsAny<CancellationToken>()))
            .ReturnsAsync(policy);

        _repositoryMock
            .Setup(r => r.CountActiveProcessesAsync(clientId, processType, It.IsAny<CancellationToken>()))
            .ReturnsAsync(5); // At limit

        // Act
        var act = async () => await _service.CreateProcessAsync(
            clientId,
            processType,
            "order-123",
            "idempotency-123",
            null);

        // Assert
        await act.Should().ThrowAsync<PolicyViolationException>()
            .WithMessage("*concurrent*")
            .Where(ex => ex.ClientId == clientId && ex.ProcessType == processType);

        _idempotencyMock.Verify(
            i => i.StoreIdempotencyKeyAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<Guid>(),
                It.IsAny<TimeSpan?>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task CreateProcessAsync_Should_NotEnforceConcurrency_WhenLimitIsZero()
    {
        // Arrange
        var clientId = "test-client";
        var processType = "order";
        var policy = CreateTestPolicy(maxConcurrent: 0); // No limit

        SetupSuccessfulCreation(policy);

        // Act
        await _service.CreateProcessAsync(
            clientId,
            processType,
            "order-123",
            "idempotency-123",
            null);

        // Assert
        _repositoryMock.Verify(
            r => r.CountActiveProcessesAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task CreateProcessAsync_Should_NotEnforceConcurrency_WhenLimitIsNull()
    {
        // Arrange
        var clientId = "test-client";
        var processType = "order";
        var policy = CreateTestPolicy(maxConcurrent: null); // Unlimited

        SetupSuccessfulCreation(policy);

        // Act
        await _service.CreateProcessAsync(
            clientId,
            processType,
            "order-123",
            "idempotency-123",
            null);

        // Assert
        _repositoryMock.Verify(
            r => r.CountActiveProcessesAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task CreateProcessAsync_Should_SetTimeoutFromPolicy()
    {
        // Arrange
        var clientId = "test-client";
        var processType = "order";
        var policy = CreateTestPolicy(timeoutSeconds: 3600);
        Process? capturedProcess = null;

        SetupSuccessfulCreation(policy);

        _repositoryMock
            .Setup(r => r.CreateAsync(It.IsAny<Process>(), It.IsAny<CancellationToken>()))
            .Callback<Process, CancellationToken>((p, ct) => capturedProcess = p)
            .ReturnsAsync((Process p, CancellationToken ct) => p);

        // Act
        await _service.CreateProcessAsync(
            clientId,
            processType,
            "order-123",
            "idempotency-123",
            null);

        // Assert
        capturedProcess.Should().NotBeNull();
        capturedProcess!.TimeoutAt.Should().NotBeNull();
        capturedProcess.TimeoutAt.Should().BeCloseTo(
            DateTime.UtcNow.AddSeconds(3600),
            TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task CreateProcessAsync_Should_SetMaxRetriesFromPolicy()
    {
        // Arrange
        var clientId = "test-client";
        var processType = "order";
        var policy = CreateTestPolicy(maxAttempts: 3);
        Process? capturedProcess = null;

        SetupSuccessfulCreation(policy);

        _repositoryMock
            .Setup(r => r.CreateAsync(It.IsAny<Process>(), It.IsAny<CancellationToken>()))
            .Callback<Process, CancellationToken>((p, ct) => capturedProcess = p)
            .ReturnsAsync((Process p, CancellationToken ct) => p);

        // Act
        await _service.CreateProcessAsync(
            clientId,
            processType,
            "order-123",
            "idempotency-123",
            null);

        // Assert
        capturedProcess.Should().NotBeNull();
        capturedProcess!.MaxRetries.Should().Be(3);
        capturedProcess.RetryCount.Should().Be(0);
        capturedProcess.Retryable.Should().BeTrue();
    }

    [Fact]
    public async Task CreateProcessAsync_Should_SetRetryableFalse_WhenMaxRetriesIsZero()
    {
        // Arrange
        var clientId = "test-client";
        var processType = "order";
        var policy = CreateTestPolicy(maxAttempts: 0);
        Process? capturedProcess = null;

        SetupSuccessfulCreation(policy);

        _repositoryMock
            .Setup(r => r.CreateAsync(It.IsAny<Process>(), It.IsAny<CancellationToken>()))
            .Callback<Process, CancellationToken>((p, ct) => capturedProcess = p)
            .ReturnsAsync((Process p, CancellationToken ct) => p);

        // Act
        await _service.CreateProcessAsync(
            clientId,
            processType,
            "order-123",
            "idempotency-123",
            null);

        // Assert
        capturedProcess.Should().NotBeNull();
        capturedProcess!.MaxRetries.Should().Be(0);
        capturedProcess.Retryable.Should().BeFalse();
    }

    [Fact]
    public async Task CreateProcessAsync_Should_SetRetentionExpirationFromPolicy()
    {
        // Arrange
        var clientId = "test-client";
        var processType = "order";
        var policy = CreateTestPolicy(retentionDays: 30);
        Process? capturedProcess = null;

        SetupSuccessfulCreation(policy);

        _repositoryMock
            .Setup(r => r.CreateAsync(It.IsAny<Process>(), It.IsAny<CancellationToken>()))
            .Callback<Process, CancellationToken>((p, ct) => capturedProcess = p)
            .ReturnsAsync((Process p, CancellationToken ct) => p);

        // Act
        await _service.CreateProcessAsync(
            clientId,
            processType,
            "order-123",
            "idempotency-123",
            null);

        // Assert
        capturedProcess.Should().NotBeNull();
        capturedProcess!.RetentionExpiresAt.Should().NotBeNull();
        capturedProcess.RetentionExpiresAt.Should().BeCloseTo(
            DateTime.UtcNow.AddDays(30),
            TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task CreateProcessAsync_Should_AllowCreation_WhenUnderConcurrencyLimit()
    {
        // Arrange
        var clientId = "test-client";
        var processType = "order";
        var policy = CreateTestPolicy(maxConcurrent: 5);

        _policyMock
            .Setup(p => p.GetEffectivePolicyAsync(clientId, processType, It.IsAny<CancellationToken>()))
            .ReturnsAsync(policy);

        _repositoryMock
            .Setup(r => r.CountActiveProcessesAsync(clientId, processType, It.IsAny<CancellationToken>()))
            .ReturnsAsync(3); // Under limit

        SetupSuccessfulCreation(policy);

        // Act
        var result = await _service.CreateProcessAsync(
            clientId,
            processType,
            "order-123",
            "idempotency-123",
            null);

        // Assert
        result.Should().NotBeNull();
        result.ClientId.Should().Be(clientId);
        result.ProcessType.Should().Be(processType);
    }

    private EffectivePolicy CreateTestPolicy(
        int maxAttempts = 3,
        int timeoutSeconds = 3600,
        int? maxConcurrent = 10,
        int retentionDays = 30)
    {
        return new EffectivePolicy
        {
            ProcessType = "order",
            ClientId = "test-client",
            Timeout = TimeSpan.FromSeconds(timeoutSeconds),
            RetryPolicy = new RetryPolicy
            {
                Enabled = maxAttempts > 0,
                MaxAttempts = maxAttempts,
                InitialDelay = TimeSpan.FromSeconds(5),
                MaxDelay = TimeSpan.FromSeconds(300),
                BackoffStrategy = BackoffStrategy.Exponential
            },
            ResultRetention = TimeSpan.FromDays(retentionDays),
            MaxConcurrentProcesses = maxConcurrent,
            Source = new PolicySource
            {
                TimeoutFromOverride = false,
                RetryPolicyFromOverride = false,
                ResultRetentionFromOverride = false,
                ConcurrencyLimitFromOverride = false
            }
        };
    }

    private void SetupSuccessfulCreation(EffectivePolicy policy)
    {
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
                It.IsAny<ProcessMessage>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
    }
}
