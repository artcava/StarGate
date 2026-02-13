namespace StarGate.Infrastructure.Tests.Caching;

using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using StackExchange.Redis;
using StarGate.Core.Domain;
using StarGate.Infrastructure.Caching;
using System.Text.Json;
using Xunit;
using DomainProcess = StarGate.Core.Domain.Process;

/// <summary>
/// UNIT TESTS for RedisStateStore using MOCKED Redis.
/// NO real Redis instance required - all operations are mocked with Moq.
/// Tests verify caching logic, error handling, and fail-safe behavior.
/// </summary>
public class RedisStateStoreTests
{
    private readonly Mock<IConnectionMultiplexer> _redisMock;
    private readonly Mock<IDatabase> _databaseMock;
    private readonly Mock<ILogger<RedisStateStore>> _loggerMock;
    private readonly Mock<CacheMetrics> _metricsMock;
    private readonly RedisStateStore _stateStore;
    private readonly TimeSpan _defaultTtl = TimeSpan.FromMinutes(30);

    public RedisStateStoreTests()
    {
        _redisMock = new Mock<IConnectionMultiplexer>();
        _databaseMock = new Mock<IDatabase>();
        _loggerMock = new Mock<ILogger<RedisStateStore>>();
        _metricsMock = new Mock<CacheMetrics>();

        // Setup default behavior: GetDatabase() returns our mock database
        _redisMock
            .Setup(r => r.GetDatabase(It.IsAny<int>(), It.IsAny<object>()))
            .Returns(_databaseMock.Object);

        _stateStore = new RedisStateStore(
            _redisMock.Object,
            _loggerMock.Object,
            _defaultTtl,
            _metricsMock.Object);
    }

    #region Constructor Tests

    [Fact]
    public void Constructor_WithValidParameters_ShouldCreateInstance()
    {
        // Act
        var store = new RedisStateStore(
            _redisMock.Object,
            _loggerMock.Object,
            _defaultTtl,
            _metricsMock.Object);

        // Assert
        store.Should().NotBeNull();
    }

    [Fact]
    public void Constructor_WithNullRedis_ShouldThrowArgumentNullException()
    {
        // Act
        Action act = () => new RedisStateStore(
            null!,
            _loggerMock.Object,
            _defaultTtl,
            _metricsMock.Object);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("redis");
    }

    [Fact]
    public void Constructor_WithNullLogger_ShouldThrowArgumentNullException()
    {
        // Act
        Action act = () => new RedisStateStore(
            _redisMock.Object,
            null!,
            _defaultTtl,
            _metricsMock.Object);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("logger");
    }

    [Fact]
    public void Constructor_WithNullTtl_ShouldUseDefaultOneHour()
    {
        // Arrange
        var processId = Guid.NewGuid();
        var process = CreateTestProcess(processId);
        var json = JsonSerializer.Serialize(process);

        TimeSpan? capturedTtl = null;
        _databaseMock
            .Setup(db => db.StringSetAsync(
                It.IsAny<RedisKey>(),
                It.IsAny<RedisValue>(),
                It.IsAny<TimeSpan?>(),
                It.IsAny<bool>(),
                It.IsAny<When>(),
                It.IsAny<CommandFlags>()))
            .Callback<RedisKey, RedisValue, TimeSpan?, bool, When, CommandFlags>(
                (k, v, ttl, keepTtl, when, flags) => capturedTtl = ttl)
            .ReturnsAsync(true);

        // Act
        var store = new RedisStateStore(
            _redisMock.Object,
            _loggerMock.Object,
            defaultTtl: null);

        store.SetProcessAsync(process).Wait();

        // Assert
        capturedTtl.Should().Be(TimeSpan.FromHours(1));
    }

    [Fact]
    public void Constructor_WithNullMetrics_ShouldNotThrow()
    {
        // Act
        Action act = () => new RedisStateStore(
            _redisMock.Object,
            _loggerMock.Object,
            _defaultTtl,
            metrics: null);

        // Assert
        act.Should().NotThrow();
    }

    #endregion

    #region GetProcessAsync Tests

    [Fact]
    public async Task GetProcessAsync_WhenCacheHit_ShouldReturnProcess()
    {
        // Arrange
        var processId = Guid.NewGuid();
        var expectedProcess = CreateTestProcess(processId);
        var json = JsonSerializer.Serialize(expectedProcess);

        _databaseMock
            .Setup(db => db.StringGetAsync(
                It.IsAny<RedisKey>(),
                It.IsAny<CommandFlags>()))
            .ReturnsAsync(json);

        // Act
        var result = await _stateStore.GetProcessAsync(processId);

        // Assert
        result.Should().NotBeNull();
        result!.ProcessId.Should().Be(processId);
        result.Status.Should().Be(expectedProcess.Status);
        result.RequestPayload.Should().Be(expectedProcess.RequestPayload);

        _metricsMock.Verify(m => m.RecordHit(), Times.Once);
        _databaseMock.Verify(
            db => db.StringGetAsync(
                It.Is<RedisKey>(k => k.ToString() == $"process:{processId}"),
                It.IsAny<CommandFlags>()),
            Times.Once);
    }

    [Fact]
    public async Task GetProcessAsync_WhenCacheMiss_ShouldReturnNull()
    {
        // Arrange
        var processId = Guid.NewGuid();

        _databaseMock
            .Setup(db => db.StringGetAsync(
                It.IsAny<RedisKey>(),
                It.IsAny<CommandFlags>()))
            .ReturnsAsync(RedisValue.Null);

        // Act
        var result = await _stateStore.GetProcessAsync(processId);

        // Assert
        result.Should().BeNull();
        _metricsMock.Verify(m => m.RecordMiss(), Times.Once);
    }

    [Fact]
    public async Task GetProcessAsync_WhenRedisException_ShouldReturnNullAndLogError()
    {
        // Arrange
        var processId = Guid.NewGuid();
        var exception = new RedisConnectionException(
            ConnectionFailureType.UnableToConnect,
            "Connection failed");

        _databaseMock
            .Setup(db => db.StringGetAsync(
                It.IsAny<RedisKey>(),
                It.IsAny<CommandFlags>()))
            .ThrowsAsync(exception);

        // Act
        var result = await _stateStore.GetProcessAsync(processId);

        // Assert
        result.Should().BeNull();
        _metricsMock.Verify(m => m.RecordError(), Times.Once);
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => true),
                exception,
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task GetProcessAsync_WhenJsonException_ShouldReturnNullInvalidateAndLogError()
    {
        // Arrange
        var processId = Guid.NewGuid();
        var invalidJson = "{ invalid json }";

        _databaseMock
            .Setup(db => db.StringGetAsync(
                It.IsAny<RedisKey>(),
                It.IsAny<CommandFlags>()))
            .ReturnsAsync(invalidJson);

        _databaseMock
            .Setup(db => db.KeyDeleteAsync(
                It.IsAny<RedisKey>(),
                It.IsAny<CommandFlags>()))
            .ReturnsAsync(true);

        // Act
        var result = await _stateStore.GetProcessAsync(processId);

        // Assert
        result.Should().BeNull();
        _metricsMock.Verify(m => m.RecordError(), Times.Once);

        // Verify cache invalidation was called
        _databaseMock.Verify(
            db => db.KeyDeleteAsync(
                It.Is<RedisKey>(k => k.ToString().StartsWith($"process:{processId}")),
                It.IsAny<CommandFlags>()),
            Times.AtLeast(1));
    }

    [Fact]
    public async Task GetProcessAsync_ShouldRecordOperationDuration()
    {
        // Arrange
        var processId = Guid.NewGuid();
        _databaseMock
            .Setup(db => db.StringGetAsync(
                It.IsAny<RedisKey>(),
                It.IsAny<CommandFlags>()))
            .ReturnsAsync(RedisValue.Null);

        // Act
        await _stateStore.GetProcessAsync(processId);

        // Assert
        _metricsMock.Verify(
            m => m.RecordOperationDuration(It.IsAny<double>()),
            Times.Once);
    }

    #endregion

    #region SetProcessAsync Tests

    [Fact]
    public async Task SetProcessAsync_WithValidProcess_ShouldCacheWithTtl()
    {
        // Arrange
        var process = CreateTestProcess(Guid.NewGuid());

        _databaseMock
            .Setup(db => db.StringSetAsync(
                It.IsAny<RedisKey>(),
                It.IsAny<RedisValue>(),
                It.IsAny<TimeSpan?>(),
                It.IsAny<bool>(),
                It.IsAny<When>(),
                It.IsAny<CommandFlags>()))
            .ReturnsAsync(true);

        // Act
        await _stateStore.SetProcessAsync(process);

        // Assert
        _databaseMock.Verify(
            db => db.StringSetAsync(
                It.Is<RedisKey>(k => k.ToString() == $"process:{process.ProcessId}"),
                It.Is<RedisValue>(v => v.ToString().Contains(process.ProcessId.ToString())),
                _defaultTtl,
                It.IsAny<bool>(),
                It.IsAny<When>(),
                It.IsAny<CommandFlags>()),
            Times.Once);
    }

    [Fact]
    public async Task SetProcessAsync_WithNullProcess_ShouldThrowArgumentNullException()
    {
        // Act
        Func<Task> act = async () => await _stateStore.SetProcessAsync(null!);

        // Assert
        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task SetProcessAsync_WhenRedisException_ShouldNotThrowAndLogError()
    {
        // Arrange
        var process = CreateTestProcess(Guid.NewGuid());
        var exception = new RedisConnectionException(
            ConnectionFailureType.UnableToConnect,
            "Connection failed");

        _databaseMock
            .Setup(db => db.StringSetAsync(
                It.IsAny<RedisKey>(),
                It.IsAny<RedisValue>(),
                It.IsAny<TimeSpan?>(),
                It.IsAny<bool>(),
                It.IsAny<When>(),
                It.IsAny<CommandFlags>()))
            .ThrowsAsync(exception);

        // Act
        Func<Task> act = async () => await _stateStore.SetProcessAsync(process);

        // Assert
        await act.Should().NotThrowAsync();
        _metricsMock.Verify(m => m.RecordError(), Times.Once);
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => true),
                exception,
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task SetProcessAsync_ShouldSerializeProcessCorrectly()
    {
        // Arrange
        var process = CreateTestProcess(Guid.NewGuid());
        RedisValue capturedValue = default;

        _databaseMock
            .Setup(db => db.StringSetAsync(
                It.IsAny<RedisKey>(),
                It.IsAny<RedisValue>(),
                It.IsAny<TimeSpan?>(),
                It.IsAny<bool>(),
                It.IsAny<When>(),
                It.IsAny<CommandFlags>()))
            .Callback<RedisKey, RedisValue, TimeSpan?, bool, When, CommandFlags>(
                (k, v, ttl, keepTtl, when, flags) => capturedValue = v)
            .ReturnsAsync(true);

        // Act
        await _stateStore.SetProcessAsync(process);

        // Assert
        capturedValue.HasValue.Should().BeTrue();
        var deserializedProcess = JsonSerializer.Deserialize<DomainProcess>(capturedValue.ToString());
        deserializedProcess.Should().NotBeNull();
        deserializedProcess!.ProcessId.Should().Be(process.ProcessId);
        deserializedProcess.Status.Should().Be(process.Status);
    }

    [Fact]
    public async Task SetProcessAsync_ShouldRecordOperationDuration()
    {
        // Arrange
        var process = CreateTestProcess(Guid.NewGuid());
        _databaseMock
            .Setup(db => db.StringSetAsync(
                It.IsAny<RedisKey>(),
                It.IsAny<RedisValue>(),
                It.IsAny<TimeSpan?>(),
                It.IsAny<bool>(),
                It.IsAny<When>(),
                It.IsAny<CommandFlags>()))
            .ReturnsAsync(true);

        // Act
        await _stateStore.SetProcessAsync(process);

        // Assert
        _metricsMock.Verify(
            m => m.RecordOperationDuration(It.IsAny<double>()),
            Times.Once);
    }

    #endregion

    #region InvalidateAsync Tests

    [Fact]
    public async Task InvalidateAsync_ShouldDeleteAllRelatedKeys()
    {
        // Arrange
        var processId = Guid.NewGuid();

        _databaseMock
            .Setup(db => db.KeyDeleteAsync(
                It.IsAny<RedisKey>(),
                It.IsAny<CommandFlags>()))
            .ReturnsAsync(true);

        // Act
        await _stateStore.InvalidateAsync(processId);

        // Assert
        _databaseMock.Verify(
            db => db.KeyDeleteAsync(
                It.Is<RedisKey>(k => k.ToString() == $"process:{processId}"),
                It.IsAny<CommandFlags>()),
            Times.Once);

        _databaseMock.Verify(
            db => db.KeyDeleteAsync(
                It.Is<RedisKey>(k => k.ToString() == $"process:{processId}:status"),
                It.IsAny<CommandFlags>()),
            Times.Once);

        _databaseMock.Verify(
            db => db.KeyDeleteAsync(
                It.Is<RedisKey>(k => k.ToString() == $"process:{processId}:version"),
                It.IsAny<CommandFlags>()),
            Times.Once);
    }

    [Fact]
    public async Task InvalidateAsync_WhenRedisException_ShouldNotThrowAndLogError()
    {
        // Arrange
        var processId = Guid.NewGuid();
        var exception = new RedisConnectionException(
            ConnectionFailureType.UnableToConnect,
            "Connection failed");

        _databaseMock
            .Setup(db => db.KeyDeleteAsync(
                It.IsAny<RedisKey>(),
                It.IsAny<CommandFlags>()))
            .ThrowsAsync(exception);

        // Act
        Func<Task> act = async () => await _stateStore.InvalidateAsync(processId);

        // Assert
        await act.Should().NotThrowAsync();
        _metricsMock.Verify(m => m.RecordError(), Times.Once);
    }

    [Fact]
    public async Task InvalidateAsync_ShouldRecordOperationDuration()
    {
        // Arrange
        var processId = Guid.NewGuid();
        _databaseMock
            .Setup(db => db.KeyDeleteAsync(
                It.IsAny<RedisKey>(),
                It.IsAny<CommandFlags>()))
            .ReturnsAsync(true);

        // Act
        await _stateStore.InvalidateAsync(processId);

        // Assert
        _metricsMock.Verify(
            m => m.RecordOperationDuration(It.IsAny<double>()),
            Times.Once);
    }

    #endregion

    #region ExistsAsync Tests

    [Fact]
    public async Task ExistsAsync_WhenKeyExists_ShouldReturnTrue()
    {
        // Arrange
        var processId = Guid.NewGuid();

        _databaseMock
            .Setup(db => db.KeyExistsAsync(
                It.IsAny<RedisKey>(),
                It.IsAny<CommandFlags>()))
            .ReturnsAsync(true);

        // Act
        var result = await _stateStore.ExistsAsync(processId);

        // Assert
        result.Should().BeTrue();
        _databaseMock.Verify(
            db => db.KeyExistsAsync(
                It.Is<RedisKey>(k => k.ToString() == $"process:{processId}"),
                It.IsAny<CommandFlags>()),
            Times.Once);
    }

    [Fact]
    public async Task ExistsAsync_WhenKeyDoesNotExist_ShouldReturnFalse()
    {
        // Arrange
        var processId = Guid.NewGuid();

        _databaseMock
            .Setup(db => db.KeyExistsAsync(
                It.IsAny<RedisKey>(),
                It.IsAny<CommandFlags>()))
            .ReturnsAsync(false);

        // Act
        var result = await _stateStore.ExistsAsync(processId);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task ExistsAsync_WhenRedisException_ShouldReturnFalseAndLogError()
    {
        // Arrange
        var processId = Guid.NewGuid();
        var exception = new RedisConnectionException(
            ConnectionFailureType.UnableToConnect,
            "Connection failed");

        _databaseMock
            .Setup(db => db.KeyExistsAsync(
                It.IsAny<RedisKey>(),
                It.IsAny<CommandFlags>()))
            .ThrowsAsync(exception);

        // Act
        var result = await _stateStore.ExistsAsync(processId);

        // Assert
        result.Should().BeFalse();
        _metricsMock.Verify(m => m.RecordError(), Times.Once);
    }

    [Fact]
    public async Task ExistsAsync_ShouldRecordOperationDuration()
    {
        // Arrange
        var processId = Guid.NewGuid();
        _databaseMock
            .Setup(db => db.KeyExistsAsync(
                It.IsAny<RedisKey>(),
                It.IsAny<CommandFlags>()))
            .ReturnsAsync(true);

        // Act
        await _stateStore.ExistsAsync(processId);

        // Assert
        _metricsMock.Verify(
            m => m.RecordOperationDuration(It.IsAny<double>()),
            Times.Once);
    }

    #endregion

    #region TrySetStatusAsync Tests

    [Fact]
    public async Task TrySetStatusAsync_WhenVersionMatches_ShouldReturnTrue()
    {
        // Arrange
        var processId = Guid.NewGuid();
        var status = ProcessStatus.Processing;
        var expectedVersion = 1L;

        _databaseMock
            .Setup(db => db.ScriptEvaluateAsync(
                It.IsAny<string>(),
                It.IsAny<RedisKey[]>(),
                It.IsAny<RedisValue[]>(),
                It.IsAny<CommandFlags>()))
            .ReturnsAsync(RedisResult.Create(1)); // 1 = success

        // Act
        var result = await _stateStore.TrySetStatusAsync(
            processId,
            status,
            expectedVersion);

        // Assert
        result.Should().BeTrue();
        _databaseMock.Verify(
            db => db.ScriptEvaluateAsync(
                It.IsAny<string>(),
                It.Is<RedisKey[]>(keys =>
                    keys.Length == 2 &&
                    keys[0].ToString() == $"process:{processId}:status" &&
                    keys[1].ToString() == $"process:{processId}:version"),
                It.Is<RedisValue[]>(values =>
                    values.Length == 4 &&
                    values[0].ToString() == status.ToString() &&
                    (long)values[1] == expectedVersion &&
                    (long)values[2] == expectedVersion + 1),
                It.IsAny<CommandFlags>()),
            Times.Once);
    }

    [Fact]
    public async Task TrySetStatusAsync_WhenVersionMismatch_ShouldReturnFalse()
    {
        // Arrange
        var processId = Guid.NewGuid();
        var status = ProcessStatus.Processing;
        var expectedVersion = 1L;

        _databaseMock
            .Setup(db => db.ScriptEvaluateAsync(
                It.IsAny<string>(),
                It.IsAny<RedisKey[]>(),
                It.IsAny<RedisValue[]>(),
                It.IsAny<CommandFlags>()))
            .ReturnsAsync(RedisResult.Create(0)); // 0 = version mismatch

        // Act
        var result = await _stateStore.TrySetStatusAsync(
            processId,
            status,
            expectedVersion);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task TrySetStatusAsync_ShouldUseLuaScriptForAtomicOperation()
    {
        // Arrange
        var processId = Guid.NewGuid();
        var status = ProcessStatus.Completed;
        var expectedVersion = 5L;
        string? capturedScript = null;

        _databaseMock
            .Setup(db => db.ScriptEvaluateAsync(
                It.IsAny<string>(),
                It.IsAny<RedisKey[]>(),
                It.IsAny<RedisValue[]>(),
                It.IsAny<CommandFlags>()))
            .Callback<string, RedisKey[], RedisValue[], CommandFlags>(
                (script, keys, values, flags) => capturedScript = script)
            .ReturnsAsync(RedisResult.Create(1));

        // Act
        await _stateStore.TrySetStatusAsync(processId, status, expectedVersion);

        // Assert
        capturedScript.Should().NotBeNullOrEmpty();
        capturedScript.Should().Contain("GET");
        capturedScript.Should().Contain("SET");
        capturedScript.Should().Contain("EXPIRE");
    }

    [Fact]
    public async Task TrySetStatusAsync_WhenRedisException_ShouldReturnFalseAndLogError()
    {
        // Arrange
        var processId = Guid.NewGuid();
        var status = ProcessStatus.Processing;
        var expectedVersion = 1L;
        var exception = new RedisConnectionException(
            ConnectionFailureType.UnableToConnect,
            "Connection failed");

        _databaseMock
            .Setup(db => db.ScriptEvaluateAsync(
                It.IsAny<string>(),
                It.IsAny<RedisKey[]>(),
                It.IsAny<RedisValue[]>(),
                It.IsAny<CommandFlags>()))
            .ThrowsAsync(exception);

        // Act
        var result = await _stateStore.TrySetStatusAsync(
            processId,
            status,
            expectedVersion);

        // Assert
        result.Should().BeFalse();
        _metricsMock.Verify(m => m.RecordError(), Times.Once);
    }

    [Fact]
    public async Task TrySetStatusAsync_ShouldSetTtlOnBothKeys()
    {
        // Arrange
        var processId = Guid.NewGuid();
        var status = ProcessStatus.Processing;
        var expectedVersion = 1L;
        RedisValue[]? capturedValues = null;

        _databaseMock
            .Setup(db => db.ScriptEvaluateAsync(
                It.IsAny<string>(),
                It.IsAny<RedisKey[]>(),
                It.IsAny<RedisValue[]>(),
                It.IsAny<CommandFlags>()))
            .Callback<string, RedisKey[], RedisValue[], CommandFlags>(
                (script, keys, values, flags) => capturedValues = values)
            .ReturnsAsync(RedisResult.Create(1));

        // Act
        await _stateStore.TrySetStatusAsync(processId, status, expectedVersion);

        // Assert
        capturedValues.Should().NotBeNull();
        capturedValues.Should().HaveCount(4);
        // Last value is TTL in seconds
        var ttlSeconds = (int)capturedValues![3];
        ttlSeconds.Should().Be((int)_defaultTtl.TotalSeconds);
    }

    [Fact]
    public async Task TrySetStatusAsync_ShouldRecordOperationDuration()
    {
        // Arrange
        var processId = Guid.NewGuid();
        _databaseMock
            .Setup(db => db.ScriptEvaluateAsync(
                It.IsAny<string>(),
                It.IsAny<RedisKey[]>(),
                It.IsAny<RedisValue[]>(),
                It.IsAny<CommandFlags>()))
            .ReturnsAsync(RedisResult.Create(1));

        // Act
        await _stateStore.TrySetStatusAsync(
            processId,
            ProcessStatus.Processing,
            1L);

        // Assert
        _metricsMock.Verify(
            m => m.RecordOperationDuration(It.IsAny<double>()),
            Times.Once);
    }

    #endregion

    #region Key Generation Tests

    [Fact]
    public async Task GetProcessAsync_ShouldUseCorrectKeyFormat()
    {
        // Arrange
        var processId = Guid.NewGuid();
        var expectedKey = $"process:{processId}";
        RedisKey? capturedKey = null;

        _databaseMock
            .Setup(db => db.StringGetAsync(
                It.IsAny<RedisKey>(),
                It.IsAny<CommandFlags>()))
            .Callback<RedisKey, CommandFlags>((k, f) => capturedKey = k)
            .ReturnsAsync(RedisValue.Null);

        // Act
        await _stateStore.GetProcessAsync(processId);

        // Assert
        capturedKey.Should().NotBeNull();
        capturedKey!.Value.ToString().Should().Be(expectedKey);
    }

    [Fact]
    public async Task TrySetStatusAsync_ShouldUseCorrectStatusKeyFormat()
    {
        // Arrange
        var processId = Guid.NewGuid();
        var expectedStatusKey = $"process:{processId}:status";
        var expectedVersionKey = $"process:{processId}:version";
        RedisKey[]? capturedKeys = null;

        _databaseMock
            .Setup(db => db.ScriptEvaluateAsync(
                It.IsAny<string>(),
                It.IsAny<RedisKey[]>(),
                It.IsAny<RedisValue[]>(),
                It.IsAny<CommandFlags>()))
            .Callback<string, RedisKey[], RedisValue[], CommandFlags>(
                (s, k, v, f) => capturedKeys = k)
            .ReturnsAsync(RedisResult.Create(1));

        // Act
        await _stateStore.TrySetStatusAsync(
            processId,
            ProcessStatus.Processing,
            1L);

        // Assert
        capturedKeys.Should().NotBeNull();
        capturedKeys!.Should().HaveCount(2);
        capturedKeys[0].ToString().Should().Be(expectedStatusKey);
        capturedKeys[1].ToString().Should().Be(expectedVersionKey);
    }

    #endregion

    #region Helper Methods

    /// <summary>
    /// Creates a test process with realistic data for testing.
    /// </summary>
    private static DomainProcess CreateTestProcess(Guid processId)
    {
        return new DomainProcess(
            processId: processId,
            requestPayload: "{\"test\":\"data\"}",
            status: ProcessStatus.Accepted,
            createdAt: DateTime.UtcNow,
            updatedAt: DateTime.UtcNow);
    }

    #endregion
}
