namespace StarGate.Application.Tests.Services;

using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using StarGate.Application.Services;
using StarGate.Core.Abstractions;
using StarGate.Core.Domain;
using StarGate.Core.Exceptions;
using Xunit;

public class ProcessServiceIdempotencyTests
{
    private readonly Mock<IProcessRepository> _repositoryMock;
    private readonly Mock<IIdempotencyService> _idempotencyMock;
    private readonly ProcessService _service;

    public ProcessServiceIdempotencyTests()
    {
        _repositoryMock = new Mock<IProcessRepository>();
        _idempotencyMock = new Mock<IIdempotencyService>();

        _service = new ProcessService(
            _repositoryMock.Object,
            _idempotencyMock.Object,
            NullLogger<ProcessService>.Instance);
    }

    [Fact]
    public async Task CreateProcessAsync_Should_CheckCacheFirst_BeforeDatabase()
    {
        // Arrange
        var clientId = "test-client";
        var processType = "order";
        var clientProcessId = "order-123";
        var idempotencyKey = "idempotency-123";
        var existingProcessId = Guid.NewGuid();

        _idempotencyMock
            .Setup(i => i.GetProcessIdByIdempotencyKeyAsync(
                clientId,
                idempotencyKey,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingProcessId);

        // Act
        var act = async () => await _service.CreateProcessAsync(
            clientId,
            processType,
            clientProcessId,
            idempotencyKey);

        // Assert
        await act.Should().ThrowAsync<DuplicateProcessException>();

        // Verify cache was checked
        _idempotencyMock.Verify(
            i => i.GetProcessIdByIdempotencyKeyAsync(
                clientId,
                idempotencyKey,
                It.IsAny<CancellationToken>()),
            Times.Once);

        // Verify database was NOT checked (fast path)
        _repositoryMock.Verify(
            r => r.GetByIdempotencyKeyAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task CreateProcessAsync_Should_CheckDatabase_WhenCacheMiss()
    {
        // Arrange
        var clientId = "test-client";
        var processType = "order";
        var clientProcessId = "order-123";
        var idempotencyKey = "idempotency-123";
        var existingProcess = new Process
        {
            ProcessId = Guid.NewGuid(),
            ClientId = clientId,
            ClientProcessId = clientProcessId,
            ProcessType = processType,
            IdempotencyKey = idempotencyKey,
            Status = ProcessStatus.Accepted,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _idempotencyMock
            .Setup(i => i.GetProcessIdByIdempotencyKeyAsync(
                clientId,
                idempotencyKey,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((Guid?)null); // Cache miss

        _repositoryMock
            .Setup(r => r.GetByIdempotencyKeyAsync(
                clientId,
                idempotencyKey,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingProcess);

        // Act
        var act = async () => await _service.CreateProcessAsync(
            clientId,
            processType,
            clientProcessId,
            idempotencyKey);

        // Assert
        await act.Should().ThrowAsync<DuplicateProcessException>();

        // Verify both cache and database were checked
        _idempotencyMock.Verify(
            i => i.GetProcessIdByIdempotencyKeyAsync(
                clientId,
                idempotencyKey,
                It.IsAny<CancellationToken>()),
            Times.Once);

        _repositoryMock.Verify(
            r => r.GetByIdempotencyKeyAsync(
                clientId,
                idempotencyKey,
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task CreateProcessAsync_Should_RepopulateCache_WhenFoundInDatabase()
    {
        // Arrange
        var clientId = "test-client";
        var processType = "order";
        var clientProcessId = "order-123";
        var idempotencyKey = "idempotency-123";
        var existingProcess = new Process
        {
            ProcessId = Guid.NewGuid(),
            ClientId = clientId,
            ClientProcessId = clientProcessId,
            ProcessType = processType,
            IdempotencyKey = idempotencyKey,
            Status = ProcessStatus.Accepted,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _idempotencyMock
            .Setup(i => i.GetProcessIdByIdempotencyKeyAsync(
                clientId,
                idempotencyKey,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((Guid?)null); // Cache miss

        _repositoryMock
            .Setup(r => r.GetByIdempotencyKeyAsync(
                clientId,
                idempotencyKey,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingProcess);

        _idempotencyMock
            .Setup(i => i.StoreIdempotencyKeyAsync(
                clientId,
                idempotencyKey,
                existingProcess.ProcessId,
                null,
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        var act = async () => await _service.CreateProcessAsync(
            clientId,
            processType,
            clientProcessId,
            idempotencyKey);

        // Assert
        await act.Should().ThrowAsync<DuplicateProcessException>();

        // Verify cache was repopulated
        _idempotencyMock.Verify(
            i => i.StoreIdempotencyKeyAsync(
                clientId,
                idempotencyKey,
                existingProcess.ProcessId,
                null,
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task CreateProcessAsync_Should_ReserveIdempotencyKey_BeforeCreatingProcess()
    {
        // Arrange
        var clientId = "test-client";
        var processType = "order";
        var clientProcessId = "order-123";
        var idempotencyKey = "idempotency-123";
        var callOrder = new List<string>();

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
            .Callback(() => callOrder.Add("store-idempotency"))
            .Returns(Task.CompletedTask);

        _repositoryMock
            .Setup(r => r.CreateAsync(
                It.IsAny<Process>(),
                It.IsAny<CancellationToken>()))
            .Callback(() => callOrder.Add("create-process"))
            .ReturnsAsync((Process p, CancellationToken ct) => p);

        // Act
        await _service.CreateProcessAsync(
            clientId,
            processType,
            clientProcessId,
            idempotencyKey);

        // Assert
        callOrder.Should().HaveCount(2);
        callOrder[0].Should().Be("store-idempotency"); // First
        callOrder[1].Should().Be("create-process"); // Second
    }

    [Fact]
    public async Task CreateProcessAsync_Should_RollbackIdempotencyKey_OnProcessCreationFailure()
    {
        // Arrange
        var clientId = "test-client";
        var processType = "order";
        var clientProcessId = "order-123";
        var idempotencyKey = "idempotency-123";

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
            .Setup(r => r.CreateAsync(
                It.IsAny<Process>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Database error"));

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
        await act.Should().ThrowAsync<InvalidOperationException>();

        // Verify rollback occurred
        _idempotencyMock.Verify(
            i => i.RemoveIdempotencyKeyAsync(
                clientId,
                idempotencyKey,
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task CreateProcessAsync_Should_CreateProcess_WhenIdempotencyKeyIsNew()
    {
        // Arrange
        var clientId = "test-client";
        var processType = "order";
        var clientProcessId = "order-123";
        var idempotencyKey = "idempotency-123";
        Process? createdProcess = null;

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
            .Setup(r => r.CreateAsync(
                It.IsAny<Process>(),
                It.IsAny<CancellationToken>()))
            .Callback<Process, CancellationToken>((p, ct) => createdProcess = p)
            .ReturnsAsync((Process p, CancellationToken ct) => p);

        // Act
        var result = await _service.CreateProcessAsync(
            clientId,
            processType,
            clientProcessId,
            idempotencyKey);

        // Assert
        result.Should().NotBeNull();
        result.IdempotencyKey.Should().Be(idempotencyKey);
        result.ClientId.Should().Be(clientId);
        result.ProcessType.Should().Be(processType);
        result.ClientProcessId.Should().Be(clientProcessId);
        result.Status.Should().Be(ProcessStatus.Accepted);

        createdProcess.Should().BeSameAs(result);

        // Verify idempotency key was stored with generated process ID
        _idempotencyMock.Verify(
            i => i.StoreIdempotencyKeyAsync(
                clientId,
                idempotencyKey,
                result.ProcessId,
                null,
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task CreateProcessAsync_Should_GenerateUniqueProcessId_ForEachCall()
    {
        // Arrange
        var clientId = "test-client";
        var processType = "order";
        var processIds = new List<Guid>();

        SetupSuccessfulCreation();

        _repositoryMock
            .Setup(r => r.CreateAsync(
                It.IsAny<Process>(),
                It.IsAny<CancellationToken>()))
            .Callback<Process, CancellationToken>((p, ct) => processIds.Add(p.ProcessId))
            .ReturnsAsync((Process p, CancellationToken ct) => p);

        // Act
        await _service.CreateProcessAsync(clientId, processType, "order-1", "key-1");
        await _service.CreateProcessAsync(clientId, processType, "order-2", "key-2");
        await _service.CreateProcessAsync(clientId, processType, "order-3", "key-3");

        // Assert
        processIds.Should().HaveCount(3);
        processIds.Should().OnlyHaveUniqueItems();
        processIds.Should().AllSatisfy(id => id.Should().NotBeEmpty());
    }

    private void SetupSuccessfulCreation()
    {
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
            .Setup(r => r.CreateAsync(
                It.IsAny<Process>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((Process p, CancellationToken ct) => p);
    }
}
