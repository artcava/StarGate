namespace StarGate.Core.Abstractions;

/// <summary>
/// Defines message serialization operations for the message broker.
/// </summary>
public interface IMessageSerializer
{
    /// <summary>
    /// Serializes a message to byte array.
    /// </summary>
    /// <typeparam name="T">Type of the message payload.</typeparam>
    /// <param name="message">The message to serialize.</param>
    /// <returns>Serialized message as byte array.</returns>
    /// <exception cref="ArgumentNullException">Thrown when message is null.</exception>
    /// <exception cref="Core.Exceptions.MessageSerializationException">Thrown when serialization fails.</exception>
    byte[] Serialize<T>(MessageEnvelope<T> message) where T : class;

    /// <summary>
    /// Deserializes a byte array to a message envelope.
    /// </summary>
    /// <typeparam name="T">Type of the message payload.</typeparam>
    /// <param name="data">The byte array to deserialize.</param>
    /// <returns>Deserialized message envelope.</returns>
    /// <exception cref="ArgumentNullException">Thrown when data is null.</exception>
    /// <exception cref="Core.Exceptions.MessageSerializationException">Thrown when deserialization fails.</exception>
    MessageEnvelope<T> Deserialize<T>(byte[] data) where T : class;

    /// <summary>
    /// Deserializes a byte array to a non-generic message envelope.
    /// Useful when the message type is unknown at compile time.
    /// </summary>
    /// <param name="data">The byte array to deserialize.</param>
    /// <returns>Deserialized message envelope with object payload.</returns>
    /// <exception cref="ArgumentNullException">Thrown when data is null.</exception>
    /// <exception cref="Core.Exceptions.MessageSerializationException">Thrown when deserialization fails.</exception>
    MessageEnvelope DeserializeUntyped(byte[] data);
}
