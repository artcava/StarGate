using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using StarGate.Core.Abstractions;
using StarGate.Core.Domain;
using StarGate.Server.Workers;
using Xunit;

namespace StarGate.Server.Tests.Workers;

/// <summary>
/// Unit tests for TimeoutScannerWorker.
/// Tests timeout scanning logic, error handling, and background service lifecycle.
/// </summary>
public class TimeoutScannerWorkerTests
{
    private readonly Mock<IProcessRepository> _repositoryMock;
    private readonly Mock<IProcessService> _serviceMock;
    private readonly TimeoutScannerWorker _scanner;

    public TimeoutScannerWorkerTests()
    {
        _repositoryMock = new Mock<IProcessRepository>();
        _serviceMock = new Mock<IProcessService>();
        _scanner = new TimeoutScannerWorker(
            _repositoryMock.Object,
            _serviceMock.Object,
            NullLogger<TimeoutScannerWorker>.Instance);
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
    public void Constructor_Should_CreateInstance_WhenAllParametersValid()
    {
        // Act
        var scanner = new TimeoutScannerWorker(
            _repositoryMock.Object,
            _serviceMock.Object,
            NullLogger<TimeoutScannerWorker>.Instance);

        // Assert
        scanner.Should().NotBeNull();
    }

    [Fact]
    public async Task ExecuteAsync_Should_CallGetTimedOutProcessesAsync()
    {
        // Arrange
        _repositoryMock
            .Setup(r => r.GetTimedOutProcessesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Process>() as IReadOnlyList<Process>);

        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(100));

        // Act
        var task = _scanner.StartAsync(cts.Token);
        await Task.Delay(50); // Let scanner run once
        await _scanner.StopAsync(CancellationToken.None);

        // Assert
        _repositoryMock.Verify(
            r => r.GetTimedOutProcessesAsync(It.IsAny<CancellationToken>()),
            Times.AtLeastOnce);
    }

    [Fact]
    public async Task ExecuteAsync_Should_CallCheckTimeoutAsync_ForEachTimedOutProcess()
    {
        // Arrange
        var timedOutProcesses = new List<Process>
        {
            CreateTimedOutProcess(),
            CreateTimedOutProcess(),
            CreateTimedOutProcess()
        };

        _repositoryMock
            .Setup(r => r.GetTimedOutProcessesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(timedOutProcesses as IReadOnlyList<Process>);

        _serviceMock
            .Setup(s => s.CheckTimeoutAsync(
                It.IsAny<Guid>(),
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(100));

        // Act
        await _scanner.StartAsync(cts.Token);
        await Task.Delay(50); // Let scanner process
        await _scanner.StopAsync(CancellationToken.None);

        // Assert
        _serviceMock.Verify(
            s => s.CheckTimeoutAsync(
                It.IsAny<Guid>(),
                It.IsAny<CancellationToken>()),
            Times.AtLeast(timedOutProcesses.Count));
    }

    [Fact]
    public async Task ExecuteAsync_Should_ContinueProcessing_WhenCheckTimeoutFails()
    {
        // Arrange
        var timedOutProcesses = new List<Process>
        {
            CreateTimedOutProcess(),
            CreateTimedOutProcess(),
            CreateTimedOutProcess()
        };

        _repositoryMock
            .Setup(r => r.GetTimedOutProcessesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(timedOutProcesses as IReadOnlyList<Process>);

        // First call fails, others succeed
        _serviceMock
            .SetupSequence(s => s.CheckTimeoutAsync(
                It.IsAny<Guid>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Process not found"))
            .Returns(Task.CompletedTask)
            .Returns(Task.CompletedTask);

        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(100));

        // Act
        await _scanner.StartAsync(cts.Token);
        await Task.Delay(50);
        await _scanner.StopAsync(CancellationToken.None);

        // Assert
        _serviceMock.Verify(
            s => s.CheckTimeoutAsync(
                It.IsAny<Guid>(),
                It.IsAny<CancellationToken>()),
            Times.Exactly(3)); // All processes should be attempted
    }

    [Fact]
    public async Task ExecuteAsync_Should_NotCallCheckTimeout_WhenNoTimedOutProcesses()
    {
        // Arrange
        _repositoryMock
            .Setup(r => r.GetTimedOutProcessesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Process>() as IReadOnlyList<Process>);

        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(100));

        // Act
        await _scanner.StartAsync(cts.Token);
        await Task.Delay(50);
        await _scanner.StopAsync(CancellationToken.None);

        // Assert
        _serviceMock.Verify(
            s => s.CheckTimeoutAsync(
                It.IsAny<Guid>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task ExecuteAsync_Should_ContinueRunning_WhenScanThrowsException()
    {
        // Arrange
        var callCount = 0;
        _repositoryMock
            .Setup(r => r.GetTimedOutProcessesAsync(It.IsAny<CancellationToken>()))
            .Returns(() =>
            {
                callCount++;
                if (callCount == 1)
                {
                    throw new InvalidOperationException("Database error");
                }
                return Task.FromResult<IReadOnlyList<Process>>(new List<Process>());
            });

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));

        // Act
        await _scanner.StartAsync(cts.Token);
        await Task.Delay(1500); // Wait for multiple scan cycles
        await _scanner.StopAsync(CancellationToken.None);

        // Assert
        callCount.Should().BeGreaterThan(1, "scanner should retry after exception");
    }

    [Fact]
    public async Task ExecuteAsync_Should_StopGracefully_WhenCancellationRequested()
    {
        // Arrange
        _repositoryMock
            .Setup(r => r.GetTimedOutProcessesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Process>() as IReadOnlyList<Process>);

        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(100));

        // Act
        await _scanner.StartAsync(cts.Token);
        await Task.Delay(50);
        var stopTask = _scanner.StopAsync(CancellationToken.None);

        // Assert
        await stopTask.WaitAsync(TimeSpan.FromSeconds(5)); // Should complete quickly
        stopTask.IsCompleted.Should().BeTrue();
    }

    private static Process CreateTimedOutProcess()
    {
        return new Process
        {
            ProcessId = Guid.NewGuid(),
            ClientProcessId = $"client-{Guid.NewGuid()}",
            ProcessType = "test-order",
            ClientId = "test-client",
            Status = ProcessStatus.Processing,
            Progress = 0,
            CreatedAt = DateTime.UtcNow.AddMinutes(-10),
            UpdatedAt = DateTime.UtcNow.AddMinutes(-5),
            TimeoutAt = DateTime.UtcNow.AddMinutes(-1), // Timed out 1 minute ago
            IdempotencyKey = Guid.NewGuid().ToString(),
            Retryable = true
        };
    }
}
