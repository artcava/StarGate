namespace StarGate.Core.Exceptions;

/// <summary>
/// Exception thrown when message broker operations fail.
/// </summary>
public class BrokerException : Exception
{
    public BrokerException(string message)
        : base(message)
    {
    }

    public BrokerException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
