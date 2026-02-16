using System.Text.Json;
using FluentAssertions;
using RabbitMQ.Client;
using StarGate.Core.Abstractions;
using StarGate.Core.Domain;
using StarGate.Integration.Tests.Fixtures;
using Xunit;

namespace StarGate.Integration.Tests.Messaging;

public class RabbitMqDeadLetterQueueTests : IClassFixture<RabbitMqFixture>, IAsyncLifetime
{
    private readonly RabbitMqFixture _fixture;
    private readonly string _testQueue;

    public RabbitMqDeadLetterQueueTests(RabbitMqFixture fixture)
    {
        _fixture = fixture;
        _testQueue = $"test.dlq.{Guid.NewGuid()}";
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
        
        _fixture.DeleteQueue(_testQueue);
        _fixture.PurgeQueue(_fixture.Options.DeadLetterQueue);
    }

    [Fact]
    public async Task Consumer_Should_SendToDLQ_WhenHandlerRejects()
    {
        // Arrange
        var tcs = new TaskCompletionSource();

        async Task Handler(Process message, MessageContext context)
        {
            await context.RejectAsync(false); // requeue = false, send to DLQ
            tcs.SetResult();
        }

        var process = CreateTestProcess();

        // Act - Publish FIRST to create queue
        await _fixture.Broker.PublishAsync(_testQueue, process);
        await _fixture.Consumer.StartConsumingAsync<Process>(Handler, CancellationToken.None);

        var consumed = await Task.WhenAny(tcs.Task, Task.Delay(5000)) == tcs.Task;

        // Assert
        consumed.Should().BeTrue();

        // Wait for message to be routed to DLQ
        await Task.Delay(1000);

        var dlqMessageCount = _fixture.GetMessageCount(_fixture.Options.DeadLetterQueue);
        dlqMessageCount.Should().Be(1, "rejected message should be in DLQ");
    }

    [Fact]
    public async Task Consumer_Should_SendMultipleMessagesToDLQ_WhenRejected()
    {
        // Arrange
        var rejectedCount = 0;
        var expectedRejects = 5;
        var tcs = new TaskCompletionSource();

        async Task Handler(Process message, MessageContext context)
        {
            await context.RejectAsync(false); // requeue = false, send to DLQ
            if (Interlocked.Increment(ref rejectedCount) >= expectedRejects)
            {
                tcs.SetResult();
            }
        }

        var processes = Enumerable.Range(0, expectedRejects)
            .Select(_ => CreateTestProcess())
            .ToList();

        // Act - Publish first message to create queue
        await _fixture.Broker.PublishAsync(_testQueue, processes[0]);
        await _fixture.Consumer.StartConsumingAsync<Process>(Handler, CancellationToken.None);

        // Publish remaining messages
        foreach (var process in processes.Skip(1))
        {
            await _fixture.Broker.PublishAsync(_testQueue, process);
        }

        var completed = await Task.WhenAny(tcs.Task, Task.Delay(10000)) == tcs.Task;

        // Assert
        completed.Should().BeTrue();

        await Task.Delay(2000);
        var dlqMessageCount = _fixture.GetMessageCount(_fixture.Options.DeadLetterQueue);
        dlqMessageCount.Should().Be((uint)expectedRejects);
    }

    [Fact]
    public async Task Consumer_Should_PreserveMessageData_InDLQ()
    {
        // Arrange
        var tcs = new TaskCompletionSource();

        async Task Handler(Process message, MessageContext context)
        {
            await context.RejectAsync(false); // requeue = false, send to DLQ
            tcs.SetResult();
        }

        var originalProcess = CreateTestProcess() with
        {
            ClientProcessId = "POISON-MESSAGE-123",
            Data = JsonDocument.Parse("{\"reason\":\"test rejection\"}")
        };

        // Act - Publish FIRST to create queue
        await _fixture.Broker.PublishAsync(_testQueue, originalProcess);
        await _fixture.Consumer.StartConsumingAsync<Process>(Handler, CancellationToken.None);

        var consumed = await Task.WhenAny(tcs.Task, Task.Delay(5000)) == tcs.Task;

        // Assert
        consumed.Should().BeTrue();
        await Task.Delay(1000);

        // Verify message in DLQ
        using var channel = _fixture.Connection.CreateModel();
        var result = channel.BasicGet(_fixture.Options.DeadLetterQueue, false);
        
        result.Should().NotBeNull("message should be in DLQ");
        
        // Deserialize message from DLQ - returns MessageEnvelope<Process>
        var envelope = _fixture.Serializer.Deserialize<Process>(result!.Body.ToArray());
        envelope.Payload.ClientProcessId.Should().Be("POISON-MESSAGE-123");
        
        // Clean up - acknowledge the DLQ message
        channel.BasicAck(result.DeliveryTag, false);
    }

    [Fact]
    public async Task ExpiredMessages_Should_GoToDLQ()
    {
        // Arrange
        var process = CreateTestProcess();
        var properties = new MessageProperties
        {
            TimeToLive = TimeSpan.FromMilliseconds(100)
        };

        // Act - Publish with short TTL but don't consume
        await _fixture.Broker.PublishAsync(_testQueue, process, properties);

        // Wait for expiration
        await Task.Delay(500);

        // Assert - Message should be in DLQ due to expiration
        var mainQueueCount = _fixture.GetMessageCount(_testQueue);
        mainQueueCount.Should().Be(0, "expired message should not be in main queue");

        var dlqMessageCount = _fixture.GetMessageCount(_fixture.Options.DeadLetterQueue);
        dlqMessageCount.Should().Be(1, "expired message should be in DLQ");

        // Clean up
        _fixture.PurgeQueue(_fixture.Options.DeadLetterQueue);
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
