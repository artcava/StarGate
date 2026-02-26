namespace StarGate.Api.Configuration;

/// <summary>
/// Configuration options for rate limiting.
/// </summary>
public class RateLimitOptions
{
    public const string SectionName = "RateLimit";

    /// <summary>
    /// Whether rate limiting is enabled.
    /// </summary>
    public bool Enabled { get; init; } = true;

    /// <summary>
    /// Default rate limit configuration.
    /// </summary>
    public RateLimitPolicy DefaultPolicy { get; init; } = new();

    /// <summary>
    /// Rate limit policies per endpoint.
    /// </summary>
    public Dictionary<string, RateLimitPolicy> EndpointPolicies { get; init; } = new();
}

/// <summary>
/// Rate limit policy configuration.
/// </summary>
public class RateLimitPolicy
{
    /// <summary>
    /// Maximum number of requests allowed in the window.
    /// </summary>
    public int PermitLimit { get; init; } = 100;

    /// <summary>
    /// Time window in seconds.
    /// </summary>
    public int WindowSeconds { get; init; } = 60;

    /// <summary>
    /// Number of requests that can queue when limit is reached.
    /// </summary>
    public int QueueLimit { get; init; } = 0;

    /// <summary>
    /// Whether to use sliding window (true) or fixed window (false).
    /// </summary>
    public bool UseSlidingWindow { get; init; } = true;
}
