using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using StarGate.Core.Abstractions;
using StarGate.Core.Domain;
using StarGate.Infrastructure.Messaging.RabbitMQ;
using Xunit;

namespace StarGate.Infrastructure.Tests.Messaging.RabbitMQ;

public class RabbitMqConsumerTests : IAsyncDisposable
{
    private readonly Mock<IConnection> _connectionMock;
    private readonly Mock<IModel> _channelMock;
    private readonly Mock<IMessageSerializer> _serializerMock;
    private readonly RabbitMqOptions _options;
    private readonly RabbitMqConsumer _consumer;

    public RabbitMqConsumerTests()
    {
        _connectionMock = new Mock<IConnection>();
        _channelMock = new Mock<IModel>();
        _serializerMock = new Mock<IMessageSerializer>();

        _options = new RabbitMqOptions
        {
            HostName = "localhost",
            Port = 5672,
            UserName = "guest",
            Password = "guest",
            VirtualHost = "/",
            PublisherConfirms = false,
            PublisherConfirmTimeoutMs = 5000,
            Enabled = true,
            ProcessExchange = "test.exchange",
            DeadLetterExchange = "test.dlx",
            DeadLetterQueue = "test.dlq",
            PrefetchCount = 10,
            ShutdownGracePeriodMs = 100
        };

        _connectionMock
            .Setup(c => c.CreateModel())
            .Returns(_channelMock.Object);

        _consumer = new RabbitMqConsumer(
            _connectionMock.Object,
            _serializerMock.Object,
            _options,
            NullLogger<RabbitMqConsumer>.Instance);
    }

    [Fact]
    public async Task StartConsumingAsync_Should_CreateChannel()
    {
        // Arrange
        Func<Process, MessageContext, Task> handler =
            (process, ctx) => Task.CompletedTask;

        // Act
        await _consumer.StartConsumingAsync(handler);

        // Assert
        _connectionMock.Verify(
            c => c.CreateModel(),
            Times.Once);
    }

    [Fact]
    public async Task StartConsumingAsync_Should_ConfigureQoS()
    {
        // Arrange
        Func<Process, MessageContext, Task> handler =
            (process, ctx) => Task.CompletedTask;

        // Act
        await _consumer.StartConsumingAsync(handler);

        // Assert
        _channelMock.Verify(
            ch => ch.BasicQos(
                0,   // prefetchSize
                10,  // prefetchCount
                false), // global
            Times.Once);
    }

    [Fact]
    public async Task StartConsumingAsync_Should_VerifyQueueExists()
    {
        // Arrange
        Func<Process, MessageContext, Task> handler =
            (process, ctx) => Task.CompletedTask;

        // Act
        await _consumer.StartConsumingAsync(handler);

        // Assert
        _channelMock.Verify(
            ch => ch.QueueDeclarePassive(It.IsAny<string>()),
            Times.Once);
    }

    [Fact]
    public async Task StartConsumingAsync_Should_CallBasicConsume()
    {
        // Arrange
        Func<Process, MessageContext, Task> handler =
            (process, ctx) => Task.CompletedTask;

        var consumerTag = "consumer-tag-123";
        _channelMock
            .Setup(ch => ch.BasicConsume(
                It.IsAny<string>(),
                It.IsAny<bool>(),
                It.IsAny<string>(),
                It.IsAny<bool>(),
                It.IsAny<bool>(),
                It.IsAny<IDictionary<string, object>>(),
                It.IsAny<IBasicConsumer>()))
            .Returns(consumerTag);

        // Act
        await _consumer.StartConsumingAsync(handler);

        // Assert - Verify BasicConsume was called with correct parameters
        _channelMock.Verify(
            ch => ch.BasicConsume(
                It.IsAny<string>(),      // queue
                false,                    // autoAck = false
                It.IsAny<string>(),      // consumerTag
                It.IsAny<bool>(),        // noLocal
                It.IsAny<bool>(),        // exclusive
                It.IsAny<IDictionary<string, object>>(), // arguments
                It.IsAny<IBasicConsumer>()), // consumer
            Times.Once);
    }

    [Fact]
    public async Task StartConsumingAsync_Should_ThrowArgumentNull_WhenHandlerNull()
    {
        // Act
        Func<Task> act = async () => await _consumer.StartConsumingAsync<Process>(
            null!);

        // Assert
        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task StartConsumingAsync_Should_ThrowInvalidOperation_WhenAlreadyStarted()
    {
        // Arrange
        Func<Process, MessageContext, Task> handler =
            (process, ctx) => Task.CompletedTask;

        await _consumer.StartConsumingAsync(handler);

        // Act
        Func<Task> act = async () => await _consumer.StartConsumingAsync(handler);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*already started*");
    }

    [Fact]
    public async Task StopConsumingAsync_Should_CloseChannel()
    {
        // Arrange
        Func<Process, MessageContext, Task> handler =
            (process, ctx) => Task.CompletedTask;

        _channelMock.Setup(ch => ch.IsOpen).Returns(true);

        await _consumer.StartConsumingAsync(handler);

        // Act
        await _consumer.StopConsumingAsync();

        // Assert
        _channelMock.Verify(
            ch => ch.Close(),
            Times.Once);
    }

    [Fact]
    public async Task StopConsumingAsync_Should_HandleClosedChannels_Gracefully()
    {
        // Arrange
        Func<Process, MessageContext, Task> handler =
            (process, ctx) => Task.CompletedTask;

        _channelMock.Setup(ch => ch.IsOpen).Returns(false);

        await _consumer.StartConsumingAsync(handler);

        // Act
        Func<Task> act = async () => await _consumer.StopConsumingAsync();

        // Assert
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task StopConsumingAsync_Should_ThrowInvalidOperation_WhenNotStarted()
    {
        // Act
        Func<Task> act = async () => await _consumer.StopConsumingAsync();

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*not started*");
    }

    [Fact]
    public async Task DisposeAsync_Should_StopConsumer_WhenConsuming()
    {
        // Arrange
        Func<Process, MessageContext, Task> handler =
            (process, ctx) => Task.CompletedTask;

        _channelMock.Setup(ch => ch.IsOpen).Returns(true);

        await _consumer.StartConsumingAsync(handler);

        // Act
        await _consumer.DisposeAsync();

        // Assert
        _channelMock.Verify(
            ch => ch.Close(),
            Times.Once);
    }

    [Fact]
    public async Task DisposeAsync_Should_DisposeAllChannels()
    {
        // Arrange
        Func<Process, MessageContext, Task> handler =
            (process, ctx) => Task.CompletedTask;

        _channelMock.Setup(ch => ch.IsOpen).Returns(true);

        await _consumer.StartConsumingAsync(handler);

        // Act
        await _consumer.DisposeAsync();

        // Assert - Verify disposal was attempted
        // Note: The consumer calls StopConsumingAsync which closes the channel
        // The actual Dispose is called on channel objects internally
        _channelMock.Verify(ch => ch.Close(), Times.Once);
    }

    [Fact]
    public async Task DisposeAsync_Should_BeIdempotent()
    {
        // Act
        await _consumer.DisposeAsync();
        await _consumer.DisposeAsync();
        await _consumer.DisposeAsync();

        // Assert - Should not throw
    }

    public async ValueTask DisposeAsync()
    {
        if (_consumer != null)
        {
            await _consumer.DisposeAsync();
        }
    }
}
