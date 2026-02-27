using Microsoft.Extensions.Logging;
using StarGate.Core.Abstractions;
using StarGate.Core.Domain;
using StarGate.Core.Exceptions;

namespace StarGate.Application.Services;

/// <summary>
/// Service for managing process lifecycle and operations.
/// Implements GUID generation, idempotency handling, and status transition validation.
/// </summary>
public class ProcessService : IProcessService
{
    private readonly IProcessRepository _processRepository;
    private readonly ILogger<ProcessService> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="ProcessService"/> class.
    /// </summary>
    /// <param name="processRepository">Repository for process persistence.</param>
    /// <param name="logger">Logger instance.</param>
    /// <exception cref="ArgumentNullException">If any parameter is null.</exception>
    public ProcessService(
        IProcessRepository processRepository,
        ILogger<ProcessService> logger)
    {
        _processRepository = processRepository ?? throw new ArgumentNullException(nameof(processRepository));
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

        // Check for idempotency (duplicate)
        var existingProcess = await _processRepository.GetByIdempotencyKeyAsync(
            clientId,
            idempotencyKey,
            cancellationToken);

        if (existingProcess is not null)
        {
            _logger.LogWarning(
                "Process with IdempotencyKey={IdempotencyKey} already exists for ClientId={ClientId}: ProcessId={ProcessId}",
                idempotencyKey,
                clientId,
                existingProcess.ProcessId);

            throw new DuplicateProcessException(idempotencyKey);
        }

        // Generate new GUID for the process
        var processId = Guid.NewGuid();
        var now = DateTime.UtcNow;

        // Create new process entity
        var process = new Process
        {
            ProcessId = processId,
            ClientId = clientId,
            ProcessType = processType,
            ClientProcessId = clientProcessId,
            IdempotencyKey = idempotencyKey,
            Status = ProcessStatus.Pending,
            Progress = 0,
            Retryable = true,
            Metadata = metadata ?? new Dictionary<string, string>(),
            CreatedAt = now,
            UpdatedAt = now
        };

        // Persist the process
        await _processRepository.CreateAsync(process, cancellationToken);

        _logger.LogInformation(
            "Process created successfully: ProcessId={ProcessId}, ClientId={ClientId}, ProcessType={ProcessType}",
            processId,
            clientId,
            processType);

        return process;
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
    public async Task UpdateProcessStatusAsync(
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
        process.Status = newStatus;
        process.UpdatedAt = DateTime.UtcNow;

        // Update completion timestamps based on status
        switch (newStatus)
        {
            case ProcessStatus.Completed:
                process.CompletedAt = DateTime.UtcNow;
                process.Progress = 100;
                break;

            case ProcessStatus.Failed:
                process.FailedAt = DateTime.UtcNow;
                break;
        }

        await _processRepository.UpdateAsync(process, cancellationToken);

        _logger.LogInformation(
            "Process status updated successfully: ProcessId={ProcessId}, NewStatus={NewStatus}",
            processId,
            newStatus);
    }

    /// <summary>
    /// Validates that a status transition is allowed according to the process state machine.
    /// </summary>
    /// <param name="currentStatus">Current process status.</param>
    /// <param name="newStatus">Target process status.</param>
    /// <exception cref="InvalidStateTransitionException">If transition is not allowed.</exception>
    private void ValidateStatusTransition(ProcessStatus currentStatus, ProcessStatus newStatus)
    {
        // Define valid transitions based on process state machine
        var validTransitions = new Dictionary<ProcessStatus, ProcessStatus[]>
        {
            [ProcessStatus.Pending] = new[] { ProcessStatus.Accepted, ProcessStatus.Rejected },
            [ProcessStatus.Accepted] = new[] { ProcessStatus.Processing, ProcessStatus.Failed },
            [ProcessStatus.Processing] = new[] { ProcessStatus.Completed, ProcessStatus.Failed, ProcessStatus.Retrying },
            [ProcessStatus.Retrying] = new[] { ProcessStatus.Processing, ProcessStatus.Failed },
            [ProcessStatus.Failed] = new[] { ProcessStatus.Retrying },
            [ProcessStatus.Completed] = Array.Empty<ProcessStatus>(),
            [ProcessStatus.Rejected] = Array.Empty<ProcessStatus>()
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
}
