namespace StarGate.Core.Exceptions;

/// <summary>
/// Exception thrown when attempting to create a process with a duplicate idempotency key.
/// </summary>
public class DuplicateProcessException : DomainException
{
    /// <summary>
    /// Gets the idempotency key that caused the duplicate.
    /// </summary>
    public string IdempotencyKey { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="DuplicateProcessException"/> class.
    /// </summary>
    /// <param name="idempotencyKey">The duplicate idempotency key.</param>
    public DuplicateProcessException(string idempotencyKey)
        : base($"Process with idempotency key '{idempotencyKey}' already exists.")
    {
        IdempotencyKey = idempotencyKey;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="DuplicateProcessException"/> class with a custom message.
    /// </summary>
    /// <param name="idempotencyKey">The duplicate idempotency key.</param>
    /// <param name="message">Custom error message.</param>
    public DuplicateProcessException(string idempotencyKey, string message)
        : base(message)
    {
        IdempotencyKey = idempotencyKey;
    }
}
