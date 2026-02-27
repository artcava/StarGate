using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using StackExchange.Redis;
using StarGate.Infrastructure.Services;
using Xunit;

namespace StarGate.Infrastructure.Tests.Services;

public class RedisIdempotencyServiceTests
{
    private readonly Mock<IConnectionMultiplexer> _redisMock;
    private readonly Mock<IDatabase> _databaseMock;
    private readonly RedisIdempotencyService _service;

    public RedisIdempotencyServiceTests()
    {
        _redisMock = new Mock<IConnectionMultiplexer>();
        _databaseMock = new Mock<IDatabase>();

        _redisMock.Setup(r => r.GetDatabase(It.IsAny<int>(), It.IsAny<object>()))
            .Returns(_databaseMock.Object);

        _service = new RedisIdempotencyService(
            _redisMock.Object,
            NullLogger<RedisIdempotencyService>.Instance);
    }

    [Fact]
    public void Constructor_Should_ThrowArgumentNullException_WhenRedisIsNull()
    {
        // Act
        var act = () => new RedisIdempotencyService(
            null!,
            NullLogger<RedisIdempotencyService>.Instance);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("redis");
    }

    [Fact]
    public void Constructor_Should_ThrowArgumentNullException_WhenLoggerIsNull()
    {
        // Act
        var act = () => new RedisIdempotencyService(
            _redisMock.Object,
            null!);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("logger");
    }

    [Fact]
    public async Task GetProcessIdByIdempotencyKeyAsync_Should_ReturnNull_WhenKeyNotFound()
    {
        // Arrange
        var clientId = "test-client";
        var idempotencyKey = "test-key-123";

        _databaseMock
            .Setup(db => db.StringGetAsync(
                It.IsAny<RedisKey>(),
                It.IsAny<CommandFlags>()))
            .ReturnsAsync(RedisValue.Null);

        // Act
        var result = await _service.GetProcessIdByIdempotencyKeyAsync(clientId, idempotencyKey);

        // Assert
        result.Should().BeNull();
        _databaseMock.Verify(
            db => db.StringGetAsync(
                It.Is<RedisKey>(k => k == "idempotency:test-client:test-key-123"),
                It.IsAny<CommandFlags>()),
            Times.Once);
    }

    [Fact]
    public async Task GetProcessIdByIdempotencyKeyAsync_Should_ReturnProcessId_WhenKeyExists()
    {
        // Arrange
        var clientId = "test-client";
        var idempotencyKey = "test-key-123";
        var expectedProcessId = Guid.NewGuid();

        _databaseMock
            .Setup(db => db.StringGetAsync(
                It.IsAny<RedisKey>(),
                It.IsAny<CommandFlags>()))
            .ReturnsAsync(expectedProcessId.ToString());

        // Act
        var result = await _service.GetProcessIdByIdempotencyKeyAsync(clientId, idempotencyKey);

        // Assert
        result.Should().Be(expectedProcessId);
    }

    [Fact]
    public async Task GetProcessIdByIdempotencyKeyAsync_Should_UseCorrectKeyFormat()
    {
        // Arrange
        var clientId = "client-456";
        var idempotencyKey = "key-789";
        var expectedKey = "idempotency:client-456:key-789";

        _databaseMock
            .Setup(db => db.StringGetAsync(
                It.IsAny<RedisKey>(),
                It.IsAny<CommandFlags>()))
            .ReturnsAsync(RedisValue.Null);

        // Act
        await _service.GetProcessIdByIdempotencyKeyAsync(clientId, idempotencyKey);

        // Assert
        _databaseMock.Verify(
            db => db.StringGetAsync(
                It.Is<RedisKey>(k => k == expectedKey),
                It.IsAny<CommandFlags>()),
            Times.Once);
    }

    [Fact]
    public async Task StoreIdempotencyKeyAsync_Should_StoreKeyWithDefaultExpiration()
    {
        // Arrange
        var clientId = "test-client";
        var idempotencyKey = "test-key-123";
        var processId = Guid.NewGuid();
        var expectedKey = "idempotency:test-client:test-key-123";

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
        await _service.StoreIdempotencyKeyAsync(clientId, idempotencyKey, processId);

        // Assert
        _databaseMock.Verify(
            db => db.StringSetAsync(
                It.Is<RedisKey>(k => k == expectedKey),
                It.Is<RedisValue>(v => v == processId.ToString()),
                It.Is<TimeSpan?>(t => t == TimeSpan.FromHours(24)),
                It.IsAny<bool>(),
                It.IsAny<When>(),
                It.IsAny<CommandFlags>()),
            Times.Once);
    }

    [Fact]
    public async Task StoreIdempotencyKeyAsync_Should_StoreKeyWithCustomExpiration()
    {
        // Arrange
        var clientId = "test-client";
        var idempotencyKey = "test-key-123";
        var processId = Guid.NewGuid();
        var customExpiration = TimeSpan.FromMinutes(30);

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
        await _service.StoreIdempotencyKeyAsync(
            clientId,
            idempotencyKey,
            processId,
            customExpiration);

        // Assert
        _databaseMock.Verify(
            db => db.StringSetAsync(
                It.IsAny<RedisKey>(),
                It.IsAny<RedisValue>(),
                It.Is<TimeSpan?>(t => t == customExpiration),
                It.IsAny<bool>(),
                It.IsAny<When>(),
                It.IsAny<CommandFlags>()),
            Times.Once);
    }

    [Fact]
    public async Task StoreIdempotencyKeyAsync_Should_ThrowException_WhenStoreFails()
    {
        // Arrange
        var clientId = "test-client";
        var idempotencyKey = "test-key-123";
        var processId = Guid.NewGuid();

        _databaseMock
            .Setup(db => db.StringSetAsync(
                It.IsAny<RedisKey>(),
                It.IsAny<RedisValue>(),
                It.IsAny<TimeSpan?>(),
                It.IsAny<bool>(),
                It.IsAny<When>(),
                It.IsAny<CommandFlags>()))
            .ReturnsAsync(false);

        // Act
        var act = async () => await _service.StoreIdempotencyKeyAsync(
            clientId,
            idempotencyKey,
            processId);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Failed to store idempotency key*");
    }

    [Fact]
    public async Task RemoveIdempotencyKeyAsync_Should_DeleteKey()
    {
        // Arrange
        var clientId = "test-client";
        var idempotencyKey = "test-key-123";
        var expectedKey = "idempotency:test-client:test-key-123";

        _databaseMock
            .Setup(db => db.KeyDeleteAsync(
                It.IsAny<RedisKey>(),
                It.IsAny<CommandFlags>()))
            .ReturnsAsync(true);

        // Act
        await _service.RemoveIdempotencyKeyAsync(clientId, idempotencyKey);

        // Assert
        _databaseMock.Verify(
            db => db.KeyDeleteAsync(
                It.Is<RedisKey>(k => k == expectedKey),
                It.IsAny<CommandFlags>()),
            Times.Once);
    }

    [Fact]
    public async Task RemoveIdempotencyKeyAsync_Should_NotThrow_WhenKeyDoesNotExist()
    {
        // Arrange
        var clientId = "test-client";
        var idempotencyKey = "test-key-123";

        _databaseMock
            .Setup(db => db.KeyDeleteAsync(
                It.IsAny<RedisKey>(),
                It.IsAny<CommandFlags>()))
            .ReturnsAsync(false);

        // Act
        var act = async () => await _service.RemoveIdempotencyKeyAsync(clientId, idempotencyKey);

        // Assert
        await act.Should().NotThrowAsync();
    }

    [Theory]
    [InlineData("client-1", "key-1")]
    [InlineData("client-2", "key-2")]
    [InlineData("test-client-123", "idempotency-key-456")]
    public async Task KeyFormat_Should_BeConsistent_AcrossOperations(string clientId, string idempotencyKey)
    {
        // Arrange
        var processId = Guid.NewGuid();
        var expectedKey = $"idempotency:{clientId}:{idempotencyKey}";

        _databaseMock
            .Setup(db => db.StringGetAsync(It.IsAny<RedisKey>(), It.IsAny<CommandFlags>()))
            .ReturnsAsync(RedisValue.Null);

        _databaseMock
            .Setup(db => db.StringSetAsync(
                It.IsAny<RedisKey>(),
                It.IsAny<RedisValue>(),
                It.IsAny<TimeSpan?>(),
                It.IsAny<bool>(),
                It.IsAny<When>(),
                It.IsAny<CommandFlags>()))
            .ReturnsAsync(true);

        _databaseMock
            .Setup(db => db.KeyDeleteAsync(It.IsAny<RedisKey>(), It.IsAny<CommandFlags>()))
            .ReturnsAsync(true);

        // Act & Assert - Get
        await _service.GetProcessIdByIdempotencyKeyAsync(clientId, idempotencyKey);
        _databaseMock.Verify(
            db => db.StringGetAsync(
                It.Is<RedisKey>(k => k == expectedKey),
                It.IsAny<CommandFlags>()),
            Times.Once);

        // Act & Assert - Store
        await _service.StoreIdempotencyKeyAsync(clientId, idempotencyKey, processId);
        _databaseMock.Verify(
            db => db.StringSetAsync(
                It.Is<RedisKey>(k => k == expectedKey),
                It.IsAny<RedisValue>(),
                It.IsAny<TimeSpan?>(),
                It.IsAny<bool>(),
                It.IsAny<When>(),
                It.IsAny<CommandFlags>()),
            Times.Once);

        // Act & Assert - Remove
        await _service.RemoveIdempotencyKeyAsync(clientId, idempotencyKey);
        _databaseMock.Verify(
            db => db.KeyDeleteAsync(
                It.Is<RedisKey>(k => k == expectedKey),
                It.IsAny<CommandFlags>()),
            Times.Once);
    }
}
