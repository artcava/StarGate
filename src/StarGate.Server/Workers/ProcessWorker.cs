using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using StarGate.Core.Abstractions;
using StarGate.Core.Configuration;
using StarGate.Core.Domain;
using StarGate.Core.Errors;
using StarGate.Core.Messages;
using System.Collections.Concurrent;
using System.Text.Json;

namespace StarGate.Server.Workers;

/// <summary>
/// Background worker that consumes process messages from the broker and executes them.
/// Implements graceful shutdown and comprehensive error handling.
/// Enforces timeout limits to prevent processes from exceeding configured timeout duration.
/// Supports retry logic with exponential backoff for transient failures.
/// Integrates ErrorClassifier for sophisticated error handling and ACK/NACK decisions.
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
        _logger.LogInformation("ProcessWorker starting with ErrorClassifier integration");

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
                    if (stoppingToken.IsCancellationRequested)
                    {
                        _logger.LogWarning(
                            "Rejecting message during shutdown: ProcessId={ProcessId}",
                            message.ProcessId);

                        await context.RejectAsync(true);
                        return;
                    }

                    var messageKey = $"{message.ProcessId}_{Guid.NewGuid()}";
                    var processingTask = HandleMessageWithTrackingAsync(
                        message,
                        context,
                        stoppingToken);

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

            await ExecuteProcessAsync(processMessage, cancellationToken);

            _logger.LogInformation(
                "Process completed successfully: ProcessId={ProcessId}",
                processId);

            await context.AcknowledgeAsync();
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            _logger.LogWarning(
                "Process execution cancelled during shutdown: ProcessId={ProcessId}",
                processId);

            await RecordCancellationAsync(processId);
            await context.RejectAsync(true);
        }
        catch (JsonException ex)
        {
            _logger.LogError(
                ex,
                "Malformed message (JSON error): ProcessId={ProcessId}",
                processId);

            // Classify error
            var classification = ErrorClassifier.Classify(ex);

            _logger.LogWarning(
                "Error classification: ErrorCode={ErrorCode}, IsRetryable={IsRetryable}, ShouldRequeue={ShouldRequeue}, Severity={Severity}",
                classification.ErrorCode,
                classification.IsRetryable,
                classification.ShouldRequeue,
                classification.Severity);

            // Record failure
            await RecordProcessFailureAsync(
                processId,
                classification,
                ex.Message,
                cancellationToken);

            // NACK without requeue (malformed message goes to DLQ)
            await context.RejectAsync(false);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Failed to process message: ProcessId={ProcessId}",
                processId);

            // Classify error
            var classification = ErrorClassifier.Classify(ex);

            _logger.LogWarning(
                "Error classification: ErrorCode={ErrorCode}, IsRetryable={IsRetryable}, ShouldRequeue={ShouldRequeue}, Severity={Severity}",
                classification.ErrorCode,
                classification.IsRetryable,
                classification.ShouldRequeue,
                classification.Severity);

            // Record process failure with classification
            await RecordProcessFailureAsync(
                processId,
                classification,
                ex.Message,
                cancellationToken);

            // Handle process failure with retry logic
            await HandleProcessFailureAsync(
                processId,
                classification,
                ex,
                cancellationToken);

            // Apply ACK/NACK strategy based on classification
            // If ShouldRequeue = false, message goes to DLQ
            // If ShouldRequeue = true, message is requeued for retry
            await context.RejectAsync(classification.ShouldRequeue);
        }
    }

    private async Task RecordProcessFailureAsync(
        Guid processId,
        ErrorClassification classification,
        string errorMessage,
        CancellationToken cancellationToken)
    {
        try
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));

            await _processService.RecordProcessErrorAsync(
                processId,
                classification.ErrorCode,
                errorMessage,
                classification.IsRetryable,
                cts.Token);

            _logger.LogInformation(
                "Process failure recorded: ProcessId={ProcessId}, ErrorCode={ErrorCode}, Severity={Severity}",
                processId,
                classification.ErrorCode,
                classification.Severity);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Failed to record process failure: ProcessId={ProcessId}",
                processId);
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

        var process = await _processService.GetProcessAsync(processId, cancellationToken);

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

        var remainingTime = process.TimeoutAt.HasValue
            ? process.TimeoutAt.Value - DateTime.UtcNow
            : TimeSpan.FromHours(1);

        if (remainingTime <= TimeSpan.Zero)
        {
            remainingTime = TimeSpan.FromSeconds(5);
        }

        _logger.LogDebug(
            "Process execution timeout: ProcessId={ProcessId}, RemainingTime={RemainingTime}s",
            processId,
            remainingTime.TotalSeconds);

        await _processService.TransitionToProcessingAsync(processId, cancellationToken);

        _logger.LogInformation(
            "Process transitioned to Processing: ProcessId={ProcessId}",
            processId);

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

        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(remainingTime);

        try
        {
            _logger.LogDebug(
                "Executing handler with timeout: ProcessType={ProcessType}, HandlerType={HandlerType}, Timeout={Timeout}s",
                processMessage.ProcessType,
                handler.GetType().Name,
                remainingTime.TotalSeconds);

            await handler.ExecuteAsync(process, timeoutCts.Token);

            await _processService.CompleteProcessAsync(processId, cancellationToken);

            _logger.LogInformation(
                "Handler execution completed: ProcessId={ProcessId}",
                processId);
        }
        catch (OperationCanceledException) when (timeoutCts.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
        {
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

            throw;
        }
    }

    private async Task HandleProcessFailureAsync(
        Guid processId,
        ErrorClassification classification,
        Exception exception,
        CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogWarning(
                "Handling process failure: ProcessId={ProcessId}, ErrorCode={ErrorCode}, IsRetryable={IsRetryable}, Severity={Severity}",
                processId,
                classification.ErrorCode,
                classification.IsRetryable,
                classification.Severity);

            var process = await _processService.GetProcessAsync(processId, cancellationToken);

            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));

            await _processService.FailProcessAsync(
                processId,
                classification.ErrorCode,
                exception.Message,
                classification.IsRetryable,
                cts.Token);

            process = await _processService.GetProcessAsync(processId, cts.Token);

            if (process.Status == ProcessStatus.Retrying)
            {
                var retryDelay = _retryConfig.CalculateDelay(process.RetryCount);

                _logger.LogInformation(
                    "Process will retry: ProcessId={ProcessId}, RetryCount={RetryCount}/{MaxRetries}, Delay={Delay}s, ErrorCode={ErrorCode}",
                    processId,
                    process.RetryCount,
                    process.MaxRetries,
                    retryDelay.TotalSeconds,
                    classification.ErrorCode);

                await PublishRetryMessageAsync(process, retryDelay, cts.Token);
            }
            else
            {
                _logger.LogWarning(
                    "Process failed permanently: ProcessId={ProcessId}, Status={Status}, RetryCount={RetryCount}, ErrorCode={ErrorCode}",
                    processId,
                    process.Status,
                    process.RetryCount,
                    classification.ErrorCode);
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
