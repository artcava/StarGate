namespace StarGate.Integration.Tests.Fixtures;

using Microsoft.Extensions.Logging.Abstractions;
using StackExchange.Redis;
using StarGate.Infrastructure.Caching;
using Testcontainers.Redis;
using Xunit;

/// <summary>
/// Provides a Redis test container for integration tests.
/// </summary>
public class RedisFixture : IAsyncLifetime
{
    private readonly RedisContainer _redisContainer;
    private IConnectionMultiplexer? _redis;
    private RedisStateStore? _stateStore;

    public RedisFixture()
    {
        _redisContainer = new RedisBuilder()
            .WithImage("redis:7.0-alpine")
            .WithPortBinding(6379, true)
            .Build();
    }

    public IConnectionMultiplexer Redis => _redis
        ?? throw new InvalidOperationException("Redis not initialized");

    public RedisStateStore StateStore => _stateStore
        ?? throw new InvalidOperationException("StateStore not initialized");

    public string ConnectionString => _redisContainer.GetConnectionString();

    public async Task InitializeAsync()
    {
        await _redisContainer.StartAsync();

        _redis = RedisConnectionFactory.CreateConnection(
            ConnectionString,
            NullLogger<RedisConnectionFactory>.Instance);

        _stateStore = new RedisStateStore(
            _redis,
            NullLogger<RedisStateStore>.Instance,
            TimeSpan.FromSeconds(30)); // Short TTL for testing
    }

    public async Task DisposeAsync()
    {
        _redis?.Dispose();
        await _redisContainer.DisposeAsync();
    }

    public async Task FlushDatabaseAsync()
    {
        var db = _redis!.GetDatabase();
        await db.ExecuteAsync("FLUSHDB");
    }
}
