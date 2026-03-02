using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using StarGate.Core.Abstractions;
using StarGate.Core.Messages;
using StarGate.Server.Workers;
using Xunit;

namespace StarGate.Server.Tests.Workers;

public class ProcessWorkerTests
{
    private readonly Mock<IMessageConsumer> _consumerMock;
    private readonly Mock<IProcessService> _processServiceMock;
    private readonly Mock<IProcessHandlerFactory> _handlerFactoryMock;
    private readonly ProcessWorker _worker;

    public ProcessWorkerTests()
    {
        _consumerMock = new Mock<IMessageConsumer>();
        _processServiceMock = new Mock<IProcessService>();
        _handlerFactoryMock = new Mock<IProcessHandlerFactory>();

        _worker = new ProcessWorker(
            _consumerMock.Object,
            _processServiceMock.Object,
            _handlerFactoryMock.Object,
            NullLogger<ProcessWorker>.Instance);
    }

    [Fact]
    public void Constructor_Should_ThrowArgumentNullException_WhenConsumerIsNull()
    {
        // Act
        Action act = () => new ProcessWorker(
            null!,
            _processServiceMock.Object,
            _handlerFactoryMock.Object,
            NullLogger<ProcessWorker>.Instance);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("messageConsumer");
    }

    [Fact]
    public void Constructor_Should_ThrowArgumentNullException_WhenProcessServiceIsNull()
    {
        // Act
        Action act = () => new ProcessWorker(
            _consumerMock.Object,
            null!,
            _handlerFactoryMock.Object,
            NullLogger<ProcessWorker>.Instance);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("processService");
    }

    [Fact]
    public void Constructor_Should_ThrowArgumentNullException_WhenHandlerFactoryIsNull()
    {
        // Act
        Action act = () => new ProcessWorker(
            _consumerMock.Object,
            _processServiceMock.Object,
            null!,
            NullLogger<ProcessWorker>.Instance);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("handlerFactory");
    }

    [Fact]
    public void Constructor_Should_ThrowArgumentNullException_WhenLoggerIsNull()
    {
        // Act
        Action act = () => new ProcessWorker(
            _consumerMock.Object,
            _processServiceMock.Object,
            _handlerFactoryMock.Object,
            null!);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("logger");
    }

    [Fact]
    public void Constructor_Should_CreateInstance_WhenAllParametersAreValid()
    {
        // Act
        var worker = new ProcessWorker(
            _consumerMock.Object,
            _processServiceMock.Object,
            _handlerFactoryMock.Object,
            NullLogger<ProcessWorker>.Instance);

        // Assert
        worker.Should().NotBeNull();
    }

    [Fact]
    public async Task ExecuteAsync_Should_StartConsumer_WhenCalled()
    {
        // Arrange
        var cts = new CancellationTokenSource();
        cts.CancelAfter(TimeSpan.FromMilliseconds(100));

        _consumerMock
            .Setup(x => x.StartConsumingAsync<ProcessMessage>(
                It.IsAny<Func<ProcessMessage, MessageContext, Task>>(),
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _consumerMock
            .Setup(x => x.StopConsumingAsync())
            .Returns(Task.CompletedTask);

        // Act
        try
        {
            await _worker.StartAsync(cts.Token);
            await Task.Delay(TimeSpan.FromMilliseconds(200), CancellationToken.None);
            await _worker.StopAsync(CancellationToken.None);
        }
        catch (OperationCanceledException)
        {
            // Expected when cancellation token is triggered
        }

        // Assert
        _consumerMock.Verify(
            x => x.StartConsumingAsync<ProcessMessage>(
                It.IsAny<Func<ProcessMessage, MessageContext, Task>>(),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_Should_StopConsumer_WhenStopping()
    {
        // Arrange
        var cts = new CancellationTokenSource();
        cts.CancelAfter(TimeSpan.FromMilliseconds(100));

        _consumerMock
            .Setup(x => x.StartConsumingAsync<ProcessMessage>(
                It.IsAny<Func<ProcessMessage, MessageContext, Task>>(),
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _consumerMock
            .Setup(x => x.StopConsumingAsync())
            .Returns(Task.CompletedTask);

        // Act
        try
        {
            await _worker.StartAsync(cts.Token);
            await Task.Delay(TimeSpan.FromMilliseconds(200), CancellationToken.None);
            await _worker.StopAsync(CancellationToken.None);
        }
        catch (OperationCanceledException)
        {
            // Expected when cancellation token is triggered
        }

        // Assert
        _consumerMock.Verify(
            x => x.StopConsumingAsync(),
            Times.Once);
    }

    [Fact]
    public void Dispose_Should_DisposeResources_WhenCalled()
    {
        // Act
        Action act = () => _worker.Dispose();

        // Assert
        act.Should().NotThrow();
    }
}
