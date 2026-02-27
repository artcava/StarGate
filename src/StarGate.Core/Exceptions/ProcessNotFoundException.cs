namespace StarGate.Core.Exceptions;

/// <summary>
/// Exception thrown when a process is not found.
/// </summary>
public class ProcessNotFoundException : DomainException
{
    /// <summary>
    /// Gets the process ID that was not found (if applicable).
    /// </summary>
    public Guid? ProcessId { get; }

    /// <summary>
    /// Gets the client ID that was used in the search (if applicable).
    /// </summary>
    public string? ClientId { get; }

    /// <summary>
    /// Gets the client process ID that was used in the search (if applicable).
    /// </summary>
    public string? ClientProcessId { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="ProcessNotFoundException"/> class
    /// for process ID lookup.
    /// </summary>
    /// <param name="processId">The process ID that was not found.</param>
    public ProcessNotFoundException(Guid processId)
        : base($"Process with ID '{processId}' was not found.")
    {
        ProcessId = processId;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="ProcessNotFoundException"/> class
    /// for client-based lookup.
    /// </summary>
    /// <param name="clientId">The client ID used in the search.</param>
    /// <param name="clientProcessId">The client process ID used in the search.</param>
    public ProcessNotFoundException(string clientId, string clientProcessId)
        : base($"Process not found for ClientId='{clientId}', ClientProcessId='{clientProcessId}'.")
    {
        ClientId = clientId;
        ClientProcessId = clientProcessId;
    }
}
