namespace StarGate.Core.Domain.Configuration;

/// <summary>
/// Defines default policies for a specific process type.
/// Applied to all clients unless overridden by client-specific policies.
/// Provides baseline behavior for timeout, retry, retention, and concurrency management.
/// </summary>
public record ProcessTypePolicy
{
    /// <summary>
    /// Process type identifier (e.g., "order", "shipping", "invoice").
    /// Used to match incoming processes with their corresponding policy configuration.
    /// </summary>
    public required string ProcessType { get; init; }

    /// <summary>
    /// Maximum execution time before process is automatically failed.
    /// Prevents runaway processes from consuming resources indefinitely.
    /// Example: TimeSpan.FromMinutes(30) for long-running batch operations.
    /// </summary>
    public required TimeSpan Timeout { get; init; }

    /// <summary>
    /// Retry configuration for failed processes.
    /// Defines automatic retry behavior including max attempts and backoff strategy.
    /// </summary>
    public required RetryPolicy RetryPolicy { get; init; }

    /// <summary>
    /// How long to retain completed process results.
    /// After this period, results may be purged to free storage.
    /// Example: TimeSpan.FromDays(30) for compliance retention.
    /// </summary>
    public required TimeSpan ResultRetention { get; init; }

    /// <summary>
    /// Maximum number of concurrent processes per client for this process type.
    /// Null means no limit (subject to global rate limiting).
    /// Example: 10 for resource-intensive operations, null for lightweight processes.
    /// </summary>
    public int? MaxConcurrentProcesses { get; init; }

    /// <summary>
    /// Timestamp when policy was last updated (UTC).
    /// Used for cache invalidation and audit trails.
    /// </summary>
    public required DateTime UpdatedAt { get; init; }
}
