using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using MongoDB.Bson;
using MongoDB.Driver;
using Moq;
using StarGate.Core.Domain;
using StarGate.Infrastructure.Persistence;

namespace StarGate.Infrastructure.Tests.Persistence;

/// <summary>
/// Unit tests for MongoProcessRepository using mocked MongoDB driver.
/// No actual database connection required.
/// </summary>
public class MongoProcessRepositoryTests
{
    private readonly Mock<IMongoDatabase> _databaseMock;
    private readonly Mock<IMongoCollection<ProcessDocument>> _collectionMock;
    private readonly MongoProcessRepository _repository;

    public MongoProcessRepositoryTests()
    {
        _databaseMock = new Mock<IMongoDatabase>();
        _collectionMock = new Mock<IMongoCollection<ProcessDocument>>();

        _databaseMock
            .Setup(db => db.GetCollection<ProcessDocument>("processes", null))
            .Returns(_collectionMock.Object);

        _repository = new MongoProcessRepository(
            _databaseMock.Object,
            NullLogger<MongoProcessRepository>.Instance);
    }

    #region CreateAsync Tests

    [Fact]
    public async Task CreateAsync_Should_InsertProcess_WhenValid()
    {
        // Arrange
        var process = CreateValidProcess();

        _collectionMock
            .Setup(c => c.InsertOneAsync(
                It.IsAny<ProcessDocument>(),
                null,
                default))
            .Returns(Task.CompletedTask);

        // Act
        var result = await _repository.CreateAsync(process);

        // Assert
        result.Should().Be(process);
        _collectionMock.Verify(
            c => c.InsertOneAsync(
                It.Is<ProcessDocument>(d => d.ProcessId == process.ProcessId),
                null,
                default),
            Times.Once);
    }

    [Fact]
    public async Task CreateAsync_Should_ThrowException_WhenDuplicateProcessId()
    {
        // Arrange
        var process = CreateValidProcess();

        // Mock MongoWriteException with duplicate key error message
        var mongoException = new MongoWriteException(
            null!,
            null!,
            null,
            null);

        // Use reflection to set the Message property via inner exception
        var innerException = new Exception("E11000 duplicate key error collection: stargate.processes index: ProcessId dup key");
        typeof(Exception).GetField("_message", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)?
            .SetValue(mongoException, innerException.Message);

        _collectionMock
            .Setup(c => c.InsertOneAsync(
                It.IsAny<ProcessDocument>(),
                null,
                default))
            .ThrowsAsync(mongoException);

        // Act
        Func<Task> act = async () => await _repository.CreateAsync(process);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage($"Process with ID '{process.ProcessId}' already exists");
    }

    [Fact]
    public async Task CreateAsync_Should_ThrowException_WhenDuplicateIdempotencyKey()
    {
        // Arrange
        var process = CreateValidProcess();

        var mongoException = new MongoWriteException(
            null!,
            null!,
            null,
            null);

        var innerException = new Exception("E11000 duplicate key error collection: stargate.processes index: IdempotencyKey dup key");
        typeof(Exception).GetField("_message", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)?
            .SetValue(mongoException, innerException.Message);

        _collectionMock
            .Setup(c => c.InsertOneAsync(
                It.IsAny<ProcessDocument>(),
                null,
                default))
            .ThrowsAsync(mongoException);

        // Act
        Func<Task> act = async () => await _repository.CreateAsync(process);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage($"Process with idempotency key '{process.IdempotencyKey}' already exists");
    }

    [Fact]
    public async Task CreateAsync_Should_ThrowException_WhenDuplicateClientProcessId()
    {
        // Arrange
        var process = CreateValidProcess();

        var mongoException = new MongoWriteException(
            null!,
            null!,
            null,
            null);

        var innerException = new Exception("E11000 duplicate key error collection: stargate.processes index: ClientId_ClientProcessId dup key");
        typeof(Exception).GetField("_message", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)?
            .SetValue(mongoException, innerException.Message);

        _collectionMock
            .Setup(c => c.InsertOneAsync(
                It.IsAny<ProcessDocument>(),
                null,
                default))
            .ThrowsAsync(mongoException);

        // Act
        Func<Task> act = async () => await _repository.CreateAsync(process);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage($"Process with ClientId '{process.ClientId}' and ClientProcessId '{process.ClientProcessId}' already exists");
    }

    [Fact]
    public async Task CreateAsync_Should_ThrowArgumentNullException_WhenProcessIsNull()
    {
        // Act
        Func<Task> act = async () => await _repository.CreateAsync(null!);

        // Assert
        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    #endregion

    #region GetByIdAsync Tests

    [Fact]
    public async Task GetByIdAsync_Should_ReturnProcess_WhenExists()
    {
        // Arrange
        var processId = Guid.NewGuid();
        var baseDoc = CreateValidDocument();
        var document = new ProcessDocument
        {
            ProcessId = processId,
            ClientProcessId = baseDoc.ClientProcessId,
            ProcessType = baseDoc.ProcessType,
            ClientId = baseDoc.ClientId,
            Status = baseDoc.Status,
            Progress = baseDoc.Progress,
            CreatedAt = baseDoc.CreatedAt,
            UpdatedAt = baseDoc.UpdatedAt,
            IdempotencyKey = baseDoc.IdempotencyKey,
            Retryable = baseDoc.Retryable
        };

        var cursorMock = new Mock<IAsyncCursor<ProcessDocument>>();
        cursorMock
            .SetupSequence(c => c.MoveNextAsync(default))
            .ReturnsAsync(true)
            .ReturnsAsync(false);
        cursorMock
            .Setup(c => c.Current)
            .Returns(new[] { document });

        _collectionMock
            .Setup(c => c.FindAsync(
                It.IsAny<FilterDefinition<ProcessDocument>>(),
                It.IsAny<FindOptions<ProcessDocument, ProcessDocument>>(),
                default))
            .ReturnsAsync(cursorMock.Object);

        // Act
        var result = await _repository.GetByIdAsync(processId);

        // Assert
        result.Should().NotBeNull();
        result!.ProcessId.Should().Be(processId);
    }

    [Fact]
    public async Task GetByIdAsync_Should_ReturnNull_WhenNotFound()
    {
        // Arrange
        var processId = Guid.NewGuid();

        var cursorMock = new Mock<IAsyncCursor<ProcessDocument>>();
        cursorMock
            .Setup(c => c.MoveNextAsync(default))
            .ReturnsAsync(false);

        _collectionMock
            .Setup(c => c.FindAsync(
                It.IsAny<FilterDefinition<ProcessDocument>>(),
                It.IsAny<FindOptions<ProcessDocument, ProcessDocument>>(),
                default))
            .ReturnsAsync(cursorMock.Object);

        // Act
        var result = await _repository.GetByIdAsync(processId);

        // Assert
        result.Should().BeNull();
    }

    #endregion

    #region GetByClientProcessIdAsync Tests

    [Fact]
    public async Task GetByClientProcessIdAsync_Should_ReturnProcess_WhenExists()
    {
        // Arrange
        var clientId = "test-client";
        var clientProcessId = "client-process-123";
        var baseDoc = CreateValidDocument();
        var document = new ProcessDocument
        {
            ProcessId = baseDoc.ProcessId,
            ClientProcessId = clientProcessId,
            ProcessType = baseDoc.ProcessType,
            ClientId = clientId,
            Status = baseDoc.Status,
            Progress = baseDoc.Progress,
            CreatedAt = baseDoc.CreatedAt,
            UpdatedAt = baseDoc.UpdatedAt,
            IdempotencyKey = baseDoc.IdempotencyKey,
            Retryable = baseDoc.Retryable
        };

        var cursorMock = new Mock<IAsyncCursor<ProcessDocument>>();
        cursorMock
            .SetupSequence(c => c.MoveNextAsync(default))
            .ReturnsAsync(true)
            .ReturnsAsync(false);
        cursorMock
            .Setup(c => c.Current)
            .Returns(new[] { document });

        _collectionMock
            .Setup(c => c.FindAsync(
                It.IsAny<FilterDefinition<ProcessDocument>>(),
                It.IsAny<FindOptions<ProcessDocument, ProcessDocument>>(),
                default))
            .ReturnsAsync(cursorMock.Object);

        // Act
        var result = await _repository.GetByClientProcessIdAsync(clientId, clientProcessId);

        // Assert
        result.Should().NotBeNull();
        result!.ClientId.Should().Be(clientId);
        result.ClientProcessId.Should().Be(clientProcessId);
    }

    [Fact]
    public async Task GetByClientProcessIdAsync_Should_ReturnNull_WhenNotFound()
    {
        // Arrange
        var cursorMock = new Mock<IAsyncCursor<ProcessDocument>>();
        cursorMock
            .Setup(c => c.MoveNextAsync(default))
            .ReturnsAsync(false);

        _collectionMock
            .Setup(c => c.FindAsync(
                It.IsAny<FilterDefinition<ProcessDocument>>(),
                It.IsAny<FindOptions<ProcessDocument, ProcessDocument>>(),
                default))
            .ReturnsAsync(cursorMock.Object);

        // Act
        var result = await _repository.GetByClientProcessIdAsync("test-client", "process-123");

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task GetByClientProcessIdAsync_Should_ThrowArgumentException_WhenClientIdIsEmpty()
    {
        // Act
        Func<Task> act = async () => await _repository.GetByClientProcessIdAsync("", "process-id");

        // Assert
        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task GetByClientProcessIdAsync_Should_ThrowArgumentException_WhenClientProcessIdIsEmpty()
    {
        // Act
        Func<Task> act = async () => await _repository.GetByClientProcessIdAsync("client-id", "");

        // Assert
        await act.Should().ThrowAsync<ArgumentException>();
    }

    #endregion

    #region UpdateAsync Tests

    [Fact]
    public async Task UpdateAsync_Should_ReplaceDocument_WhenExists()
    {
        // Arrange
        var process = CreateValidProcess();

        var replaceResult = new Mock<ReplaceOneResult>();
        replaceResult.Setup(r => r.MatchedCount).Returns(1);

        _collectionMock
            .Setup(c => c.ReplaceOneAsync(
                It.IsAny<FilterDefinition<ProcessDocument>>(),
                It.IsAny<ProcessDocument>(),
                It.IsAny<ReplaceOptions>(),
                default))
            .ReturnsAsync(replaceResult.Object);

        // Act
        var result = await _repository.UpdateAsync(process);

        // Assert
        result.Should().Be(process);
        _collectionMock.Verify(
            c => c.ReplaceOneAsync(
                It.IsAny<FilterDefinition<ProcessDocument>>(),
                It.Is<ProcessDocument>(d => d.ProcessId == process.ProcessId),
                It.IsAny<ReplaceOptions>(),
                default),
            Times.Once);
    }

    [Fact]
    public async Task UpdateAsync_Should_ThrowException_WhenNotFound()
    {
        // Arrange
        var process = CreateValidProcess();

        var replaceResult = new Mock<ReplaceOneResult>();
        replaceResult.Setup(r => r.MatchedCount).Returns(0);

        _collectionMock
            .Setup(c => c.ReplaceOneAsync(
                It.IsAny<FilterDefinition<ProcessDocument>>(),
                It.IsAny<ProcessDocument>(),
                It.IsAny<ReplaceOptions>(),
                default))
            .ReturnsAsync(replaceResult.Object);

        // Act
        Func<Task> act = async () => await _repository.UpdateAsync(process);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage($"Process with ID '{process.ProcessId}' not found");
    }

    [Fact]
    public async Task UpdateAsync_Should_ThrowArgumentNullException_WhenProcessIsNull()
    {
        // Act
        Func<Task> act = async () => await _repository.UpdateAsync(null!);

        // Assert
        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    #endregion

    #region GetByStatusAsync Tests

    [Fact]
    public async Task GetByStatusAsync_Should_ReturnFilteredProcesses()
    {
        // Arrange
        var status = ProcessStatus.Processing;
        var baseDoc = CreateValidDocument();
        var documents = new[]
        {
            new ProcessDocument
            {
                ProcessId = Guid.NewGuid(),
                ClientProcessId = baseDoc.ClientProcessId,
                ProcessType = baseDoc.ProcessType,
                ClientId = baseDoc.ClientId,
                Status = "Processing",
                Progress = baseDoc.Progress,
                CreatedAt = baseDoc.CreatedAt,
                UpdatedAt = baseDoc.UpdatedAt,
                IdempotencyKey = Guid.NewGuid().ToString(),
                Retryable = baseDoc.Retryable
            },
            new ProcessDocument
            {
                ProcessId = Guid.NewGuid(),
                ClientProcessId = $"client-{Guid.NewGuid()}",
                ProcessType = baseDoc.ProcessType,
                ClientId = baseDoc.ClientId,
                Status = "Processing",
                Progress = baseDoc.Progress,
                CreatedAt = baseDoc.CreatedAt,
                UpdatedAt = baseDoc.UpdatedAt,
                IdempotencyKey = Guid.NewGuid().ToString(),
                Retryable = baseDoc.Retryable
            }
        };

        var cursorMock = new Mock<IAsyncCursor<ProcessDocument>>();
        cursorMock
            .SetupSequence(c => c.MoveNextAsync(default))
            .ReturnsAsync(true)
            .ReturnsAsync(false);
        cursorMock
            .Setup(c => c.Current)
            .Returns(documents);

        _collectionMock
            .Setup(c => c.FindAsync(
                It.IsAny<FilterDefinition<ProcessDocument>>(),
                It.IsAny<FindOptions<ProcessDocument, ProcessDocument>>(),
                default))
            .ReturnsAsync(cursorMock.Object);

        // Act
        var result = await _repository.GetByStatusAsync(status, 100);

        // Assert
        result.Should().HaveCount(2);
        result.Should().AllSatisfy(p => p.Status.Should().Be(ProcessStatus.Processing));
    }

    [Fact]
    public async Task GetByStatusAsync_Should_ThrowArgumentOutOfRangeException_WhenLimitTooLow()
    {
        // Act
        Func<Task> act = async () => await _repository.GetByStatusAsync(ProcessStatus.Accepted, 0);

        // Assert
        await act.Should().ThrowAsync<ArgumentOutOfRangeException>();
    }

    [Fact]
    public async Task GetByStatusAsync_Should_ThrowArgumentOutOfRangeException_WhenLimitTooHigh()
    {
        // Act
        Func<Task> act = async () => await _repository.GetByStatusAsync(ProcessStatus.Accepted, 1001);

        // Assert
        await act.Should().ThrowAsync<ArgumentOutOfRangeException>();
    }

    #endregion

    #region GetByClientIdAsync Tests

    [Fact]
    public async Task GetByClientIdAsync_Should_ReturnProcessesForClient()
    {
        // Arrange
        var clientId = "test-client";
        var baseDoc = CreateValidDocument();
        var documents = new[]
        {
            new ProcessDocument
            {
                ProcessId = Guid.NewGuid(),
                ClientProcessId = baseDoc.ClientProcessId,
                ProcessType = baseDoc.ProcessType,
                ClientId = clientId,
                Status = baseDoc.Status,
                Progress = baseDoc.Progress,
                CreatedAt = baseDoc.CreatedAt,
                UpdatedAt = baseDoc.UpdatedAt,
                IdempotencyKey = Guid.NewGuid().ToString(),
                Retryable = baseDoc.Retryable
            },
            new ProcessDocument
            {
                ProcessId = Guid.NewGuid(),
                ClientProcessId = $"client-{Guid.NewGuid()}",
                ProcessType = baseDoc.ProcessType,
                ClientId = clientId,
                Status = baseDoc.Status,
                Progress = baseDoc.Progress,
                CreatedAt = baseDoc.CreatedAt,
                UpdatedAt = baseDoc.UpdatedAt,
                IdempotencyKey = Guid.NewGuid().ToString(),
                Retryable = baseDoc.Retryable
            }
        };

        var cursorMock = new Mock<IAsyncCursor<ProcessDocument>>();
        cursorMock
            .SetupSequence(c => c.MoveNextAsync(default))
            .ReturnsAsync(true)
            .ReturnsAsync(false);
        cursorMock
            .Setup(c => c.Current)
            .Returns(documents);

        _collectionMock
            .Setup(c => c.FindAsync(
                It.IsAny<FilterDefinition<ProcessDocument>>(),
                It.IsAny<FindOptions<ProcessDocument, ProcessDocument>>(),
                default))
            .ReturnsAsync(cursorMock.Object);

        // Act
        var result = await _repository.GetByClientIdAsync(clientId, 0, 100);

        // Assert
        result.Should().HaveCount(2);
        result.Should().AllSatisfy(p => p.ClientId.Should().Be(clientId));
    }

    [Fact]
    public async Task GetByClientIdAsync_Should_ThrowArgumentException_WhenClientIdIsEmpty()
    {
        // Act
        Func<Task> act = async () => await _repository.GetByClientIdAsync("", 0, 100);

        // Assert
        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task GetByClientIdAsync_Should_ThrowArgumentOutOfRangeException_WhenSkipIsNegative()
    {
        // Act
        Func<Task> act = async () => await _repository.GetByClientIdAsync("client-id", -1, 100);

        // Assert
        await act.Should().ThrowAsync<ArgumentOutOfRangeException>();
    }

    [Fact]
    public async Task GetByClientIdAsync_Should_ThrowArgumentOutOfRangeException_WhenLimitTooLow()
    {
        // Act
        Func<Task> act = async () => await _repository.GetByClientIdAsync("client-id", 0, 0);

        // Assert
        await act.Should().ThrowAsync<ArgumentOutOfRangeException>();
    }

    #endregion

    #region CountActiveProcessesAsync Tests

    [Fact]
    public async Task CountActiveProcessesAsync_Should_ReturnCorrectCount()
    {
        // Arrange
        var clientId = "test-client";
        var processType = "order";
        var expectedCount = 5L;

        _collectionMock
            .Setup(c => c.CountDocumentsAsync(
                It.IsAny<FilterDefinition<ProcessDocument>>(),
                null,
                default))
            .ReturnsAsync(expectedCount);

        // Act
        var result = await _repository.CountActiveProcessesAsync(clientId, processType);

        // Assert
        result.Should().Be(5);
    }

    [Fact]
    public async Task CountActiveProcessesAsync_Should_ThrowArgumentException_WhenClientIdIsEmpty()
    {
        // Act
        Func<Task> act = async () => await _repository.CountActiveProcessesAsync("", "order");

        // Assert
        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task CountActiveProcessesAsync_Should_ThrowArgumentException_WhenProcessTypeIsEmpty()
    {
        // Act
        Func<Task> act = async () => await _repository.CountActiveProcessesAsync("client-id", "");

        // Assert
        await act.Should().ThrowAsync<ArgumentException>();
    }

    #endregion

    #region Helper Methods

    private static Process CreateValidProcess() => new()
    {
        ProcessId = Guid.NewGuid(),
        ClientProcessId = $"client-{Guid.NewGuid()}",
        ProcessType = "order",
        ClientId = "test-client",
        Status = ProcessStatus.Accepted,
        Progress = 0,
        CreatedAt = DateTime.UtcNow,
        UpdatedAt = DateTime.UtcNow,
        IdempotencyKey = Guid.NewGuid().ToString(),
        Retryable = true
    };

    private static ProcessDocument CreateValidDocument() => new()
    {
        ProcessId = Guid.NewGuid(),
        ClientProcessId = $"client-{Guid.NewGuid()}",
        ProcessType = "order",
        ClientId = "test-client",
        Status = "Accepted",
        Progress = 0,
        CreatedAt = DateTime.UtcNow,
        UpdatedAt = DateTime.UtcNow,
        IdempotencyKey = Guid.NewGuid().ToString(),
        Retryable = true
    };

    #endregion
}
