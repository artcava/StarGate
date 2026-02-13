using FluentAssertions;
using StarGate.Infrastructure.Messaging;
using Xunit;

namespace StarGate.Infrastructure.Tests.Messaging;

public class MessageEnvelopeFactoryTests
{
    [Fact]
    public void Create_Should_GenerateEnvelopeWithMetadata()
    {
        // Arrange
        var payload = new TestPayload { Value = "test" };

        // Act
        var envelope = MessageEnvelopeFactory.Create(payload);

        // Assert
        envelope.Should().NotBeNull();
        envelope.MessageId.Should().NotBeNullOrWhiteSpace();
        envelope.MessageType.Should().Contain("TestPayload");
        envelope.Timestamp.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
        envelope.Payload.Should().Be(payload);
    }

    [Fact]
    public void Create_Should_SetCorrelationId_WhenProvided()
    {
        // Arrange
        var payload = new TestPayload { Value = "test" };
        var correlationId = "CORR-12345";

        // Act
        var envelope = MessageEnvelopeFactory.Create(payload, correlationId);

        // Assert
        envelope.CorrelationId.Should().Be(correlationId);
    }

    [Fact]
    public void Create_Should_SetMetadata_WhenProvided()
    {
        // Arrange
        var payload = new TestPayload { Value = "test" };
        var metadata = new Dictionary<string, object>
        {
            ["Source"] = "API",
            ["Version"] = "1.0"
        };

        // Act
        var envelope = MessageEnvelopeFactory.Create(payload, metadata: metadata);

        // Assert
        envelope.Metadata.Should().NotBeNull();
        envelope.Metadata!["Source"].Should().Be("API");
        envelope.Metadata["Version"].Should().Be("1.0");
    }

    [Fact]
    public void Create_Should_ThrowArgumentNull_WhenPayloadIsNull()
    {
        // Act
        Action act = () => MessageEnvelopeFactory.Create<TestPayload>(null!);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Create_Should_GenerateUniqueMessageIds()
    {
        // Arrange
        var payload = new TestPayload { Value = "test" };

        // Act
        var envelope1 = MessageEnvelopeFactory.Create(payload);
        var envelope2 = MessageEnvelopeFactory.Create(payload);

        // Assert
        envelope1.MessageId.Should().NotBe(envelope2.MessageId);
    }

    [Fact]
    public void Create_Should_UseFullNameForMessageType()
    {
        // Arrange
        var payload = new TestPayload { Value = "test" };

        // Act
        var envelope = MessageEnvelopeFactory.Create(payload);

        // Assert
        envelope.MessageType.Should().Be(typeof(TestPayload).FullName);
    }

    [Fact]
    public void Create_Should_SetUtcTimestamp()
    {
        // Arrange
        var payload = new TestPayload { Value = "test" };
        var beforeCreate = DateTime.UtcNow;

        // Act
        var envelope = MessageEnvelopeFactory.Create(payload);
        var afterCreate = DateTime.UtcNow;

        // Assert
        envelope.Timestamp.Should().BeOnOrAfter(beforeCreate);
        envelope.Timestamp.Should().BeOnOrBefore(afterCreate);
        envelope.Timestamp.Kind.Should().Be(DateTimeKind.Utc);
    }

    private class TestPayload
    {
        public string Value { get; set; } = string.Empty;
    }
}
