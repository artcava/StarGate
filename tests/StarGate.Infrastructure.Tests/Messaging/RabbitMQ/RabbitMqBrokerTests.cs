namespace StarGate.Infrastructure.Tests.Messaging.RabbitMQ;

using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using RabbitMQ.Client;
using RabbitMQ.Client.Exceptions;
using StarGate.Core.Abstractions;
using StarGate.Core.Domain;
using StarGate.Core.Exceptions;
using StarGate.Infrastructure.Messaging;
using StarGate.Infrastructure.Messaging.RabbitMQ;
using Xunit;

public class RabbitMqBrokerTests : IDisposable
{
    private readonly Mock<IConnection> _connectionMock;
    private readonly Mock<IModel> _channelMock;
    private readonly Mock<IMessageSerializer> _serializerMock;
    private readonly RabbitMqOptions _options;
    private readonly RabbitMqBroker _broker;

    public RabbitMqBrokerTests()
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
            PublisherConfirms = true,
            PublisherConfirmTimeoutMs = 5000,
            Enabled = true,
            ProcessExchange = "test.exchange",
            DeadLetterExchange = "test.dlx",
            DeadLetterQueue = "test.dlq"
        };

        // Setup connection to return channel
        _connectionMock
            .Setup(c => c.CreateModel())
            .Returns(_channelMock.Object);

        // Setup channel basic properties creation
        _channelMock
            .Setup(ch => ch.CreateBasicProperties())
            .Returns(new Mock<IBasicProperties>().Object);

        // Setup publisher confirms
        _channelMock
            .Setup(ch => ch.WaitForConfirms(It.IsAny<TimeSpan>()))
            .Returns(true);

        _broker = new RabbitMqBroker(
            _connectionMock.Object,
            _serializerMock.Object,
            _options,
            NullLogger<RabbitMqBroker>.Instance);
    }

    [Fact]
    public void Constructor_Should_InitializeChannel()
    {
        // Assert
        _connectionMock.Verify(
            c => c.CreateModel(),
            Times.Once);
    }

    [Fact]
    public void Constructor_Should_EnablePublisherConfirms_WhenConfigured()
    {
        // Assert
        _channelMock.Verify(
            ch => ch.ConfirmSelect(),
            Times.Once);
    }

    [Fact]
    public void Constructor_Should_DeclareInfrastructure()
    {
        // Assert - DLX declared
        _channelMock.Verify(
            ch => ch.ExchangeDeclare(
                _options.DeadLetterExchange,
                ExchangeType.Direct,
                true,
                false,
                It.IsAny<IDictionary<string, object>>()),
            Times.Once);

        // Assert - DLQ declared
        _channelMock.Verify(
            ch => ch.QueueDeclare(
                _options.DeadLetterQueue,
                true,
                false,
                false,
                It.IsAny<IDictionary<string, object>>()),
            Times.Once);

        // Assert - Main exchange declared
        _channelMock.Verify(
            ch => ch.ExchangeDeclare(
                _options.ProcessExchange,
                ExchangeType.Direct,
                true,
                false,
                It.IsAny<IDictionary<string, object>>()),
            Times.Once);
    }

    [Fact]
    public async Task PublishAsync_Should_DeclareQueue()
    {
        // Arrange
        var queueName = "test.queue";
        var process = CreateTestProcess();
        var serializedData = new byte[] { 1, 2, 3 };

        _serializerMock
            .Setup(s => s.Serialize(It.IsAny<MessageEnvelope<Process>>()))
            .Returns(serializedData);

        // Act
        await _broker.PublishAsync(queueName, process);

        // Assert
        _channelMock.Verify(
            ch => ch.QueueDeclare(
                queueName,
                true,
                false,
                false,
                It.IsAny<IDictionary<string, object>>()),
            Times.Once);
    }

    [Fact]
    public async Task PublishAsync_Should_PublishMessage()
    {
        // Arrange
        var queueName = "test.queue";
        var process = CreateTestProcess();
        var serializedData = new byte[] { 1, 2, 3 };

        _serializerMock
            .Setup(s => s.Serialize(It.IsAny<MessageEnvelope<Process>>()))
            .Returns(serializedData);

        // Act
        await _broker.PublishAsync(queueName, process);

        // Assert
        _channelMock.Verify(
            ch => ch.BasicPublish(
                _options.ProcessExchange,
                queueName,
                true,
                It.IsAny<IBasicProperties>(),
                serializedData),
            Times.Once);
    }

    [Fact]
    public async Task PublishAsync_Should_WaitForConfirms_WhenEnabled()
    {
        // Arrange
        var queueName = "test.queue";
        var process = CreateTestProcess();
        var serializedData = new byte[] { 1, 2, 3 };

        _serializerMock
            .Setup(s => s.Serialize(It.IsAny<MessageEnvelope<Process>>()))
            .Returns(serializedData);

        // Act
        await _broker.PublishAsync(queueName, process);

        // Assert
        _channelMock.Verify(
            ch => ch.WaitForConfirms(TimeSpan.FromMilliseconds(_options.PublisherConfirmTimeoutMs)),
            Times.Once);
    }

    [Fact]
    public async Task PublishAsync_Should_ThrowBrokerException_WhenConfirmFails()
    {
        // Arrange
        var queueName = "test.queue";
        var process = CreateTestProcess();
        var serializedData = new byte[] { 1, 2, 3 };

        _serializerMock
            .Setup(s => s.Serialize(It.IsAny<MessageEnvelope<Process>>()))
            .Returns(serializedData);

        _channelMock
            .Setup(ch => ch.WaitForConfirms(It.IsAny<TimeSpan>()))
            .Returns(false); // Confirm failed

        // Act
        Func<Task> act = async () => await _broker.PublishAsync(queueName, process);

        // Assert
        await act.Should().ThrowAsync<BrokerException>()
            .WithMessage("*Failed to confirm*");
    }

    [Fact]
    public async Task PublishAsync_Should_ThrowBrokerException_WhenChannelClosed()
    {
        // Arrange
        var queueName = "test.queue";
        var process = CreateTestProcess();

        _channelMock
            .Setup(ch => ch.QueueDeclare(
                It.IsAny<string>(),
                It.IsAny<bool>(),
                It.IsAny<bool>(),
                It.IsAny<bool>(),
                It.IsAny<IDictionary<string, object>>()))
            .Throws<AlreadyClosedException>();

        // Act
        Func<Task> act = async () => await _broker.PublishAsync(queueName, process);

        // Assert
        await act.Should().ThrowAsync<BrokerException>()
            .WithMessage("*Connection closed*");
    }

    [Fact]
    public async Task PublishAsync_Should_ThrowBrokerException_WhenOperationInterrupted()
    {
        // Arrange
        var queueName = "test.queue";
        var process = CreateTestProcess();
        var shutdownArgs = new ShutdownEventArgs(
            ShutdownInitiator.Application,
            501,
            "Test shutdown");

        _channelMock
            .Setup(ch => ch.BasicPublish(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<bool>(),
                It.IsAny<IBasicProperties>(),
                It.IsAny<ReadOnlyMemory<byte>>()))
            .Throws(new OperationInterruptedException(shutdownArgs));

        _serializerMock
            .Setup(s => s.Serialize(It.IsAny<MessageEnvelope<Process>>()))
            .Returns(new byte[] { 1, 2, 3 });

        // Act
        Func<Task> act = async () => await _broker.PublishAsync(queueName, process);

        // Assert
        await act.Should().ThrowAsync<BrokerException>()
            .WithMessage("*Operation interrupted*");
    }

    [Fact]
    public async Task PublishAsync_Should_ThrowArgumentException_WhenQueueNameEmpty()
    {
        // Arrange
        var process = CreateTestProcess();

        // Act
        Func<Task> act = async () => await _broker.PublishAsync("", process);

        // Assert
        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task PublishAsync_Should_ThrowArgumentNull_WhenMessageNull()
    {
        // Arrange
        var queueName = "test.queue";

        // Act
        Func<Task> act = async () => await _broker.PublishAsync<Process>(queueName, null!);

        // Assert
        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task PublishAsync_Should_UseMessageProperties_WhenProvided()
    {
        // Arrange
        var queueName = "test.queue";
        var process = CreateTestProcess();
        var properties = new MessageProperties
        {
            MessageId = "msg-123",
            CorrelationId = "corr-456",
            Priority = 5,
            TimeToLive = TimeSpan.FromMinutes(10)
        };

        var basicPropertiesMock = new Mock<IBasicProperties>();
        _channelMock
            .Setup(ch => ch.CreateBasicProperties())
            .Returns(basicPropertiesMock.Object);

        _serializerMock
            .Setup(s => s.Serialize(It.IsAny<MessageEnvelope<Process>>()))
            .Returns(new byte[] { 1, 2, 3 });

        // Act
        await _broker.PublishAsync(queueName, process, properties);

        // Assert
        basicPropertiesMock.VerifySet(p => p.MessageId = "msg-123", Times.Once);
        basicPropertiesMock.VerifySet(p => p.CorrelationId = "corr-456", Times.Once);
        basicPropertiesMock.VerifySet(p => p.Priority = 5, Times.Once);
    }

    [Fact]
    public void Dispose_Should_CloseChannel()
    {
        // Act
        _broker.Dispose();

        // Assert
        _channelMock.Verify(ch => ch.Close(), Times.Once);
        _channelMock.Verify(ch => ch.Dispose(), Times.Once);
    }

    [Fact]
    public void Dispose_Should_BeIdempotent()
    {
        // Act
        _broker.Dispose();
        _broker.Dispose();
        _broker.Dispose();

        // Assert
        _channelMock.Verify(ch => ch.Close(), Times.Once);
        _channelMock.Verify(ch => ch.Dispose(), Times.Once);
    }

    public void Dispose()
    {
        _broker?.Dispose();
    }

    private static Process CreateTestProcess() => new()
    {
        ProcessId = Guid.NewGuid(),
        ClientProcessId = "client-123",
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
