namespace StarGate.Infrastructure.Services;

using Microsoft.Extensions.Logging;
using StackExchange.Redis;
using StarGate.Core.Abstractions;

/// <summary>
/// Redis-based implementation of idempotency service.
/// Uses Redis for fast, distributed idempotency key storage and validation.
/// </summary>
public class RedisIdempotencyService : IIdempotencyService
{
    private readonly IConnectionMultiplexer _redis;
    private readonly ILogger<RedisIdempotencyService> _logger;
    private const string KeyPrefix = "idempotency";
    private static readonly TimeSpan DefaultExpiration = TimeSpan.FromHours(24);

    public RedisIdempotencyService(
        IConnectionMultiplexer redis,
        ILogger<RedisIdempotencyService> logger)
    {
        _redis = redis ?? throw new ArgumentNullException(nameof(redis));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc />
    public async Task<Guid?> GetProcessIdByIdempotencyKeyAsync(
        string clientId,
        string idempotencyKey,
        CancellationToken cancellationToken = default)
    {
        var key = BuildKey(clientId, idempotencyKey);

        _logger.LogDebug(
            "Checking idempotency key: ClientId={ClientId}, IdempotencyKey={IdempotencyKey}",
            clientId,
            idempotencyKey);

        var db = _redis.GetDatabase();
        var value = await db.StringGetAsync(key);

        if (!value.HasValue)
        {
            _logger.LogDebug(
                "Idempotency key not found: ClientId={ClientId}, IdempotencyKey={IdempotencyKey}",
                clientId,
                idempotencyKey);
            return null;
        }

        var processId = Guid.Parse(value!);

        _logger.LogDebug(
            "Idempotency key found: ClientId={ClientId}, IdempotencyKey={IdempotencyKey}, ProcessId={ProcessId}",
            clientId,
            idempotencyKey,
            processId);

        return processId;
    }

    /// <inheritdoc />
    public async Task StoreIdempotencyKeyAsync(
        string clientId,
        string idempotencyKey,
        Guid processId,
        TimeSpan? expiration = null,
        CancellationToken cancellationToken = default)
    {
        var key = BuildKey(clientId, idempotencyKey);
        var value = processId.ToString();
        var expirationTime = expiration ?? DefaultExpiration;

        _logger.LogInformation(
            "Storing idempotency key: ClientId={ClientId}, IdempotencyKey={IdempotencyKey}, ProcessId={ProcessId}, Expiration={Expiration}",
            clientId,
            idempotencyKey,
            processId,
            expirationTime);

        var db = _redis.GetDatabase();
        var success = await db.StringSetAsync(key, value, expirationTime);

        if (!success)
        {
            _logger.LogWarning(
                "Failed to store idempotency key: ClientId={ClientId}, IdempotencyKey={IdempotencyKey}",
                clientId,
                idempotencyKey);

            throw new InvalidOperationException(
                $"Failed to store idempotency key for ClientId='{clientId}', IdempotencyKey='{idempotencyKey}'");
        }

        _logger.LogDebug(
            "Idempotency key stored successfully: ClientId={ClientId}, IdempotencyKey={IdempotencyKey}",
            clientId,
            idempotencyKey);
    }

    /// <inheritdoc />
    public async Task RemoveIdempotencyKeyAsync(
        string clientId,
        string idempotencyKey,
        CancellationToken cancellationToken = default)
    {
        var key = BuildKey(clientId, idempotencyKey);

        _logger.LogInformation(
            "Removing idempotency key: ClientId={ClientId}, IdempotencyKey={IdempotencyKey}",
            clientId,
            idempotencyKey);

        var db = _redis.GetDatabase();
        await db.KeyDeleteAsync(key);

        _logger.LogDebug(
            "Idempotency key removed: ClientId={ClientId}, IdempotencyKey={IdempotencyKey}",
            clientId,
            idempotencyKey);
    }

    private static string BuildKey(string clientId, string idempotencyKey)
    {
        return $"{KeyPrefix}:{clientId}:{idempotencyKey}";
    }
}
