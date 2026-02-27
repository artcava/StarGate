using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using StarGate.Application.Services;
using StarGate.Core.Abstractions;
using StarGate.Core.Domain;
using StarGate.Core.Domain.Configuration;
using StarGate.Core.Exceptions;
using StarGate.Core.Messages;
using Xunit;

namespace StarGate.Application.Tests.Services;

public class ProcessServiceBrokerTests
{
    private readonly Mock<IProcessRepository> _repositoryMock;
    private readonly Mock<IIdempotencyService> _idempotencyMock;
    private readonly Mock<IMessageBroker> _brokerMock;
    private readonly Mock<IPolicyProvider> _policyMock;
    private readonly ProcessService _service;

    public ProcessServiceBrokerTests()
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
    public async Task CreateProcessAsync_Should_PublishMessageToBroker_AfterCreation()
    {
        // Arrange
        var clientId = "test-client";
        var processType = "order";
        var clientProcessId = "order-123";
        var idempotencyKey = "key-123";

        SetupSuccessfulCreation();

        // Act
        await _service.CreateProcessAsync(clientId, processType, clientProcessId, idempotencyKey);

        // Assert
        _brokerMock.Verify(
            b => b.PublishAsync(
                It.IsAny<ProcessMessage>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task CreateProcessAsync_Should_UseCorrectRoutingKey_ForProcessType()
    {
        // Arrange
        var clientId = "test-client";
        var processType = "order";
        var clientProcessId = "order-123";
        var idempotencyKey = "key-123";
        var capturedRoutingKey = string.Empty;

        SetupSuccessfulCreation();

        _brokerMock
            .Setup(b => b.PublishAsync(
                It.IsAny<ProcessMessage>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .Callback<object, string, CancellationToken>((msg, key, ct) => capturedRoutingKey = key)
            .Returns(Task.CompletedTask);

        // Act
        await _service.CreateProcessAsync(clientId, processType, clientProcessId, idempotencyKey);

        // Assert
        capturedRoutingKey.Should().Be($"process.{processType}");
    }

    [Fact]
    public async Task CreateProcessAsync_Should_PublishCorrectMessagePayload()
    {
        // Arrange
        var clientId = "test-client";
        var processType = "order";
        var clientProcessId = "order-123";
        var idempotencyKey = "key-123";
        ProcessMessage? capturedMessage = null;

        SetupSuccessfulCreation();

        _brokerMock
            .Setup(b => b.PublishAsync(
                It.IsAny<ProcessMessage>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .Callback<object, string, CancellationToken>((msg, key, ct) => capturedMessage = msg as ProcessMessage)
            .Returns(Task.CompletedTask);

        // Act
        var result = await _service.CreateProcessAsync(clientId, processType, clientProcessId, idempotencyKey);

        // Assert
        capturedMessage.Should().NotBeNull();
        capturedMessage!.ProcessId.Should().Be(result.ProcessId);
        capturedMessage.ClientId.Should().Be(clientId);
        capturedMessage.ProcessType.Should().Be(processType);
        capturedMessage.ClientProcessId.Should().Be(clientProcessId);
        capturedMessage.Priority.Should().Be(5);
        capturedMessage.Timestamp.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(2));
    }

    [Fact]
    public async Task CreateProcessAsync_Should_PublishMessage_OnlyAfterDatabasePersistence()
    {
        // Arrange
        var clientId = "test-client";
        var processType = "order";
        var clientProcessId = "order-123";
        var idempotencyKey = "key-123";
        var callOrder = new List<string>();

        SetupSuccessfulCreation();

        _repositoryMock
            .Setup(r => r.CreateAsync(
                It.IsAny<Process>(),
                It.IsAny<CancellationToken>()))
            .Callback(() => callOrder.Add("database-create"))
            .ReturnsAsync((Process p, CancellationToken ct) => p);

        _brokerMock
            .Setup(b => b.PublishAsync(
                It.IsAny<object>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .Callback(() => callOrder.Add("broker-publish"))
            .Returns(Task.CompletedTask);

        // Act
        await _service.CreateProcessAsync(clientId, processType, clientProcessId, idempotencyKey);

        // Assert
        callOrder.Should().HaveCount(2);
        callOrder[0].Should().Be("database-create");
        callOrder[1].Should().Be("broker-publish");
    }

    [Fact]
    public async Task CreateProcessAsync_Should_UpdateProcessToFailed_WhenBrokerPublishFails()
    {
        // Arrange
        var clientId = "test-client";
        var processType = "order";
        var clientProcessId = "order-123";
        var idempotencyKey = "key-123";
        Process? updatedProcess = null;

        SetupSuccessfulCreation();

        _brokerMock
            .Setup(b => b.PublishAsync(
                It.IsAny<object>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Broker connection failed"));

        _repositoryMock
            .Setup(r => r.UpdateAsync(
                It.IsAny<Process>(),
                It.IsAny<CancellationToken>()))
            .Callback<Process, CancellationToken>((p, ct) => updatedProcess = p)
            .ReturnsAsync((Process p, CancellationToken ct) => p);

        // Act
        var act = async () => await _service.CreateProcessAsync(
            clientId,
            processType,
            clientProcessId,
            idempotencyKey);

        // Assert
        await act.Should().ThrowAsync<MessageBrokerException>();

        updatedProcess.Should().NotBeNull();
        updatedProcess!.Status.Should().Be(ProcessStatus.Failed);
        updatedProcess.FailedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task CreateProcessAsync_Should_RollbackIdempotencyKey_WhenBrokerPublishFails()
    {
        // Arrange
        var clientId = "test-client";
        var processType = "order";
        var clientProcessId = "order-123";
        var idempotencyKey = "key-123";

        SetupSuccessfulCreation();

        _brokerMock
            .Setup(b => b.PublishAsync(
                It.IsAny<object>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Broker connection failed"));

        _repositoryMock
            .Setup(r => r.UpdateAsync(
                It.IsAny<Process>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((Process p, CancellationToken ct) => p);

        _idempotencyMock
            .Setup(i => i.RemoveIdempotencyKeyAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        var act = async () => await _service.CreateProcessAsync(
            clientId,
            processType,
            clientProcessId,
            idempotencyKey);

        // Assert
        await act.Should().ThrowAsync<MessageBrokerException>();

        _idempotencyMock.Verify(
            i => i.RemoveIdempotencyKeyAsync(
                clientId,
                idempotencyKey,
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task CreateProcessAsync_Should_ThrowMessageBrokerException_WithDetails_WhenPublishFails()
    {
        // Arrange
        var clientId = "test-client";
        var processType = "order";
        var clientProcessId = "order-123";
        var idempotencyKey = "key-123";

        SetupSuccessfulCreation();

        _brokerMock
            .Setup(b => b.PublishAsync(
                It.IsAny<object>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Broker connection failed"));

        _repositoryMock
            .Setup(r => r.UpdateAsync(
                It.IsAny<Process>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((Process p, CancellationToken ct) => p);

        // Act
        var act = async () => await _service.CreateProcessAsync(
            clientId,
            processType,
            clientProcessId,
            idempotencyKey);

        // Assert
        await act.Should().ThrowAsync<MessageBrokerException>()
            .WithMessage("*Failed to publish process message*")
            .WithMessage("*Process marked as Failed*")
            .WithMessage("*idempotency key removed*");
    }

    [Fact]
    public async Task CreateProcessAsync_Should_CreateProcess_EvenIfBrokerSlow()
    {
        // Arrange
        var clientId = "test-client";
        var processType = "order";
        var clientProcessId = "order-123";
        var idempotencyKey = "key-123";

        SetupSuccessfulCreation();

        _brokerMock
            .Setup(b => b.PublishAsync(
                It.IsAny<object>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .Returns(async () =>
            {
                await Task.Delay(100);
                return Task.CompletedTask;
            });

        // Act
        var result = await _service.CreateProcessAsync(
            clientId,
            processType,
            clientProcessId,
            idempotencyKey);

        // Assert
        result.Should().NotBeNull();
        result.Status.Should().Be(ProcessStatus.Accepted);

        _brokerMock.Verify(
            b => b.PublishAsync(
                It.IsAny<object>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Theory]
    [InlineData("order")]
    [InlineData("payment")]
    [InlineData("inventory")]
    [InlineData("shipment")]
    public async Task CreateProcessAsync_Should_UseProcessTypeSpecificRoutingKey(string processType)
    {
        // Arrange
        var clientId = "test-client";
        var clientProcessId = $"{processType}-123";
        var idempotencyKey = $"key-{processType}";
        var capturedRoutingKey = string.Empty;

        SetupSuccessfulCreation();

        _brokerMock
            .Setup(b => b.PublishAsync(
                It.IsAny<ProcessMessage>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .Callback<object, string, CancellationToken>((msg, key, ct) => capturedRoutingKey = key)
            .Returns(Task.CompletedTask);

        // Act
        await _service.CreateProcessAsync(clientId, processType, clientProcessId, idempotencyKey);

        // Assert
        capturedRoutingKey.Should().Be($"process.{processType}");
    }

    [Fact]
    public async Task CreateProcessAsync_Should_NotPublishMessage_WhenDatabaseCreationFails()
    {
        // Arrange
        var clientId = "test-client";
        var processType = "order";
        var clientProcessId = "order-123";
        var idempotencyKey = "key-123";

        SetupSuccessfulCreation();

        _repositoryMock
            .Setup(r => r.CreateAsync(
                It.IsAny<Process>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Database error"));

        // Act
        var act = async () => await _service.CreateProcessAsync(
            clientId,
            processType,
            clientProcessId,
            idempotencyKey);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>();

        _brokerMock.Verify(
            b => b.PublishAsync(
                It.IsAny<object>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task CreateProcessAsync_Should_IncludeRoutingFields_InBrokerMessage()
    {
        // Arrange
        var clientId = "test-client";
        var processType = "order";
        var clientProcessId = "order-123";
        var idempotencyKey = "key-123";
        ProcessMessage? capturedMessage = null;

        SetupSuccessfulCreation();

        _brokerMock
            .Setup(b => b.PublishAsync(
                It.IsAny<ProcessMessage>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .Callback<object, string, CancellationToken>((msg, key, ct) => capturedMessage = msg as ProcessMessage)
            .Returns(Task.CompletedTask);

        // Act
        var result = await _service.CreateProcessAsync(clientId, processType, clientProcessId, idempotencyKey);

        // Assert
        capturedMessage.Should().NotBeNull();
        capturedMessage!.ProcessId.Should().Be(result.ProcessId);
        capturedMessage.ProcessId.Should().NotBeEmpty();
        capturedMessage.ClientId.Should().Be(clientId);
        capturedMessage.ProcessType.Should().Be(processType);
        capturedMessage.ClientProcessId.Should().Be(clientProcessId);
        capturedMessage.Priority.Should().Be(5);
        capturedMessage.Timestamp.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(2));
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
