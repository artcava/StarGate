namespace StarGate.Core.Exceptions;

/// <summary>
/// Exception thrown when attempting to create a duplicate process.
/// </summary>
public class DuplicateProcessException : DomainException
{
    public string IdempotencyKey { get; }

    public DuplicateProcessException(string idempotencyKey)
        : base($"A process with IdempotencyKey '{idempotencyKey}' already exists")
    {
        IdempotencyKey = idempotencyKey;
    }
}
