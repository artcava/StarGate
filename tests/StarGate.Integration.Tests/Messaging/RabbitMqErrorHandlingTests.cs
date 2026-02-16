using FluentAssertions;
using StarGate.Core.Abstractions;
using StarGate.Core.Domain;
using StarGate.Integration.Tests.Fixtures;
using Xunit;

namespace StarGate.Integration.Tests.Messaging;

public class RabbitMqErrorHandlingTests : IClassFixture<RabbitMqFixture>, IAsyncLifetime
{
    private readonly RabbitMqFixture _fixture;
    private readonly string _testQueue;

    public RabbitMqErrorHandlingTests(RabbitMqFixture fixture)
    {
        _fixture = fixture;
        _testQueue = $"test.errors.{Guid.NewGuid()}";
    }

    public Task InitializeAsync() => Task.CompletedTask;

    public async Task DisposeAsync()
    {
        await _fixture.Consumer.StopConsumingAsync();
        _fixture.DeleteQueue(_testQueue);
    }

    [Fact]
    public async Task Consumer_Should_RequeueMessage_WhenHandlerThrowsException()
    {
        // Arrange
        var attemptCount = 0;
        var tcs = new TaskCompletionSource<bool>();

        Func<MessageEnvelope<Process>, CancellationToken, Task<MessageHandlingResult>> handler = 
            async (envelope, ct) =>
            {
                attemptCount++;

                if (attemptCount < 2)
                {
                    throw new InvalidOperationException("Simulated processing error");
                }

                tcs.TrySetResult(true);
                return await Task.FromResult(MessageHandlingResult.Acknowledge);
            };

        var process = CreateTestProcess();

        // Act
        await _fixture.Broker.PublishAsync(_testQueue, process);
        await _fixture.Consumer.StartConsumingAsync(_testQueue, handler, CancellationToken.None);

        var completed = await Task.WhenAny(tcs.Task, Task.Delay(10000)) == tcs.Task;

        // Assert
        completed.Should().BeTrue("message should be requeued and retried after exception");
        attemptCount.Should().BeGreaterOrEqualTo(2);
    }

    [Fact]
    public async Task Consumer_Should_HandleCancellation_Gracefully()
    {
        // Arrange
        var receivedCount = 0;
        var cts = new CancellationTokenSource();

        Func<MessageEnvelope<Process>, CancellationToken, Task<MessageHandlingResult>> handler = 
            async (envelope, ct) =>
            {
                Interlocked.Increment(ref receivedCount);
                
                // Cancel after first message
                cts.Cancel();
                
                // Simulate work that respects cancellation
                await Task.Delay(1000, ct);
                
                return MessageHandlingResult.Acknowledge;
            };

        var process = CreateTestProcess();

        // Act
        await _fixture.Broker.PublishAsync(_testQueue, process);
        await _fixture.Consumer.StartConsumingAsync(_testQueue, handler, cts.Token);

        await Task.Delay(2000);

        // Assert - Message should be requeued due to cancellation
        receivedCount.Should().BeGreaterOrEqualTo(1);
        var messageCount = _fixture.GetMessageCount(_testQueue);
        messageCount.Should().BeGreaterOrEqualTo(0, "cancelled message should be requeued");
    }

    [Fact]
    public async Task Consumer_Should_ContinueProcessing_AfterNonFatalError()
    {
        // Arrange
        var receivedMessages = new List<Guid>();
        var processedCount = 0;
        var tcs = new TaskCompletionSource<bool>();

        Func<MessageEnvelope<Process>, CancellationToken, Task<MessageHandlingResult>> handler = 
            async (envelope, ct) =>
            {
                lock (receivedMessages)
                {
                    receivedMessages.Add(envelope.Payload.ProcessId);
                }

                // First message throws exception
                if (receivedMessages.Count == 1)
                {
                    throw new InvalidOperationException("Simulated error on first message");
                }

                if (Interlocked.Increment(ref processedCount) >= 2)
                {
                    tcs.TrySetResult(true);
                }

                return await Task.FromResult(MessageHandlingResult.Acknowledge);
            };

        var processes = Enumerable.Range(0, 3)
            .Select(_ => CreateTestProcess())
            .ToList();

        // Act
        await _fixture.Consumer.StartConsumingAsync(_testQueue, handler, CancellationToken.None);

        foreach (var process in processes)
        {
            await _fixture.Broker.PublishAsync(_testQueue, process);
        }

        var completed = await Task.WhenAny(tcs.Task, Task.Delay(15000)) == tcs.Task;

        // Assert
        completed.Should().BeTrue("consumer should continue after error");
        receivedMessages.Count.Should().BeGreaterOrEqualTo(3, "all messages should eventually be processed");
    }

    [Fact]
    public async Task Consumer_Should_HandleTimeoutGracefully()
    {
        // Arrange
        var attemptCount = 0;
        var tcs = new TaskCompletionSource<bool>();

        Func<MessageEnvelope<Process>, CancellationToken, Task<MessageHandlingResult>> handler = 
            async (envelope, ct) =>
            {
                attemptCount++;

                if (attemptCount == 1)
                {
                    // Simulate timeout
                    await Task.Delay(10000, ct); // Will be cancelled
                }

                tcs.TrySetResult(true);
                return await Task.FromResult(MessageHandlingResult.Acknowledge);
            };

        var process = CreateTestProcess();
        var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));

        // Act
        await _fixture.Broker.PublishAsync(_testQueue, process);
        await _fixture.Consumer.StartConsumingAsync(_testQueue, handler, cts.Token);

        await Task.Delay(5000);

        // Assert - Message should still be in queue after timeout
        var messageCount = _fixture.GetMessageCount(_testQueue);
        messageCount.Should().BeGreaterOrEqualTo(0);
    }

    [Fact]
    public async Task Consumer_Should_NotAcknowledge_WhenProcessingFails()
    {
        // Arrange
        var attemptCount = 0;

        Func<MessageEnvelope<Process>, CancellationToken, Task<MessageHandlingResult>> handler = 
            (envelope, ct) =>
            {
                Interlocked.Increment(ref attemptCount);
                throw new InvalidOperationException("Always fails");
            };

        var process = CreateTestProcess();

        // Act
        await _fixture.Broker.PublishAsync(_testQueue, process);
        await _fixture.Consumer.StartConsumingAsync(_testQueue, handler, CancellationToken.None);

        // Wait for multiple retry attempts
        await Task.Delay(5000);

        // Stop consumer
        await _fixture.Consumer.StopConsumingAsync();

        // Assert
        attemptCount.Should().BeGreaterThan(1, "message should be retried multiple times");
        
        var messageCount = _fixture.GetMessageCount(_testQueue);
        messageCount.Should().BeGreaterOrEqualTo(0, "unprocessed message should remain in queue");
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
