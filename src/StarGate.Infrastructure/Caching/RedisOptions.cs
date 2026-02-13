namespace StarGate.Infrastructure.Caching;

/// <summary>
/// Configuration options for Redis cache.
/// </summary>
public class RedisOptions
{
    /// <summary>
    /// Configuration section name in appsettings.json.
    /// </summary>
    public const string SectionName = "Redis";

    /// <summary>
    /// Redis connection string.
    /// Format: "host:port,password=xxx,ssl=true" or "host1:port1,host2:port2" for cluster.
    /// </summary>
    public required string ConnectionString { get; init; }

    /// <summary>
    /// Default TTL (Time-To-Live) for cached items in seconds.
    /// Default: 3600 seconds (1 hour).
    /// </summary>
    public int DefaultTtlSeconds { get; init; } = 3600;

    /// <summary>
    /// Whether Redis caching is enabled.
    /// When false, NullStateStore is used as a no-op implementation.
    /// </summary>
    public bool Enabled { get; init; } = true;
}
