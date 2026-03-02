namespace StarGate.Core.Domain;

/// <summary>
/// Context provided to process handlers during execution.
/// Encapsulates process information and execution environment.
/// </summary>
public class ProcessContext
{
    /// <summary>
    /// Unique process identifier.
    /// </summary>
    public Guid ProcessId { get; set; }

    /// <summary>
    /// Client identifier.
    /// </summary>
    public string ClientId { get; set; } = string.Empty;

    /// <summary>
    /// Process type.
    /// </summary>
    public string ProcessType { get; set; } = string.Empty;

    /// <summary>
    /// Client-specific process identifier.
    /// </summary>
    public string ClientProcessId { get; set; } = string.Empty;

    /// <summary>
    /// Process metadata.
    /// </summary>
    public Dictionary<string, string> Metadata { get; set; } = new();

    /// <summary>
    /// Cancellation token for the operation.
    /// </summary>
    public CancellationToken CancellationToken { get; set; }

    /// <summary>
    /// Gets a metadata value.
    /// </summary>
    /// <param name="key">Metadata key.</param>
    /// <returns>Metadata value or null if not found.</returns>
    public string? GetMetadata(string key)
    {
        return Metadata.TryGetValue(key, out var value) ? value : null;
    }

    /// <summary>
    /// Sets a metadata value.
    /// </summary>
    /// <param name="key">Metadata key.</param>
    /// <param name="value">Metadata value.</param>
    public void SetMetadata(string key, string value)
    {
        Metadata[key] = value;
    }
}
