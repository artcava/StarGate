using FluentValidation;
using FluentValidation.Results;
using StarGate.Api.Filters;

namespace StarGate.Api.Extensions;

/// <summary>
/// Extension methods for validation.
/// </summary>
public static class ValidationExtensions
{
    /// <summary>
    /// Converts a ValidationResult to a dictionary of errors.
    /// </summary>
    /// <param name="validationResult">The validation result to convert.</param>
    /// <returns>A dictionary mapping property names to error message arrays.</returns>
    public static IDictionary<string, string[]> ToDictionary(this ValidationResult validationResult)
    {
        return validationResult.Errors
            .GroupBy(e => e.PropertyName)
            .ToDictionary(
                g => g.Key,
                g => g.Select(e => e.ErrorMessage).ToArray());
    }

    /// <summary>
    /// Adds a validation filter to the endpoint.
    /// </summary>
    /// <typeparam name="T">The type of request to validate.</typeparam>
    /// <param name="builder">The route handler builder.</param>
    /// <returns>The route handler builder with validation filter applied.</returns>
    public static RouteHandlerBuilder AddValidation<T>(
        this RouteHandlerBuilder builder) where T : class
    {
        return builder.AddEndpointFilter<ValidationFilter<T>>();
    }
}
