namespace StarGate.Integration.Tests.Messaging;

using FluentAssertions;
using RabbitMQ.Client;
using StarGate.Core.Abstractions;
using StarGate.Core.Domain;
using StarGate.Integration.Tests.Fixtures;
using Xunit;

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
        await _fixture.Consumer.StopConsumingAsync();
        _fixture.DeleteQueue(_testQueue);
        _fixture.PurgeQueue(_fixture.Options.DeadLetterQueue);
    }

    [Fact]
    public async Task Consumer_Should_SendToDLQ_WhenHandlerReturnsReject()
    {
        // Arrange
        var tcs = new TaskCompletionSource<bool>();

        Func<MessageEnvelope<Process>, CancellationToken, Task<MessageHandlingResult>> handler = 
            async (envelope, ct) =>
            {
                tcs.TrySetResult(true);
                return await Task.FromResult(MessageHandlingResult.Reject);
            };

        var process = CreateTestProcess();

        // Act
        await _fixture.Broker.PublishAsync(_testQueue, process);
        await _fixture.Consumer.StartConsumingAsync(_testQueue, handler, CancellationToken.None);

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
        var tcs = new TaskCompletionSource<bool>();

        Func<MessageEnvelope<Process>, CancellationToken, Task<MessageHandlingResult>> handler = 
            async (envelope, ct) =>
            {
                if (Interlocked.Increment(ref rejectedCount) >= expectedRejects)
                {
                    tcs.TrySetResult(true);
                }
                return await Task.FromResult(MessageHandlingResult.Reject);
            };

        var processes = Enumerable.Range(0, expectedRejects)
            .Select(_ => CreateTestProcess())
            .ToList();

        // Act
        await _fixture.Consumer.StartConsumingAsync(_testQueue, handler, CancellationToken.None);

        foreach (var process in processes)
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
        var tcs = new TaskCompletionSource<bool>();

        Func<MessageEnvelope<Process>, CancellationToken, Task<MessageHandlingResult>> handler = 
            async (envelope, ct) =>
            {
                tcs.TrySetResult(true);
                return await Task.FromResult(MessageHandlingResult.Reject);
            };

        var originalProcess = CreateTestProcess() with
        {
            ClientProcessId = "POISON-MESSAGE-123",
            Data = new { reason = "test rejection" }
        };

        // Act
        await _fixture.Broker.PublishAsync(_testQueue, originalProcess);
        await _fixture.Consumer.StartConsumingAsync(_testQueue, handler, CancellationToken.None);

        var consumed = await Task.WhenAny(tcs.Task, Task.Delay(5000)) == tcs.Task;

        // Assert
        consumed.Should().BeTrue();
        await Task.Delay(1000);

        // Verify message in DLQ
        using var channel = _fixture.Connection.CreateModel();
        var result = channel.BasicGet(_fixture.Options.DeadLetterQueue, false);
        
        result.Should().NotBeNull("message should be in DLQ");
        
        var envelope = _fixture.Serializer.DeserializeEnvelope<Process>(result!.Body.ToArray());
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
            Expiration = TimeSpan.FromMilliseconds(100)
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
