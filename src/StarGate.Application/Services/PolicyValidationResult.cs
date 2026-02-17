namespace StarGate.Application.Services;

/// <summary>
/// Result of policy validation.
/// Encapsulates validation outcome and error messages for policy configurations.
/// </summary>
public class PolicyValidationResult
{
    /// <summary>
    /// Indicates whether the policy is valid.
    /// </summary>
    public required bool IsValid { get; init; }

    /// <summary>
    /// List of validation errors.
    /// Empty when IsValid is true.
    /// </summary>
    public required List<string> Errors { get; init; }

    /// <summary>
    /// Gets a formatted error message.
    /// </summary>
    /// <returns>Semicolon-separated list of errors, or empty string if valid.</returns>
    public string GetErrorMessage() =>
        Errors.Count > 0
            ? string.Join("; ", Errors)
            : string.Empty;
}
