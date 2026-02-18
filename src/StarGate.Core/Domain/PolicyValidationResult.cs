namespace StarGate.Core.Domain;

/// <summary>
/// Result of policy validation.
/// Provides structured validation outcomes with detailed error information.
/// </summary>
public record PolicyValidationResult
{
    /// <summary>
    /// Indicates whether the validation was successful.
    /// </summary>
    public required bool IsValid { get; init; }

    /// <summary>
    /// Collection of validation errors. Empty when IsValid is true.
    /// </summary>
    public List<PolicyValidationError> Errors { get; init; } = new();

    /// <summary>
    /// Creates a successful validation result.
    /// </summary>
    /// <returns>A validation result indicating success.</returns>
    public static PolicyValidationResult Success() => new() { IsValid = true };

    /// <summary>
    /// Creates a failed validation result with specified errors.
    /// </summary>
    /// <param name="errors">Array of validation errors.</param>
    /// <returns>A validation result indicating failure with error details.</returns>
    public static PolicyValidationResult Failure(params PolicyValidationError[] errors) => new()
    {
        IsValid = false,
        Errors = new List<PolicyValidationError>(errors)
    };
}

/// <summary>
/// Represents a policy validation error.
/// Provides detailed information about what validation rule failed and why.
/// </summary>
public record PolicyValidationError
{
    /// <summary>
    /// Name of the property that failed validation.
    /// </summary>
    public required string PropertyName { get; init; }

    /// <summary>
    /// Machine-readable error code for categorization and handling.
    /// </summary>
    public required string ErrorCode { get; init; }

    /// <summary>
    /// Human-readable error message describing the validation failure.
    /// </summary>
    public required string ErrorMessage { get; init; }

    /// <summary>
    /// The value that was attempted to be set, causing the validation failure.
    /// Null if not applicable.
    /// </summary>
    public object? AttemptedValue { get; init; }
}
