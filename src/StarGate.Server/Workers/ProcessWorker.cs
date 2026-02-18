using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using StarGate.Core.Abstractions;
using StarGate.Core.Domain;
using System.Threading.Channels;

namespace StarGate.Server.Workers;

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
        await _consumer.StartConsumingAsync<Process>(
            async (process, context) => await HandleMessageAsync(process, context, cancellationToken),
            cancellationToken);
    }

    private async Task HandleMessageAsync(
        Process process,
        MessageContext context,
        CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogInformation(
                "Received message for process {ProcessId}, Type: {ProcessType}, ClientId: {ClientId}",
                process.ProcessId,
                process.ProcessType,
                process.ClientId);

            // Load policy for this process
            var policy = await _policyProvider.GetPolicyAsync(
                process.ClientId,
                process.ProcessType,
                cancellationToken);

            if (policy == null)
            {
                _logger.LogError(
                    "No policy found for process type {ProcessType}, client {ClientId}",
                    process.ProcessType,
                    process.ClientId);
                await context.RejectAsync();
                return;
            }

            // Validate policy constraints
            if (!ValidatePolicy(policy))
            {
                _logger.LogError(
                    "Policy validation failed for process {ProcessId}",
                    process.ProcessId);
                await context.RejectAsync();
                return;
            }

            // Queue for execution with policy context
            var executionContext = new ProcessExecutionContext
            {
                Process = process,
                Policy = policy,
                MessageId = context.MessageId,
                CorrelationId = context.CorrelationId ?? process.ProcessId.ToString()
            };

            await _executionChannel.Writer.WriteAsync(executionContext, cancellationToken);

            _logger.LogDebug(
                "Process {ProcessId} queued for execution",
                process.ProcessId);

            await context.AcknowledgeAsync();
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation(
                "Message handling cancelled for process {ProcessId}",
                process.ProcessId);
            await context.RequeueAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Error handling message for process {ProcessId}",
                process.ProcessId);
            await context.RequeueAsync();
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
        var maxConcurrency = policy.MaxConcurrentProcesses ?? Environment.ProcessorCount * 2;
        var typeSemaphore = GetOrCreateTypeSemaphore(process.ProcessType, maxConcurrency);

        await _globalSemaphore.WaitAsync(cancellationToken);
        try
        {
            await typeSemaphore.WaitAsync(cancellationToken);
            try
            {
                _logger.LogInformation(
                    "Executing process {ProcessId} with policy: Timeout={Timeout}s, MaxRetries={MaxRetries}, MaxConcurrency={MaxConcurrency}",
                    process.ProcessId,
                    policy.Timeout.TotalSeconds,
                    policy.RetryPolicy.MaxAttempts,
                    maxConcurrency);

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
        var attemptCount = process.RetryCount;

        for (var attempt = attemptCount; attempt <= policy.RetryPolicy.MaxAttempts; attempt++)
        {
            try
            {
                // Create timeout token
                using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                timeoutCts.CancelAfter(policy.Timeout);

                // Update process status
                var updatedProcess = process with
                {
                    Status = ProcessStatus.Processing,
                    RetryCount = attempt,
                    UpdatedAt = DateTime.UtcNow
                };
                await _repository.UpdateAsync(updatedProcess);

                // Get handler
                var handler = _handlerFactory.GetHandler(process.ProcessType);

                // Execute with timeout
                await handler.ExecuteAsync(updatedProcess, timeoutCts.Token);

                // Success - update status
                updatedProcess = updatedProcess with
                {
                    Status = ProcessStatus.Completed,
                    Progress = 100,
                    CompletedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };
                await _repository.UpdateAsync(updatedProcess);

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

                var requeuedProcess = process with
                {
                    Status = ProcessStatus.Accepted,
                    UpdatedAt = DateTime.UtcNow
                };
                await _repository.UpdateAsync(requeuedProcess);
                throw;
            }
            catch (OperationCanceledException)
            {
                // Timeout
                _logger.LogWarning(
                    "Process {ProcessId} execution timed out after {Timeout}s (attempt {Attempt}/{MaxAttempts})",
                    process.ProcessId,
                    policy.Timeout.TotalSeconds,
                    attempt + 1,
                    policy.RetryPolicy.MaxAttempts + 1);

                if (attempt >= policy.RetryPolicy.MaxAttempts)
                {
                    await HandleMaxRetriesExceededAsync(process, "Execution timeout");
                    return;
                }

                // Retry with exponential backoff
                await DelayForRetryAsync(attempt, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Process {ProcessId} execution failed (attempt {Attempt}/{MaxAttempts}): {Error}",
                    process.ProcessId,
                    attempt + 1,
                    policy.RetryPolicy.MaxAttempts + 1,
                    ex.Message);

                if (attempt >= policy.RetryPolicy.MaxAttempts || !process.Retryable)
                {
                    await HandleMaxRetriesExceededAsync(process, ex.Message);
                    return;
                }

                // Retry with exponential backoff
                await DelayForRetryAsync(attempt, cancellationToken);
            }
        }
    }

    private async Task DelayForRetryAsync(
        int attemptNumber,
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
            process.RetryCount + 1,
            errorMessage);

        var failedProcess = process with
        {
            Status = ProcessStatus.Failed,
            Error = new ProcessError("MAX_RETRIES_EXCEEDED", errorMessage, null),
            CompletedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        await _repository.UpdateAsync(failedProcess);
    }

    private bool ValidatePolicy(EffectivePolicy policy)
    {
        // Validate timeout
        if (policy.Timeout <= TimeSpan.Zero)
        {
            _logger.LogError(
                "Invalid timeout in policy: {Timeout}s",
                policy.Timeout.TotalSeconds);
            return false;
        }

        // Validate max retries
        if (policy.RetryPolicy.MaxAttempts < 0)
        {
            _logger.LogError(
                "Invalid max retry attempts in policy: {MaxRetries}",
                policy.RetryPolicy.MaxAttempts);
            return false;
        }

        // Validate concurrency
        if (policy.MaxConcurrentProcesses.HasValue && policy.MaxConcurrentProcesses.Value <= 0)
        {
            _logger.LogError(
                "Invalid max concurrent executions in policy: {MaxConcurrency}",
                policy.MaxConcurrentProcesses.Value);
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
    public required EffectivePolicy Policy { get; init; }
    public required string MessageId { get; init; }
    public required string CorrelationId { get; init; }
}
