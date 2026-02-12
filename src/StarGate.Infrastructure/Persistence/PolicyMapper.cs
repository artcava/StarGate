using MongoDB.Bson;
using StarGate.Core.Domain.Configuration;

namespace StarGate.Infrastructure.Persistence;

/// <summary>
/// Maps between domain Policy entities and MongoDB PolicyDocuments.
/// </summary>
public static class PolicyMapper
{
    // ProcessTypePolicy mapping

    /// <summary>
    /// Converts a domain ProcessTypePolicy to a MongoDB document.
    /// </summary>
    /// <exception cref="ArgumentNullException">Thrown when policy is null.</exception>
    public static ProcessTypePolicyDocument MapToDocument(ProcessTypePolicy policy)
    {
        ArgumentNullException.ThrowIfNull(policy);

        return new ProcessTypePolicyDocument
        {
            ProcessType = policy.ProcessType,
            Timeout = policy.Timeout,
            RetryPolicy = MapToDocument(policy.RetryPolicy),
            ResultRetention = policy.ResultRetention,
            MaxConcurrentProcesses = policy.MaxConcurrentProcesses,
            UpdatedAt = policy.UpdatedAt
        };
    }

    /// <summary>
    /// Converts a MongoDB ProcessTypePolicyDocument to a domain entity.
    /// </summary>
    /// <exception cref="ArgumentNullException">Thrown when document is null.</exception>
    public static ProcessTypePolicy MapToDomain(ProcessTypePolicyDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);

        return new ProcessTypePolicy
        {
            ProcessType = document.ProcessType,
            Timeout = document.Timeout,
            RetryPolicy = MapToDomain(document.RetryPolicy),
            ResultRetention = document.ResultRetention,
            MaxConcurrentProcesses = document.MaxConcurrentProcesses,
            UpdatedAt = document.UpdatedAt
        };
    }

    // ClientPolicyOverride mapping

    /// <summary>
    /// Converts a domain ClientPolicyOverride to a MongoDB document.
    /// Note: Id is a composite key (clientId:processType) managed by the repository.
    /// For new documents: repository will generate the composite key.
    /// For updates: repository must preserve the existing Id.
    /// </summary>
    /// <exception cref="ArgumentNullException">Thrown when clientOverride is null.</exception>
    public static ClientPolicyOverrideDocument MapToDocument(ClientPolicyOverride clientOverride)
    {
        ArgumentNullException.ThrowIfNull(clientOverride);

        return new ClientPolicyOverrideDocument
        {
            // Id is a composite key managed by the repository during save.
            // Placeholder empty string - will be set to "{clientId}:{processType}" by repository
            Id = string.Empty,
            ClientId = clientOverride.ClientId,
            ProcessType = clientOverride.ProcessType,
            Timeout = clientOverride.Timeout,
            RetryPolicy = clientOverride.RetryPolicy != null 
                ? MapToDocument(clientOverride.RetryPolicy) 
                : null,
            ResultRetention = clientOverride.ResultRetention,
            MaxConcurrentProcesses = clientOverride.MaxConcurrentProcesses,
            UpdatedAt = clientOverride.UpdatedAt
        };
    }

    /// <summary>
    /// Converts a MongoDB ClientPolicyOverrideDocument to a domain entity.
    /// </summary>
    /// <exception cref="ArgumentNullException">Thrown when document is null.</exception>
    public static ClientPolicyOverride MapToDomain(ClientPolicyOverrideDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);

        return new ClientPolicyOverride
        {
            ClientId = document.ClientId,
            ProcessType = document.ProcessType,
            Timeout = document.Timeout,
            RetryPolicy = document.RetryPolicy != null 
                ? MapToDomain(document.RetryPolicy) 
                : null,
            ResultRetention = document.ResultRetention,
            MaxConcurrentProcesses = document.MaxConcurrentProcesses,
            UpdatedAt = document.UpdatedAt
        };
    }

    // RetryPolicy mapping (private - internal use only)

    private static RetryPolicyDocument MapToDocument(RetryPolicy policy)
    {
        ArgumentNullException.ThrowIfNull(policy);

        return new RetryPolicyDocument
        {
            Enabled = policy.Enabled,
            MaxAttempts = policy.MaxAttempts,
            InitialDelay = policy.InitialDelay,
            BackoffStrategy = policy.BackoffStrategy.ToString(),
            MaxDelay = policy.MaxDelay
        };
    }

    private static RetryPolicy MapToDomain(RetryPolicyDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);

        if (!Enum.TryParse<BackoffStrategy>(document.BackoffStrategy, ignoreCase: true, out BackoffStrategy strategy))
        {
            throw new InvalidOperationException(
                $"Invalid BackoffStrategy value '{document.BackoffStrategy}'");
        }

        return new RetryPolicy
        {
            Enabled = document.Enabled,
            MaxAttempts = document.MaxAttempts,
            InitialDelay = document.InitialDelay,
            BackoffStrategy = strategy,
            MaxDelay = document.MaxDelay
        };
    }
}
