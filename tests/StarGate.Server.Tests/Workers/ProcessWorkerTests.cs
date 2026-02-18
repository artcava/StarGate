namespace StarGate.Server.Tests.Workers;

using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using StarGate.Core.Abstractions;
using StarGate.Core.Domain;
using StarGate.Core.Domain.Configuration;
using StarGate.Core.Exceptions;
using StarGate.Server.Workers;
using Xunit;

public class ProcessWorkerTests
{
    private readonly Mock<IMessageConsumer> _consumerMock;
    private readonly Mock<IProcessHandlerFactory> _handlerFactoryMock;
    private readonly Mock<IProcessRepository> _repositoryMock;
    private readonly Mock<IPolicyProvider> _policyProviderMock;
    private readonly ProcessWorker _worker;

    public ProcessWorkerTests()
    {
        _consumerMock = new Mock<IMessageConsumer>();
        _handlerFactoryMock = new Mock<IProcessHandlerFactory>();
        _repositoryMock = new Mock<IProcessRepository>();
        _policyProviderMock = new Mock<IPolicyProvider>();

        _worker = new ProcessWorker(
            _consumerMock.Object,
            _handlerFactoryMock.Object,
            _repositoryMock.Object,
            _policyProviderMock.Object,
            NullLogger<ProcessWorker>.Instance);
    }

    [Fact]
    public void Constructor_Should_ThrowArgumentNullException_WhenConsumerIsNull()
    {
        // Act
        Action act = () => new ProcessWorker(
            null!,
            _handlerFactoryMock.Object,
            _repositoryMock.Object,
            _policyProviderMock.Object,
            NullLogger<ProcessWorker>.Instance);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("consumer");
    }

    [Fact]
    public void Constructor_Should_ThrowArgumentNullException_WhenHandlerFactoryIsNull()
    {
        // Act
        Action act = () => new ProcessWorker(
            _consumerMock.Object,
            null!,
            _repositoryMock.Object,
            _policyProviderMock.Object,
            NullLogger<ProcessWorker>.Instance);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("handlerFactory");
    }

    [Fact]
    public void Constructor_Should_ThrowArgumentNullException_WhenRepositoryIsNull()
    {
        // Act
        Action act = () => new ProcessWorker(
            _consumerMock.Object,
            _handlerFactoryMock.Object,
            null!,
            _policyProviderMock.Object,
            NullLogger<ProcessWorker>.Instance);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("repository");
    }

    [Fact]
    public void Constructor_Should_ThrowArgumentNullException_WhenPolicyProviderIsNull()
    {
        // Act
        Action act = () => new ProcessWorker(
            _consumerMock.Object,
            _handlerFactoryMock.Object,
            _repositoryMock.Object,
            null!,
            NullLogger<ProcessWorker>.Instance);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("policyProvider");
    }

    [Fact]
    public void Constructor_Should_ThrowArgumentNullException_WhenLoggerIsNull()
    {
        // Act
        Action act = () => new ProcessWorker(
            _consumerMock.Object,
            _handlerFactoryMock.Object,
            _repositoryMock.Object,
            _policyProviderMock.Object,
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
            _handlerFactoryMock.Object,
            _repositoryMock.Object,
            _policyProviderMock.Object,
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
            .Setup(x => x.StartConsumingAsync(
                It.IsAny<string>(),
                It.IsAny<Func<MessageEnvelope<Process>, CancellationToken, Task<MessageHandlingResult>>>(),
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
            x => x.StartConsumingAsync(
                "stargate.processes",
                It.IsAny<Func<MessageEnvelope<Process>, CancellationToken, Task<MessageHandlingResult>>>(),
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
            .Setup(x => x.StartConsumingAsync(
                It.IsAny<string>(),
                It.IsAny<Func<MessageEnvelope<Process>, CancellationToken, Task<MessageHandlingResult>>>(),
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
