using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using StarGate.Core.Abstractions;
using StarGate.Core.Messages;
using System.Text.Json;

namespace StarGate.Server.Workers;

/// <summary>
/// Background worker that consumes process messages from the broker and executes them.
/// Implements graceful shutdown and comprehensive error handling.
/// </summary>
public class ProcessWorker : BackgroundService
{
    private readonly IMessageConsumer _messageConsumer;
    private readonly IProcessService _processService;
    private readonly IProcessHandlerFactory _handlerFactory;
    private readonly ILogger<ProcessWorker> _logger;

    public ProcessWorker(
        IMessageConsumer messageConsumer,
        IProcessService processService,
        IProcessHandlerFactory handlerFactory,
        ILogger<ProcessWorker> logger)
    {
        _messageConsumer = messageConsumer ?? throw new ArgumentNullException(nameof(messageConsumer));
        _processService = processService ?? throw new ArgumentNullException(nameof(processService));
        _handlerFactory = handlerFactory ?? throw new ArgumentNullException(nameof(handlerFactory));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("ProcessWorker starting");

        try
        {
            await _messageConsumer.StartConsumingAsync<ProcessMessage>(
                messageHandler: async (message, context) => await HandleMessageAsync(message, context, stoppingToken),
                ct: stoppingToken);
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("ProcessWorker stopped gracefully");
        }
        catch (Exception ex)
        {
            _logger.LogCritical(ex, "ProcessWorker encountered fatal error");
            throw;
        }
    }

    private async Task HandleMessageAsync(
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

            // Handle process failure
            await HandleProcessFailureAsync(
                processId,
                ex,
                cancellationToken);

            // NACK and requeue for retry
            await context.RejectAsync(true);
        }
    }

    private async Task ExecuteProcessAsync(
        ProcessMessage processMessage,
        CancellationToken cancellationToken)
    {
        var processId = processMessage.ProcessId;

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

        _logger.LogDebug(
            "Executing handler: ProcessType={ProcessType}, HandlerType={HandlerType}",
            processMessage.ProcessType,
            handler.GetType().Name);

        // Get process entity
        var process = await _processService.GetProcessAsync(processId, cancellationToken);

        // Execute handler
        await handler.ExecuteAsync(process, cancellationToken);

        // Complete process
        await _processService.CompleteProcessAsync(processId, cancellationToken);

        _logger.LogInformation(
            "Handler execution completed: ProcessId={ProcessId}",
            processId);
    }

    private async Task HandleProcessFailureAsync(
        Guid processId,
        Exception exception,
        CancellationToken cancellationToken)
    {
        try
        {
            var errorCode = exception switch
            {
                TimeoutException => "PROCESS_TIMEOUT",
                OperationCanceledException => "PROCESS_CANCELLED",
                InvalidOperationException => "INVALID_OPERATION",
                _ => "UNKNOWN_ERROR"
            };

            var canRetry = exception is not InvalidOperationException;

            await _processService.FailProcessAsync(
                processId,
                errorCode,
                exception.Message,
                canRetry,
                cancellationToken);

            _logger.LogWarning(
                "Process failure recorded: ProcessId={ProcessId}, ErrorCode={ErrorCode}, CanRetry={CanRetry}",
                processId,
                errorCode,
                canRetry);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Failed to handle process failure: ProcessId={ProcessId}",
                processId);
        }
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("ProcessWorker stopping...");
        
        await _messageConsumer.StopConsumingAsync();
        await base.StopAsync(cancellationToken);
        
        _logger.LogInformation("ProcessWorker stopped");
    }
}
