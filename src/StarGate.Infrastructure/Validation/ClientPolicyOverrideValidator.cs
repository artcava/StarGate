using FluentValidation;
using StarGate.Core.Domain.Configuration;

namespace StarGate.Infrastructure.Validation;

/// <summary>
/// Validator for ClientPolicyOverride entities.
/// Validates client-specific policy overrides with conditional rules for optional fields.
/// </summary>
public class ClientPolicyOverrideValidator : AbstractValidator<ClientPolicyOverride>
{
    private static readonly TimeSpan _minTimeout = TimeSpan.FromSeconds(1);
    private static readonly TimeSpan _maxTimeout = TimeSpan.FromHours(1);
    private const int _minConcurrentProcesses = 1;
    private const int _maxConcurrentProcesses = 100;

    public ClientPolicyOverrideValidator()
    {
        RuleFor(o => o.ClientId)
            .NotEmpty()
            .WithErrorCode("CLIENT_ID_REQUIRED")
            .WithMessage("Client ID is required")
            .MaximumLength(100)
            .WithErrorCode("CLIENT_ID_TOO_LONG")
            .WithMessage("Client ID must not exceed 100 characters");

        RuleFor(o => o.ProcessType)
            .NotEmpty()
            .WithErrorCode("PROCESS_TYPE_REQUIRED")
            .WithMessage("Process type is required")
            .MaximumLength(100)
            .WithErrorCode("PROCESS_TYPE_TOO_LONG")
            .WithMessage("Process type must not exceed 100 characters");

        // Optional overrides - validate only if provided
        When(o => o.Timeout.HasValue, () =>
        {
            RuleFor(o => o.Timeout!.Value)
                .Must(t => t >= _minTimeout && t <= _maxTimeout)
                .WithErrorCode("TIMEOUT_OUT_OF_RANGE")
                .WithMessage($"Timeout must be between {_minTimeout.TotalSeconds} and {_maxTimeout.TotalSeconds} seconds");
        });

        When(o => o.RetryPolicy != null, () =>
        {
            RuleFor(o => o.RetryPolicy!.MaxAttempts)
                .InclusiveBetween(0, 10)
                .WithErrorCode("MAX_RETRY_OUT_OF_RANGE")
                .WithMessage("Max retry attempts must be between 0 and 10");
        });

        When(o => o.ResultRetention.HasValue, () =>
        {
            RuleFor(o => o.ResultRetention!.Value)
                .Must(r => r >= TimeSpan.Zero)
                .WithErrorCode("RESULT_RETENTION_NEGATIVE")
                .WithMessage("Result retention cannot be negative");
        });

        When(o => o.MaxConcurrentProcesses.HasValue, () =>
        {
            RuleFor(o => o.MaxConcurrentProcesses!.Value)
                .InclusiveBetween(_minConcurrentProcesses, _maxConcurrentProcesses)
                .WithErrorCode("MAX_CONCURRENCY_OUT_OF_RANGE")
                .WithMessage($"Max concurrent processes must be between {_minConcurrentProcesses} and {_maxConcurrentProcesses}");
        });

        RuleFor(o => o.UpdatedAt)
            .NotEmpty()
            .WithErrorCode("UPDATED_AT_REQUIRED")
            .WithMessage("UpdatedAt timestamp is required");
    }
}
