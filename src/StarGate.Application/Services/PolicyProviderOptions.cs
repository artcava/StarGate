namespace StarGate.Application.Services;

/// <summary>
/// Configuration options for PolicyProvider service.
/// Maps to "PolicyProvider" section in appsettings.json.
/// </summary>
public class PolicyProviderOptions
{
    /// <summary>
    /// Configuration section name.
    /// </summary>
    public const string SectionName = "PolicyProvider";

    /// <summary>
    /// Cache TTL in minutes for policies in Redis.
    /// Default: 60 minutes.
    /// </summary>
    public int CacheTtlMinutes { get; set; } = 60;

    /// <summary>
    /// Default timeout in seconds when no policy is found.
    /// Default: 300 seconds (5 minutes).
    /// </summary>
    public int DefaultTimeoutSeconds { get; set; } = 300;

    /// <summary>
    /// Default maximum retry attempts when no policy is found.
    /// Default: 3 attempts.
    /// </summary>
    public int DefaultMaxRetryAttempts { get; set; } = 3;

    /// <summary>
    /// Default retry delay in seconds when no policy is found.
    /// Default: 5 seconds.
    /// </summary>
    public int DefaultRetryDelaySeconds { get; set; } = 5;

    /// <summary>
    /// Default retention period in days when no policy is found.
    /// Default: 30 days.
    /// </summary>
    public int DefaultRetentionDays { get; set; } = 30;

    /// <summary>
    /// Default maximum concurrent executions when no policy is found.
    /// Null means no limit.
    /// Default: 10 concurrent executions.
    /// </summary>
    public int? DefaultMaxConcurrentProcesses { get; set; } = 10;

    /// <summary>
    /// Default backoff strategy when no policy is found.
    /// Default: Exponential.
    /// </summary>
    public string DefaultBackoffStrategy { get; set; } = "Exponential";
}
