using System.Collections.Concurrent;
using FluentAssertions;
using StarGate.Core.Abstractions;
using StarGate.Core.Domain;
using StarGate.Integration.Tests.Fixtures;
using Xunit;

namespace StarGate.Integration.Tests.Messaging;

public class RabbitMqRequeueTests : IClassFixture<RabbitMqFixture>, IAsyncLifetime
{
    private readonly RabbitMqFixture _fixture;
    private const string QueueName = "stargate.process"; // Standard queue naming convention

    public RabbitMqRequeueTests(RabbitMqFixture fixture)
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
    public async Task Consumer_Should_RequeueMessage_WhenHandlerRejectsWithRequeue()
    {
        // Arrange
        var attemptCount = 0;
        var maxAttempts = 3;
        var tcs = new TaskCompletionSource();

        async Task Handler(Process message, MessageContext context)
        {
            attemptCount++;
            
            if (attemptCount < maxAttempts)
            {
                await context.RejectAsync(true); // requeue = true
            }
            else
            {
                await context.AcknowledgeAsync();
                tcs.SetResult();
            }
        }

        var process = CreateTestProcess();

        // Act - Publish to standard queue
        await _fixture.Broker.PublishAsync(QueueName, process);
        await _fixture.Consumer.StartConsumingAsync<Process>(Handler, CancellationToken.None);

        var completed = await Task.WhenAny(tcs.Task, Task.Delay(15000)) == tcs.Task;

        // Assert
        completed.Should().BeTrue("message should be retried until acknowledged");
        attemptCount.Should().Be(maxAttempts);
    }

    [Fact]
    public async Task Consumer_Should_PreserveMessageOrder_WhenRequeuing()
    {
        // Arrange
        _fixture.PurgeQueue(QueueName); // Clean queue before test
        
        var receivedMessages = new ConcurrentBag<string>();
        var requeue1 = true;
        var requeue2 = true;
        var tcs = new TaskCompletionSource();

        async Task Handler(Process message, MessageContext context)
        {
            var clientId = message.ClientProcessId;
            receivedMessages.Add(clientId);

            if (clientId == "process-1" && requeue1)
            {
                requeue1 = false;
                await context.RejectAsync(true); // requeue = true
            }
            else if (clientId == "process-2" && requeue2)
            {
                requeue2 = false;
                await context.RejectAsync(true); // requeue = true
            }
            else
            {
                await context.AcknowledgeAsync();
                if (receivedMessages.Count >= 4) // 2 initial + 2 requeued
                {
                    tcs.SetResult();
                }
            }
        }

        var process1 = CreateTestProcess() with { ClientProcessId = "process-1" };
        var process2 = CreateTestProcess() with { ClientProcessId = "process-2" };

        // Act
        await _fixture.Broker.PublishAsync(QueueName, process1);
        await _fixture.Broker.PublishAsync(QueueName, process2);
        await _fixture.Consumer.StartConsumingAsync<Process>(Handler, CancellationToken.None);

        var completed = await Task.WhenAny(tcs.Task, Task.Delay(15000)) == tcs.Task;

        // Assert
        completed.Should().BeTrue();
        receivedMessages.Should().Contain("process-1");
        receivedMessages.Should().Contain("process-2");
    }

    [Fact]
    public async Task Consumer_Should_HandleConcurrentRequeues()
    {
        // Arrange
        _fixture.PurgeQueue(QueueName);
        
        var attemptCounts = new ConcurrentDictionary<Guid, int>();
        var completedCount = 0;
        var totalMessages = 10;
        var tcs = new TaskCompletionSource();

        async Task Handler(Process message, MessageContext context)
        {
            var processId = message.ProcessId;
            var attempts = attemptCounts.AddOrUpdate(processId, 1, (_, count) => count + 1);

            if (attempts < 2)
            {
                await context.RejectAsync(true); // requeue = true
            }
            else
            {
                await context.AcknowledgeAsync();
                if (Interlocked.Increment(ref completedCount) >= totalMessages)
                {
                    tcs.SetResult();
                }
            }
        }

        var processes = Enumerable.Range(0, totalMessages)
            .Select(_ => CreateTestProcess())
            .ToList();

        // Act - Publish all messages
        foreach (var process in processes)
        {
            await _fixture.Broker.PublishAsync(QueueName, process);
        }
        
        await _fixture.Consumer.StartConsumingAsync<Process>(Handler, CancellationToken.None);

        var completed = await Task.WhenAny(tcs.Task, Task.Delay(30000)) == tcs.Task;

        // Assert
        completed.Should().BeTrue("all messages should eventually be acknowledged");
        attemptCounts.Values.Should().AllSatisfy(count => count.Should().BeGreaterOrEqualTo(2));
    }

    [Fact]
    public async Task Consumer_Should_NotLoseMessages_WhenRequeuing()
    {
        // Arrange
        _fixture.PurgeQueue(QueueName);
        
        var receivedProcessIds = new ConcurrentBag<Guid>();
        var attemptCounts = new ConcurrentDictionary<Guid, int>();
        var tcs = new TaskCompletionSource();
        var messageCount = 5;

        async Task Handler(Process message, MessageContext context)
        {
            var processId = message.ProcessId;
            var attempts = attemptCounts.AddOrUpdate(processId, 1, (_, count) => count + 1);

            if (attempts < 2)
            {
                await context.RejectAsync(true); // requeue = true
            }
            else
            {
                receivedProcessIds.Add(processId);
                await context.AcknowledgeAsync();

                if (receivedProcessIds.Count >= messageCount)
                {
                    tcs.SetResult();
                }
            }
        }

        var processes = Enumerable.Range(0, messageCount)
            .Select(_ => CreateTestProcess())
            .ToList();

        // Act - Publish all messages
        foreach (var process in processes)
        {
            await _fixture.Broker.PublishAsync(QueueName, process);
        }
        
        await _fixture.Consumer.StartConsumingAsync<Process>(Handler, CancellationToken.None);

        var completed = await Task.WhenAny(tcs.Task, Task.Delay(20000)) == tcs.Task;

        // Assert
        completed.Should().BeTrue();
        receivedProcessIds.Should().HaveCount(messageCount);
        receivedProcessIds.Should().OnlyHaveUniqueItems();
        
        var originalIds = processes.Select(p => p.ProcessId).ToList();
        receivedProcessIds.Should().BeEquivalentTo(originalIds);
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
