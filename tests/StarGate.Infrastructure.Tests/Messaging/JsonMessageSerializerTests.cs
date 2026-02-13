namespace StarGate.Infrastructure.Tests.Messaging;

using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using StarGate.Core.Abstractions;
using StarGate.Core.Exceptions;
using StarGate.Infrastructure.Messaging;
using Xunit;

public class JsonMessageSerializerTests
{
    private readonly JsonMessageSerializer _serializer;

    public JsonMessageSerializerTests()
    {
        _serializer = new JsonMessageSerializer(
            NullLogger<JsonMessageSerializer>.Instance);
    }

    [Fact]
    public void Serialize_Should_ConvertMessageToBytes()
    {
        // Arrange
        var payload = new TestMessage { OrderId = "ORD-123", Amount = 99.99m };
        var envelope = CreateEnvelope(payload);

        // Act
        var bytes = _serializer.Serialize(envelope);

        // Assert
        bytes.Should().NotBeEmpty();
        bytes.Length.Should().BeGreaterThan(0);
    }

    [Fact]
    public void Serialize_Should_ThrowArgumentNull_WhenMessageIsNull()
    {
        // Act
        Action act = () => _serializer.Serialize<TestMessage>(null!);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Deserialize_Should_ReconstructMessage()
    {
        // Arrange
        var payload = new TestMessage { OrderId = "ORD-456", Amount = 149.99m };
        var originalEnvelope = CreateEnvelope(payload);
        var bytes = _serializer.Serialize(originalEnvelope);

        // Act
        var deserializedEnvelope = _serializer.Deserialize<TestMessage>(bytes);

        // Assert
        deserializedEnvelope.Should().NotBeNull();
        deserializedEnvelope.MessageId.Should().Be(originalEnvelope.MessageId);
        deserializedEnvelope.MessageType.Should().Be(originalEnvelope.MessageType);
        deserializedEnvelope.Payload.OrderId.Should().Be("ORD-456");
        deserializedEnvelope.Payload.Amount.Should().Be(149.99m);
    }

    [Fact]
    public void Deserialize_Should_ThrowArgumentNull_WhenDataIsNull()
    {
        // Act
        Action act = () => _serializer.Deserialize<TestMessage>(null!);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Deserialize_Should_ThrowSerializationException_WhenDataIsEmpty()
    {
        // Act
        Action act = () => _serializer.Deserialize<TestMessage>(Array.Empty<byte>());

        // Assert
        act.Should().Throw<MessageSerializationException>()
            .WithMessage("*empty byte array*");
    }

    [Fact]
    public void Deserialize_Should_ThrowSerializationException_WhenDataIsInvalid()
    {
        // Arrange
        var invalidBytes = new byte[] { 0xFF, 0xFE, 0xFD };

        // Act
        Action act = () => _serializer.Deserialize<TestMessage>(invalidBytes);

        // Assert
        act.Should().Throw<MessageSerializationException>();
    }

    [Fact]
    public void DeserializeUntyped_Should_ReconstructMessageAsObject()
    {
        // Arrange
        var payload = new TestMessage { OrderId = "ORD-789", Amount = 199.99m };
        var originalEnvelope = CreateEnvelope(payload);
        var bytes = _serializer.Serialize(originalEnvelope);

        // Act
        var deserializedEnvelope = _serializer.DeserializeUntyped(bytes);

        // Assert
        deserializedEnvelope.Should().NotBeNull();
        deserializedEnvelope.MessageId.Should().Be(originalEnvelope.MessageId);
        deserializedEnvelope.MessageType.Should().Be(originalEnvelope.MessageType);
        deserializedEnvelope.Payload.Should().NotBeNull();
    }

    [Fact]
    public void DeserializeUntyped_Should_ThrowArgumentNull_WhenDataIsNull()
    {
        // Act
        Action act = () => _serializer.DeserializeUntyped(null!);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void RoundTrip_Should_PreserveAllData()
    {
        // Arrange
        var payload = new TestMessage { OrderId = "ORD-999", Amount = 299.99m };
        var metadata = new Dictionary<string, object>
        {
            ["Source"] = "WebAPI",
            ["Priority"] = "High"
        };
        var originalEnvelope = new MessageEnvelope<TestMessage>
        {
            MessageId = Guid.NewGuid().ToString(),
            CorrelationId = "CORR-123",
            MessageType = typeof(TestMessage).FullName!,
            Timestamp = DateTime.UtcNow,
            Payload = payload,
            Metadata = metadata
        };

        // Act
        var bytes = _serializer.Serialize(originalEnvelope);
        var deserializedEnvelope = _serializer.Deserialize<TestMessage>(bytes);

        // Assert
        deserializedEnvelope.MessageId.Should().Be(originalEnvelope.MessageId);
        deserializedEnvelope.CorrelationId.Should().Be(originalEnvelope.CorrelationId);
        deserializedEnvelope.MessageType.Should().Be(originalEnvelope.MessageType);
        deserializedEnvelope.Timestamp.Should().BeCloseTo(originalEnvelope.Timestamp, TimeSpan.FromSeconds(1));
        deserializedEnvelope.Payload.OrderId.Should().Be(payload.OrderId);
        deserializedEnvelope.Payload.Amount.Should().Be(payload.Amount);
        deserializedEnvelope.Metadata.Should().NotBeNull();
        deserializedEnvelope.Metadata!["Source"].ToString().Should().Be("WebAPI");
        deserializedEnvelope.Metadata["Priority"].ToString().Should().Be("High");
    }

    [Fact]
    public void Serialize_Should_HandleComplexPayload()
    {
        // Arrange
        var payload = new ComplexMessage
        {
            Id = Guid.NewGuid(),
            Name = "Test",
            Items = new[] { "Item1", "Item2", "Item3" },
            Properties = new Dictionary<string, object>
            {
                ["Key1"] = "Value1",
                ["Key2"] = 42
            }
        };
        var envelope = CreateEnvelope(payload);

        // Act
        var bytes = _serializer.Serialize(envelope);
        var deserialized = _serializer.Deserialize<ComplexMessage>(bytes);

        // Assert
        deserialized.Payload.Id.Should().Be(payload.Id);
        deserialized.Payload.Name.Should().Be(payload.Name);
        deserialized.Payload.Items.Should().BeEquivalentTo(payload.Items);
        deserialized.Payload.Properties.Should().ContainKey("Key1");
    }

    [Fact]
    public void Serialize_Should_HandleNullMetadata()
    {
        // Arrange
        var payload = new TestMessage { OrderId = "ORD-000", Amount = 0 };
        var envelope = new MessageEnvelope<TestMessage>
        {
            MessageId = Guid.NewGuid().ToString(),
            CorrelationId = null,
            MessageType = typeof(TestMessage).FullName!,
            Timestamp = DateTime.UtcNow,
            Payload = payload,
            Metadata = null
        };

        // Act
        var bytes = _serializer.Serialize(envelope);
        var deserialized = _serializer.Deserialize<TestMessage>(bytes);

        // Assert
        deserialized.CorrelationId.Should().BeNull();
        deserialized.Metadata.Should().BeNull();
    }

    private static MessageEnvelope<T> CreateEnvelope<T>(T payload) where T : class
    {
        return new MessageEnvelope<T>
        {
            MessageId = Guid.NewGuid().ToString(),
            CorrelationId = "TEST-CORR",
            MessageType = typeof(T).FullName ?? typeof(T).Name,
            Timestamp = DateTime.UtcNow,
            Payload = payload
        };
    }

    private class TestMessage
    {
        public string OrderId { get; set; } = string.Empty;
        public decimal Amount { get; set; }
    }

    private class ComplexMessage
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string[] Items { get; set; } = Array.Empty<string>();
        public Dictionary<string, object> Properties { get; set; } = new();
    }
}
