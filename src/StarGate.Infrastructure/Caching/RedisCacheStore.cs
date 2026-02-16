namespace StarGate.Infrastructure.Caching;

using Microsoft.Extensions.Logging;
using StackExchange.Redis;
using StarGate.Core.Abstractions;
using System.Text.Json;

/// <summary>
/// Redis implementation of ICacheStore for generic caching.
/// Provides distributed caching with serialization support.
/// Thread-safe and supports TTL (time-to-live) for cache entries.
/// </summary>
public class RedisCacheStore : ICacheStore
{
    private readonly IConnectionMultiplexer _redis;
    private readonly ILogger<RedisCacheStore> _logger;
    private readonly JsonSerializerOptions _jsonOptions;

    public RedisCacheStore(
        IConnectionMultiplexer redis,
        ILogger<RedisCacheStore> logger)
    {
        _redis = redis ?? throw new ArgumentNullException(nameof(redis));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            WriteIndented = false
        };
    }

    /// <inheritdoc/>
    public async Task<T?> GetAsync<T>(string key, CancellationToken ct = default) where T : class
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key, nameof(key));

        try
        {
            var db = _redis.GetDatabase();
            var value = await db.StringGetAsync(key);

            if (!value.HasValue)
            {
                _logger.LogDebug("Cache miss: {Key}", key);
                return null;
            }

            _logger.LogDebug("Cache hit: {Key}", key);
            return JsonSerializer.Deserialize<T>(value.ToString(), _jsonOptions);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving from cache: {Key}", key);
            return null;
        }
    }

    /// <inheritdoc/>
    public async Task SetAsync<T>(
        string key,
        T value,
        TimeSpan ttl,
        CancellationToken ct = default) where T : class
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key, nameof(key));
        ArgumentNullException.ThrowIfNull(value, nameof(value));

        try
        {
            var db = _redis.GetDatabase();
            var serialized = JsonSerializer.Serialize(value, _jsonOptions);
            await db.StringSetAsync(key, serialized, ttl);

            _logger.LogDebug("Cached: {Key}, TTL={Ttl}s", key, ttl.TotalSeconds);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error setting cache: {Key}", key);
        }
    }

    /// <inheritdoc/>
    public async Task DeleteAsync(string key, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key, nameof(key));

        try
        {
            var db = _redis.GetDatabase();
            await db.KeyDeleteAsync(key);
            _logger.LogDebug("Deleted from cache: {Key}", key);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting from cache: {Key}", key);
        }
    }

    /// <inheritdoc/>
    public async Task DeleteByPatternAsync(string pattern, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(pattern, nameof(pattern));

        try
        {
            var server = _redis.GetServer(_redis.GetEndPoints().First());
            var db = _redis.GetDatabase();
            var keys = server.Keys(pattern: pattern).ToArray();

            if (keys.Length > 0)
            {
                await db.KeyDeleteAsync(keys);
                _logger.LogInformation(
                    "Deleted {Count} keys matching pattern: {Pattern}",
                    keys.Length,
                    pattern);
            }
            else
            {
                _logger.LogDebug("No keys found matching pattern: {Pattern}", pattern);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting by pattern: {Pattern}", pattern);
        }
    }

    /// <inheritdoc/>
    public async Task<bool> ExistsAsync(string key, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key, nameof(key));

        try
        {
            var db = _redis.GetDatabase();
            return await db.KeyExistsAsync(key);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking key existence: {Key}", key);
            return false;
        }
    }
}
