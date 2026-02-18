namespace StarGate.Infrastructure.Validation;

using FluentValidation;
using StarGate.Core.Domain.Configuration;

/// <summary>
/// Validator for ProcessTypePolicy entities.
/// Ensures all policy fields are within acceptable operational bounds.
/// </summary>
public class ProcessTypePolicyValidator : AbstractValidator<ProcessTypePolicy>
{
    // Policy constraints - operational limits
    private static readonly TimeSpan MinTimeout = TimeSpan.FromSeconds(1);
    private static readonly TimeSpan MaxTimeout = TimeSpan.FromHours(1); // 3600 seconds
    private const int MinConcurrentProcesses = 1;
    private const int MaxConcurrentProcesses = 100;

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
            .Must(t => t >= MinTimeout && t <= MaxTimeout)
            .WithErrorCode("TIMEOUT_OUT_OF_RANGE")
            .WithMessage($"Timeout must be between {MinTimeout.TotalSeconds} and {MaxTimeout.TotalSeconds} seconds");

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
                .InclusiveBetween(MinConcurrentProcesses, MaxConcurrentProcesses)
                .WithErrorCode("MAX_CONCURRENCY_OUT_OF_RANGE")
                .WithMessage($"Max concurrent processes must be between {MinConcurrentProcesses} and {MaxConcurrentProcesses}");
        });

        RuleFor(p => p.UpdatedAt)
            .NotEmpty()
            .WithErrorCode("UPDATED_AT_REQUIRED")
            .WithMessage("UpdatedAt timestamp is required");
    }
}
