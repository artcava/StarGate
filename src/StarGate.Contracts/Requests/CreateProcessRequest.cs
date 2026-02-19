using System.ComponentModel.DataAnnotations;

namespace StarGate.Contracts.Requests;

/// <summary>
/// Request to create a new process.
/// </summary>
public record CreateProcessRequest
{
    /// <summary>
    /// Client identifier.
    /// </summary>
    [Required]
    public required string ClientId { get; init; }

    /// <summary>
    /// Process type identifier (e.g., "order", "payment", "shipping").
    /// </summary>
    [Required]
    public required string ProcessType { get; init; }

    /// <summary>
    /// Client-specific process identifier.
    /// </summary>
    [Required]
    public required string ClientProcessId { get; init; }

    /// <summary>
    /// Idempotency key to prevent duplicate submissions.
    /// </summary>
    [Required]
    public required string IdempotencyKey { get; init; }

    /// <summary>
    /// Optional metadata for the process.
    /// </summary>
    public Dictionary<string, string>? Metadata { get; init; }
}
