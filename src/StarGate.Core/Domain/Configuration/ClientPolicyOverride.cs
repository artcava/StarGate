namespace StarGate.Core.Domain.Configuration;

/// <summary>
/// Client-specific policy overrides for a process type.
/// When present, these values override the process type defaults.
/// Enables custom SLA agreements and resource allocation per client.
/// </summary>
public record ClientPolicyOverride
{
    /// <summary>
    /// Client identifier from authentication token.
    /// Identifies which client this override applies to.
    /// </summary>
    public required string ClientId { get; init; }

    /// <summary>
    /// Process type this override applies to.
    /// Must match an existing ProcessTypePolicy.ProcessType.
    /// </summary>
    public required string ProcessType { get; init; }

    /// <summary>
    /// Custom timeout (overrides ProcessTypePolicy.Timeout).
    /// Null means use process type default.
    /// Example: Premium clients may have longer timeouts.
    /// </summary>
    public TimeSpan? Timeout { get; init; }

    /// <summary>
    /// Custom retry policy (overrides ProcessTypePolicy.RetryPolicy).
    /// Null means use process type default.
    /// Example: Critical clients may have more aggressive retry policies.
    /// </summary>
    public RetryPolicy? RetryPolicy { get; init; }

    /// <summary>
    /// Custom result retention (overrides ProcessTypePolicy.ResultRetention).
    /// Null means use process type default.
    /// Example: Compliance clients may require longer retention periods.
    /// </summary>
    public TimeSpan? ResultRetention { get; init; }

    /// <summary>
    /// Custom concurrency limit (overrides ProcessTypePolicy.MaxConcurrentProcesses).
    /// Null means use process type default.
    /// Example: Premium clients may have higher concurrency limits.
    /// </summary>
    public int? MaxConcurrentProcesses { get; init; }

    /// <summary>
    /// Timestamp when override was last updated (UTC).
    /// Used for cache invalidation and audit trails.
    /// </summary>
    public required DateTime UpdatedAt { get; init; }
}
