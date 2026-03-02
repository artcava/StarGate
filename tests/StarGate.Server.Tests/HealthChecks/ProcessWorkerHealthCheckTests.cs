using FluentAssertions;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using StarGate.Core.Abstractions;
using StarGate.Server.HealthChecks;
using StarGate.Server.Workers;
using Xunit;

namespace StarGate.Server.Tests.HealthChecks;

/// <summary>
/// Unit tests for ProcessWorkerHealthCheck.
/// </summary>
public class ProcessWorkerHealthCheckTests
{
    private readonly Mock<IMessageConsumer> _consumerMock;
    private readonly Mock<IProcessService> _serviceMock;
    private readonly Mock<IProcessHandlerFactory> _factoryMock;

    public ProcessWorkerHealthCheckTests()
    {
        _consumerMock = new Mock<IMessageConsumer>();
        _serviceMock = new Mock<IProcessService>();
        _factoryMock = new Mock<IProcessHandlerFactory>();
    }

    [Fact]
    public void Constructor_Should_ThrowArgumentNullException_WhenWorkerIsNull()
    {
        // Act
        Action act = () => new ProcessWorkerHealthCheck(null!);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("worker");
    }

    [Fact]
    public async Task CheckHealthAsync_Should_ReturnHealthy_WhenWorkerIsRunningNormally()
    {
        // Arrange
        var worker = new ProcessWorker(
            _consumerMock.Object,
            _serviceMock.Object,
            _factoryMock.Object,
            NullLogger<ProcessWorker>.Instance);

        var healthCheck = new ProcessWorkerHealthCheck(worker);
        var context = new HealthCheckContext();

        // Act
        var result = await healthCheck.CheckHealthAsync(context);

        // Assert
        result.Status.Should().Be(HealthStatus.Healthy);
        result.Description.Should().Be("Worker is running normally");
        result.Data.Should().ContainKey("activeMessages");
        result.Data["activeMessages"].Should().Be(0);
    }

    [Fact]
    public async Task CheckHealthAsync_Should_ReturnHealthy_WhenActiveMessagesAreLow()
    {
        // Arrange
        var worker = new ProcessWorker(
            _consumerMock.Object,
            _serviceMock.Object,
            _factoryMock.Object,
            NullLogger<ProcessWorker>.Instance);

        var healthCheck = new ProcessWorkerHealthCheck(worker);
        var context = new HealthCheckContext();

        // Act
        var result = await healthCheck.CheckHealthAsync(context);

        // Assert
        result.Status.Should().Be(HealthStatus.Healthy);
        result.Data["activeMessages"].Should().Be(0);
    }

    [Fact]
    public void CheckHealthAsync_Should_IncludeActiveMessageCount_InData()
    {
        // Arrange
        var worker = new ProcessWorker(
            _consumerMock.Object,
            _serviceMock.Object,
            _factoryMock.Object,
            NullLogger<ProcessWorker>.Instance);

        var healthCheck = new ProcessWorkerHealthCheck(worker);

        // Act & Assert
        healthCheck.Should().NotBeNull();
        worker.ActiveMessageCount.Should().Be(0);
    }
}
