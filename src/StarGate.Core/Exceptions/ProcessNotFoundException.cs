namespace StarGate.Core.Exceptions;

/// <summary>
/// Exception thrown when a requested process cannot be found.
/// </summary>
public class ProcessNotFoundException : DomainException
{
    public Guid? ProcessId { get; }
    public string? ClientId { get; }
    public string? ClientProcessId { get; }

    public ProcessNotFoundException(Guid processId)
        : base($"Process with ID '{processId}' not found")
    {
        ProcessId = processId;
    }

    public ProcessNotFoundException(string clientId, string clientProcessId)
        : base($"Process with ClientId '{clientId}' and ClientProcessId '{clientProcessId}' not found")
    {
        ClientId = clientId;
        ClientProcessId = clientProcessId;
    }
}
