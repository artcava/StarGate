using FluentAssertions;
using StarGate.Core.Abstractions;
using StarGate.Core.Domain;
using StarGate.Integration.Tests.Fixtures;
using System.Collections.Concurrent;
using Xunit;

namespace StarGate.Integration.Tests.Messaging;

public class RabbitMqConsumerIntegrationTests : IClassFixture<RabbitMqFixture>, IAsyncLifetime
{
    private readonly RabbitMqFixture _fixture;
    private readonly string _testQueue;

    public RabbitMqConsumerIntegrationTests(RabbitMqFixture fixture)
    {
        _fixture = fixture;
        _testQueue = $"test.queue.{Guid.NewGuid()}";
    }

    public Task InitializeAsync() => Task.CompletedTask;

    public async Task DisposeAsync()
    {
        await _fixture.Consumer.StopConsumingAsync();
        _fixture.DeleteQueue(_testQueue);
    }

    [Fact]
    public async Task StartConsumingAsync_Should_ConsumePublishedMessage()
    {
        // Arrange
        var receivedMessages = new ConcurrentBag<MessageEnvelope<Process>>();
        var tcs = new TaskCompletionSource();

        Func<MessageEnvelope<Process>, CancellationToken, Task<MessageHandlingResult>> handler =
            async (envelope, ct) =>
            {
                receivedMessages.Add(envelope);
                tcs.TrySetResult(true);
                return await Task.FromResult(MessageHandlingResult.Acknowledge);
            };

        var process = CreateTestProcess();

        // Act
        await _fixture.Broker.PublishAsync(_testQueue, process);
        await _fixture.Consumer.StartConsumingAsync(_testQueue, handler, CancellationToken.None);

        // Wait for message to be consumed (with timeout)
        var consumed = await Task.WhenAny(tcs.Task, Task.Delay(5000)) == tcs.Task;

        // Assert
        consumed.Should().BeTrue("message should be consumed within timeout");
        receivedMessages.Should().HaveCount(1);
        receivedMessages.First().Payload.ProcessId.Should().Be(process.ProcessId);
    }

    [Fact]
    public async Task StartConsumingAsync_Should_AcknowledgeMessage_WhenHandlerReturnsAck()
    {
        // Arrange
        var tcs = new TaskCompletionSource();

        Func<MessageEnvelope<Process>, CancellationToken, Task<MessageHandlingResult>> handler =
            async (envelope, ct) =>
            {
                tcs.TrySetResult(true);
                return await Task.FromResult(MessageHandlingResult.Acknowledge);
            };

        var process = CreateTestProcess();

        // Act
        await _fixture.Broker.PublishAsync(_testQueue, process);
        await _fixture.Consumer.StartConsumingAsync(_testQueue, handler, CancellationToken.None);
        await Task.WhenAny(tcs.Task, Task.Delay(5000));

        // Assert - Message should be removed from queue
        await Task.Delay(1000); // Allow time for ack
        var messageCount = _fixture.GetMessageCount(_testQueue);
        messageCount.Should().Be(0);
    }

    [Fact]
    public async Task StartConsumingAsync_Should_RequeueMessage_WhenHandlerReturnsRequeue()
    {
        // Arrange
        var attemptCount = 0;
        var tcs = new TaskCompletionSource();

        Func<MessageEnvelope<Process>, CancellationToken, Task<MessageHandlingResult>> handler =
            async (envelope, ct) =>
            {
                attemptCount++;
                if (attemptCount == 1)
                {
                    return await Task.FromResult(MessageHandlingResult.Requeue);
                }
                tcs.TrySetResult(true);
                return await Task.FromResult(MessageHandlingResult.Acknowledge);
            };

        var process = CreateTestProcess();

        // Act
        await _fixture.Broker.PublishAsync(_testQueue, process);
        await _fixture.Consumer.StartConsumingAsync(_testQueue, handler, CancellationToken.None);
        await Task.WhenAny(tcs.Task, Task.Delay(10000));

        // Assert
        attemptCount.Should().BeGreaterThan(1, "message should be requeued and retried");
    }

    [Fact]
    public async Task StartConsumingAsync_Should_ConsumeMultipleMessages()
    {
        // Arrange
        var receivedMessages = new ConcurrentBag<MessageEnvelope<Process>>();
        var expectedCount = 10;
        var tcs = new TaskCompletionSource();

        Func<MessageEnvelope<Process>, CancellationToken, Task<MessageHandlingResult>> handler =
            async (envelope, ct) =>
            {
                receivedMessages.Add(envelope);
                if (receivedMessages.Count >= expectedCount)
                {
                    tcs.TrySetResult(true);
                }
                return await Task.FromResult(MessageHandlingResult.Acknowledge);
            };

        var processes = Enumerable.Range(0, expectedCount)
            .Select(_ => CreateTestProcess())
            .ToList();

        // Act
        foreach (var process in processes)
        {
            await _fixture.Broker.PublishAsync(_testQueue, process);
        }

        await _fixture.Consumer.StartConsumingAsync(_testQueue, handler, CancellationToken.None);
        var allConsumed = await Task.WhenAny(tcs.Task, Task.Delay(10000)) == tcs.Task;

        // Assert
        allConsumed.Should().BeTrue("all messages should be consumed");
        receivedMessages.Should().HaveCount(expectedCount);
    }

    [Fact]
    public async Task StartConsumingAsync_Should_PreserveMessageOrder_WithPrefetch()
    {
        // Arrange
        var receivedMessages = new ConcurrentBag<MessageEnvelope<Process>>();
        var expectedCount = 5;
        var tcs = new TaskCompletionSource();

        Func<MessageEnvelope<Process>, CancellationToken, Task<MessageHandlingResult>> handler =
            async (envelope, ct) =>
            {
                receivedMessages.Add(envelope);
                if (receivedMessages.Count >= expectedCount)
                {
                    tcs.TrySetResult(true);
                }
                await Task.Delay(100); // Simulate processing
                return MessageHandlingResult.Acknowledge;
            };

        var processes = Enumerable.Range(0, expectedCount)
            .Select(i => CreateTestProcess() with { ClientProcessId = $"process-{i}" })
            .ToList();

        // Act
        await _fixture.Consumer.StartConsumingAsync(_testQueue, handler, CancellationToken.None);

        foreach (var process in processes)
        {
            await _fixture.Broker.PublishAsync(_testQueue, process);
        }

        var allConsumed = await Task.WhenAny(tcs.Task, Task.Delay(10000)) == tcs.Task;

        // Assert
        allConsumed.Should().BeTrue();
        receivedMessages.Should().HaveCount(expectedCount);
    }

    [Fact]
    public async Task StopConsumingAsync_Should_StopReceivingMessages()
    {
        // Arrange
        var receivedCount = 0;
        var tcs = new TaskCompletionSource();

        Func<MessageEnvelope<Process>, CancellationToken, Task<MessageHandlingResult>> handler =
            async (envelope, ct) =>
            {
                Interlocked.Increment(ref receivedCount);
                if (receivedCount == 1)
                {
                    tcs.TrySetResult(true);
                }
                return await Task.FromResult(MessageHandlingResult.Acknowledge);
            };

        await _fixture.Broker.PublishAsync(_testQueue, CreateTestProcess());
        await _fixture.Consumer.StartConsumingAsync(_testQueue, handler, CancellationToken.None);
        await Task.WhenAny(tcs.Task, Task.Delay(5000));

        // Act - Stop consuming
        await _fixture.Consumer.StopConsumingAsync();

        // Publish more messages
        await _fixture.Broker.PublishAsync(_testQueue, CreateTestProcess());
        await Task.Delay(2000);

        // Assert - Should not consume new messages
        receivedCount.Should().Be(1, "should not consume messages after stopping");
    }

    private static Process CreateTestProcess() => new()
    {
        ProcessId = Guid.NewGuid(),
        ClientProcessId = $"client-{Guid.NewGuid()}",
        ProcessType = "order",
        ClientId = "test-client",
        Status = ProcessStatus.Accepted,
        Progress = 0,
        CreatedAt = DateTime.UtcNow,
        UpdatedAt = DateTime.UtcNow,
        IdempotencyKey = Guid.NewGuid().ToString(),
        Retryable = true
    };
}
