namespace StarGate.Api.Filters;

using FluentValidation;
using Microsoft.AspNetCore.Http;

/// <summary>
/// Filter that automatically validates requests using FluentValidation.
/// </summary>
/// <typeparam name="T">The type of request to validate.</typeparam>
public class ValidationFilter<T> : IEndpointFilter where T : class
{
    private readonly IValidator<T> _validator;

    /// <summary>
    /// Initializes a new instance of the <see cref="ValidationFilter{T}"/> class.
    /// </summary>
    /// <param name="validator">The validator for the request type.</param>
    /// <exception cref="ArgumentNullException">Thrown when validator is null.</exception>
    public ValidationFilter(IValidator<T> validator)
    {
        _validator = validator ?? throw new ArgumentNullException(nameof(validator));
    }

    /// <summary>
    /// Invokes the filter to validate the request.
    /// </summary>
    /// <param name="context">The endpoint filter invocation context.</param>
    /// <param name="next">The next filter or endpoint handler.</param>
    /// <returns>The result of validation or the next handler.</returns>
    public async ValueTask<object?> InvokeAsync(
        EndpointFilterInvocationContext context,
        EndpointFilterDelegate next)
    {
        var request = context.Arguments
            .OfType<T>()
            .FirstOrDefault();

        if (request == null)
        {
            return Results.BadRequest(new
            {
                errors = new[] { "Request body is required" }
            });
        }

        var validationResult = await _validator.ValidateAsync(request);

        if (!validationResult.IsValid)
        {
            var errors = validationResult.Errors
                .GroupBy(e => e.PropertyName)
                .ToDictionary(
                    g => g.Key,
                    g => g.Select(e => e.ErrorMessage).ToArray());

            return Results.ValidationProblem(errors);
        }

        return await next(context);
    }
}
