using StarGate.Core.Domain.Configuration;

namespace StarGate.Core.Abstractions;

/// <summary>
/// Repository for policy configuration persistence.
/// Manages process type defaults and client-specific overrides.
/// Supports hierarchical policy resolution: defaults + overrides.
/// </summary>
public interface IPolicyRepository
{
    /// <summary>
    /// Retrieves process type policy defaults.
    /// These are baseline policies applied to all clients unless overridden.
    /// </summary>
    /// <param name="processType">Process type identifier.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Process type policy.</returns>
    /// <exception cref="KeyNotFoundException">If policy not found.</exception>
    public Task<ProcessTypePolicy> GetProcessTypePolicyAsync(
        string processType,
        CancellationToken ct = default);

    /// <summary>
    /// Retrieves client-specific policy overrides.
    /// Returns null if no override exists (use process type defaults).
    /// </summary>
    /// <param name="clientId">Client identifier.</param>
    /// <param name="processType">Process type identifier.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Client policy override if exists, null otherwise.</returns>
    public Task<ClientPolicyOverride?> GetClientOverrideAsync(
        string clientId,
        string processType,
        CancellationToken ct = default);

    /// <summary>
    /// Creates or updates process type policy.
    /// Upsert operation: creates if not exists, updates if exists.
    /// Updates UpdatedAt timestamp automatically.
    /// </summary>
    /// <param name="policy">Policy to save.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Saved policy with updated timestamp.</returns>
    /// <exception cref="ArgumentNullException">If policy is null.</exception>
    public Task<ProcessTypePolicy> SaveProcessTypePolicyAsync(
        ProcessTypePolicy policy,
        CancellationToken ct = default);

    /// <summary>
    /// Creates or updates client policy override.
    /// Upsert operation: creates if not exists, updates if exists.
    /// Updates UpdatedAt timestamp automatically.
    /// </summary>
    /// <param name="override">Override to save.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Saved override with updated timestamp.</returns>
    /// <exception cref="ArgumentNullException">If override is null.</exception>
    public Task<ClientPolicyOverride> SaveClientOverrideAsync(
        ClientPolicyOverride @override,
        CancellationToken ct = default);

    /// <summary>
    /// Deletes client policy override.
    /// After deletion, client will use process type defaults.
    /// </summary>
    /// <param name="clientId">Client identifier.</param>
    /// <param name="processType">Process type identifier.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>True if deleted, false if not found.</returns>
    public Task<bool> DeleteClientOverrideAsync(
        string clientId,
        string processType,
        CancellationToken ct = default);

    /// <summary>
    /// Lists all process type policies.
    /// Used by admin UI for policy management.
    /// Results not paginated (expected low cardinality: 10-100 process types).
    /// </summary>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>List of all process type policies.</returns>
    public Task<IReadOnlyList<ProcessTypePolicy>> ListProcessTypePoliciesAsync(
        CancellationToken ct = default);

    /// <summary>
    /// Lists all client overrides for a specific client.
    /// Used by client management UI to view custom SLA configurations.
    /// Results not paginated (expected low cardinality per client).
    /// </summary>
    /// <param name="clientId">Client identifier.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>List of client policy overrides.</returns>
    public Task<IReadOnlyList<ClientPolicyOverride>> ListClientOverridesAsync(
        string clientId,
        CancellationToken ct = default);

    /// <summary>
    /// Retrieves all process type policy defaults.
    /// Used by cache warming to preload all type defaults at startup.
    /// Returns complete list of all configured process type policies.
    /// </summary>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>List of all process type policies with metadata.</returns>
    public Task<IReadOnlyList<ProcessTypePolicy>> GetAllTypeDefaultsAsync(
        CancellationToken ct = default);

    /// <summary>
    /// Retrieves all client-specific policy overrides.
    /// Used by cache warming to preload all client overrides at startup.
    /// Returns complete list of all client override configurations.
    /// </summary>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>List of all client policy overrides.</returns>
    public Task<IReadOnlyList<ClientPolicyOverride>> GetAllClientOverridesAsync(
        CancellationToken ct = default);
}
