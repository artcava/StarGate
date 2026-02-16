namespace StarGate.Core.Domain.Configuration;

/// <summary>
/// Client-specific policy overrides for a process type.
/// When present, these values override the process type defaults.
/// Immutable record type ensures thread safety.
/// </summary>
public record ClientPolicyOverride
{
    /// <summary>
    /// Client identifier.
    /// </summary>
    public required string ClientId { get; init; }

    /// <summary>
    /// Process type this override applies to.
    /// </summary>
    public required string ProcessType { get; init; }

    /// <summary>
    /// Custom timeout (overrides ProcessTypePolicy.Timeout).
    /// </summary>
    public TimeSpan? Timeout { get; init; }

    /// <summary>
    /// Custom retry policy (overrides ProcessTypePolicy.RetryPolicy).
    /// </summary>
    public RetryPolicy? RetryPolicy { get; init; }

    /// <summary>
    /// Custom result retention (overrides ProcessTypePolicy.ResultRetention).
    /// </summary>
    public TimeSpan? ResultRetention { get; init; }

    /// <summary>
    /// Custom concurrency limit (overrides ProcessTypePolicy.MaxConcurrentProcesses).
    /// </summary>
    public int? MaxConcurrentProcesses { get; init; }

    /// <summary>
    /// Timestamp when override was last updated (UTC).
    /// Used for cache invalidation and audit trails.
    /// </summary>
    public required DateTime UpdatedAt { get; init; }
}
