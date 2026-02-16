using Microsoft.Extensions.Logging;
using StarGate.Core.Domain.Configuration;

namespace StarGate.Application.Services;

/// <summary>
/// Service for resolving and merging process policies.
/// Implements hierarchical policy resolution with client overrides taking precedence over type defaults.
/// Thread-safe and stateless for concurrent access.
/// </summary>
public class PolicyResolutionService
{
    private readonly ILogger<PolicyResolutionService> _logger;

    // Validation constants
    private const int _maxTimeoutSeconds = 86400; // 24 hours
    private const int _maxRetryAttempts = 10;
    private const int _maxRetryDelaySeconds = 3600; // 1 hour
    private const int _maxRetentionDays = 365;
    private const int _maxConcurrentExecutions = 1000;

    public PolicyResolutionService(ILogger<PolicyResolutionService> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Resolves the effective policy by merging type default with client override.
    /// Client override values take precedence over type defaults.
    /// </summary>
    /// <param name="typeDefault">Base policy for the process type.</param>
    /// <param name="clientOverride">Optional client-specific overrides.</param>
    /// <returns>Resolved policy with merged values.</returns>
    public ProcessTypePolicy ResolvePolicy(
        ProcessTypePolicy typeDefault,
        ClientPolicyOverride? clientOverride)
    {
        ArgumentNullException.ThrowIfNull(typeDefault);

        if (clientOverride == null)
        {
            _logger.LogDebug(
                "No client override provided for ProcessType={ProcessType}, using type default",
                typeDefault.ProcessType);
            return typeDefault;
        }

        _logger.LogDebug(
            "Resolving policy: ProcessType={ProcessType}, ClientId={ClientId}",
            typeDefault.ProcessType,
            clientOverride.ClientId);

        var resolved = typeDefault with
        {
            // Apply overrides with null-coalescing (client override ?? type default)
            Timeout = clientOverride.Timeout ?? typeDefault.Timeout,
            RetryPolicy = clientOverride.RetryPolicy ?? typeDefault.RetryPolicy,
            ResultRetention = clientOverride.ResultRetention ?? typeDefault.ResultRetention,
            MaxConcurrentProcesses = clientOverride.MaxConcurrentProcesses ?? typeDefault.MaxConcurrentProcesses,
            UpdatedAt = DateTime.UtcNow
        };

        LogPolicyResolution(typeDefault, clientOverride, resolved);

        return resolved;
    }

    /// <summary>
    /// Validates that a policy has valid values.
    /// </summary>
    /// <param name="policy">Policy to validate.</param>
    /// <returns>Validation result with any errors found.</returns>
    public PolicyValidationResult ValidatePolicy(ProcessTypePolicy policy)
    {
        ArgumentNullException.ThrowIfNull(policy);

        var errors = new List<string>();

        if (string.IsNullOrWhiteSpace(policy.ProcessType))
        {
            errors.Add("ProcessType cannot be null or empty");
        }

        // Validate Timeout
        if (policy.Timeout <= TimeSpan.Zero)
        {
            errors.Add($"Timeout must be positive (value: {policy.Timeout})");
        }

        if (policy.Timeout.TotalSeconds > _maxTimeoutSeconds)
        {
            errors.Add($"Timeout cannot exceed 24 hours (value: {policy.Timeout})");
        }

        // Validate RetryPolicy
        if (policy.RetryPolicy != null)
        {
            var retryErrors = ValidateRetryPolicy(policy.RetryPolicy);
            errors.AddRange(retryErrors);
        }

        // Validate ResultRetention
        if (policy.ResultRetention <= TimeSpan.Zero)
        {
            errors.Add($"ResultRetention must be positive (value: {policy.ResultRetention})");
        }

        if (policy.ResultRetention.TotalDays > _maxRetentionDays)
        {
            errors.Add($"ResultRetention cannot exceed 365 days (value: {policy.ResultRetention})");
        }

        // Validate MaxConcurrentProcesses
        if (policy.MaxConcurrentProcesses.HasValue)
        {
            if (policy.MaxConcurrentProcesses.Value <= 0)
            {
                errors.Add($"MaxConcurrentProcesses must be positive (value: {policy.MaxConcurrentProcesses})");
            }

            if (policy.MaxConcurrentProcesses.Value > _maxConcurrentExecutions)
            {
                errors.Add($"MaxConcurrentProcesses cannot exceed {_maxConcurrentExecutions} (value: {policy.MaxConcurrentProcesses})");
            }
        }

        var isValid = errors.Count == 0;

        if (!isValid)
        {
            _logger.LogWarning(
                "Policy validation failed for ProcessType={ProcessType}: {Errors}",
                policy.ProcessType,
                string.Join(", ", errors));
        }

        return new PolicyValidationResult
        {
            IsValid = isValid,
            Errors = errors
        };
    }

    /// <summary>
    /// Validates a client policy override.
    /// Only validates fields that are actually overridden (non-null).
    /// </summary>
    /// <param name="clientOverride">Client override to validate.</param>
    /// <returns>Validation result with any errors found.</returns>
    public PolicyValidationResult ValidateClientOverride(ClientPolicyOverride clientOverride)
    {
        ArgumentNullException.ThrowIfNull(clientOverride);

        var errors = new List<string>();

        if (string.IsNullOrWhiteSpace(clientOverride.ClientId))
        {
            errors.Add("ClientId cannot be null or empty");
        }

        if (string.IsNullOrWhiteSpace(clientOverride.ProcessType))
        {
            errors.Add("ProcessType cannot be null or empty");
        }

        // Validate individual override values if provided
        if (clientOverride.Timeout.HasValue)
        {
            if (clientOverride.Timeout.Value <= TimeSpan.Zero)
            {
                errors.Add($"Timeout must be positive (value: {clientOverride.Timeout})");
            }

            if (clientOverride.Timeout.Value.TotalSeconds > _maxTimeoutSeconds)
            {
                errors.Add($"Timeout cannot exceed 24 hours (value: {clientOverride.Timeout})");
            }
        }

        if (clientOverride.RetryPolicy != null)
        {
            var retryErrors = ValidateRetryPolicy(clientOverride.RetryPolicy);
            errors.AddRange(retryErrors);
        }

        if (clientOverride.ResultRetention.HasValue)
        {
            if (clientOverride.ResultRetention.Value <= TimeSpan.Zero)
            {
                errors.Add($"ResultRetention must be positive (value: {clientOverride.ResultRetention})");
            }

            if (clientOverride.ResultRetention.Value.TotalDays > _maxRetentionDays)
            {
                errors.Add($"ResultRetention cannot exceed 365 days (value: {clientOverride.ResultRetention})");
            }
        }

        if (clientOverride.MaxConcurrentProcesses.HasValue)
        {
            if (clientOverride.MaxConcurrentProcesses.Value <= 0)
            {
                errors.Add($"MaxConcurrentProcesses must be positive (value: {clientOverride.MaxConcurrentProcesses})");
            }

            if (clientOverride.MaxConcurrentProcesses.Value > _maxConcurrentExecutions)
            {
                errors.Add($"MaxConcurrentProcesses cannot exceed {_maxConcurrentExecutions} (value: {clientOverride.MaxConcurrentProcesses})");
            }
        }

        var isValid = errors.Count == 0;

        if (!isValid)
        {
            _logger.LogWarning(
                "Client override validation failed for ClientId={ClientId}, ProcessType={ProcessType}: {Errors}",
                clientOverride.ClientId,
                clientOverride.ProcessType,
                string.Join(", ", errors));
        }

        return new PolicyValidationResult
        {
            IsValid = isValid,
            Errors = errors
        };
    }

    /// <summary>
    /// Compares two policies and returns the differences.
    /// </summary>
    /// <param name="baseline">Baseline policy for comparison.</param>
    /// <param name="comparison">Policy to compare against baseline.</param>
    /// <returns>Policy difference result with detailed change descriptions.</returns>
    public PolicyDifference ComparePolicies(
        ProcessTypePolicy baseline,
        ProcessTypePolicy comparison)
    {
        ArgumentNullException.ThrowIfNull(baseline);
        ArgumentNullException.ThrowIfNull(comparison);

        var differences = new List<string>();

        if (baseline.Timeout != comparison.Timeout)
        {
            differences.Add($"Timeout: {baseline.Timeout} -> {comparison.Timeout}");
        }

        if (!AreRetryPoliciesEqual(baseline.RetryPolicy, comparison.RetryPolicy))
        {
            differences.Add($"RetryPolicy: {FormatRetryPolicy(baseline.RetryPolicy)} -> {FormatRetryPolicy(comparison.RetryPolicy)}");
        }

        if (baseline.ResultRetention != comparison.ResultRetention)
        {
            differences.Add($"ResultRetention: {baseline.ResultRetention} -> {comparison.ResultRetention}");
        }

        if (baseline.MaxConcurrentProcesses != comparison.MaxConcurrentProcesses)
        {
            differences.Add($"MaxConcurrentProcesses: {baseline.MaxConcurrentProcesses?.ToString() ?? "null"} -> {comparison.MaxConcurrentProcesses?.ToString() ?? "null"}");
        }

        return new PolicyDifference
        {
            HasDifferences = differences.Count > 0,
            Differences = differences
        };
    }

    /// <summary>
    /// Checks if a client override actually changes any values from the type default.
    /// </summary>
    /// <param name="typeDefault">Type default policy.</param>
    /// <param name="clientOverride">Client override to check.</param>
    /// <returns>True if the override contains meaningful changes.</returns>
    public bool HasEffectiveOverride(
        ProcessTypePolicy typeDefault,
        ClientPolicyOverride clientOverride)
    {
        ArgumentNullException.ThrowIfNull(typeDefault);
        ArgumentNullException.ThrowIfNull(clientOverride);

        return (clientOverride.Timeout.HasValue && clientOverride.Timeout.Value != typeDefault.Timeout) ||
               (clientOverride.RetryPolicy != null && !AreRetryPoliciesEqual(clientOverride.RetryPolicy, typeDefault.RetryPolicy)) ||
               (clientOverride.ResultRetention.HasValue && clientOverride.ResultRetention.Value != typeDefault.ResultRetention) ||
               (clientOverride.MaxConcurrentProcesses.HasValue && clientOverride.MaxConcurrentProcesses.Value != typeDefault.MaxConcurrentProcesses);
    }

    /// <summary>
    /// Validates a retry policy configuration.
    /// </summary>
    private List<string> ValidateRetryPolicy(RetryPolicy retryPolicy)
    {
        var errors = new List<string>();

        if (retryPolicy.MaxAttempts < 0)
        {
            errors.Add($"RetryPolicy.MaxAttempts cannot be negative (value: {retryPolicy.MaxAttempts})");
        }

        if (retryPolicy.MaxAttempts > _maxRetryAttempts)
        {
            errors.Add($"RetryPolicy.MaxAttempts cannot exceed {_maxRetryAttempts} (value: {retryPolicy.MaxAttempts})");
        }

        if (retryPolicy.InitialDelay < TimeSpan.Zero)
        {
            errors.Add($"RetryPolicy.InitialDelay cannot be negative (value: {retryPolicy.InitialDelay})");
        }

        if (retryPolicy.InitialDelay.TotalSeconds > _maxRetryDelaySeconds)
        {
            errors.Add($"RetryPolicy.InitialDelay cannot exceed 1 hour (value: {retryPolicy.InitialDelay})");
        }

        if (retryPolicy.MaxDelay.HasValue)
        {
            if (retryPolicy.MaxDelay.Value < TimeSpan.Zero)
            {
                errors.Add($"RetryPolicy.MaxDelay cannot be negative (value: {retryPolicy.MaxDelay})");
            }

            if (retryPolicy.MaxDelay.Value < retryPolicy.InitialDelay)
            {
                errors.Add($"RetryPolicy.MaxDelay ({retryPolicy.MaxDelay}) cannot be less than InitialDelay ({retryPolicy.InitialDelay})");
            }
        }

        return errors;
    }

    /// <summary>
    /// Compares two retry policies for equality.
    /// </summary>
    private bool AreRetryPoliciesEqual(RetryPolicy? policy1, RetryPolicy? policy2)
    {
        if (policy1 == null && policy2 == null)
        {
            return true;
        }

        if (policy1 == null || policy2 == null)
        {
            return false;
        }

        return policy1.Enabled == policy2.Enabled &&
               policy1.MaxAttempts == policy2.MaxAttempts &&
               policy1.InitialDelay == policy2.InitialDelay &&
               policy1.BackoffStrategy == policy2.BackoffStrategy &&
               policy1.MaxDelay == policy2.MaxDelay;
    }

    /// <summary>
    /// Formats a retry policy for display.
    /// </summary>
    private string FormatRetryPolicy(RetryPolicy? policy)
    {
        if (policy == null)
        {
            return "null";
        }

        return $"{{Enabled={policy.Enabled}, MaxAttempts={policy.MaxAttempts}, " +
               $"InitialDelay={policy.InitialDelay}, BackoffStrategy={policy.BackoffStrategy}, " +
               $"MaxDelay={policy.MaxDelay?.ToString() ?? "null"}}}";
    }

    /// <summary>
    /// Logs detailed information about policy resolution.
    /// </summary>
    private void LogPolicyResolution(
        ProcessTypePolicy typeDefault,
        ClientPolicyOverride clientOverride,
        ProcessTypePolicy resolved)
    {
        var overrides = new List<string>();

        if (clientOverride.Timeout.HasValue)
        {
            overrides.Add($"Timeout: {typeDefault.Timeout} -> {resolved.Timeout}");
        }

        if (clientOverride.RetryPolicy != null)
        {
            overrides.Add($"RetryPolicy: {FormatRetryPolicy(typeDefault.RetryPolicy)} -> {FormatRetryPolicy(resolved.RetryPolicy)}");
        }

        if (clientOverride.ResultRetention.HasValue)
        {
            overrides.Add($"ResultRetention: {typeDefault.ResultRetention} -> {resolved.ResultRetention}");
        }

        if (clientOverride.MaxConcurrentProcesses.HasValue)
        {
            overrides.Add($"MaxConcurrentProcesses: {typeDefault.MaxConcurrentProcesses?.ToString() ?? "null"} -> {resolved.MaxConcurrentProcesses?.ToString() ?? "null"}");
        }

        if (overrides.Count > 0)
        {
            _logger.LogInformation(
                "Policy overrides applied for ClientId={ClientId}, ProcessType={ProcessType}: {Overrides}",
                clientOverride.ClientId,
                typeDefault.ProcessType,
                string.Join("; ", overrides));
        }
        else
        {
            _logger.LogDebug(
                "No effective overrides for ClientId={ClientId}, ProcessType={ProcessType}",
                clientOverride.ClientId,
                typeDefault.ProcessType);
        }
    }
}
