using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using StarGate.Core.Abstractions;
using StarGate.Server.Workers;
using Xunit;

namespace StarGate.Server.Tests.Workers;

/// <summary>
/// Unit tests for TimeoutScannerWorker.
/// Tests constructor validation and basic initialization.
/// Full execution testing requires integration tests with real dependencies.
/// </summary>
public class TimeoutScannerWorkerTests
{
    private readonly Mock<IProcessRepository> _repositoryMock;
    private readonly Mock<IProcessService> _serviceMock;

    public TimeoutScannerWorkerTests()
    {
        _repositoryMock = new Mock<IProcessRepository>();
        _serviceMock = new Mock<IProcessService>();
    }

    [Fact]
    public void Constructor_Should_ThrowArgumentNullException_WhenRepositoryIsNull()
    {
        // Act
        var act = () => new TimeoutScannerWorker(
            null!,
            _serviceMock.Object,
            NullLogger<TimeoutScannerWorker>.Instance);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("processRepository");
    }

    [Fact]
    public void Constructor_Should_ThrowArgumentNullException_WhenServiceIsNull()
    {
        // Act
        var act = () => new TimeoutScannerWorker(
            _repositoryMock.Object,
            null!,
            NullLogger<TimeoutScannerWorker>.Instance);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("processService");
    }

    [Fact]
    public void Constructor_Should_ThrowArgumentNullException_WhenLoggerIsNull()
    {
        // Act
        var act = () => new TimeoutScannerWorker(
            _repositoryMock.Object,
            _serviceMock.Object,
            null!);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("logger");
    }

    [Fact]
    public void Constructor_Should_CreateInstance_WhenAllDependenciesProvided()
    {
        // Act
        var worker = new TimeoutScannerWorker(
            _repositoryMock.Object,
            _serviceMock.Object,
            NullLogger<TimeoutScannerWorker>.Instance);

        // Assert
        worker.Should().NotBeNull();
    }
}
