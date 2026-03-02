using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using StarGate.Core.Abstractions;
using StarGate.Core.Configuration;
using StarGate.Core.Domain;
using StarGate.Core.Messages;
using System.Collections.Concurrent;
using System.Text.Json;

namespace StarGate.Server.Workers;

/// <summary>
/// Background worker that consumes process messages from the broker and executes them.
/// Implements graceful shutdown and comprehensive error handling.
/// Enforces timeout limits to prevent processes from exceeding configured timeout duration.
/// Supports retry logic with exponential backoff for transient failures.
/// </summary>
public class ProcessWorker : BackgroundService
{
    private readonly IMessageConsumer _messageConsumer;
    private readonly IProcessService _processService;
    private readonly IProcessHandlerFactory _handlerFactory;
    private readonly IMessageBroker _messageBroker;
    private readonly RetryConfiguration _retryConfig;
    private readonly ILogger<ProcessWorker> _logger;
    private readonly ConcurrentDictionary<string, Task> _activeMessages;
    private readonly TimeSpan _shutdownTimeout = TimeSpan.FromSeconds(30);

    public ProcessWorker(
        IMessageConsumer messageConsumer,
        IProcessService processService,
        IProcessHandlerFactory handlerFactory,
        IMessageBroker messageBroker,
        IOptions<RetryConfiguration> retryConfig,
        ILogger<ProcessWorker> logger)
    {
        _messageConsumer = messageConsumer ?? throw new ArgumentNullException(nameof(messageConsumer));
        _processService = processService ?? throw new ArgumentNullException(nameof(processService));
        _handlerFactory = handlerFactory ?? throw new ArgumentNullException(nameof(handlerFactory));
        _messageBroker = messageBroker ?? throw new ArgumentNullException(nameof(messageBroker));
        _retryConfig = retryConfig?.Value ?? throw new ArgumentNullException(nameof(retryConfig));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _activeMessages = new ConcurrentDictionary<string, Task>();
    }

    /// <summary>
    /// Gets the number of messages currently being processed.
    /// </summary>
    public int ActiveMessageCount => _activeMessages.Count;

    /// <summary>
    /// Indicates if the worker is shutting down.
    /// </summary>
    public bool IsShuttingDown { get; private set; }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("ProcessWorker starting");

        // Register shutdown callback
        stoppingToken.Register(() =>
        {
            IsShuttingDown = true;
            _logger.LogInformation(
                "Shutdown requested. Active messages: {ActiveMessageCount}",
                ActiveMessageCount);
        });

        try
        {
            await _messageConsumer.StartConsumingAsync<ProcessMessage>(
                messageHandler: async (message, context) =>
                {
                    // Don't accept new messages during shutdown
                    if (stoppingToken.IsCancellationRequested)
                    {
                        _logger.LogWarning(
                            "Rejecting message during shutdown: ProcessId={ProcessId}",
                            message.ProcessId);

                        // NACK to requeue
                        await context.RejectAsync(true);
                        return;
                    }

                    // Track message processing with unique key
                    var messageKey = $"{message.ProcessId}_{Guid.NewGuid()}";
                    var processingTask = HandleMessageWithTrackingAsync(
                        message,
                        context,
                        stoppingToken);

                    // Store task for graceful shutdown tracking
                    _activeMessages.TryAdd(messageKey, processingTask);

                    try
                    {
                        await processingTask;
                    }
                    finally
                    {
                        _activeMessages.TryRemove(messageKey, out _);
                    }
                },
                ct: stoppingToken);
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("ProcessWorker cancellation requested");
        }
        catch (Exception ex)
        {
            _logger.LogCritical(ex, "ProcessWorker encountered fatal error");
            throw;
        }
        finally
        {
            await WaitForActiveMessagesToCompleteAsync();
        }
    }

    private async Task HandleMessageWithTrackingAsync(
        ProcessMessage processMessage,
        MessageContext context,
        CancellationToken cancellationToken)
    {
        var processId = processMessage.ProcessId;

        try
        {
            _logger.LogInformation(
                "Handling process: ProcessId={ProcessId}, ProcessType={ProcessType}, ClientId={ClientId}",
                processId,
                processMessage.ProcessType,
                processMessage.ClientId);

            // Execute process
            await ExecuteProcessAsync(processMessage, cancellationToken);

            _logger.LogInformation(
                "Process completed successfully: ProcessId={ProcessId}",
                processId);

            // ACK message
            await context.AcknowledgeAsync();
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            _logger.LogWarning(
                "Process execution cancelled during shutdown: ProcessId={ProcessId}",
                processId);

            // Record cancellation for audit trail
            await RecordCancellationAsync(processId);

            // NACK to requeue - will be processed after restart
            await context.RejectAsync(true);
        }
        catch (JsonException ex)
        {
            _logger.LogError(
                ex,
                "Failed to process malformed message: ProcessId={ProcessId}",
                processId);

            // NACK message without requeue (malformed message)
            await context.RejectAsync(false);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Failed to process message: ProcessId={ProcessId}",
                processId);

            // Handle process failure with retry logic
            await HandleProcessFailureAsync(
                processId,
                ex,
                cancellationToken);

            // NACK - message will be requeued if retry is scheduled
            await context.RejectAsync(false);
        }
    }

    private async Task WaitForActiveMessagesToCompleteAsync()
    {
        if (_activeMessages.IsEmpty)
        {
            _logger.LogInformation("No active messages to wait for");
            return;
        }

        _logger.LogInformation(
            "Waiting for {ActiveMessageCount} active message(s) to complete. Timeout: {Timeout}s",
            ActiveMessageCount,
            _shutdownTimeout.TotalSeconds);

        var allTasks = _activeMessages.Values.ToArray();

        try
        {
            using var cts = new CancellationTokenSource(_shutdownTimeout);
            await Task.WhenAll(allTasks).WaitAsync(cts.Token);

            _logger.LogInformation(
                "All active messages completed successfully");
        }
        catch (TimeoutException)
        {
            _logger.LogWarning(
                "Shutdown timeout exceeded. {RemainingCount} message(s) still processing",
                _activeMessages.Count);
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning(
                "Graceful shutdown cancelled. {RemainingCount} message(s) still processing",
                _activeMessages.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Error while waiting for active messages to complete");
        }
    }

    private async Task RecordCancellationAsync(Guid processId)
    {
        try
        {
            // Use a fresh cancellation token to allow this operation to complete
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));

            await _processService.RecordProcessErrorAsync(
                processId,
                "PROCESS_CANCELLED",
                "Process execution was cancelled during graceful shutdown",
                retryable: true,
                cts.Token);

            _logger.LogInformation(
                "Cancellation recorded for process: ProcessId={ProcessId}",
                processId);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Failed to record cancellation: ProcessId={ProcessId}",
                processId);
        }
    }

    private async Task ExecuteProcessAsync(
        ProcessMessage processMessage,
        CancellationToken cancellationToken)
    {
        var processId = processMessage.ProcessId;

        // Get process to check timeout
        var process = await _processService.GetProcessAsync(processId, cancellationToken);

        // Check if process has already timed out while waiting in queue
        if (process.IsTimedOut)
        {
            _logger.LogWarning(
                "Process timed out before execution: ProcessId={ProcessId}, TimeoutAt={TimeoutAt}",
                processId,
                process.TimeoutAt);

            await _processService.FailProcessAsync(
                processId,
                "PROCESS_TIMEOUT",
                $"Process timed out before handler execution (timeout: {process.TimeoutAt})",
                canRetry: true,
                cancellationToken);

            return;
        }

        // Calculate remaining time for execution
        var remainingTime = process.TimeoutAt.HasValue
            ? process.TimeoutAt.Value - DateTime.UtcNow
            : TimeSpan.FromHours(1); // Default if no timeout set

        if (remainingTime <= TimeSpan.Zero)
        {
            remainingTime = TimeSpan.FromSeconds(5); // Minimum grace period
        }

        _logger.LogDebug(
            "Process execution timeout: ProcessId={ProcessId}, RemainingTime={RemainingTime}s",
            processId,
            remainingTime.TotalSeconds);

        // Transition to Processing
        await _processService.TransitionToProcessingAsync(processId, cancellationToken);

        _logger.LogInformation(
            "Process transitioned to Processing: ProcessId={ProcessId}",
            processId);

        // Get appropriate handler for process type
        if (!_handlerFactory.HasHandler(processMessage.ProcessType))
        {
            _logger.LogError(
                "No handler found for process type: ProcessType={ProcessType}, ProcessId={ProcessId}",
                processMessage.ProcessType,
                processId);

            await _processService.FailProcessAsync(
                processId,
                "NO_HANDLER_FOUND",
                $"No handler registered for process type '{processMessage.ProcessType}'",
                canRetry: false,
                cancellationToken);

            return;
        }

        var handler = _handlerFactory.GetHandler(processMessage.ProcessType);

        // Create timeout cancellation token
        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(remainingTime);

        try
        {
            _logger.LogDebug(
                "Executing handler with timeout: ProcessType={ProcessType}, HandlerType={HandlerType}, Timeout={Timeout}s",
                processMessage.ProcessType,
                handler.GetType().Name,
                remainingTime.TotalSeconds);

            // Execute handler with timeout
            await handler.ExecuteAsync(process, timeoutCts.Token);

            // Complete process
            await _processService.CompleteProcessAsync(processId, cancellationToken);

            _logger.LogInformation(
                "Handler execution completed: ProcessId={ProcessId}",
                processId);
        }
        catch (OperationCanceledException) when (timeoutCts.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
        {
            // Timeout occurred (not graceful shutdown)
            _logger.LogWarning(
                "Process execution timed out: ProcessId={ProcessId}, Timeout={Timeout}s",
                processId,
                remainingTime.TotalSeconds);

            await _processService.FailProcessAsync(
                processId,
                "PROCESS_TIMEOUT",
                $"Handler execution exceeded timeout of {remainingTime.TotalSeconds} seconds",
                canRetry: true,
                cancellationToken);

            throw; // Re-throw to trigger NACK
        }
    }

    private async Task HandleProcessFailureAsync(
        Guid processId,
        Exception exception,
        CancellationToken cancellationToken)
    {
        try
        {
            // Determine error classification
            var errorCode = exception switch
            {
                TimeoutException => "PROCESS_TIMEOUT",
                OperationCanceledException => "PROCESS_CANCELLED",
                InvalidOperationException => "INVALID_OPERATION",
                HttpRequestException => "HTTP_ERROR",
                _ => "UNKNOWN_ERROR"
            };

            // Determine if error is retryable
            var canRetry = exception is not InvalidOperationException;

            _logger.LogWarning(
                "Handling process failure: ProcessId={ProcessId}, ErrorCode={ErrorCode}, CanRetry={CanRetry}, Exception={Exception}",
                processId,
                errorCode,
                canRetry,
                exception.GetType().Name);

            // Get current process state
            var process = await _processService.GetProcessAsync(processId, cancellationToken);

            // Use fresh token for error recording to ensure it completes
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));

            // Fail process (service determines retry vs final failure)
            await _processService.FailProcessAsync(
                processId,
                errorCode,
                exception.Message,
                canRetry,
                cts.Token);

            // Reload process to check new status
            process = await _processService.GetProcessAsync(processId, cts.Token);

            if (process.Status == ProcessStatus.Retrying)
            {
                // Calculate retry delay
                var retryDelay = _retryConfig.CalculateDelay(process.RetryCount);

                _logger.LogInformation(
                    "Process will retry: ProcessId={ProcessId}, RetryCount={RetryCount}/{MaxRetries}, Delay={Delay}s",
                    processId,
                    process.RetryCount,
                    process.MaxRetries,
                    retryDelay.TotalSeconds);

                // Publish delayed retry message
                await PublishRetryMessageAsync(process, retryDelay, cts.Token);
            }
            else
            {
                _logger.LogWarning(
                    "Process failed permanently: ProcessId={ProcessId}, Status={Status}, RetryCount={RetryCount}",
                    processId,
                    process.Status,
                    process.RetryCount);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Failed to handle process failure: ProcessId={ProcessId}",
                processId);
        }
    }

    private async Task PublishRetryMessageAsync(
        Process process,
        TimeSpan delay,
        CancellationToken cancellationToken)
    {
        try
        {
            var message = ProcessMessage.FromProcess(process);
            var routingKey = $"process.{process.ProcessType}";

            _logger.LogDebug(
                "Publishing retry message: ProcessId={ProcessId}, Delay={Delay}s",
                process.ProcessId,
                delay.TotalSeconds);

            await _messageBroker.PublishWithDelayAsync(
                message,
                routingKey,
                delay,
                cancellationToken);

            _logger.LogInformation(
                "Retry message published: ProcessId={ProcessId}, ScheduledFor={ScheduledTime}",
                process.ProcessId,
                DateTime.UtcNow.Add(delay));
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Failed to publish retry message: ProcessId={ProcessId}",
                process.ProcessId);
        }
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation(
            "ProcessWorker stopping. Active messages: {ActiveMessageCount}",
            ActiveMessageCount);

        await _messageConsumer.StopConsumingAsync();
        await base.StopAsync(cancellationToken);

        _logger.LogInformation("ProcessWorker stopped");
    }
}
