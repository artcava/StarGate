using FluentValidation;
using StarGate.Core.Domain.Configuration;

namespace StarGate.Infrastructure.Validation;

/// <summary>
/// Validator for ProcessTypePolicy entities.
/// Ensures all policy fields are within acceptable operational bounds.
/// </summary>
public class ProcessTypePolicyValidator : AbstractValidator<ProcessTypePolicy>
{
    // Policy constraints - operational limits
    private static readonly TimeSpan _minTimeout = TimeSpan.FromSeconds(1);
    private static readonly TimeSpan _maxTimeout = TimeSpan.FromHours(1); // 3600 seconds
    private const int _minConcurrentProcesses = 1;
    private const int _maxConcurrentProcesses = 100;

    public ProcessTypePolicyValidator()
    {
        RuleFor(p => p.ProcessType)
            .NotEmpty()
            .WithErrorCode("PROCESS_TYPE_REQUIRED")
            .WithMessage("Process type is required")
            .MaximumLength(100)
            .WithErrorCode("PROCESS_TYPE_TOO_LONG")
            .WithMessage("Process type must not exceed 100 characters");

        RuleFor(p => p.Timeout)
            .Must(t => t >= _minTimeout && t <= _maxTimeout)
            .WithErrorCode("TIMEOUT_OUT_OF_RANGE")
            .WithMessage($"Timeout must be between {_minTimeout.TotalSeconds} and {_maxTimeout.TotalSeconds} seconds");

        RuleFor(p => p.RetryPolicy)
            .NotNull()
            .WithErrorCode("RETRY_POLICY_REQUIRED")
            .WithMessage("Retry policy is required");

        When(p => p.RetryPolicy != null, () =>
        {
            RuleFor(p => p.RetryPolicy.MaxAttempts)
                .InclusiveBetween(0, 10)
                .WithErrorCode("MAX_RETRY_OUT_OF_RANGE")
                .WithMessage("Max retry attempts must be between 0 and 10");
        });

        RuleFor(p => p.ResultRetention)
            .Must(r => r >= TimeSpan.Zero)
            .WithErrorCode("RESULT_RETENTION_NEGATIVE")
            .WithMessage("Result retention cannot be negative");

        When(p => p.MaxConcurrentProcesses.HasValue, () =>
        {
            RuleFor(p => p.MaxConcurrentProcesses!.Value)
                .InclusiveBetween(_minConcurrentProcesses, _maxConcurrentProcesses)
                .WithErrorCode("MAX_CONCURRENCY_OUT_OF_RANGE")
                .WithMessage($"Max concurrent processes must be between {_minConcurrentProcesses} and {_maxConcurrentProcesses}");
        });

        RuleFor(p => p.UpdatedAt)
            .NotEmpty()
            .WithErrorCode("UPDATED_AT_REQUIRED")
            .WithMessage("UpdatedAt timestamp is required");
    }
}
