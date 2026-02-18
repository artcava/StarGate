using FluentValidation;
using Microsoft.Extensions.Logging;
using StarGate.Core.Domain;
using StarGate.Core.Domain.Configuration;

namespace StarGate.Infrastructure.Services;

/// <summary>
/// Service for validating policy configurations.
/// Provides three-level validation: type policies, client overrides, and resolved policies.
/// </summary>
public interface IPolicyValidator
{
    /// <summary>
    /// Validates a process type policy.
    /// </summary>
    /// <param name="policy">The policy to validate.</param>
    /// <returns>Validation result with detailed error information if validation fails.</returns>
    public PolicyValidationResult ValidateTypePolicy(ProcessTypePolicy policy);

    /// <summary>
    /// Validates a client policy override.
    /// </summary>
    /// <param name="clientOverride">The client override to validate.</param>
    /// <returns>Validation result with detailed error information if validation fails.</returns>
    public PolicyValidationResult ValidateClientOverride(ClientPolicyOverride clientOverride);
}

/// <summary>
/// Implementation of policy validation service.
/// Uses FluentValidation for declarative validation rules.
/// </summary>
public class PolicyValidator : IPolicyValidator
{
    private readonly IValidator<ProcessTypePolicy> _typePolicyValidator;
    private readonly IValidator<ClientPolicyOverride> _clientOverrideValidator;
    private readonly ILogger<PolicyValidator> _logger;

    public PolicyValidator(
        IValidator<ProcessTypePolicy> typePolicyValidator,
        IValidator<ClientPolicyOverride> clientOverrideValidator,
        ILogger<PolicyValidator> logger)
    {
        _typePolicyValidator = typePolicyValidator ?? throw new ArgumentNullException(nameof(typePolicyValidator));
        _clientOverrideValidator = clientOverrideValidator ?? throw new ArgumentNullException(nameof(clientOverrideValidator));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc />
    public PolicyValidationResult ValidateTypePolicy(ProcessTypePolicy policy)
    {
        ArgumentNullException.ThrowIfNull(policy);

        var validationResult = _typePolicyValidator.Validate(policy);

        if (validationResult.IsValid)
        {
            return PolicyValidationResult.Success();
        }

        var errors = validationResult.Errors.Select(e => new PolicyValidationError
        {
            PropertyName = e.PropertyName,
            ErrorCode = e.ErrorCode,
            ErrorMessage = e.ErrorMessage,
            AttemptedValue = e.AttemptedValue
        }).ToList();

        _logger.LogWarning(
            "ProcessTypePolicy validation failed for {ProcessType}: {Errors}",
            policy.ProcessType,
            string.Join(", ", errors.Select(e => $"{e.PropertyName}: {e.ErrorMessage}")));

        return new PolicyValidationResult
        {
            IsValid = false,
            Errors = errors
        };
    }

    /// <inheritdoc />
    public PolicyValidationResult ValidateClientOverride(ClientPolicyOverride clientOverride)
    {
        ArgumentNullException.ThrowIfNull(clientOverride);

        var validationResult = _clientOverrideValidator.Validate(clientOverride);

        if (validationResult.IsValid)
        {
            return PolicyValidationResult.Success();
        }

        var errors = validationResult.Errors.Select(e => new PolicyValidationError
        {
            PropertyName = e.PropertyName,
            ErrorCode = e.ErrorCode,
            ErrorMessage = e.ErrorMessage,
            AttemptedValue = e.AttemptedValue
        }).ToList();

        _logger.LogWarning(
            "ClientPolicyOverride validation failed for {ClientId}/{ProcessType}: {Errors}",
            clientOverride.ClientId,
            clientOverride.ProcessType,
            string.Join(", ", errors.Select(e => $"{e.PropertyName}: {e.ErrorMessage}")));

        return new PolicyValidationResult
        {
            IsValid = false,
            Errors = errors
        };
    }
}
