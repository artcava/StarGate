namespace StarGate.Core.Configuration;

/// <summary>
/// Configuration for retry behavior.
/// </summary>
public class RetryConfiguration
{
    /// <summary>
    /// Base delay for first retry (seconds).
    /// </summary>
    public int BaseDelaySeconds { get; set; } = 5;

    /// <summary>
    /// Maximum delay between retries (seconds).
    /// </summary>
    public int MaxDelaySeconds { get; set; } = 300; // 5 minutes

    /// <summary>
    /// Exponential backoff multiplier.
    /// </summary>
    public double BackoffMultiplier { get; set; } = 2.0;

    /// <summary>
    /// Whether to add jitter to retry delays.
    /// </summary>
    public bool UseJitter { get; set; } = true;

    /// <summary>
    /// Calculates delay for a specific retry attempt.
    /// </summary>
    /// <param name="retryCount">Current retry attempt number (0-based).</param>
    /// <returns>Time span representing the delay before next retry.</returns>
    public TimeSpan CalculateDelay(int retryCount)
    {
        var delaySeconds = Math.Min(
            BaseDelaySeconds * Math.Pow(BackoffMultiplier, retryCount),
            MaxDelaySeconds);

        if (UseJitter)
        {
            var random = new Random();
            var jitter = random.NextDouble() * 0.3 * delaySeconds; // +/- 30%
            delaySeconds = delaySeconds * (1 + jitter - 0.15);
        }

        return TimeSpan.FromSeconds(delaySeconds);
    }
}
