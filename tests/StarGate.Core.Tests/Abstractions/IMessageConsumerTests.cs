using FluentAssertions;
using StarGate.Core.Abstractions;

namespace StarGate.Core.Tests.Abstractions;

/// <summary>
/// Contract tests for IMessageConsumer implementations.
/// Verifies expected behavior of any consumer implementation.
/// Concrete implementations must inherit this class and implement required methods.
/// Override test methods with [Fact] attributes to enable test execution.
/// </summary>
public abstract class IMessageConsumerTests
{
    /// <summary>
    /// Creates a consumer instance for testing.
    /// Each test will call this to get a fresh consumer.
    /// </summary>
    protected abstract IMessageConsumer CreateConsumer(string queueName);

    /// <summary>
    /// Publishes a test message to a queue.
    /// Used to setup test messages for consumer tests.
    /// </summary>
    protected abstract Task PublishTestMessageAsync<T>(string queueName, T message) where T : class;

    /// <summary>
    /// Cleans up test data after each test.
    /// Ensures tests don't interfere with each other.
    /// </summary>
    protected abstract Task CleanupAsync();

    protected virtual async Task StartConsumingAsync_Should_ProcessMessages()
    {
        // Arrange
        string queueName = "test-consumer-queue";
        IMessageConsumer consumer = CreateConsumer(queueName);
        TestMessage message = new() { Id = Guid.NewGuid(), Content = "Test" };
        TestMessage? receivedMessage = default;
        TaskCompletionSource<bool> tcs = new();

        await PublishTestMessageAsync(queueName, message);

        // Act
        await consumer.StartConsumingAsync<TestMessage>(async (msg, ctx) =>
        {
            receivedMessage = msg;
            await ctx.AcknowledgeAsync();
            tcs.SetResult(true);
        });

        await Task.WhenAny(tcs.Task, Task.Delay(TimeSpan.FromSeconds(5)));

        // Assert
        receivedMessage.Should().NotBeNull();
        receivedMessage!.Id.Should().Be(message.Id);

        await consumer.StopConsumingAsync();
        await consumer.DisposeAsync();
        await CleanupAsync();
    }

    protected virtual async Task MessageContext_Should_ProvideAcknowledgment()
    {
        // Arrange
        string queueName = "test-ack-queue";
        IMessageConsumer consumer = CreateConsumer(queueName);
        TestMessage message = new() { Id = Guid.NewGuid(), Content = "Test" };
        bool acknowledged = false;
        TaskCompletionSource<bool> tcs = new();

        await PublishTestMessageAsync(queueName, message);

        // Act
        await consumer.StartConsumingAsync<TestMessage>(async (msg, ctx) =>
        {
            ctx.MessageId.Should().NotBeNullOrEmpty();
            ctx.Timestamp.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(10));
            ctx.DeliveryTag.Should().BeGreaterThan(0);

            await ctx.AcknowledgeAsync();
            acknowledged = true;
            tcs.SetResult(true);
        });

        await Task.WhenAny(tcs.Task, Task.Delay(TimeSpan.FromSeconds(5)));

        // Assert
        acknowledged.Should().BeTrue();

        await consumer.StopConsumingAsync();
        await consumer.DisposeAsync();
        await CleanupAsync();
    }

    protected record TestMessage
    {
        public Guid Id { get; init; }
        public string Content { get; init; } = string.Empty;
    }
}
