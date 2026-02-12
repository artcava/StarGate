using StarGate.Core.Domain.Configuration;
using StarGate.Infrastructure.Persistence.Documents;

namespace StarGate.Infrastructure.Persistence.Mappers;

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
            Timeout = document.Timeout,
            RetryPolicy = new RetryPolicy
            {
                Enabled = document.RetryPolicy.Enabled,
                MaxAttempts = document.RetryPolicy.MaxAttempts,
                InitialDelay = document.RetryPolicy.InitialDelay,
                BackoffStrategy = Enum.Parse<BackoffStrategy>(document.RetryPolicy.BackoffStrategy),
                MaxDelay = document.RetryPolicy.MaxDelay
            },
            ResultRetention = document.ResultRetention,
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
            Timeout = policy.Timeout,
            RetryPolicy = new RetryPolicyDocument
            {
                Enabled = policy.RetryPolicy.Enabled,
                MaxAttempts = policy.RetryPolicy.MaxAttempts,
                InitialDelay = policy.RetryPolicy.InitialDelay,
                BackoffStrategy = policy.RetryPolicy.BackoffStrategy.ToString(),
                MaxDelay = policy.RetryPolicy.MaxDelay
            },
            ResultRetention = policy.ResultRetention,
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
            Timeout = document.Timeout,
            ResultRetention = document.ResultRetention,
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
            Id = $"{override_.ClientId}:{override_.ProcessType}",
            ClientId = override_.ClientId,
            ProcessType = override_.ProcessType,
            Timeout = override_.Timeout,
            ResultRetention = override_.ResultRetention,
            MaxConcurrentProcesses = override_.MaxConcurrentProcesses,
            UpdatedAt = DateTime.SpecifyKind(override_.UpdatedAt, DateTimeKind.Utc)
        };
    }
}
