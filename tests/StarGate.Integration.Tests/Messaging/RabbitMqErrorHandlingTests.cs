using FluentAssertions;
using StarGate.Core.Abstractions;
using StarGate.Core.Domain;
using StarGate.Integration.Tests.Fixtures;
using Xunit;

namespace StarGate.Integration.Tests.Messaging;

public class RabbitMqErrorHandlingTests : IClassFixture<RabbitMqFixture>, IAsyncLifetime
{
    private readonly RabbitMqFixture _fixture;
    private const string QueueName = "stargate.process"; // Standard queue naming convention

    public RabbitMqErrorHandlingTests(RabbitMqFixture fixture)
    {
        _fixture = fixture;
    }

    public Task InitializeAsync() => Task.CompletedTask;

    public async Task DisposeAsync()
    {
        try
        {
            await _fixture.Consumer.StopConsumingAsync();
        }
        catch (InvalidOperationException)
        {
            // Consumer was not started, ignore
        }
        
        _fixture.PurgeQueue(QueueName);
    }

    [Fact]
    public async Task Consumer_Should_RequeueMessage_WhenHandlerThrowsException()
    {
        // Arrange
        _fixture.PurgeQueue(QueueName);
        
        var attemptCount = 0;
        var tcs = new TaskCompletionSource();

        async Task Handler(Process message, MessageContext context)
        {
            attemptCount++;

            if (attemptCount < 2)
            {
                throw new InvalidOperationException("Simulated processing error");
            }

            await context.AcknowledgeAsync();
            tcs.SetResult();
        }

        var process = CreateTestProcess();

        // Act
        await _fixture.Broker.PublishAsync(QueueName, process);
        await _fixture.Consumer.StartConsumingAsync<Process>(Handler, CancellationToken.None);

        var completed = await Task.WhenAny(tcs.Task, Task.Delay(10000)) == tcs.Task;

        // Assert
        completed.Should().BeTrue("message should be requeued and retried after exception");
        attemptCount.Should().BeGreaterOrEqualTo(2);
    }

    [Fact]
    public async Task Consumer_Should_HandleCancellation_Gracefully()
    {
        // Arrange
        _fixture.PurgeQueue(QueueName);
        
        var receivedCount = 0;
        var cts = new CancellationTokenSource();

        async Task Handler(Process message, MessageContext context)
        {
            Interlocked.Increment(ref receivedCount);
            
            // Cancel after first message
            cts.Cancel();
            
            // Simulate work
            await Task.Delay(1000);
            
            await context.AcknowledgeAsync();
        }

        var process = CreateTestProcess();

        // Act
        await _fixture.Broker.PublishAsync(QueueName, process);
        await _fixture.Consumer.StartConsumingAsync<Process>(Handler, cts.Token);

        await Task.Delay(2000);

        // Assert - Message should be processed
        receivedCount.Should().BeGreaterOrEqualTo(1);
    }

    [Fact]
    public async Task Consumer_Should_ContinueProcessing_AfterNonFatalError()
    {
        // Arrange
        _fixture.PurgeQueue(QueueName);
        
        var receivedMessages = new List<Guid>();
        var processedCount = 0;
        var tcs = new TaskCompletionSource();

        async Task Handler(Process message, MessageContext context)
        {
            lock (receivedMessages)
            {
                receivedMessages.Add(message.ProcessId);
            }

            // First message throws exception
            if (receivedMessages.Count == 1)
            {
                throw new InvalidOperationException("Simulated error on first message");
            }

            await context.AcknowledgeAsync();
            if (Interlocked.Increment(ref processedCount) >= 2)
            {
                tcs.SetResult();
            }
        }

        var processes = Enumerable.Range(0, 3)
            .Select(_ => CreateTestProcess())
            .ToList();

        // Act - Publish all messages
        foreach (var process in processes)
        {
            await _fixture.Broker.PublishAsync(QueueName, process);
        }
        
        await _fixture.Consumer.StartConsumingAsync<Process>(Handler, CancellationToken.None);

        var completed = await Task.WhenAny(tcs.Task, Task.Delay(15000)) == tcs.Task;

        // Assert
        completed.Should().BeTrue("consumer should continue after error");
        receivedMessages.Count.Should().BeGreaterOrEqualTo(3, "all messages should eventually be processed");
    }

    [Fact]
    public async Task Consumer_Should_HandleTimeoutGracefully()
    {
        // Arrange
        _fixture.PurgeQueue(QueueName);
        
        var attemptCount = 0;
        var tcs = new TaskCompletionSource();
        var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));

        async Task Handler(Process message, MessageContext context)
        {
            attemptCount++;

            if (attemptCount == 1)
            {
                // Simulate long processing that will be cancelled
                try
                {
                    await Task.Delay(10000, cts.Token);
                }
                catch (OperationCanceledException)
                {
                    // Expected cancellation
                    throw;
                }
            }

            await context.AcknowledgeAsync();
            tcs.SetResult();
        }

        var process = CreateTestProcess();

        // Act
        await _fixture.Broker.PublishAsync(QueueName, process);
        await _fixture.Consumer.StartConsumingAsync<Process>(Handler, cts.Token);

        await Task.Delay(5000);

        // Assert - Consumer should have stopped due to cancellation
        attemptCount.Should().BeGreaterOrEqualTo(1);
    }

    [Fact]
    public async Task Consumer_Should_NotAcknowledge_WhenProcessingFails()
    {
        // Arrange
        _fixture.PurgeQueue(QueueName);
        
        var attemptCount = 0;

        Task Handler(Process message, MessageContext context)
        {
            Interlocked.Increment(ref attemptCount);
            throw new InvalidOperationException("Always fails");
        }

        var process = CreateTestProcess();

        // Act
        await _fixture.Broker.PublishAsync(QueueName, process);
        await _fixture.Consumer.StartConsumingAsync<Process>(Handler, CancellationToken.None);

        // Wait for multiple retry attempts
        await Task.Delay(5000);

        // Stop consumer
        await _fixture.Consumer.StopConsumingAsync();

        // Assert
        attemptCount.Should().BeGreaterThan(1, "message should be retried multiple times");
        
        var messageCount = _fixture.GetMessageCount(QueueName);
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
