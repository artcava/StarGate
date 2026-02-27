namespace StarGate.Core.Exceptions;

/// <summary>
/// Exception thrown when message broker operations fail.
/// </summary>
public class MessageBrokerException : DomainException
{
    public MessageBrokerException(string message)
        : base(message)
    {
    }

    public MessageBrokerException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
