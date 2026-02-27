using Microsoft.Extensions.Logging;
using StarGate.Contracts.Requests;
using StarGate.Core.Abstractions;
using StarGate.Core.Domain;
using StarGate.Core.Exceptions;
using StarGate.Core.Messages;

namespace StarGate.Application.Services;

/// <summary>
/// Service for managing process lifecycle and operations.
/// Implements GUID generation, idempotency handling, status transition validation,
/// policy enforcement, and message broker publishing for asynchronous processing.
/// </summary>
public class ProcessService : IProcessService
{
    private readonly IProcessRepository _processRepository;
    private readonly IIdempotencyService _idempotencyService;
    private readonly IMessageBroker _messageBroker;
    private readonly IPolicyProvider _policyProvider;
    private readonly ILogger<ProcessService> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="ProcessService"/> class.
    /// </summary>
    /// <param name="processRepository">Repository for process persistence.</param>
    /// <param name="idempotencyService">Service for idempotency key management.</param>
    /// <param name="messageBroker">Message broker for asynchronous processing.</param>
    /// <param name="policyProvider">Provider for policy resolution and enforcement.</param>
    /// <param name="logger">Logger instance.</param>
    /// <exception cref="ArgumentNullException">If any parameter is null.</exception>
    public ProcessService(
        IProcessRepository processRepository,
        IIdempotencyService idempotencyService,
        IMessageBroker messageBroker,
        IPolicyProvider policyProvider,
        ILogger<ProcessService> logger)
    {
        _processRepository = processRepository ?? throw new ArgumentNullException(nameof(processRepository));
        _idempotencyService = idempotencyService ?? throw new ArgumentNullException(nameof(idempotencyService));
        _messageBroker = messageBroker ?? throw new ArgumentNullException(nameof(messageBroker));
        _policyProvider = policyProvider ?? throw new ArgumentNullException(nameof(policyProvider));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc />
    public async Task<Process> CreateProcessAsync(
        string clientId,
        string processType,
        string clientProcessId,
        string idempotencyKey,
        Dictionary<string, string>? metadata = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(clientId);
        ArgumentException.ThrowIfNullOrWhiteSpace(processType);
        ArgumentException.ThrowIfNullOrWhiteSpace(clientProcessId);
        ArgumentException.ThrowIfNullOrWhiteSpace(idempotencyKey);

        _logger.LogInformation(
            "Creating process: ClientId={ClientId}, ProcessType={ProcessType}, ClientProcessId={ClientProcessId}, IdempotencyKey={IdempotencyKey}",
            clientId,
            processType,
            clientProcessId,
            idempotencyKey);

        // Retrieve effective policy for this client and process type
        EffectivePolicy policy;
        try
        {
            policy = await _policyProvider.GetEffectivePolicyAsync(
                clientId,
                processType,
                cancellationToken);
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(
                ex,
                "No policy found for ClientId={ClientId}, ProcessType={ProcessType}",
                clientId,
                processType);

            throw new PolicyNotFoundException(clientId, processType);
        }

        _logger.LogDebug(
            "Policy retrieved: ClientId={ClientId}, ProcessType={ProcessType}, MaxAttempts={MaxAttempts}, Timeout={Timeout}, MaxConcurrent={MaxConcurrent}",
            clientId,
            processType,
            policy.RetryPolicy.MaxAttempts,
            policy.Timeout,
            policy.MaxConcurrentProcesses);

        // Check concurrency limit before proceeding
        await EnforceConcurrencyLimitAsync(clientId, processType, policy, cancellationToken);

        // Two-tier idempotency check strategy:
        // 1. Fast path: Check Redis cache first
        var cachedProcessId = await _idempotencyService.GetProcessIdByIdempotencyKeyAsync(
            clientId,
            idempotencyKey,
            cancellationToken);

        if (cachedProcessId.HasValue)
        {
            _logger.LogWarning(
                "Idempotency key found in cache: IdempotencyKey={IdempotencyKey}, ClientId={ClientId}, ProcessId={ProcessId}",
                idempotencyKey,
                clientId,
                cachedProcessId.Value);

            throw new DuplicateProcessException(idempotencyKey);
        }

        // 2. Fallback: Check database if not in cache
        var existingProcess = await _processRepository.GetByIdempotencyKeyAsync(
            clientId,
            idempotencyKey,
            cancellationToken);

        if (existingProcess is not null)
        {
            _logger.LogWarning(
                "Idempotency key found in database (cache miss): IdempotencyKey={IdempotencyKey}, ClientId={ClientId}, ProcessId={ProcessId}",
                idempotencyKey,
                clientId,
                existingProcess.ProcessId);

            // Repopulate cache from database
            await _idempotencyService.StoreIdempotencyKeyAsync(
                clientId,
                idempotencyKey,
                existingProcess.ProcessId,
                cancellationToken: cancellationToken);

            throw new DuplicateProcessException(idempotencyKey);
        }

        // Generate new GUID for the process
        var processId = Guid.NewGuid();

        // Reserve idempotency key BEFORE creating process (prevents race conditions)
        await _idempotencyService.StoreIdempotencyKeyAsync(
            clientId,
            idempotencyKey,
            processId,
            cancellationToken: cancellationToken);

        try
        {
            var now = DateTime.UtcNow;

            // Calculate timeout based on policy
            var timeoutAt = now.Add(policy.Timeout);

            // Calculate retention expiration based on policy
            var retentionExpiresAt = now.Add(policy.ResultRetention);

            // Create new process entity (immutable record) with policy-based settings
            var process = new Process
            {
                ProcessId = processId,
                ClientId = clientId,
                ProcessType = processType,
                ClientProcessId = clientProcessId,
                IdempotencyKey = idempotencyKey,
                Status = ProcessStatus.Accepted, // Initial status
                Progress = 0,
                Retryable = policy.RetryPolicy.MaxAttempts > 0,
                MaxRetries = policy.RetryPolicy.MaxAttempts,
                RetryCount = 0,
                TimeoutAt = timeoutAt,
                RetentionExpiresAt = retentionExpiresAt,
                CreatedAt = now,
                UpdatedAt = now
            };

            // Persist the process
            await _processRepository.CreateAsync(process, cancellationToken);

            _logger.LogInformation(
                "Process created successfully: ProcessId={ProcessId}, ClientId={ClientId}, ProcessType={ProcessType}, TimeoutAt={TimeoutAt}, MaxRetries={MaxRetries}",
                processId,
                clientId,
                processType,
                timeoutAt,
                policy.RetryPolicy.MaxAttempts);

            // Publish message to broker for asynchronous processing
            try
            {
                var message = ProcessMessage.FromProcess(process);
                var routingKey = $"process.{processType}";

                _logger.LogDebug(
                    "Publishing process message: ProcessId={ProcessId}, RoutingKey={RoutingKey}",
                    processId,
                    routingKey);

                await _messageBroker.PublishAsync(
                    message,
                    routingKey,
                    cancellationToken);

                _logger.LogInformation(
                    "Process message published successfully: ProcessId={ProcessId}, RoutingKey={RoutingKey}",
                    processId,
                    routingKey);
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Failed to publish process message: ProcessId={ProcessId}, ProcessType={ProcessType}",
                    processId,
                    processType);

                // Update process status to Failed and add error details
                var failedProcess = process with
                {
                    Status = ProcessStatus.Failed,
                    UpdatedAt = DateTime.UtcNow,
                    FailedAt = DateTime.UtcNow
                };

                await _processRepository.UpdateAsync(failedProcess, cancellationToken);

                // Rollback idempotency key to allow retry
                await _idempotencyService.RemoveIdempotencyKeyAsync(
                    clientId,
                    idempotencyKey,
                    cancellationToken);

                throw new MessageBrokerException(
                    $"Failed to publish process message for ProcessId={processId}. Process marked as Failed and idempotency key removed for retry.",
                    ex);
            }

            return process;
        }
        catch (MessageBrokerException)
        {
            // Re-throw broker exceptions (already handled above)
            throw;
        }
        catch (Exception ex)
        {
            // Rollback: Remove idempotency key if process creation fails
            _logger.LogError(
                ex,
                "Failed to create process, rolling back idempotency key: ProcessId={ProcessId}, IdempotencyKey={IdempotencyKey}",
                processId,
                idempotencyKey);

            await _idempotencyService.RemoveIdempotencyKeyAsync(
                clientId,
                idempotencyKey,
                cancellationToken);

            throw;
        }
    }

    /// <summary>
    /// Enforces concurrency limit policy for a client and process type.
    /// Checks if the number of active processes exceeds the policy limit.
    /// Active processes are those in Accepted or Processing status.
    /// </summary>
    /// <param name="clientId">Client identifier.</param>
    /// <param name="processType">Process type.</param>
    /// <param name="policy">Effective policy containing concurrency limit.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <exception cref="PolicyViolationException">When concurrency limit is exceeded.</exception>
    private async Task EnforceConcurrencyLimitAsync(
        string clientId,
        string processType,
        EffectivePolicy policy,
        CancellationToken cancellationToken)
    {
        // No limit if MaxConcurrentProcesses is null or <= 0
        if (policy.MaxConcurrentProcesses is null or <= 0)
        {
            _logger.LogDebug(
                "No concurrency limit enforced: ClientId={ClientId}, ProcessType={ProcessType}",
                clientId,
                processType);
            return;
        }

        var activeProcessCount = await _processRepository.CountActiveProcessesAsync(
            clientId,
            processType,
            cancellationToken);

        _logger.LogDebug(
            "Checking concurrency limit: ClientId={ClientId}, ProcessType={ProcessType}, Active={Active}, Limit={Limit}",
            clientId,
            processType,
            activeProcessCount,
            policy.MaxConcurrentProcesses);

        if (activeProcessCount >= policy.MaxConcurrentProcesses)
        {
            _logger.LogWarning(
                "Concurrency limit exceeded: ClientId={ClientId}, ProcessType={ProcessType}, Active={Active}, Limit={Limit}",
                clientId,
                processType,
                activeProcessCount,
                policy.MaxConcurrentProcesses);

            throw new PolicyViolationException(
                clientId,
                processType,
                $"Maximum concurrent processes limit exceeded ({activeProcessCount}/{policy.MaxConcurrentProcesses})");
        }
    }

    /// <inheritdoc />
    public async Task<Process> SubmitProcessAsync(
        string clientId,
        SubmitProcessRequest request,
        CancellationToken cancellationToken = default)
    {
        return await CreateProcessAsync(
            clientId,
            request.ProcessType,
            request.ClientProcessId,
            request.IdempotencyKey,
            null, // metadata not used in current implementation
            cancellationToken);
    }

    /// <inheritdoc />
    public async Task<Process> GetProcessAsync(
        Guid processId,
        CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Retrieving process: ProcessId={ProcessId}", processId);

        var process = await _processRepository.GetByIdAsync(processId, cancellationToken);

        if (process is null)
        {
            _logger.LogWarning("Process not found: ProcessId={ProcessId}", processId);
            throw new ProcessNotFoundException(processId);
        }

        return process;
    }

    /// <inheritdoc />
    public async Task<Process?> GetProcessByIdAsync(
        Guid processId,
        CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Retrieving process: ProcessId={ProcessId}", processId);
        return await _processRepository.GetByIdAsync(processId, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<Process> GetProcessByClientIdAsync(
        string clientId,
        string clientProcessId,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(clientId);
        ArgumentException.ThrowIfNullOrWhiteSpace(clientProcessId);

        _logger.LogDebug(
            "Retrieving process: ClientId={ClientId}, ClientProcessId={ClientProcessId}",
            clientId,
            clientProcessId);

        var process = await _processRepository.GetByClientProcessIdAsync(
            clientId,
            clientProcessId,
            cancellationToken);

        if (process is null)
        {
            _logger.LogWarning(
                "Process not found: ClientId={ClientId}, ClientProcessId={ClientProcessId}",
                clientId,
                clientProcessId);

            throw new ProcessNotFoundException(clientId, clientProcessId);
        }

        return process;
    }

    /// <inheritdoc />
    public async Task<Process?> GetProcessByClientProcessIdAsync(
        string clientId,
        string clientProcessId,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(clientId);
        ArgumentException.ThrowIfNullOrWhiteSpace(clientProcessId);

        _logger.LogDebug(
            "Retrieving process: ClientId={ClientId}, ClientProcessId={ClientProcessId}",
            clientId,
            clientProcessId);

        return await _processRepository.GetByClientProcessIdAsync(
            clientId,
            clientProcessId,
            cancellationToken);
    }

    /// <inheritdoc />
    public async Task<Process> UpdateProcessStatusAsync(
        Guid processId,
        ProcessStatus newStatus,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "Updating process status: ProcessId={ProcessId}, NewStatus={NewStatus}",
            processId,
            newStatus);

        var process = await GetProcessAsync(processId, cancellationToken);

        // Validate status transition
        ValidateStatusTransition(process.Status, newStatus);

        // Update status and timestamp
        var previousStatus = process.Status;
        var now = DateTime.UtcNow;

        // Update status-specific fields
        var updatedProcess = UpdateStatusSpecificFields(process, newStatus, now);

        await _processRepository.UpdateAsync(updatedProcess, cancellationToken);

        _logger.LogInformation(
            "Process status updated: ProcessId={ProcessId}, From={FromStatus}, To={ToStatus}",
            processId,
            previousStatus,
            newStatus);

        return updatedProcess;
    }

    /// <inheritdoc />
    public async Task<Process> UpdateProcessProgressAsync(
        Guid processId,
        int progress,
        CancellationToken cancellationToken = default)
    {
        if (progress < 0 || progress > 100)
        {
            throw new ArgumentOutOfRangeException(
                nameof(progress),
                progress,
                "Progress must be between 0 and 100");
        }

        _logger.LogDebug(
            "Updating process progress: ProcessId={ProcessId}, Progress={Progress}",
            processId,
            progress);

        var process = await GetProcessAsync(processId, cancellationToken);

        var updatedProcess = process with
        {
            Progress = progress,
            UpdatedAt = DateTime.UtcNow
        };

        await _processRepository.UpdateAsync(updatedProcess, cancellationToken);

        _logger.LogDebug(
            "Process progress updated: ProcessId={ProcessId}, Progress={Progress}",
            processId,
            progress);

        return updatedProcess;
    }

    /// <inheritdoc />
    public async Task<Process> RecordProcessErrorAsync(
        Guid processId,
        string errorCode,
        string message,
        bool retryable = false,
        CancellationToken cancellationToken = default)
    {
        _logger.LogWarning(
            "Recording process error: ProcessId={ProcessId}, ErrorCode={ErrorCode}, Message={Message}, Retryable={Retryable}",
            processId,
            errorCode,
            message,
            retryable);

        var process = await GetProcessAsync(processId, cancellationToken);

        var updatedProcess = process.AddError(errorCode, message, retryable) with
        {
            UpdatedAt = DateTime.UtcNow
        };

        await _processRepository.UpdateAsync(updatedProcess, cancellationToken);

        _logger.LogInformation(
            "Process error recorded: ProcessId={ProcessId}, ErrorCode={ErrorCode}, TotalErrors={TotalErrors}",
            processId,
            errorCode,
            updatedProcess.Errors?.Count ?? 0);

        return updatedProcess;
    }

    /// <inheritdoc />
    public async Task<Process> IncrementRetryCountAsync(
        Guid processId,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "Incrementing retry count: ProcessId={ProcessId}",
            processId);

        var process = await GetProcessAsync(processId, cancellationToken);

        var updatedProcess = process with
        {
            RetryCount = process.RetryCount + 1,
            UpdatedAt = DateTime.UtcNow
        };

        await _processRepository.UpdateAsync(updatedProcess, cancellationToken);

        _logger.LogInformation(
            "Retry count incremented: ProcessId={ProcessId}, RetryCount={RetryCount}, MaxRetries={MaxRetries}",
            processId,
            updatedProcess.RetryCount,
            updatedProcess.MaxRetries);

        return updatedProcess;
    }

    /// <inheritdoc />
    public async Task<Process> TransitionToProcessingAsync(
        Guid processId,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "Transitioning process to Processing: ProcessId={ProcessId}",
            processId);

        var process = await GetProcessAsync(processId, cancellationToken);

        ValidateStatusTransition(process.Status, ProcessStatus.Processing);

        var updatedProcess = process with
        {
            Status = ProcessStatus.Processing,
            UpdatedAt = DateTime.UtcNow
        };

        await _processRepository.UpdateAsync(updatedProcess, cancellationToken);

        _logger.LogInformation(
            "Process transitioned to Processing: ProcessId={ProcessId}",
            processId);

        return updatedProcess;
    }

    /// <inheritdoc />
    public async Task<Process> CompleteProcessAsync(
        Guid processId,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "Completing process: ProcessId={ProcessId}",
            processId);

        var process = await GetProcessAsync(processId, cancellationToken);

        ValidateStatusTransition(process.Status, ProcessStatus.Completed);

        var now = DateTime.UtcNow;
        var updatedProcess = process with
        {
            Status = ProcessStatus.Completed,
            Progress = 100,
            CompletedAt = now,
            UpdatedAt = now
        };

        await _processRepository.UpdateAsync(updatedProcess, cancellationToken);

        _logger.LogInformation(
            "Process completed successfully: ProcessId={ProcessId}, Duration={Duration}ms",
            processId,
            (updatedProcess.CompletedAt!.Value - process.CreatedAt).TotalMilliseconds);

        return updatedProcess;
    }

    /// <inheritdoc />
    public async Task<Process> FailProcessAsync(
        Guid processId,
        string errorCode,
        string errorMessage,
        bool canRetry = false,
        CancellationToken cancellationToken = default)
    {
        _logger.LogWarning(
            "Failing process: ProcessId={ProcessId}, ErrorCode={ErrorCode}, CanRetry={CanRetry}",
            processId,
            errorCode,
            canRetry);

        var process = await GetProcessAsync(processId, cancellationToken);

        // Record error
        var processWithError = process.AddError(errorCode, errorMessage, canRetry);

        // Determine if retry should happen
        var shouldRetry = canRetry &&
                         process.Retryable &&
                         !process.IsRetryLimitExceeded;

        var now = DateTime.UtcNow;
        Process updatedProcess;

        if (shouldRetry)
        {
            ValidateStatusTransition(process.Status, ProcessStatus.Retrying);
            updatedProcess = processWithError with
            {
                Status = ProcessStatus.Retrying,
                RetryCount = process.RetryCount + 1,
                UpdatedAt = now
            };

            _logger.LogInformation(
                "Process will retry: ProcessId={ProcessId}, RetryCount={RetryCount}/{MaxRetries}",
                processId,
                updatedProcess.RetryCount,
                updatedProcess.MaxRetries);
        }
        else
        {
            ValidateStatusTransition(process.Status, ProcessStatus.Failed);
            updatedProcess = processWithError with
            {
                Status = ProcessStatus.Failed,
                FailedAt = now,
                UpdatedAt = now
            };

            var reason = !canRetry ? "non-retryable error" :
                        !process.Retryable ? "process not retryable" :
                        "retry limit exceeded";

            _logger.LogWarning(
                "Process failed permanently: ProcessId={ProcessId}, Reason={Reason}, RetryCount={RetryCount}",
                processId,
                reason,
                updatedProcess.RetryCount);
        }

        await _processRepository.UpdateAsync(updatedProcess, cancellationToken);

        return updatedProcess;
    }

    /// <inheritdoc />
    public async Task<Process> RejectProcessAsync(
        Guid processId,
        string reason,
        CancellationToken cancellationToken = default)
    {
        _logger.LogWarning(
            "Rejecting process: ProcessId={ProcessId}, Reason={Reason}",
            processId,
            reason);

        var process = await GetProcessAsync(processId, cancellationToken);

        ValidateStatusTransition(process.Status, ProcessStatus.Rejected);

        var updatedProcess = process.AddError("PROCESS_REJECTED", reason, retryable: false) with
        {
            Status = ProcessStatus.Rejected,
            UpdatedAt = DateTime.UtcNow
        };

        await _processRepository.UpdateAsync(updatedProcess, cancellationToken);

        _logger.LogInformation(
            "Process rejected: ProcessId={ProcessId}",
            processId);

        return updatedProcess;
    }

    /// <inheritdoc />
    public async Task<Process> CheckTimeoutAsync(
        Guid processId,
        CancellationToken cancellationToken = default)
    {
        var process = await GetProcessAsync(processId, cancellationToken);

        if (!process.IsTimedOut)
        {
            return process;
        }

        _logger.LogWarning(
            "Process timed out: ProcessId={ProcessId}, TimeoutAt={TimeoutAt}",
            processId,
            process.TimeoutAt);

        return await FailProcessAsync(
            processId,
            "PROCESS_TIMEOUT",
            $"Process exceeded timeout of {(process.TimeoutAt!.Value - process.CreatedAt).TotalSeconds} seconds",
            canRetry: true,
            cancellationToken);
    }

    /// <summary>
    /// Validates that a status transition is allowed according to the process state machine.
    /// </summary>
    /// <param name="currentStatus">Current process status.</param>
    /// <param name="newStatus">Target process status.</param>
    /// <exception cref="InvalidStateTransitionException">If transition is not allowed.</exception>
    private void ValidateStatusTransition(ProcessStatus currentStatus, ProcessStatus newStatus)
    {
        var validTransitions = new Dictionary<ProcessStatus, ProcessStatus[]>
        {
            [ProcessStatus.Pending] = new[]
            {
                ProcessStatus.Accepted,
                ProcessStatus.Rejected
            },
            [ProcessStatus.Accepted] = new[]
            {
                ProcessStatus.Processing,
                ProcessStatus.Failed,
                ProcessStatus.Rejected
            },
            [ProcessStatus.Processing] = new[]
            {
                ProcessStatus.Completed,
                ProcessStatus.Failed,
                ProcessStatus.Retrying
            },
            [ProcessStatus.Retrying] = new[]
            {
                ProcessStatus.Processing,
                ProcessStatus.Failed
            },
            [ProcessStatus.Failed] = Array.Empty<ProcessStatus>(), // Terminal state
            [ProcessStatus.Completed] = Array.Empty<ProcessStatus>(), // Terminal state
            [ProcessStatus.Rejected] = Array.Empty<ProcessStatus>() // Terminal state
        };

        if (!validTransitions.ContainsKey(currentStatus))
        {
            throw new InvalidStateTransitionException(
                currentStatus,
                newStatus,
                $"Invalid current status: {currentStatus}");
        }

        if (!validTransitions[currentStatus].Contains(newStatus))
        {
            throw new InvalidStateTransitionException(
                currentStatus,
                newStatus,
                $"Cannot transition from {currentStatus} to {newStatus}");
        }
    }

    /// <summary>
    /// Updates status-specific fields based on the new status.
    /// </summary>
    /// <param name="process">Current process.</param>
    /// <param name="newStatus">New status to transition to.</param>
    /// <param name="now">Current UTC timestamp.</param>
    /// <returns>Updated process with status-specific fields set.</returns>
    private Process UpdateStatusSpecificFields(Process process, ProcessStatus newStatus, DateTime now)
    {
        return newStatus switch
        {
            ProcessStatus.Completed => process with
            {
                Status = newStatus,
                CompletedAt = now,
                Progress = 100,
                UpdatedAt = now
            },
            ProcessStatus.Failed => process with
            {
                Status = newStatus,
                FailedAt = now,
                UpdatedAt = now
            },
            _ => process with
            {
                Status = newStatus,
                UpdatedAt = now
            }
        };
    }
}
