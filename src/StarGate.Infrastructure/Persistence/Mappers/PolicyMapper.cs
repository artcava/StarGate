namespace StarGate.Infrastructure.Persistence.Mappers;

using StarGate.Core.Domain.Configuration;
using StarGate.Infrastructure.Persistence.Documents;

/// <summary>
/// Maps policy domain entities to/from MongoDB documents.
/// </summary>
public static class PolicyMapper
{
    /// <summary>
    /// Maps ProcessTypePolicyDocument to ProcessTypePolicy domain entity.
    /// </summary>
    public static ProcessTypePolicy MapToDomain(ProcessTypePolicyDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);

        return new ProcessTypePolicy
        {
            ProcessType = document.ProcessType,
            Timeout = TimeSpan.FromSeconds(document.TimeoutSeconds),
            RetryPolicy = new RetryPolicy
            {
                Enabled = document.RetryEnabled,
                MaxAttempts = document.RetryMaxAttempts,
                InitialDelay = TimeSpan.FromSeconds(document.RetryInitialDelaySeconds),
                BackoffStrategy = ParseBackoffStrategy(document.RetryBackoffStrategy),
                MaxDelay = TimeSpan.FromSeconds(document.RetryMaxDelaySeconds)
            },
            ResultRetention = TimeSpan.FromDays(document.ResultRetentionDays),
            MaxConcurrentProcesses = document.MaxConcurrentProcesses,
            UpdatedAt = DateTime.SpecifyKind(document.UpdatedAt, DateTimeKind.Utc)
        };
    }

    /// <summary>
    /// Maps ProcessTypePolicy domain entity to ProcessTypePolicyDocument.
    /// </summary>
    public static ProcessTypePolicyDocument MapToDocument(ProcessTypePolicy policy)
    {
        ArgumentNullException.ThrowIfNull(policy);

        return new ProcessTypePolicyDocument
        {
            ProcessType = policy.ProcessType,
            TimeoutSeconds = (int)policy.Timeout.TotalSeconds,
            RetryEnabled = policy.RetryPolicy.Enabled,
            RetryMaxAttempts = policy.RetryPolicy.MaxAttempts,
            RetryInitialDelaySeconds = (int)policy.RetryPolicy.InitialDelay.TotalSeconds,
            RetryBackoffStrategy = policy.RetryPolicy.BackoffStrategy.ToString(),
            RetryMaxDelaySeconds = (int)policy.RetryPolicy.MaxDelay.TotalSeconds,
            ResultRetentionDays = (int)policy.ResultRetention.TotalDays,
            MaxConcurrentProcesses = policy.MaxConcurrentProcesses,
            UpdatedAt = DateTime.SpecifyKind(policy.UpdatedAt, DateTimeKind.Utc)
        };
    }

    /// <summary>
    /// Maps ClientPolicyOverrideDocument to ClientPolicyOverride domain entity.
    /// </summary>
    public static ClientPolicyOverride MapToDomain(ClientPolicyOverrideDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);

        return new ClientPolicyOverride
        {
            ClientId = document.ClientId,
            ProcessType = document.ProcessType,
            Timeout = document.TimeoutSeconds.HasValue
                ? TimeSpan.FromSeconds(document.TimeoutSeconds.Value)
                : null,
            ResultRetention = document.ResultRetentionDays.HasValue
                ? TimeSpan.FromDays(document.ResultRetentionDays.Value)
                : null,
            MaxConcurrentProcesses = document.MaxConcurrentProcesses,
            UpdatedAt = DateTime.SpecifyKind(document.UpdatedAt, DateTimeKind.Utc)
        };
    }

    /// <summary>
    /// Maps ClientPolicyOverride domain entity to ClientPolicyOverrideDocument.
    /// </summary>
    public static ClientPolicyOverrideDocument MapToDocument(ClientPolicyOverride override_)
    {
        ArgumentNullException.ThrowIfNull(override_);

        return new ClientPolicyOverrideDocument
        {
            ClientId = override_.ClientId,
            ProcessType = override_.ProcessType,
            TimeoutSeconds = override_.Timeout.HasValue
                ? (int)override_.Timeout.Value.TotalSeconds
                : null,
            ResultRetentionDays = override_.ResultRetention.HasValue
                ? (int)override_.ResultRetention.Value.TotalDays
                : null,
            MaxConcurrentProcesses = override_.MaxConcurrentProcesses,
            UpdatedAt = DateTime.SpecifyKind(override_.UpdatedAt, DateTimeKind.Utc)
        };
    }

    private static BackoffStrategy ParseBackoffStrategy(string strategy)
    {
        return strategy switch
        {
            "Linear" => BackoffStrategy.Linear,
            "Exponential" => BackoffStrategy.Exponential,
            "Constant" => BackoffStrategy.Constant,
            _ => throw new ArgumentException($"Unknown backoff strategy: {strategy}", nameof(strategy))
        };
    }
}
