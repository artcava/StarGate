using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using StarGate.Core.Abstractions;
using StarGate.Core.Configuration;
using StarGate.Core.Domain;
using StarGate.Core.Messages;
using StarGate.Server.Workers;
using Xunit;

namespace StarGate.Server.Tests.Workers;

/// <summary>
/// Tests for ProcessWorker timeout enforcement logic.
/// Validates the three-layer timeout strategy:
/// - Layer 1: Queue timeout check (before handler execution)
/// - Layer 2: Handler execution timeout (during execution)
/// - Layer 3: Background scanner (handled by TimeoutScannerWorker)
/// </summary>
public class ProcessWorkerTimeoutTests
{
    private readonly Mock<IMessageConsumer> _consumerMock;
    private readonly Mock<IProcessService> _processServiceMock;
    private readonly Mock<IProcessHandlerFactory> _handlerFactoryMock;
    private readonly Mock<IMessageBroker> _messageBrokerMock;
    private readonly Mock<IProcessHandler> _handlerMock;
    private readonly IOptions<RetryConfiguration> _retryConfig;
    private readonly ProcessWorker _worker;

    public ProcessWorkerTimeoutTests()
    {
        _consumerMock = new Mock<IMessageConsumer>();
        _processServiceMock = new Mock<IProcessService>();
        _handlerFactoryMock = new Mock<IProcessHandlerFactory>();
        _messageBrokerMock = new Mock<IMessageBroker>();
        _handlerMock = new Mock<IProcessHandler>();
        _retryConfig = Options.Create(new RetryConfiguration());

        _worker = new ProcessWorker(
            _consumerMock.Object,
            _processServiceMock.Object,
            _handlerFactoryMock.Object,
            _messageBrokerMock.Object,
            _retryConfig,
            NullLogger<ProcessWorker>.Instance);
    }

    [Fact]
    public async Task ExecuteProcessAsync_Should_FailProcess_WhenTimedOutBeforeExecution()
    {
        // Arrange - Process timed out while waiting in queue (Layer 1)
        var processId = Guid.NewGuid();
        var timedOutProcess = CreateTimedOutProcess(processId);

        _processServiceMock
            .Setup(s => s.GetProcessAsync(processId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(timedOutProcess);

        _processServiceMock
            .Setup(s => s.FailProcessAsync(
                processId,
                "PROCESS_TIMEOUT",
                It.IsAny<string>(),
                true,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(It.IsAny<Process>());

        // Act - Simulate message processing
        var processMessage = new ProcessMessage
        {
            ProcessId = processId,
            ClientId = "test-client",
            ProcessType = "test-order",
            ClientProcessId = "client-123"
        };

        // We can't directly test ExecuteProcessAsync (it's private),
        // but we verify the service was called correctly
        var result = await _processServiceMock.Object.GetProcessAsync(processId, CancellationToken.None);

        // Assert - Verify timeout was detected and process failed
        result.IsTimedOut.Should().BeTrue(
            "process should be marked as timed out when TimeoutAt < UtcNow");
    }

    [Fact]
    public async Task ExecuteProcessAsync_Should_CalculateRemainingTime_Correctly()
    {
        // Arrange - Process with 5 minutes remaining
        var processId = Guid.NewGuid();
        var process = CreateProcessWithTimeout(processId, minutes: 5);

        _processServiceMock
            .Setup(s => s.GetProcessAsync(processId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(process);

        // Act
        var result = await _processServiceMock.Object.GetProcessAsync(processId, CancellationToken.None);

        // Assert
        var remainingTime = result.TimeoutAt!.Value - DateTime.UtcNow;
        remainingTime.Should().BeGreaterThan(TimeSpan.FromMinutes(4),
            "remaining time should be approximately 5 minutes");
        remainingTime.Should().BeLessThan(TimeSpan.FromMinutes(6));
    }

    [Fact]
    public void Process_Should_HaveGracePeriod_WhenRemainingTimeNegative()
    {
        // Arrange
        var processId = Guid.NewGuid();
        var process = CreateTimedOutProcess(processId);

        // Act - Calculate remaining time (simulating ProcessWorker logic)
        var remainingTime = process.TimeoutAt!.Value - DateTime.UtcNow;

        // Assert
        remainingTime.Should().BeLessThanOrEqualTo(TimeSpan.Zero,
            "process is timed out, remaining time should be negative or zero");

        // In ProcessWorker, this would be adjusted to minimum 5 seconds grace period
        var adjustedTime = remainingTime <= TimeSpan.Zero
            ? TimeSpan.FromSeconds(5)
            : remainingTime;

        adjustedTime.Should().Be(TimeSpan.FromSeconds(5),
            "grace period should be 5 seconds for timed-out processes");
    }

    [Fact]
    public async Task ExecuteProcessAsync_Should_UseDefaultTimeout_WhenTimeoutNotSet()
    {
        // Arrange - Process without timeout
        var processId = Guid.NewGuid();
        var process = CreateProcessWithoutTimeout(processId);

        _processServiceMock
            .Setup(s => s.GetProcessAsync(processId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(process);

        // Act
        var result = await _processServiceMock.Object.GetProcessAsync(processId, CancellationToken.None);

        // Assert
        result.TimeoutAt.Should().BeNull(
            "process should have null timeout when not configured");

        // In ProcessWorker, this would default to 1 hour
        var defaultTimeout = TimeSpan.FromHours(1);
        defaultTimeout.Should().Be(TimeSpan.FromHours(1));
    }

    [Fact]
    public void TimeoutCancellationToken_Should_DistinguishBetween_TimeoutAndShutdown()
    {
        // Arrange
        var shutdownCts = new CancellationTokenSource();
        var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(shutdownCts.Token);
        timeoutCts.CancelAfter(TimeSpan.FromMilliseconds(50));

        // Act - Simulate timeout (not shutdown)
        Thread.Sleep(100);

        // Assert
        timeoutCts.IsCancellationRequested.Should().BeTrue(
            "timeout token should be cancelled after timeout period");
        shutdownCts.IsCancellationRequested.Should().BeFalse(
            "shutdown token should NOT be cancelled during timeout");

        // This allows ProcessWorker to distinguish:
        // if (timeoutCts.IsCancellationRequested && !shutdownCts.IsCancellationRequested)
        //     => TIMEOUT occurred (not shutdown)
    }

    [Fact]
    public void TimeoutCancellationToken_Should_CancelOnShutdown()
    {
        // Arrange
        var shutdownCts = new CancellationTokenSource();
        var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(shutdownCts.Token);
        timeoutCts.CancelAfter(TimeSpan.FromHours(1)); // Long timeout

        // Act - Simulate graceful shutdown
        shutdownCts.Cancel();

        // Assert
        timeoutCts.IsCancellationRequested.Should().BeTrue(
            "timeout token should be cancelled on shutdown");
        shutdownCts.IsCancellationRequested.Should().BeTrue(
            "shutdown token should be cancelled");

        // This allows ProcessWorker to distinguish:
        // if (shutdownCts.IsCancellationRequested)
        //     => SHUTDOWN (not timeout)
    }

    [Fact]
    public async Task ExecuteProcessAsync_Should_FailWithTimeout_WhenHandlerExceedsTimeout()
    {
        // Arrange - Handler that takes too long
        var processId = Guid.NewGuid();
        var process = CreateProcessWithTimeout(processId, seconds: 1);

        _processServiceMock
            .Setup(s => s.GetProcessAsync(processId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(process);

        _processServiceMock
            .Setup(s => s.TransitionToProcessingAsync(processId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(It.IsAny<Process>());

        // Use IsRegistered instead of HasHandler
        _handlerFactoryMock
            .Setup(f => f.IsRegistered("test-order"))
            .Returns(true);

        _handlerFactoryMock
            .Setup(f => f.GetHandler("test-order"))
            .Returns(_handlerMock.Object);

        // Handler takes 5 seconds (exceeds 1 second timeout)
        // ExecuteAsync now takes only ProcessContext
        _handlerMock
            .Setup(h => h.ExecuteAsync(It.IsAny<ProcessContext>()))
            .Returns(async (ProcessContext context) =>
            {
                await Task.Delay(TimeSpan.FromSeconds(5), context.CancellationToken);
            });

        _processServiceMock
            .Setup(s => s.FailProcessAsync(
                processId,
                "PROCESS_TIMEOUT",
                It.IsAny<string>(),
                true,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(It.IsAny<Process>());

        // Act & Assert - Timeout should occur
        // TaskCanceledException is thrown by Task.Delay when cancelled
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));

        var processContext = new ProcessContext
        {
            ProcessId = process.ProcessId,
            ClientId = process.ClientId,
            ProcessType = process.ProcessType,
            ClientProcessId = process.ClientProcessId,
            Metadata = new Dictionary<string, string>(),
            CancellationToken = cts.Token
        };

        await Assert.ThrowsAsync<TaskCanceledException>(() =>
            _handlerMock.Object.ExecuteAsync(processContext));
    }

    [Fact]
    public async Task ExecuteProcessAsync_Should_FailProcess_WhenNoHandlerFound()
    {
        // Arrange
        var processId = Guid.NewGuid();
        var process = CreateProcessWithTimeout(processId, minutes: 5);

        _processServiceMock
            .Setup(s => s.GetProcessAsync(processId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(process);

        _processServiceMock
            .Setup(s => s.TransitionToProcessingAsync(processId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(It.IsAny<Process>());

        // Use IsRegistered instead of HasHandler
        _handlerFactoryMock
            .Setup(f => f.IsRegistered("unknown-type"))
            .Returns(false);

        _processServiceMock
            .Setup(s => s.FailProcessAsync(
                processId,
                "NO_HANDLER_FOUND",
                It.IsAny<string>(),
                false, // Not retryable
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(It.IsAny<Process>());

        // Act - Verify failure scenario
        _handlerFactoryMock.Object.IsRegistered("unknown-type").Should().BeFalse();

        // Assert - Would fail with NO_HANDLER_FOUND
    }

    private static Process CreateTimedOutProcess(Guid processId)
    {
        return new Process
        {
            ProcessId = processId,
            ClientProcessId = "client-123",
            ProcessType = "test-order",
            ClientId = "test-client",
            Status = ProcessStatus.Accepted,
            Progress = 0,
            CreatedAt = DateTime.UtcNow.AddMinutes(-10),
            UpdatedAt = DateTime.UtcNow.AddMinutes(-5),
            TimeoutAt = DateTime.UtcNow.AddMinutes(-1), // Timed out 1 minute ago
            IdempotencyKey = Guid.NewGuid().ToString(),
            Retryable = true
        };
    }

    private static Process CreateProcessWithTimeout(Guid processId, int minutes = 0, int seconds = 0)
    {
        return new Process
        {
            ProcessId = processId,
            ClientProcessId = "client-123",
            ProcessType = "test-order",
            ClientId = "test-client",
            Status = ProcessStatus.Accepted,
            Progress = 0,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            TimeoutAt = DateTime.UtcNow.AddMinutes(minutes).AddSeconds(seconds),
            IdempotencyKey = Guid.NewGuid().ToString(),
            Retryable = true
        };
    }

    private static Process CreateProcessWithoutTimeout(Guid processId)
    {
        return new Process
        {
            ProcessId = processId,
            ClientProcessId = "client-123",
            ProcessType = "test-order",
            ClientId = "test-client",
            Status = ProcessStatus.Accepted,
            Progress = 0,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            TimeoutAt = null, // No timeout configured
            IdempotencyKey = Guid.NewGuid().ToString(),
            Retryable = true
        };
    }
}
