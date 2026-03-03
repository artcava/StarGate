namespace StarGate.Infrastructure.Resilience;

/// <summary>
/// Configuration for circuit breaker policies.
/// </summary>
public class CircuitBreakerConfiguration
{
    /// <summary>
    /// Number of consecutive failures before breaking the circuit.
    /// </summary>
    public int FailureThreshold { get; set; } = 5;

    /// <summary>
    /// Percentage of failures in sampling duration before breaking.
    /// </summary>
    public double FailureRateThreshold { get; set; } = 0.5; // 50%

    /// <summary>
    /// Minimum throughput before considering failure rate.
    /// </summary>
    public int MinimumThroughput { get; set; } = 10;

    /// <summary>
    /// Duration to keep circuit open before testing recovery (seconds).
    /// </summary>
    public double BreakDurationSeconds { get; set; } = 30.0;

    /// <summary>
    /// Duration to sample for failure rate calculation (seconds).
    /// </summary>
    public double SamplingDurationSeconds { get; set; } = 60.0;

    /// <summary>
    /// Gets the break duration as TimeSpan.
    /// </summary>
    public TimeSpan BreakDuration => TimeSpan.FromSeconds(BreakDurationSeconds);

    /// <summary>
    /// Gets the sampling duration as TimeSpan.
    /// </summary>
    public TimeSpan SamplingDuration => TimeSpan.FromSeconds(SamplingDurationSeconds);
}
