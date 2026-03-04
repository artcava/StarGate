namespace StarGate.Infrastructure.Resilience;

/// <summary>
/// Configuration for retry policies.
/// </summary>
public class RetryPolicyConfiguration
{
    /// <summary>
    /// Maximum number of retry attempts.
    /// </summary>
    public int MaxRetryAttempts { get; set; } = 3;

    /// <summary>
    /// Initial delay before first retry (seconds).
    /// </summary>
    public double InitialDelaySeconds { get; set; } = 1.0;

    /// <summary>
    /// Maximum delay between retries (seconds).
    /// </summary>
    public double MaxDelaySeconds { get; set; } = 30.0;

    /// <summary>
    /// Exponential backoff multiplier.
    /// </summary>
    public double BackoffMultiplier { get; set; } = 2.0;

    /// <summary>
    /// Whether to use jitter to prevent thundering herd.
    /// </summary>
    public bool UseJitter { get; set; } = true;

    /// <summary>
    /// Calculates delay for a specific retry attempt.
    /// </summary>
    /// <param name="retryAttempt">The retry attempt number (1-based).</param>
    /// <returns>Time span representing the delay before next retry.</returns>
    public TimeSpan CalculateDelay(int retryAttempt)
    {
        var exponentialDelay = InitialDelaySeconds * Math.Pow(BackoffMultiplier, retryAttempt - 1);
        var delay = Math.Min(exponentialDelay, MaxDelaySeconds);

        if (UseJitter)
        {
            var random = new Random();
            // Generate jitter between -10% and +10%
            var jitter = delay * 0.2 * (random.NextDouble() - 0.5);
            delay += jitter;
        }

        return TimeSpan.FromSeconds(Math.Max(delay, 0));
    }
}
