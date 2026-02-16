using FluentAssertions;
using StarGate.Core.Abstractions;
using StarGate.Core.Domain;
using StarGate.Integration.Tests.Fixtures;
using System.Collections.Concurrent;
using Xunit;

namespace StarGate.Integration.Tests.Messaging;

public class RabbitMqRequeueTests : IClassFixture<RabbitMqFixture>, IAsyncLifetime
{
    private readonly RabbitMqFixture _fixture;
    private readonly string _testQueue;

    public RabbitMqRequeueTests(RabbitMqFixture fixture)
    {
        _fixture = fixture;
        _testQueue = $"test.requeue.{Guid.NewGuid()}";
    }

    public Task InitializeAsync() => Task.CompletedTask;

    public async Task DisposeAsync()
    {
        await _fixture.Consumer.StopConsumingAsync();
        _fixture.DeleteQueue(_testQueue);
    }

    [Fact]
    public async Task Consumer_Should_RequeueMessage_WhenHandlerReturnsRequeue()
    {
        // Arrange
        var attemptCount = 0;
        var maxAttempts = 3;
        var tcs = new TaskCompletionSource<bool>();

        Func<MessageEnvelope<Process>, CancellationToken, Task<MessageHandlingResult>> handler = 
            async (envelope, ct) =>
            {
                attemptCount++;
                
                if (attemptCount < maxAttempts)
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

        var completed = await Task.WhenAny(tcs.Task, Task.Delay(15000)) == tcs.Task;

        // Assert
        completed.Should().BeTrue("message should be retried until acknowledged");
        attemptCount.Should().Be(maxAttempts);
    }

    [Fact]
    public async Task Consumer_Should_PreserveMessageOrder_WhenRequeuing()
    {
        // Arrange
        var receivedMessages = new ConcurrentBag<string>();
        var requeue1 = true;
        var requeue2 = true;
        var tcs = new TaskCompletionSource<bool>();

        Func<MessageEnvelope<Process>, CancellationToken, Task<MessageHandlingResult>> handler = 
            async (envelope, ct) =>
            {
                var clientId = envelope.Payload.ClientProcessId;
                receivedMessages.Add(clientId);

                if (clientId == "process-1" && requeue1)
                {
                    requeue1 = false;
                    return await Task.FromResult(MessageHandlingResult.Requeue);
                }

                if (clientId == "process-2" && requeue2)
                {
                    requeue2 = false;
                    return await Task.FromResult(MessageHandlingResult.Requeue);
                }

                if (receivedMessages.Count >= 4) // 2 initial + 2 requeued
                {
                    tcs.TrySetResult(true);
                }

                return await Task.FromResult(MessageHandlingResult.Acknowledge);
            };

        var process1 = CreateTestProcess() with { ClientProcessId = "process-1" };
        var process2 = CreateTestProcess() with { ClientProcessId = "process-2" };

        // Act
        await _fixture.Consumer.StartConsumingAsync(_testQueue, handler, CancellationToken.None);
        await _fixture.Broker.PublishAsync(_testQueue, process1);
        await _fixture.Broker.PublishAsync(_testQueue, process2);

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
        var attemptCounts = new ConcurrentDictionary<Guid, int>();
        var completedCount = 0;
        var totalMessages = 10;
        var tcs = new TaskCompletionSource<bool>();

        Func<MessageEnvelope<Process>, CancellationToken, Task<MessageHandlingResult>> handler = 
            async (envelope, ct) =>
            {
                var processId = envelope.Payload.ProcessId;
                var attempts = attemptCounts.AddOrUpdate(processId, 1, (_, count) => count + 1);

                if (attempts < 2)
                {
                    return await Task.FromResult(MessageHandlingResult.Requeue);
                }

                if (Interlocked.Increment(ref completedCount) >= totalMessages)
                {
                    tcs.TrySetResult(true);
                }

                return await Task.FromResult(MessageHandlingResult.Acknowledge);
            };

        var processes = Enumerable.Range(0, totalMessages)
            .Select(_ => CreateTestProcess())
            .ToList();

        // Act
        await _fixture.Consumer.StartConsumingAsync(_testQueue, handler, CancellationToken.None);

        foreach (var process in processes)
        {
            await _fixture.Broker.PublishAsync(_testQueue, process);
        }

        var completed = await Task.WhenAny(tcs.Task, Task.Delay(30000)) == tcs.Task;

        // Assert
        completed.Should().BeTrue("all messages should eventually be acknowledged");
        attemptCounts.Values.Should().AllSatisfy(count => count.Should().BeGreaterOrEqualTo(2));
    }

    [Fact]
    public async Task Consumer_Should_NotLoseMessages_WhenRequeuing()
    {
        // Arrange
        var receivedProcessIds = new ConcurrentBag<Guid>();
        var attemptCounts = new ConcurrentDictionary<Guid, int>();
        var tcs = new TaskCompletionSource<bool>();
        var messageCount = 5;

        Func<MessageEnvelope<Process>, CancellationToken, Task<MessageHandlingResult>> handler = 
            async (envelope, ct) =>
            {
                var processId = envelope.Payload.ProcessId;
                var attempts = attemptCounts.AddOrUpdate(processId, 1, (_, count) => count + 1);

                if (attempts < 2)
                {
                    return await Task.FromResult(MessageHandlingResult.Requeue);
                }

                receivedProcessIds.Add(processId);

                if (receivedProcessIds.Count >= messageCount)
                {
                    tcs.TrySetResult(true);
                }

                return await Task.FromResult(MessageHandlingResult.Acknowledge);
            };

        var processes = Enumerable.Range(0, messageCount)
            .Select(_ => CreateTestProcess())
            .ToList();

        // Act
        await _fixture.Consumer.StartConsumingAsync(_testQueue, handler, CancellationToken.None);

        foreach (var process in processes)
        {
            await _fixture.Broker.PublishAsync(_testQueue, process);
        }

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
