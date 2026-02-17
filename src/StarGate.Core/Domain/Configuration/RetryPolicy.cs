namespace StarGate.Core.Domain.Configuration;

/// <summary>
/// Retry strategy configuration.
/// Defines automatic retry behavior for failed processes including
/// max attempts, delay timing, and backoff strategy.
/// </summary>
public record RetryPolicy
{
    /// <summary>
    /// Whether automatic retries are enabled.
    /// When false, failed processes require manual intervention.
    /// </summary>
    public required bool Enabled { get; init; }

    /// <summary>
    /// Maximum number of retry attempts.
    /// After exhausting attempts, the process enters permanent failure state.
    /// Example: 3 attempts = 1 initial + 3 retries = 4 total executions.
    /// </summary>
    public required int MaxAttempts { get; init; }

    /// <summary>
    /// Initial delay before first retry.
    /// Subsequent delays depend on BackoffStrategy.
    /// Example: TimeSpan.FromSeconds(5) for quick retry after transient failures.
    /// </summary>
    public required TimeSpan InitialDelay { get; init; }

    /// <summary>
    /// Backoff strategy determining how retry delays increase.
    /// Linear: constant increase (delay * attempt)
    /// Exponential: exponential increase (delay * 2^attempt)
    /// </summary>
    public required BackoffStrategy BackoffStrategy { get; init; }

    /// <summary>
    /// Maximum delay between retries (prevents exponential explosion).
    /// Caps the delay regardless of backoff calculation.
    /// Example: TimeSpan.FromMinutes(5) prevents waiting hours between retries.
    /// </summary>
    public required TimeSpan MaxDelay { get; init; }
}
