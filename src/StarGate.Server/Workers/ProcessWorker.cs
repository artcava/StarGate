namespace StarGate.Server.Workers;

using Microsoft.Extensions.Hosting;
using StarGate.Core.Abstractions;
using StarGate.Core.Domain;
using StarGate.Core.Domain.Configuration;
using StarGate.Core.Exceptions;
using System.Threading.Channels;

/// <summary>
/// Background worker that consumes messages from RabbitMQ and executes processes.
/// Integrates policy enforcement for timeout, retry, and concurrency control.
/// </summary>
public class ProcessWorker : BackgroundService
{
    private readonly IMessageConsumer _consumer;
    private readonly IProcessHandlerFactory _handlerFactory;
    private readonly IProcessRepository _repository;
    private readonly IPolicyProvider _policyProvider;
    private readonly ILogger<ProcessWorker> _logger;
    private readonly Channel<ProcessExecutionContext> _executionChannel;
    private readonly SemaphoreSlim _globalSemaphore;
    private readonly Dictionary<string, SemaphoreSlim> _processTypeSemaphores;

    public ProcessWorker(
        IMessageConsumer consumer,
        IProcessHandlerFactory handlerFactory,
        IProcessRepository repository,
        IPolicyProvider policyProvider,
        ILogger<ProcessWorker> logger)
    {
        _consumer = consumer ?? throw new ArgumentNullException(nameof(consumer));
        _handlerFactory = handlerFactory ?? throw new ArgumentNullException(nameof(handlerFactory));
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _policyProvider = policyProvider ?? throw new ArgumentNullException(nameof(policyProvider));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        // Create bounded channel for execution queue
        _executionChannel = Channel.CreateBounded<ProcessExecutionContext>(new BoundedChannelOptions(100)
        {
            FullMode = BoundedChannelFullMode.Wait
        });

        _globalSemaphore = new SemaphoreSlim(Environment.ProcessorCount * 2);
        _processTypeSemaphores = new Dictionary<string, SemaphoreSlim>();
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("ProcessWorker starting...");

        // Start consumer
        var consumerTask = StartConsumerAsync(stoppingToken);

        // Start execution workers
        var workerTasks = Enumerable.Range(0, Environment.ProcessorCount)
            .Select(i => ExecuteProcessesAsync(i, stoppingToken))
            .ToArray();

        try
        {
            await Task.WhenAll(workerTasks.Append(consumerTask));
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("ProcessWorker stopping...");
        }
        catch (Exception ex)
        {
            _logger.LogCritical(ex, "ProcessWorker failed with unhandled exception");
            throw;
        }
        finally
        {
            await _consumer.StopConsumingAsync();
            _logger.LogInformation("ProcessWorker stopped");
        }
    }

    private async Task StartConsumerAsync(CancellationToken cancellationToken)
    {
        await _consumer.StartConsumingAsync(
            "stargate.processes",
            HandleMessageAsync,
            cancellationToken);
    }

    private async Task<MessageHandlingResult> HandleMessageAsync(
        MessageEnvelope<Process> envelope,
        CancellationToken cancellationToken)
    {
        var process = envelope.Payload;

        try
        {
            _logger.LogInformation(
                "Received message for process {ProcessId}, Type: {ProcessType}, ClientId: {ClientId}",
                process.ProcessId,
                process.ProcessType,
                process.ClientId);

            // Load policy for this process
            var policy = await _policyProvider.GetPolicyAsync(
                process.ProcessType,
                process.ClientId,
                cancellationToken);

            if (policy == null)
            {
                _logger.LogError(
                    "No policy found for process type {ProcessType}, client {ClientId}",
                    process.ProcessType,
                    process.ClientId);
                return MessageHandlingResult.Reject;
            }

            // Validate policy constraints
            if (!ValidatePolicy(policy, process))
            {
                _logger.LogError(
                    "Policy validation failed for process {ProcessId}",
                    process.ProcessId);
                return MessageHandlingResult.Reject;
            }

            // Queue for execution with policy context
            var context = new ProcessExecutionContext
            {
                Process = process,
                Policy = policy,
                MessageId = envelope.MessageId,
                CorrelationId = envelope.CorrelationId ?? process.ProcessId.ToString()
            };

            await _executionChannel.Writer.WriteAsync(context, cancellationToken);

            _logger.LogDebug(
                "Process {ProcessId} queued for execution",
                process.ProcessId);

            return MessageHandlingResult.Acknowledge;
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation(
                "Message handling cancelled for process {ProcessId}",
                process.ProcessId);
            return MessageHandlingResult.Requeue;
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Error handling message for process {ProcessId}",
                process.ProcessId);
            return MessageHandlingResult.Requeue;
        }
    }

    private async Task ExecuteProcessesAsync(int workerId, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Execution worker {WorkerId} started", workerId);

        await foreach (var context in _executionChannel.Reader.ReadAllAsync(cancellationToken))
        {
            await ExecuteWithPolicyAsync(context, cancellationToken);
        }

        _logger.LogInformation("Execution worker {WorkerId} stopped", workerId);
    }

    private async Task ExecuteWithPolicyAsync(
        ProcessExecutionContext context,
        CancellationToken cancellationToken)
    {
        var process = context.Process;
        var policy = context.Policy;

        // Get or create semaphore for process type concurrency control
        var typeSemaphore = GetOrCreateTypeSemaphore(process.ProcessType, policy.MaxConcurrentExecutions);

        await _globalSemaphore.WaitAsync(cancellationToken);
        try
        {
            await typeSemaphore.WaitAsync(cancellationToken);
            try
            {
                _logger.LogInformation(
                    "Executing process {ProcessId} with policy: Timeout={Timeout}s, MaxRetries={MaxRetries}, MaxConcurrency={MaxConcurrency}",
                    process.ProcessId,
                    policy.TimeoutSeconds,
                    policy.MaxRetryAttempts,
                    policy.MaxConcurrentExecutions);

                await ExecuteProcessWithRetryAsync(context, cancellationToken);
            }
            finally
            {
                typeSemaphore.Release();
            }
        }
        finally
        {
            _globalSemaphore.Release();
        }
    }

    private async Task ExecuteProcessWithRetryAsync(
        ProcessExecutionContext context,
        CancellationToken cancellationToken)
    {
        var process = context.Process;
        var policy = context.Policy;
        var attemptCount = process.RetryCount ?? 0;

        for (var attempt = attemptCount; attempt <= policy.MaxRetryAttempts; attempt++)
        {
            try
            {
                // Create timeout token
                using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                timeoutCts.CancelAfter(TimeSpan.FromSeconds(policy.TimeoutSeconds));

                // Update process status
                process.Status = ProcessStatus.Running;
                process.RetryCount = attempt;
                process.UpdatedAt = DateTime.UtcNow;
                await _repository.UpdateAsync(process);

                // Get handler
                var handler = _handlerFactory.GetHandler(process.ProcessType);

                // Execute with timeout
                await handler.ExecuteAsync(process, timeoutCts.Token);

                // Success - update status
                process.Status = ProcessStatus.Completed;
                process.Progress = 100;
                process.CompletedAt = DateTime.UtcNow;
                process.UpdatedAt = DateTime.UtcNow;
                await _repository.UpdateAsync(process);

                _logger.LogInformation(
                    "Process {ProcessId} completed successfully after {Attempts} attempt(s)",
                    process.ProcessId,
                    attempt + 1);

                return; // Success
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                // Worker shutdown - requeue
                _logger.LogWarning(
                    "Process {ProcessId} execution cancelled due to worker shutdown",
                    process.ProcessId);

                process.Status = ProcessStatus.Pending;
                process.UpdatedAt = DateTime.UtcNow;
                await _repository.UpdateAsync(process);
                throw;
            }
            catch (OperationCanceledException)
            {
                // Timeout
                _logger.LogWarning(
                    "Process {ProcessId} execution timed out after {Timeout}s (attempt {Attempt}/{MaxAttempts})",
                    process.ProcessId,
                    policy.TimeoutSeconds,
                    attempt + 1,
                    policy.MaxRetryAttempts + 1);

                if (attempt >= policy.MaxRetryAttempts)
                {
                    await HandleMaxRetriesExceededAsync(process, "Execution timeout");
                    return;
                }

                // Retry with exponential backoff
                await DelayForRetryAsync(attempt, policy, cancellationToken);
            }
            catch (ProcessExecutionException ex)
            {
                _logger.LogError(
                    ex,
                    "Process {ProcessId} execution failed (attempt {Attempt}/{MaxAttempts}): {Error}",
                    process.ProcessId,
                    attempt + 1,
                    policy.MaxRetryAttempts + 1,
                    ex.Message);

                if (attempt >= policy.MaxRetryAttempts || !process.Retryable)
                {
                    await HandleMaxRetriesExceededAsync(process, ex.Message);
                    return;
                }

                // Retry with exponential backoff
                await DelayForRetryAsync(attempt, policy, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Unexpected error executing process {ProcessId} (attempt {Attempt}/{MaxAttempts})",
                    process.ProcessId,
                    attempt + 1,
                    policy.MaxRetryAttempts + 1);

                if (attempt >= policy.MaxRetryAttempts)
                {
                    await HandleMaxRetriesExceededAsync(process, ex.Message);
                    return;
                }

                await DelayForRetryAsync(attempt, policy, cancellationToken);
            }
        }
    }

    private async Task DelayForRetryAsync(
        int attemptNumber,
        ProcessTypePolicy policy,
        CancellationToken cancellationToken)
    {
        // Exponential backoff: 2^attempt seconds (1s, 2s, 4s, 8s, ...)
        var delaySeconds = Math.Min(Math.Pow(2, attemptNumber), 60); // Max 60 seconds
        var delay = TimeSpan.FromSeconds(delaySeconds);

        _logger.LogInformation(
            "Waiting {Delay}s before retry attempt {Attempt}",
            delaySeconds,
            attemptNumber + 1);

        await Task.Delay(delay, cancellationToken);
    }

    private async Task HandleMaxRetriesExceededAsync(Process process, string errorMessage)
    {
        _logger.LogError(
            "Process {ProcessId} failed after {MaxAttempts} attempts: {Error}",
            process.ProcessId,
            (process.RetryCount ?? 0) + 1,
            errorMessage);

        process.Status = ProcessStatus.Failed;
        process.FailedAt = DateTime.UtcNow;
        process.UpdatedAt = DateTime.UtcNow;

        if (process.Errors == null)
        {
            process.Errors = new List<ProcessError>();
        }

        process.Errors.Add(new ProcessError
        {
            ErrorCode = "MAX_RETRIES_EXCEEDED",
            Message = errorMessage,
            Timestamp = DateTime.UtcNow,
            Retryable = false
        });

        await _repository.UpdateAsync(process);
    }

    private bool ValidatePolicy(ProcessTypePolicy policy, Process process)
    {
        // Validate timeout
        if (policy.TimeoutSeconds <= 0)
        {
            _logger.LogError(
                "Invalid timeout in policy: {Timeout}s",
                policy.TimeoutSeconds);
            return false;
        }

        // Validate max retries
        if (policy.MaxRetryAttempts < 0)
        {
            _logger.LogError(
                "Invalid max retry attempts in policy: {MaxRetries}",
                policy.MaxRetryAttempts);
            return false;
        }

        // Validate concurrency
        if (policy.MaxConcurrentExecutions <= 0)
        {
            _logger.LogError(
                "Invalid max concurrent executions in policy: {MaxConcurrency}",
                policy.MaxConcurrentExecutions);
            return false;
        }

        return true;
    }

    private SemaphoreSlim GetOrCreateTypeSemaphore(string processType, int maxConcurrency)
    {
        lock (_processTypeSemaphores)
        {
            if (!_processTypeSemaphores.TryGetValue(processType, out var semaphore))
            {
                semaphore = new SemaphoreSlim(maxConcurrency, maxConcurrency);
                _processTypeSemaphores[processType] = semaphore;

                _logger.LogInformation(
                    "Created concurrency semaphore for process type {ProcessType} with limit {MaxConcurrency}",
                    processType,
                    maxConcurrency);
            }

            return semaphore;
        }
    }

    public override void Dispose()
    {
        _globalSemaphore?.Dispose();

        foreach (var semaphore in _processTypeSemaphores.Values)
        {
            semaphore?.Dispose();
        }

        base.Dispose();
    }
}

/// <summary>
/// Context for process execution including policy.
/// </summary>
internal record ProcessExecutionContext
{
    public required Process Process { get; init; }
    public required ProcessTypePolicy Policy { get; init; }
    public required string MessageId { get; init; }
    public required string CorrelationId { get; init; }
}
