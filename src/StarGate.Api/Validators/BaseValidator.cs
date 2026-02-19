using FluentValidation;

namespace StarGate.Api.Validators;

/// <summary>
/// Base validator with common validation rules.
/// </summary>
/// <typeparam name="T">The type being validated.</typeparam>
public abstract class BaseValidator<T> : AbstractValidator<T>
{
    /// <summary>
    /// Validates that a string contains only alphanumeric characters and hyphens.
    /// </summary>
    /// <param name="ruleBuilder">The rule builder.</param>
    /// <returns>The rule builder options.</returns>
    protected IRuleBuilderOptions<T, string> MustBeAlphanumericWithHyphens(
        IRuleBuilder<T, string> ruleBuilder)
    {
        return ruleBuilder
            .Matches("^[a-z0-9-]+$")
            .WithMessage("{PropertyName} must contain only lowercase letters, numbers, and hyphens");
    }

    /// <summary>
    /// Validates that a GUID is not empty.
    /// </summary>
    /// <param name="ruleBuilder">The rule builder.</param>
    /// <returns>The rule builder options.</returns>
    protected IRuleBuilderOptions<T, Guid> MustNotBeEmptyGuid(
        IRuleBuilder<T, Guid> ruleBuilder)
    {
        return ruleBuilder
            .NotEqual(Guid.Empty)
            .WithMessage("{PropertyName} cannot be an empty GUID");
    }

    /// <summary>
    /// Validates that a dictionary has a maximum number of entries.
    /// </summary>
    /// <param name="ruleBuilder">The rule builder.</param>
    /// <param name="maxEntries">Maximum number of entries allowed.</param>
    /// <returns>The rule builder options.</returns>
    protected IRuleBuilderOptions<T, Dictionary<string, string>?> MustHaveMaxEntries(
        IRuleBuilder<T, Dictionary<string, string>?> ruleBuilder,
        int maxEntries)
    {
        return ruleBuilder
            .Must(dict => dict == null || dict.Count <= maxEntries)
            .WithMessage($"{{PropertyName}} cannot contain more than {maxEntries} entries");
    }
}
