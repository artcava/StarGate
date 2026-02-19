namespace StarGate.Api.Validators;

using FluentValidation;
using StarGate.Contracts.Requests;

/// <summary>
/// Validator for CreateProcessRequest.
/// </summary>
public class CreateProcessRequestValidator : BaseValidator<CreateProcessRequest>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="CreateProcessRequestValidator"/> class.
    /// </summary>
    public CreateProcessRequestValidator()
    {
        RuleFor(x => x.ClientId)
            .NotEmpty()
            .WithMessage("ClientId is required")
            .MaximumLength(100)
            .WithMessage("ClientId must not exceed 100 characters")
            .Must(BeValidClientId)
            .WithMessage("ClientId contains invalid characters");

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
            .WithMessage("IdempotencyKey must not exceed 100 characters")
            .Matches("^[a-zA-Z0-9-_]+$")
            .WithMessage("IdempotencyKey must contain only alphanumeric characters, hyphens, and underscores");

        RuleFor(x => x.Metadata)
            .Must(metadata => metadata == null || metadata.Count <= 50)
            .WithMessage("Metadata cannot contain more than 50 entries")
            .Must(BeValidMetadata)
            .WithMessage("Metadata keys and values must not exceed 200 characters");
    }

    private static bool BeValidClientId(string clientId)
    {
        if (string.IsNullOrWhiteSpace(clientId))
            return false;

        // Allow alphanumeric, hyphens, underscores, and dots
        return System.Text.RegularExpressions.Regex.IsMatch(clientId, "^[a-zA-Z0-9-_.]+$");
    }

    private static bool BeValidMetadata(Dictionary<string, string>? metadata)
    {
        if (metadata == null)
            return true;

        return metadata.All(kvp =>
            !string.IsNullOrWhiteSpace(kvp.Key) &&
            kvp.Key.Length <= 200 &&
            (kvp.Value == null || kvp.Value.Length <= 200));
    }
}
