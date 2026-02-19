using FluentValidation;
using StarGate.Contracts.Requests;

namespace StarGate.Api.Validators;

/// <summary>
/// Validator for CreateProcessRequest.
/// </summary>
public class CreateProcessRequestValidator : AbstractValidator<CreateProcessRequest>
{
    public CreateProcessRequestValidator()
    {
        RuleFor(x => x.ClientId)
            .NotEmpty()
            .WithMessage("ClientId is required")
            .MaximumLength(100)
            .WithMessage("ClientId must not exceed 100 characters");

        RuleFor(x => x.ProcessType)
            .NotEmpty()
            .WithMessage("ProcessType is required")
            .MaximumLength(100)
            .WithMessage("ProcessType must not exceed 100 characters")
            .Matches("^[a-z0-9-]+$")
            .WithMessage("ProcessType must contain only lowercase letters, numbers, and hyphens");

        RuleFor(x => x.ClientProcessId)
            .NotEmpty()
            .WithMessage("ClientProcessId is required")
            .MaximumLength(200)
            .WithMessage("ClientProcessId must not exceed 200 characters");

        RuleFor(x => x.IdempotencyKey)
            .NotEmpty()
            .WithMessage("IdempotencyKey is required")
            .MaximumLength(100)
            .WithMessage("IdempotencyKey must not exceed 100 characters");

        RuleFor(x => x.Metadata)
            .Must(metadata => metadata == null || metadata.Count <= 50)
            .WithMessage("Metadata cannot contain more than 50 entries");
    }
}
