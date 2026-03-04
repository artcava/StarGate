namespace StarGate.Infrastructure.Resilience;

/// <summary>
/// Configuration for timeout policies.
/// </summary>
public class TimeoutConfiguration
{
    /// <summary>
    /// Timeout for HTTP requests (seconds).
    /// </summary>
    public double HttpTimeoutSeconds { get; set; } = 30.0;

    /// <summary>
    /// Timeout for database operations (seconds).
    /// </summary>
    public double DatabaseTimeoutSeconds { get; set; } = 10.0;

    /// <summary>
    /// Timeout for message broker operations (seconds).
    /// </summary>
    public double BrokerTimeoutSeconds { get; set; } = 5.0;

    /// <summary>
    /// Whether to use pessimistic timeout (cancels operation).
    /// If false, uses optimistic timeout (monitors but doesn't cancel).
    /// </summary>
    public bool UsePessimisticTimeout { get; set; } = true;

    /// <summary>
    /// Gets HTTP timeout as TimeSpan.
    /// </summary>
    public TimeSpan HttpTimeout => TimeSpan.FromSeconds(HttpTimeoutSeconds);

    /// <summary>
    /// Gets database timeout as TimeSpan.
    /// </summary>
    public TimeSpan DatabaseTimeout => TimeSpan.FromSeconds(DatabaseTimeoutSeconds);

    /// <summary>
    /// Gets broker timeout as TimeSpan.
    /// </summary>
    public TimeSpan BrokerTimeout => TimeSpan.FromSeconds(BrokerTimeoutSeconds);
}
