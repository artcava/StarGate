using StarGate.Core.Domain;

namespace StarGate.Core.Abstractions;

/// <summary>
/// Handler for executing specific process types.
/// Each process type implements its own business logic through this interface.
/// Strategy pattern: different handlers for different process types.
/// </summary>
public interface IProcessHandler
{
    /// <summary>
    /// Process type this handler is responsible for.
    /// Used by factory to match handler to process.
    /// Must be unique across all handlers.
    /// </summary>
    public string ProcessType { get; }

    /// <summary>
    /// Executes the process business logic.
    /// Called by worker after process is dequeued from message broker.
    /// Handler should update progress via IProcessService if long-running.
    /// </summary>
    /// <param name="process">Process to execute with payload and metadata.</param>
    /// <param name="ct">Cancellation token for timeout and cancellation.</param>
    /// <returns>Process result object (will be serialized).</returns>
    /// <exception cref="InvalidOperationException">When execution fails due to business logic error.</exception>
    /// <exception cref="ArgumentNullException">If process is null.</exception>
    public Task<object> ExecuteAsync(Process process, CancellationToken ct);

    /// <summary>
    /// Validates process data before execution.
    /// Called before ExecuteAsync to ensure data integrity.
    /// If validation fails, process is rejected without execution.
    /// </summary>
    /// <param name="process">Process to validate.</param>
    /// <returns>Validation result with errors if any.</returns>
    /// <exception cref="ArgumentNullException">If process is null.</exception>
    public Task<ValidationResult> ValidateAsync(Process process);

    /// <summary>
    /// Estimates execution duration for this process.
    /// Used for timeout calculation and user expectations.
    /// Should return conservative estimate (better to overestimate).
    /// </summary>
    /// <param name="process">Process to estimate.</param>
    /// <returns>Estimated duration.</returns>
    /// <exception cref="ArgumentNullException">If process is null.</exception>
    public Task<TimeSpan> EstimateExecutionTimeAsync(Process process);
}

/// <summary>
/// Result of process data validation.
/// Immutable record with factory methods for convenience.
/// </summary>
public record ValidationResult
{
    /// <summary>
    /// Indicates whether validation passed.
    /// True if no errors, false otherwise.
    /// </summary>
    public required bool IsValid { get; init; }

    /// <summary>
    /// Collection of validation errors.
    /// Null if validation passed, non-empty if failed.
    /// </summary>
    public IReadOnlyList<ValidationError>? Errors { get; init; }

    /// <summary>
    /// Creates a successful validation result.
    /// </summary>
    /// <returns>Validation result with IsValid = true.</returns>
    public static ValidationResult Success() => new() { IsValid = true, Errors = null };

    /// <summary>
    /// Creates a failed validation result with errors.
    /// </summary>
    /// <param name="errors">Validation errors.</param>
    /// <returns>Validation result with IsValid = false and error collection.</returns>
    public static ValidationResult Failure(params ValidationError[] errors) => new()
    {
        IsValid = false,
        Errors = errors
    };
}

/// <summary>
/// Represents a validation error.
/// Immutable record with structured error information.
/// </summary>
/// <param name="Field">Field name that failed validation.</param>
/// <param name="Message">Human-readable error message.</param>
/// <param name="Code">Machine-readable error code for client handling.</param>
public record ValidationError(
    string Field,
    string Message,
    string Code);
