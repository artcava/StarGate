using FluentAssertions;
using StarGate.Core.Abstractions;
using Xunit;

namespace StarGate.Core.Tests.Abstractions;

/// <summary>
/// Unit tests for MessageProperties record type.
/// Verifies immutability, header support, and property validation.
/// </summary>
public class MessagePropertiesTests
{
    [Fact]
    public void MessageProperties_Should_BeImmutable()
    {
        // Arrange
        MessageProperties properties = new()
        {
            MessageId = "msg-123",
            CorrelationId = "corr-456",
            Priority = 5
        };

        // Act
        MessageProperties modified = properties with { Priority = 10 };

        // Assert
        properties.Priority.Should().Be(5);
        modified.Priority.Should().Be(10);
        properties.MessageId.Should().Be(modified.MessageId);
    }

    [Fact]
    public void MessageProperties_Should_SupportCustomHeaders()
    {
        // Arrange & Act
        MessageProperties properties = new()
        {
            Headers = new Dictionary<string, object>
            {
                ["CustomHeader1"] = "Value1",
                ["CustomHeader2"] = 123,
                ["CustomHeader3"] = true
            }
        };

        // Assert
        properties.Headers.Should().NotBeNull();
        properties.Headers.Should().HaveCount(3);
        properties.Headers!["CustomHeader1"].Should().Be("Value1");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(5)]
    [InlineData(10)]
    [InlineData(255)]
    public void MessageProperties_Should_AcceptValidPriorities(int priority)
    {
        // Arrange & Act
        MessageProperties properties = new() { Priority = priority };

        // Assert
        properties.Priority.Should().Be(priority);
    }

    [Fact]
    public void MessageProperties_Should_SupportTimeToLive()
    {
        // Arrange & Act
        TimeSpan ttl = TimeSpan.FromMinutes(30);
        MessageProperties properties = new() { TimeToLive = ttl };

        // Assert
        properties.TimeToLive.Should().Be(ttl);
    }
}
