namespace StarGate.Core.Messages;

using System.Text.Json.Serialization;

/// <summary>
/// Message sent to the broker when a process is ready for execution.
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

    [JsonPropertyName("metadata")]
    public Dictionary<string, string>? Metadata { get; set; }

    [JsonPropertyName("priority")]
    public int Priority { get; set; } = 5;

    [JsonPropertyName("timestamp")]
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;

    public static ProcessMessage FromProcess(Core.Domain.Process process)
    {
        return new ProcessMessage
        {
            ProcessId = process.ProcessId,
            ClientId = process.ClientId,
            ProcessType = process.ProcessType,
            ClientProcessId = process.ClientProcessId,
            Metadata = process.Metadata,
            Priority = 5, // Default priority, can be made configurable
            Timestamp = DateTime.UtcNow
        };
    }
}
