namespace StarGate.Core.Exceptions;

/// <summary>
/// Exception thrown when message serialization or deserialization fails.
/// </summary>
public class MessageSerializationException : Exception
{
    /// <summary>
    /// Initializes a new instance of the <see cref="MessageSerializationException"/> class.
    /// </summary>
    /// <param name="message">The error message.</param>
    public MessageSerializationException(string message)
        : base(message)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="MessageSerializationException"/> class.
    /// </summary>
    /// <param name="message">The error message.</param>
    /// <param name="innerException">The inner exception.</param>
    public MessageSerializationException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
