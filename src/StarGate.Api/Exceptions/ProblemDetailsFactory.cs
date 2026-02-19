using Microsoft.AspNetCore.Mvc;
using StarGate.Core.Exceptions;

namespace StarGate.Api.Exceptions;

/// <summary>
/// Factory for creating Problem Details responses.
/// </summary>
public static class ProblemDetailsFactory
{
    /// <summary>
    /// Creates a ProblemDetails response for an exception.
    /// </summary>
    public static ProblemDetails CreateProblemDetails(
        Exception exception,
        HttpContext httpContext,
        bool includeDetails = false)
    {
        var (statusCode, title, detail) = MapException(exception, includeDetails);

        return new ProblemDetails
        {
            Type = GetTypeUri(statusCode),
            Title = title,
            Status = statusCode,
            Detail = detail,
            Instance = httpContext.Request.Path,
            Extensions =
            {
                ["traceId"] = httpContext.TraceIdentifier,
                ["timestamp"] = DateTime.UtcNow
            }
        };
    }

    private static (int statusCode, string title, string detail) MapException(
        Exception exception,
        bool includeDetails)
    {
        return exception switch
        {
            ProcessNotFoundException notFound => (
                StatusCodes.Status404NotFound,
                "Process Not Found",
                notFound.Message),

            DuplicateProcessException duplicate => (
                StatusCodes.Status409Conflict,
                "Duplicate Process",
                duplicate.Message),

            PolicyViolationException policyViolation => (
                StatusCodes.Status429TooManyRequests,
                "Policy Violation",
                policyViolation.Message),

            FluentValidation.ValidationException validation => (
                StatusCodes.Status400BadRequest,
                "Validation Error",
                validation.Message),

            DomainException domain => (
                StatusCodes.Status400BadRequest,
                "Domain Error",
                domain.Message),

            ArgumentException argument => (
                StatusCodes.Status400BadRequest,
                "Invalid Argument",
                includeDetails ? argument.Message : "One or more arguments are invalid"),

            OperationCanceledException => (
                StatusCodes.Status499ClientClosedRequest,
                "Request Cancelled",
                "The request was cancelled by the client"),

            TimeoutException => (
                StatusCodes.Status408RequestTimeout,
                "Request Timeout",
                "The request timed out"),

            _ => (
                StatusCodes.Status500InternalServerError,
                "Internal Server Error",
                includeDetails
                    ? exception.Message
                    : "An unexpected error occurred. Please try again later.")
        };
    }

    private static string GetTypeUri(int statusCode) => statusCode switch
    {
        400 => "https://tools.ietf.org/html/rfc7231#section-6.5.1",
        404 => "https://tools.ietf.org/html/rfc7231#section-6.5.4",
        408 => "https://tools.ietf.org/html/rfc7231#section-6.5.7",
        409 => "https://tools.ietf.org/html/rfc7231#section-6.5.8",
        429 => "https://tools.ietf.org/html/rfc6585#section-4",
        499 => "https://httpstatuses.com/499",
        500 => "https://tools.ietf.org/html/rfc7231#section-6.6.1",
        _ => "https://tools.ietf.org/html/rfc7231"
    };
}
