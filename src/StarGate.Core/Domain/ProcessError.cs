namespace StarGate.Core.Domain;

/// <summary>
/// Represents an error that occurred during process execution.
/// Immutable record providing structured error information for diagnostics and client feedback.
/// </summary>
/// <param name="Code">Error code for categorization (e.g., "TIMEOUT", "VALIDATION_ERROR", "BUSINESS_RULE_VIOLATION").</param>
/// <param name="Message">Human-readable error message describing what went wrong.</param>
/// <param name="Details">Additional error context as JSON document (stack traces, validation errors, etc.).</param>
public record ProcessError(
    string Code,
    string Message,
    System.Text.Json.JsonDocument? Details);
