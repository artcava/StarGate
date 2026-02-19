using StarGate.Core.Domain;

namespace StarGate.Api.Models;

/// <summary>
/// Response containing process information.
/// </summary>
public record ProcessResponse
{
    public required Guid ProcessId { get; init; }
    public required string ClientId { get; init; }
    public required string ProcessType { get; init; }
    public required string ClientProcessId { get; init; }
    public required string Status { get; init; }
    public required int Progress { get; init; }
    public required DateTime CreatedAt { get; init; }
    public required DateTime UpdatedAt { get; init; }
    public DateTime? CompletedAt { get; init; }
    public DateTime? FailedAt { get; init; }
    public DateTime? TimeoutAt { get; init; }
    public int? RetryCount { get; init; }
    public Dictionary<string, string>? Metadata { get; init; }
    public List<ProcessErrorResponse>? Errors { get; init; }

    public static ProcessResponse FromDomain(Process process)
    {
        return new ProcessResponse
        {
            ProcessId = process.ProcessId,
            ClientId = process.ClientId,
            ProcessType = process.ProcessType,
            ClientProcessId = process.ClientProcessId,
            Status = process.Status.ToString(),
            Progress = process.Progress,
            CreatedAt = process.CreatedAt,
            UpdatedAt = process.UpdatedAt,
            CompletedAt = process.CompletedAt,
            FailedAt = process.Status == ProcessStatus.Failed ? process.CompletedAt : null,
            TimeoutAt = process.TimeoutAt,
            RetryCount = process.RetryCount,
            Metadata = null, // Process domain doesn't have Metadata currently
            Errors = process.Error != null ? new List<ProcessErrorResponse> { ProcessErrorResponse.FromDomain(process.Error) } : null
        };
    }
}

public record ProcessErrorResponse
{
    public required string ErrorCode { get; init; }
    public required string Message { get; init; }
    public required DateTime Timestamp { get; init; }
    public bool Retryable { get; init; }

    public static ProcessErrorResponse FromDomain(ProcessError error)
    {
        return new ProcessErrorResponse
        {
            ErrorCode = error.Code,
            Message = error.Message,
            Timestamp = DateTime.UtcNow,
            Retryable = true // Default, could be enhanced based on error code
        };
    }
}
