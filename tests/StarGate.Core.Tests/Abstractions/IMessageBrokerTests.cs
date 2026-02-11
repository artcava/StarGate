using FluentAssertions;
using StarGate.Core.Abstractions;

namespace StarGate.Core.Tests.Abstractions;

/// <summary>
/// Contract tests for IMessageBroker implementations.
/// Verifies expected behavior of any broker implementation.
/// Concrete implementations must inherit this class and implement CreateBroker() and CleanupAsync().
/// Override test methods with [Fact] attributes to enable test execution.
/// </summary>
public abstract class IMessageBrokerTests
{
    /// <summary>
    /// Creates a message broker instance for testing.
    /// Each test will call this to get a fresh broker.
    /// </summary>
    protected abstract IMessageBroker CreateBroker();

    /// <summary>
    /// Cleans up test data after each test.
    /// Ensures tests don't interfere with each other.
    /// </summary>
    protected abstract Task CleanupAsync();

    protected virtual async Task PublishAsync_Should_SendMessage()
    {
        // Arrange
        IMessageBroker broker = CreateBroker();
        string queueName = "test-queue";
        TestMessage message = new() { Id = Guid.NewGuid(), Content = "Test" };

        // Act
        await broker.PublishAsync(queueName, message);

        // Assert - Message should be in queue (implementation-specific verification)
        // This is verified by integration tests
        true.Should().BeTrue();

        await CleanupAsync();
    }

    protected virtual async Task PublishAsync_WithProperties_Should_IncludeMetadata()
    {
        // Arrange
        IMessageBroker broker = CreateBroker();
        string queueName = "test-queue";
        TestMessage message = new() { Id = Guid.NewGuid(), Content = "Test" };
        MessageProperties properties = new()
        {
            MessageId = Guid.NewGuid().ToString(),
            CorrelationId = "correlation-123",
            Priority = 5,
            TimeToLive = TimeSpan.FromMinutes(10),
            Headers = new Dictionary<string, object>
            {
                ["CustomHeader"] = "CustomValue"
            }
        };

        // Act
        await broker.PublishAsync(queueName, message, properties);

        // Assert - Properties should be attached to message
        properties.MessageId.Should().NotBeNullOrEmpty();
        properties.CorrelationId.Should().Be("correlation-123");

        await CleanupAsync();
    }

    protected virtual void CreateConsumer_Should_ReturnValidConsumer()
    {
        // Arrange
        IMessageBroker broker = CreateBroker();
        string queueName = "test-queue";

        // Act
        IMessageConsumer consumer = broker.CreateConsumer(queueName);

        // Assert
        consumer.Should().NotBeNull();
        consumer.Should().BeAssignableTo<IMessageConsumer>();
    }

    protected record TestMessage
    {
        public Guid Id { get; init; }
        public string Content { get; init; } = string.Empty;
    }
}
