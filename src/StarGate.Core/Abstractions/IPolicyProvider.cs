using StarGate.Core.Domain.Configuration;

namespace StarGate.Core.Abstractions;

/// <summary>
/// Resolves effective policies by merging process type defaults with client overrides.
/// Implements hierarchical configuration model: client overrides take precedence.
/// Caches resolved policies for performance.
/// </summary>
public interface IPolicyProvider
{
    /// <summary>
    /// Gets the effective timeout for a client and process type.
    /// Client override takes precedence over process type default.
    /// Returns process type default if no client override exists.
    /// </summary>
    /// <param name="clientId">Client identifier.</param>
    /// <param name="processType">Process type.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Effective timeout duration.</returns>
    /// <exception cref="ArgumentNullException">If clientId or processType is null.</exception>
    /// <exception cref="InvalidOperationException">If process type not found.</exception>
    public Task<TimeSpan> GetTimeoutAsync(
        string clientId,
        string processType,
        CancellationToken ct = default);

    /// <summary>
    /// Gets the effective retry policy for a client and process type.
    /// Merges client override with process type default.
    /// </summary>
    /// <param name="clientId">Client identifier.</param>
    /// <param name="processType">Process type.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Effective retry policy.</returns>
    /// <exception cref="ArgumentNullException">If clientId or processType is null.</exception>
    /// <exception cref="InvalidOperationException">If process type not found.</exception>
    public Task<RetryPolicy> GetRetryPolicyAsync(
        string clientId,
        string processType,
        CancellationToken ct = default);

    /// <summary>
    /// Gets the effective result retention period for a client and process type.
    /// Client override takes precedence over process type default.
    /// </summary>
    /// <param name="clientId">Client identifier.</param>
    /// <param name="processType">Process type.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Effective result retention duration.</returns>
    /// <exception cref="ArgumentNullException">If clientId or processType is null.</exception>
    /// <exception cref="InvalidOperationException">If process type not found.</exception>
    public Task<TimeSpan> GetResultRetentionAsync(
        string clientId,
        string processType,
        CancellationToken ct = default);

    /// <summary>
    /// Gets the effective concurrency limit for a client and process type.
    /// Client override takes precedence over process type default.
    /// </summary>
    /// <param name="clientId">Client identifier.</param>
    /// <param name="processType">Process type.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Effective concurrency limit, or null if unlimited.</returns>
    /// <exception cref="ArgumentNullException">If clientId or processType is null.</exception>
    /// <exception cref="InvalidOperationException">If process type not found.</exception>
    public Task<int?> GetConcurrencyLimitAsync(
        string clientId,
        string processType,
        CancellationToken ct = default);

    /// <summary>
    /// Gets the complete effective policy for a client and process type.
    /// Useful when multiple policy values are needed to avoid multiple calls.
    /// Returns merged policy with source tracking.
    /// </summary>
    /// <param name="clientId">Client identifier.</param>
    /// <param name="processType">Process type.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Merged effective policy with source information.</returns>
    /// <exception cref="ArgumentNullException">If clientId or processType is null.</exception>
    /// <exception cref="InvalidOperationException">If process type not found.</exception>
    public Task<EffectivePolicy> GetEffectivePolicyAsync(
        string clientId,
        string processType,
        CancellationToken ct = default);
}

/// <summary>
/// Represents the resolved effective policy after merging defaults and overrides.
/// Immutable record type ensures thread safety.
/// Includes source tracking to identify origin of each value.
/// </summary>
public record EffectivePolicy
{
    /// <summary>
    /// Process type this policy applies to.
    /// </summary>
    public required string ProcessType { get; init; }

    /// <summary>
    /// Client this policy is resolved for.
    /// </summary>
    public required string ClientId { get; init; }

    /// <summary>
    /// Effective timeout duration.
    /// Maximum time allowed for process execution.
    /// </summary>
    public required TimeSpan Timeout { get; init; }

    /// <summary>
    /// Effective retry policy.
    /// Defines retry behavior on failures.
    /// </summary>
    public required RetryPolicy RetryPolicy { get; init; }

    /// <summary>
    /// Effective result retention period.
    /// How long completed results are kept.
    /// </summary>
    public required TimeSpan ResultRetention { get; init; }

    /// <summary>
    /// Effective concurrency limit.
    /// Maximum concurrent processes of this type for this client.
    /// Null means unlimited.
    /// </summary>
    public int? MaxConcurrentProcesses { get; init; }

    /// <summary>
    /// Source tracking for each policy value.
    /// Indicates whether value comes from override or default.
    /// </summary>
    public required PolicySource Source { get; init; }
}

/// <summary>
/// Indicates the source of policy values.
/// Tracks whether each value comes from client override or process type default.
/// Useful for debugging and auditing policy resolution.
/// </summary>
public record PolicySource
{
    /// <summary>
    /// True if timeout comes from client override, false if from process type default.
    /// </summary>
    public required bool TimeoutFromOverride { get; init; }

    /// <summary>
    /// True if retry policy comes from client override, false if from process type default.
    /// </summary>
    public required bool RetryPolicyFromOverride { get; init; }

    /// <summary>
    /// True if result retention comes from client override, false if from process type default.
    /// </summary>
    public required bool ResultRetentionFromOverride { get; init; }

    /// <summary>
    /// True if concurrency limit comes from client override, false if from process type default.
    /// </summary>
    public required bool ConcurrencyLimitFromOverride { get; init; }
}
