namespace StarGate.Core.Domain.Configuration;

/// <summary>
/// Defines the backoff strategy for retry delays.
/// Determines how delay time increases with each retry attempt.
/// </summary>
public enum BackoffStrategy
{
    /// <summary>
    /// Linear backoff: delay increases by a constant amount.
    /// Formula: InitialDelay * attempt
    /// Example: 5s, 10s, 15s, 20s...
    /// Use for: Predictable, steady retry patterns.
    /// </summary>
    Linear = 0,

    /// <summary>
    /// Exponential backoff: delay doubles with each retry.
    /// Formula: InitialDelay * 2^attempt (capped by MaxDelay)
    /// Example: 5s, 10s, 20s, 40s, 80s... (up to MaxDelay)
    /// Use for: Reducing load on failing systems, allowing recovery time.
    /// </summary>
    Exponential = 1
}
