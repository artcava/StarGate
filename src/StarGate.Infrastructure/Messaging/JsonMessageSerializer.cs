using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using StarGate.Core.Abstractions;
using StarGate.Core.Exceptions;
namespace StarGate.Infrastructure.Messaging;


/// <summary>
/// JSON-based implementation of IMessageSerializer using System.Text.Json.
/// </summary>
public class JsonMessageSerializer : IMessageSerializer
{
    private readonly ILogger<JsonMessageSerializer> _logger;
    private readonly JsonSerializerOptions _options;

    /// <summary>
    /// Initializes a new instance of the <see cref="JsonMessageSerializer"/> class.
    /// </summary>
    /// <param name="logger">The logger instance.</param>
    public JsonMessageSerializer(ILogger<JsonMessageSerializer> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        _options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            WriteIndented = false,
            Converters =
            {
                new JsonStringEnumConverter(JsonNamingPolicy.CamelCase)
            }
        };
    }

    /// <inheritdoc />
    public byte[] Serialize<T>(MessageEnvelope<T> message) where T : class
    {
        ArgumentNullException.ThrowIfNull(message);

        try
        {
            var json = JsonSerializer.Serialize(message, _options);
            var bytes = Encoding.UTF8.GetBytes(json);

            _logger.LogDebug(
                "Serialized message {MessageId} of type {MessageType}, size: {Size} bytes",
                message.MessageId,
                message.MessageType,
                bytes.Length);

            return bytes;
        }
        catch (JsonException ex)
        {
            _logger.LogError(
                ex,
                "Failed to serialize message {MessageId} of type {MessageType}",
                message.MessageId,
                message.MessageType);

            throw new MessageSerializationException(
                $"Failed to serialize message '{message.MessageId}' of type '{message.MessageType}'",
                ex);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Unexpected error serializing message {MessageId}",
                message.MessageId);

            throw new MessageSerializationException(
                $"Unexpected error serializing message '{message.MessageId}'",
                ex);
        }
    }

    /// <inheritdoc />
    public MessageEnvelope<T> Deserialize<T>(byte[] data) where T : class
    {
        ArgumentNullException.ThrowIfNull(data);

        if (data.Length == 0)
        {
            throw new MessageSerializationException("Cannot deserialize empty byte array");
        }

        try
        {
            var json = Encoding.UTF8.GetString(data);
            var envelope = JsonSerializer.Deserialize<MessageEnvelope<T>>(json, _options) ?? throw new MessageSerializationException(
                    "Deserialization returned null envelope");

            _logger.LogDebug(
                "Deserialized message {MessageId} of type {MessageType}",
                envelope.MessageId,
                envelope.MessageType);

            return envelope;
        }
        catch (JsonException ex)
        {
            _logger.LogError(
                ex,
                "Failed to deserialize message, size: {Size} bytes",
                data.Length);

            throw new MessageSerializationException(
                $"Failed to deserialize message of size {data.Length} bytes",
                ex);
        }
        catch (Exception ex) when (ex is not MessageSerializationException)
        {
            _logger.LogError(
                ex,
                "Unexpected error deserializing message");

            throw new MessageSerializationException(
                "Unexpected error deserializing message",
                ex);
        }
    }

    /// <inheritdoc />
    public MessageEnvelope DeserializeUntyped(byte[] data)
    {
        ArgumentNullException.ThrowIfNull(data);

        if (data.Length == 0)
        {
            throw new MessageSerializationException("Cannot deserialize empty byte array");
        }

        try
        {
            var json = Encoding.UTF8.GetString(data);
            var envelope = JsonSerializer.Deserialize<MessageEnvelope>(json, _options) ?? throw new MessageSerializationException(
                    "Deserialization returned null envelope");

            _logger.LogDebug(
                "Deserialized untyped message {MessageId} of type {MessageType}",
                envelope.MessageId,
                envelope.MessageType);

            return envelope;
        }
        catch (JsonException ex)
        {
            _logger.LogError(
                ex,
                "Failed to deserialize untyped message, size: {Size} bytes",
                data.Length);

            throw new MessageSerializationException(
                $"Failed to deserialize untyped message of size {data.Length} bytes",
                ex);
        }
        catch (Exception ex) when (ex is not MessageSerializationException)
        {
            _logger.LogError(
                ex,
                "Unexpected error deserializing untyped message");

            throw new MessageSerializationException(
                "Unexpected error deserializing untyped message",
                ex);
        }
    }
}
