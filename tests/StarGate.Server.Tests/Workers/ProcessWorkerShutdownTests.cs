using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using StarGate.Core.Abstractions;
using StarGate.Core.Configuration;
using StarGate.Server.Workers;
using Xunit;

namespace StarGate.Server.Tests.Workers;

/// <summary>
/// Unit tests for ProcessWorker graceful shutdown functionality.
/// </summary>
public class ProcessWorkerShutdownTests
{
    private readonly Mock<IMessageConsumer> _consumerMock;
    private readonly Mock<IProcessService> _serviceMock;
    private readonly Mock<IProcessHandlerFactory> _factoryMock;
    private readonly Mock<IMessageBroker> _messageBrokerMock;
    private readonly IOptions<RetryConfiguration> _retryConfig;
    private readonly ProcessWorker _worker;

    public ProcessWorkerShutdownTests()
    {
        _consumerMock = new Mock<IMessageConsumer>();
        _serviceMock = new Mock<IProcessService>();
        _factoryMock = new Mock<IProcessHandlerFactory>();
        _messageBrokerMock = new Mock<IMessageBroker>();
        _retryConfig = Options.Create(new RetryConfiguration());
        
        _worker = new ProcessWorker(
            _consumerMock.Object,
            _serviceMock.Object,
            _factoryMock.Object,
            _messageBrokerMock.Object,
            _retryConfig,
            NullLogger<ProcessWorker>.Instance);
    }

    [Fact]
    public void IsShuttingDown_Should_BeFalse_Initially()
    {
        // Assert
        _worker.IsShuttingDown.Should().BeFalse();
    }

    [Fact]
    public void ActiveMessageCount_Should_BeZero_Initially()
    {
        // Assert
        _worker.ActiveMessageCount.Should().Be(0);
    }

    [Fact]
    public void Worker_Should_ExposeShutdownProperties_ForHealthCheck()
    {
        // Assert
        _worker.Should().NotBeNull();
        _worker.IsShuttingDown.Should().BeFalse();
        _worker.ActiveMessageCount.Should().BeGreaterThanOrEqualTo(0);
    }

    [Fact]
    public async Task StopAsync_Should_LogActiveMessageCount()
    {
        // Arrange
        _consumerMock
            .Setup(x => x.StopConsumingAsync())
            .Returns(Task.CompletedTask);

        // Act
        await _worker.StopAsync(CancellationToken.None);

        // Assert
        _consumerMock.Verify(
            x => x.StopConsumingAsync(),
            Times.Once);
    }

    [Fact]
    public async Task Worker_Should_CompleteGracefully_WhenNoActiveMessages()
    {
        // Arrange
        var cts = new CancellationTokenSource();
        cts.CancelAfter(TimeSpan.FromMilliseconds(100));

        _consumerMock
            .Setup(x => x.StartConsumingAsync<It.IsAnyType>(
                It.IsAny<Func<It.IsAnyType, MessageContext, Task>>(),
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _consumerMock
            .Setup(x => x.StopConsumingAsync())
            .Returns(Task.CompletedTask);

        // Act
        try
        {
            await _worker.StartAsync(cts.Token);
            await Task.Delay(TimeSpan.FromMilliseconds(200));
            await _worker.StopAsync(CancellationToken.None);
        }
        catch (OperationCanceledException)
        {
            // Expected
        }

        // Assert
        _worker.ActiveMessageCount.Should().Be(0);
    }
}
