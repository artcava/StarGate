using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;

namespace StarGate.Infrastructure.Caching;

/// <summary>
/// Health check for Redis connection and operations.
/// Tests connectivity and performs basic read/write operations.
/// </summary>
public class RedisHealthCheck : IHealthCheck
{
    private readonly IConnectionMultiplexer _redis;
    private readonly ILogger<RedisHealthCheck> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="RedisHealthCheck"/> class.
    /// </summary>
    /// <param name="redis">Redis connection multiplexer.</param>
    /// <param name="logger">Logger for health check events.</param>
    /// <exception cref="ArgumentNullException">If redis or logger is null.</exception>
    public RedisHealthCheck(
        IConnectionMultiplexer redis,
        ILogger<RedisHealthCheck> logger)
    {
        _redis = redis ?? throw new ArgumentNullException(nameof(redis));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Checks Redis health by testing connection and performing read/write operations.
    /// </summary>
    /// <param name="context">Health check context.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>
    /// Healthy if connection is established and read/write operations succeed.
    /// Degraded if timeout occurs or read/write mismatch.
    /// Unhealthy if connection is not established or connection errors occur.
    /// </returns>
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            if (!_redis.IsConnected)
            {
                _logger.LogWarning("Redis health check: Connection not established");
                return HealthCheckResult.Unhealthy(
                    "Redis connection is not established");
            }

            IDatabase db = _redis.GetDatabase();
            string key = "health:check";
            string value = DateTime.UtcNow.Ticks.ToString();

            // Attempt write
            await db.StringSetAsync(key, value, TimeSpan.FromSeconds(10));

            // Attempt read
            RedisValue retrieved = await db.StringGetAsync(key);

            if (retrieved != value)
            {
                _logger.LogWarning("Redis health check: Read/write mismatch");
                return HealthCheckResult.Degraded(
                    "Redis read/write operation returned unexpected value");
            }

            // Clean up
            await db.KeyDeleteAsync(key);

            System.Net.EndPoint[] endpoints = _redis.GetEndPoints();
            var serverInfo = new Dictionary<string, object>
            {
                ["endpoints"] = string.Join(", ", endpoints.Select(ep => ep.ToString())),
                ["connected"] = _redis.IsConnected,
                ["timestamp"] = DateTime.UtcNow
            };

            _logger.LogDebug("Redis health check: Healthy");

            return HealthCheckResult.Healthy(
                "Redis is responsive",
                serverInfo);
        }
        catch (RedisConnectionException ex)
        {
            _logger.LogError(ex, "Redis health check: Connection error");
            return HealthCheckResult.Unhealthy(
                "Redis connection error",
                ex);
        }
        catch (RedisTimeoutException ex)
        {
            _logger.LogError(ex, "Redis health check: Timeout");
            return HealthCheckResult.Degraded(
                "Redis timeout",
                ex);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Redis health check: Unexpected error");
            return HealthCheckResult.Unhealthy(
                "Redis health check failed",
                ex);
        }
    }
}
