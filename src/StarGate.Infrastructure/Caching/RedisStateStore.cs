using Microsoft.Extensions.Logging;
using StackExchange.Redis;
using StarGate.Core.Abstractions;
using StarGate.Core.Domain;
using System.Text.Json;

namespace StarGate.Infrastructure.Caching;

/// <summary>
/// Redis-based implementation of IStateStore for process caching.
/// Implements fail-safe pattern: cache failures do not break the application.
/// All exceptions are caught, logged, and result in graceful degradation.
/// </summary>
public class RedisStateStore : IStateStore
{
    private readonly IConnectionMultiplexer _redis;
    private readonly ILogger<RedisStateStore> _logger;
    private readonly TimeSpan _defaultTtl;
    private const string KeyPrefix = "process:";
    private const string StatusKey = ":status";
    private const string VersionKey = ":version";

    /// <summary>
    /// Initializes a new instance of RedisStateStore.
    /// </summary>
    /// <param name="redis">Redis connection multiplexer.</param>
    /// <param name="logger">Logger instance.</param>
    /// <param name="defaultTtl">Default TTL for cached items. Defaults to 1 hour if not specified.</param>
    /// <exception cref="ArgumentNullException">If redis or logger is null.</exception>
    public RedisStateStore(
        IConnectionMultiplexer redis,
        ILogger<RedisStateStore> logger,
        TimeSpan? defaultTtl = null)
    {
        _redis = redis ?? throw new ArgumentNullException(nameof(redis));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _defaultTtl = defaultTtl ?? TimeSpan.FromHours(1);
    }

    /// <inheritdoc />
    public async Task<Process?> GetProcessAsync(Guid processId)
    {
        try
        {
            var db = _redis.GetDatabase();
            var key = GetKey(processId);
            var cached = await db.StringGetAsync(key);

            if (!cached.HasValue)
            {
                _logger.LogDebug(
                    "Cache miss for process {ProcessId}",
                    processId);
                return null;
            }

            var process = JsonSerializer.Deserialize<Process>(cached!);

            _logger.LogDebug(
                "Cache hit for process {ProcessId}",
                processId);

            return process;
        }
        catch (RedisException ex)
        {
            _logger.LogError(
                ex,
                "Redis error while getting process {ProcessId}. Failing gracefully.",
                processId);
            return null; // Fail gracefully - caller will fetch from repository
        }
        catch (JsonException ex)
        {
            _logger.LogError(
                ex,
                "JSON deserialization error for process {ProcessId}",
                processId);

            // Invalidate corrupted cache entry
            await InvalidateAsync(processId);
            return null;
        }
    }

    /// <inheritdoc />
    public async Task SetProcessAsync(Process process)
    {
        ArgumentNullException.ThrowIfNull(process);

        try
        {
            var db = _redis.GetDatabase();
            var key = GetKey(process.ProcessId);
            var json = JsonSerializer.Serialize(process);

            await db.StringSetAsync(key, json, _defaultTtl);

            _logger.LogDebug(
                "Cached process {ProcessId} with TTL {TTL}",
                process.ProcessId,
                _defaultTtl);
        }
        catch (RedisException ex)
        {
            _logger.LogError(
                ex,
                "Redis error while caching process {ProcessId}. Failing gracefully.",
                process.ProcessId);
            // Don't throw - caching failure shouldn't break the application
        }
        catch (JsonException ex)
        {
            _logger.LogError(
                ex,
                "JSON serialization error for process {ProcessId}",
                process.ProcessId);
        }
    }

    /// <inheritdoc />
    public async Task InvalidateAsync(Guid processId)
    {
        try
        {
            var db = _redis.GetDatabase();
            var key = GetKey(processId);
            var statusKey = GetStatusKey(processId);
            var versionKey = GetVersionKey(processId);

            // Delete all related keys
            await Task.WhenAll(
                db.KeyDeleteAsync(key),
                db.KeyDeleteAsync(statusKey),
                db.KeyDeleteAsync(versionKey));

            _logger.LogDebug(
                "Invalidated cache for process {ProcessId}",
                processId);
        }
        catch (RedisException ex)
        {
            _logger.LogError(
                ex,
                "Redis error while invalidating cache for process {ProcessId}",
                processId);
            // Don't throw - cache invalidation failure is not critical
        }
    }

    /// <inheritdoc />
    public async Task<bool> ExistsAsync(Guid processId)
    {
        try
        {
            var db = _redis.GetDatabase();
            var key = GetKey(processId);
            return await db.KeyExistsAsync(key);
        }
        catch (RedisException ex)
        {
            _logger.LogError(
                ex,
                "Redis error while checking existence of process {ProcessId}",
                processId);
            return false; // Fail gracefully
        }
    }

    /// <inheritdoc />
    public async Task<bool> TrySetStatusAsync(
        Guid processId,
        ProcessStatus status,
        long expectedVersion)
    {
        try
        {
            var db = _redis.GetDatabase();
            var statusKey = GetStatusKey(processId);
            var versionKey = GetVersionKey(processId);

            // Lua script for atomic compare-and-set with version check
            var script = @"
                local currentVersion = redis.call('GET', KEYS[2])
                if currentVersion == false or tonumber(currentVersion) == tonumber(ARGV[2]) then
                    redis.call('SET', KEYS[1], ARGV[1])
                    redis.call('SET', KEYS[2], ARGV[3])
                    redis.call('EXPIRE', KEYS[1], ARGV[4])
                    redis.call('EXPIRE', KEYS[2], ARGV[4])
                    return 1
                else
                    return 0
                end
            ";

            var result = await db.ScriptEvaluateAsync(
                script,
                new RedisKey[] { statusKey, versionKey },
                new RedisValue[]
                {
                    status.ToString(),
                    expectedVersion,
                    expectedVersion + 1,
                    (int)_defaultTtl.TotalSeconds
                });

            var success = (int)result == 1;

            if (success)
            {
                _logger.LogDebug(
                    "Updated status for process {ProcessId} to {Status} with version {Version}",
                    processId,
                    status,
                    expectedVersion + 1);
            }
            else
            {
                _logger.LogDebug(
                    "Version mismatch for process {ProcessId}. Expected {ExpectedVersion}",
                    processId,
                    expectedVersion);
            }

            return success;
        }
        catch (RedisException ex)
        {
            _logger.LogError(
                ex,
                "Redis error while setting status for process {ProcessId}",
                processId);
            return false; // Fail gracefully
        }
    }

    private static string GetKey(Guid processId) => $"{KeyPrefix}{processId}";
    private static string GetStatusKey(Guid processId) => $"{KeyPrefix}{processId}{StatusKey}";
    private static string GetVersionKey(Guid processId) => $"{KeyPrefix}{processId}{VersionKey}";
}
