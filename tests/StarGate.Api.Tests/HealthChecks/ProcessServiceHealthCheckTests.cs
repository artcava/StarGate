using FluentAssertions;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using StarGate.Api.HealthChecks;
using StarGate.Core.Abstractions;
using StarGate.Core.Domain;
using Xunit;

namespace StarGate.Api.Tests.HealthChecks;

public class ProcessServiceHealthCheckTests
{
    private readonly Mock<IProcessRepository> _repositoryMock;
    private readonly ProcessServiceHealthCheck _healthCheck;

    public ProcessServiceHealthCheckTests()
    {
        _repositoryMock = new Mock<IProcessRepository>();
        _healthCheck = new ProcessServiceHealthCheck(
            _repositoryMock.Object,
            NullLogger<ProcessServiceHealthCheck>.Instance);
    }

    [Fact]
    public async Task CheckHealthAsync_Should_ReturnHealthy_WhenRepositoryIsAccessible()
    {
        // Arrange
        _repositoryMock
            .Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Process?)null);

        var context = new HealthCheckContext();

        // Act
        var result = await _healthCheck.CheckHealthAsync(context);

        // Assert
        result.Status.Should().Be(HealthStatus.Healthy);
        result.Description.Should().Be("ProcessService is operational");
    }

    [Fact]
    public async Task CheckHealthAsync_Should_ReturnUnhealthy_WhenRepositoryThrowsException()
    {
        // Arrange
        _repositoryMock
            .Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Database connection failed"));

        var context = new HealthCheckContext();

        // Act
        var result = await _healthCheck.CheckHealthAsync(context);

        // Assert
        result.Status.Should().Be(HealthStatus.Unhealthy);
        result.Description.Should().Be("ProcessService is not operational");
        result.Exception.Should().NotBeNull();
        result.Data.Should().ContainKey("error");
        result.Data.Should().ContainKey("type");
    }

    [Fact]
    public async Task CheckHealthAsync_Should_ReturnUnhealthy_WhenCancelled()
    {
        // Arrange
        var cts = new CancellationTokenSource();
        cts.Cancel();

        _repositoryMock
            .Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new OperationCanceledException());

        var context = new HealthCheckContext();

        // Act
        var result = await _healthCheck.CheckHealthAsync(context, cts.Token);

        // Assert
        result.Status.Should().Be(HealthStatus.Unhealthy);
        result.Description.Should().Contain("cancelled");
    }

    [Fact]
    public void Constructor_Should_ThrowArgumentNullException_WhenRepositoryIsNull()
    {
        // Act
        var act = () => new ProcessServiceHealthCheck(
            null!,
            NullLogger<ProcessServiceHealthCheck>.Instance);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("repository");
    }

    [Fact]
    public void Constructor_Should_ThrowArgumentNullException_WhenLoggerIsNull()
    {
        // Act
        var act = () => new ProcessServiceHealthCheck(
            _repositoryMock.Object,
            null!);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("logger");
    }
}
