namespace StarGate.Contracts.Responses;

/// <summary>
/// Response for validation errors following RFC 7807 Problem Details standard.
/// </summary>
public record ValidationErrorResponse
{
    /// <summary>
    /// Gets the URI reference that identifies the problem type.
    /// </summary>
    public required string Type { get; init; }

    /// <summary>
    /// Gets a short, human-readable summary of the problem type.
    /// </summary>
    public required string Title { get; init; }

    /// <summary>
    /// Gets the HTTP status code.
    /// </summary>
    public required int Status { get; init; }

    /// <summary>
    /// Gets the validation errors grouped by property name.
    /// </summary>
    public required Dictionary<string, string[]> Errors { get; init; }

    /// <summary>
    /// Gets the trace identifier for debugging purposes.
    /// </summary>
    public string? TraceId { get; init; }

    /// <summary>
    /// Creates a new ValidationErrorResponse with standard values.
    /// </summary>
    /// <param name="errors">The validation errors grouped by property name.</param>
    /// <param name="traceId">Optional trace identifier.</param>
    /// <returns>A new ValidationErrorResponse instance.</returns>
    public static ValidationErrorResponse Create(
        Dictionary<string, string[]> errors,
        string? traceId = null)
    {
        return new ValidationErrorResponse
        {
            Type = "https://tools.ietf.org/html/rfc7231#section-6.5.1",
            Title = "One or more validation errors occurred.",
            Status = 400,
            Errors = errors,
            TraceId = traceId
        };
    }
}
