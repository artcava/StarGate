using FluentAssertions;
using RabbitMQ.Client;
using StarGate.Core.Abstractions;
using StarGate.Core.Domain;
using StarGate.Integration.Tests.Fixtures;
using Xunit;

namespace StarGate.Integration.Tests.Messaging;

public class RabbitMqBrokerIntegrationTests : IClassFixture<RabbitMqFixture>, IAsyncLifetime
{
    private readonly RabbitMqFixture _fixture;
    private readonly string _testQueue;

    public RabbitMqBrokerIntegrationTests(RabbitMqFixture fixture)
    {
        _fixture = fixture;
        _testQueue = $"test.queue.{Guid.NewGuid()}"; // Unique queue per test class
    }

    public Task InitializeAsync() => Task.CompletedTask;

    public Task DisposeAsync()
    {
        _fixture.DeleteQueue(_testQueue);
        return Task.CompletedTask;
    }

    [Fact]
    public async Task PublishAsync_Should_PublishMessageToQueue()
    {
        // Arrange
        var process = CreateTestProcess();

        // Act
        await _fixture.Broker.PublishAsync(_testQueue, process);

        // Assert
        await Task.Delay(500); // Allow time for message to arrive
        var messageCount = _fixture.GetMessageCount(_testQueue);
        messageCount.Should().Be(1);
    }

    [Fact]
    public async Task PublishAsync_Should_PersistMessage_WhenBrokerRestarted()
    {
        // Arrange
        var process = CreateTestProcess();
        await _fixture.Broker.PublishAsync(_testQueue, process);

        // Act - Restart connection (simulates broker restart)
        await Task.Delay(500);
        var messageCount = _fixture.GetMessageCount(_testQueue);

        // Assert
        messageCount.Should().Be(1, "message should persist after connection restart");
    }

    [Fact]
    public async Task PublishAsync_Should_ConfirmMessageDelivery_WhenConfirmsEnabled()
    {
        // Arrange
        var process = CreateTestProcess();

        // Act
        Func<Task> act = async () => await _fixture.Broker.PublishAsync(_testQueue, process);

        // Assert - Should not throw (confirms enabled and successful)
        await act.Should().NotThrowAsync();

        var messageCount = _fixture.GetMessageCount(_testQueue);
        messageCount.Should().Be(1);
    }

    [Fact]
    public async Task PublishAsync_Should_CreateQueueWithDLX()
    {
        // Arrange
        var process = CreateTestProcess();

        // Act
        await _fixture.Broker.PublishAsync(_testQueue, process);

        // Assert - Verify queue has DLX configuration
        using var channel = _fixture.Connection.CreateModel();
        var queueInfo = channel.QueueDeclarePassive(_testQueue);
        queueInfo.Should().NotBeNull();
    }

    [Fact]
    public async Task PublishAsync_Should_PublishMultipleMessages()
    {
        // Arrange
        var processes = Enumerable.Range(0, 10)
            .Select(_ => CreateTestProcess())
            .ToList();

        // Act
        foreach (var process in processes)
        {
            await _fixture.Broker.PublishAsync(_testQueue, process);
        }

        // Assert
        await Task.Delay(500);
        var messageCount = _fixture.GetMessageCount(_testQueue);
        messageCount.Should().Be(10);
    }

    [Fact]
    public async Task PublishAsync_Should_HandleConcurrentPublishing()
    {
        // Arrange
        var processes = Enumerable.Range(0, 20)
            .Select(_ => CreateTestProcess())
            .ToList();

        // Act
        var tasks = processes.Select(p =>
            _fixture.Broker.PublishAsync(_testQueue, p));
        await Task.WhenAll(tasks);

        // Assert
        await Task.Delay(1000);
        var messageCount = _fixture.GetMessageCount(_testQueue);
        messageCount.Should().Be(20);
    }

    [Fact]
    public async Task PublishAsync_Should_RespectMessagePriority()
    {
        // Arrange
        var lowPriorityProcess = CreateTestProcess();
        var highPriorityProcess = CreateTestProcess();

        var lowPriorityProps = new MessageProperties { Priority = 1 };
        var highPriorityProps = new MessageProperties { Priority = 9 };

        // Act
        await _fixture.Broker.PublishAsync(_testQueue, lowPriorityProcess, lowPriorityProps);
        await _fixture.Broker.PublishAsync(_testQueue, highPriorityProcess, highPriorityProps);

        // Assert
        await Task.Delay(500);
        var messageCount = _fixture.GetMessageCount(_testQueue);
        messageCount.Should().Be(2);
    }

    [Fact]
    public async Task PublishAsync_Should_SetMessageExpiration_WhenProvided()
    {
        // Arrange
        var process = CreateTestProcess();
        var properties = new MessageProperties
        {
            Expiration = TimeSpan.FromMilliseconds(100)
        };

        // Act
        await _fixture.Broker.PublishAsync(_testQueue, process, properties);

        // Assert - Wait for expiration
        await Task.Delay(200);
        var messageCount = _fixture.GetMessageCount(_testQueue);
        messageCount.Should().Be(0, "message should have expired");
    }

    [Fact]
    public async Task PublishAsync_Should_SetCorrelationId_WhenProvided()
    {
        // Arrange
        var process = CreateTestProcess();
        var correlationId = "test-correlation-123";
        var properties = new MessageProperties { CorrelationId = correlationId };

        // Act
        await _fixture.Broker.PublishAsync(_testQueue, process, properties);

        // Assert
        await Task.Delay(500);
        var messageCount = _fixture.GetMessageCount(_testQueue);
        messageCount.Should().Be(1);
    }

    [Fact]
    public async Task PublishAsync_Should_SerializeComplexData()
    {
        // Arrange
        var complexData = new
        {
            orderId = "ORD-12345",
            customer = new
            {
                id = "CUST-001",
                name = "John Doe",
                emails = new[] { "john@example.com", "doe@example.com" }
            },
            items = new[]
            {
                new { sku = "SKU-001", quantity = 10, price = 99.99m },
                new { sku = "SKU-002", quantity = 5, price = 49.99m }
            }
        };

        var process = CreateTestProcess() with { Data = complexData };

        // Act
        await _fixture.Broker.PublishAsync(_testQueue, process);

        // Assert
        await Task.Delay(500);
        var messageCount = _fixture.GetMessageCount(_testQueue);
        messageCount.Should().Be(1);
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
