namespace StarGate.Core.Errors;

/// <summary>
/// Classifies exceptions and determines handling strategy.
/// </summary>
public static class ErrorClassifier
{
    public static ErrorClassification Classify(Exception exception)
    {
        return exception switch
        {
            System.Text.Json.JsonException => new ErrorClassification
            {
                ErrorCode = "MALFORMED_MESSAGE",
                IsRetryable = false,
                ShouldRequeue = false,
                Severity = ErrorSeverity.Error
            },
            TimeoutException => new ErrorClassification
            {
                ErrorCode = "PROCESS_TIMEOUT",
                IsRetryable = true,
                ShouldRequeue = true,
                Severity = ErrorSeverity.Warning
            },
            HttpRequestException => new ErrorClassification
            {
                ErrorCode = "HTTP_ERROR",
                IsRetryable = true,
                ShouldRequeue = true,
                Severity = ErrorSeverity.Warning
            },
            InvalidOperationException => new ErrorClassification
            {
                ErrorCode = "INVALID_OPERATION",
                IsRetryable = false,
                ShouldRequeue = false,
                Severity = ErrorSeverity.Error
            },
            ArgumentException => new ErrorClassification
            {
                ErrorCode = "INVALID_ARGUMENT",
                IsRetryable = false,
                ShouldRequeue = false,
                Severity = ErrorSeverity.Error
            },
            _ => new ErrorClassification
            {
                ErrorCode = "UNKNOWN_ERROR",
                IsRetryable = true,
                ShouldRequeue = true,
                Severity = ErrorSeverity.Error
            }
        };
    }
}

public class ErrorClassification
{
    public string ErrorCode { get; set; } = string.Empty;
    public bool IsRetryable { get; set; }
    public bool ShouldRequeue { get; set; }
    public ErrorSeverity Severity { get; set; }
}

public enum ErrorSeverity
{
    Warning,
    Error,
    Critical
}
