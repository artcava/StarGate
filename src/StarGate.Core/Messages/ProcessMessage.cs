using System.Text.Json.Serialization;

namespace StarGate.Core.Messages;

/// <summary>
/// Message sent to the broker when a process is ready for execution.
/// Contains minimal information needed for routing and worker identification.
/// </summary>
public class ProcessMessage
{
    [JsonPropertyName("processId")]
    public Guid ProcessId { get; set; }

    [JsonPropertyName("clientId")]
    public string ClientId { get; set; } = string.Empty;

    [JsonPropertyName("processType")]
    public string ProcessType { get; set; } = string.Empty;

    [JsonPropertyName("clientProcessId")]
    public string ClientProcessId { get; set; } = string.Empty;

    [JsonPropertyName("priority")]
    public int Priority { get; set; } = 5;

    [JsonPropertyName("timestamp")]
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;

    [JsonPropertyName("metadata")]
    public Dictionary<string, string>? Metadata { get; set; }

    /// <summary>
    /// Creates a ProcessMessage from a Process entity.
    /// </summary>
    /// <param name="process">The process to create the message from.</param>
    /// <returns>A new ProcessMessage instance.</returns>
    public static ProcessMessage FromProcess(Core.Domain.Process process)
    {
        return new ProcessMessage
        {
            ProcessId = process.ProcessId,
            ClientId = process.ClientId,
            ProcessType = process.ProcessType,
            ClientProcessId = process.ClientProcessId,
            Priority = 5, // Default priority, can be made configurable based on process type
            Timestamp = DateTime.UtcNow,
            Metadata = process.Metadata
        };
    }
}
