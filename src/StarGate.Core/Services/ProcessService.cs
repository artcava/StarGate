using Microsoft.Extensions.Logging;
using StarGate.Core.Abstractions;
using StarGate.Core.Domain;
using StarGate.Contracts.Requests;
using System.Text.Json;

namespace StarGate.Core.Services;

/// <summary>
/// Core service for process lifecycle management.
/// Orchestrates process submission, retrieval, and status updates.
/// Implements cache-aside pattern and coordinates with repository, cache, and message broker.
/// </summary>
public class ProcessService : IProcessService
{
    private readonly IProcessRepository _repository;
    private readonly IStateStore _cache;
    private readonly IMessageBroker _messageBroker;
    private readonly IPolicyProvider _policyProvider;
    private readonly ILogger<ProcessService> _logger;
    private const string _processQueueName = "stargate.processes";

    /// <summary>
    /// Initializes a new instance of ProcessService.
    /// </summary>
    /// <param name="repository">Process repository for persistence.</param>
    /// <param name="cache">State store for caching.</param>
    /// <param name="messageBroker">Message broker for async processing.</param>
    /// <param name="policyProvider">Policy provider for concurrency limits.</param>
    /// <param name="logger">Logger instance.</param>
    /// <exception cref="ArgumentNullException">If any dependency is null.</exception>
    public ProcessService(
        IProcessRepository repository,
        IStateStore cache,
        IMessageBroker messageBroker,
        IPolicyProvider policyProvider,
        ILogger<ProcessService> logger)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _cache = cache ?? throw new ArgumentNullException(nameof(cache));
        _messageBroker = messageBroker ?? throw new ArgumentNullException(nameof(messageBroker));
        _policyProvider = policyProvider ?? throw new ArgumentNullException(nameof(policyProvider));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc />
    public async Task<Process> SubmitProcessAsync(
        string clientId,
        SubmitProcessRequest request,
        CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(clientId);
        ArgumentNullException.ThrowIfNull(request);

        _logger.LogInformation(
            "Submitting process for client {ClientId}, type {ProcessType}, client process ID {ClientProcessId}",
            clientId,
            request.ProcessType,
            request.ClientProcessId);

        // Check for existing process (idempotency)
        var existing = await _repository.GetByClientProcessIdAsync(
            clientId,
            request.ClientProcessId,
            ct);

        if (existing != null)
        {
            _logger.LogInformation(
                "Idempotent request detected for client process {ClientProcessId}. Returning existing process {ProcessId}",
                request.ClientProcessId,
                existing.ProcessId);

            // Ensure cached for fast subsequent lookups
            await _cache.SetProcessAsync(existing);

            return existing;
        }

        // Check concurrency limits
        var canSubmit = await CanSubmitProcessAsync(clientId, request.ProcessType, ct);
        if (!canSubmit)
        {
            throw new InvalidOperationException(
                $"Concurrency limit exceeded for client '{clientId}' and process type '{request.ProcessType}'");
        }

        var processId = Guid.NewGuid();

        // Serialize payload to JsonDocument
        var dataJson = JsonSerializer.SerializeToDocument(request.Payload);

        var process = new Process
        {
            ProcessId = processId,
            ClientProcessId = request.ClientProcessId,
            ProcessType = request.ProcessType,
            ClientId = clientId,
            Status = ProcessStatus.Accepted,
            Progress = 0,
            CurrentStep = null,
            Data = dataJson,
            Result = null,
            Error = null,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            CompletedAt = null,
            IdempotencyKey = request.IdempotencyKey,
            Retryable = true
        };

        // Write to database first (source of truth)
        await _repository.CreateAsync(process, ct);

        // Then cache (write-through pattern)
        await _cache.SetProcessAsync(process);

        // Finally, publish to message broker for async processing
        await _messageBroker.PublishAsync(
            _processQueueName,
            process,
            new MessageProperties
            {
                MessageId = processId.ToString(),
                CorrelationId = request.ClientProcessId
            },
            ct);

        _logger.LogInformation(
            "Process {ProcessId} submitted successfully and published to queue",
            processId);

        return process;
    }

    /// <inheritdoc />
    public async Task<Process?> GetProcessByIdAsync(
        Guid processId,
        CancellationToken ct = default)
    {
        if (processId == Guid.Empty)
        {
            throw new ArgumentException("Process ID cannot be empty", nameof(processId));
        }

        // Try cache first (cache-aside pattern)
        var cached = await _cache.GetProcessAsync(processId);
        if (cached != null)
        {
            _logger.LogDebug(
                "Process {ProcessId} retrieved from cache",
                processId);
            return cached;
        }

        // Cache miss - fetch from database
        _logger.LogDebug(
            "Cache miss for process {ProcessId}, fetching from database",
            processId);

        var process = await _repository.GetByIdAsync(processId, ct);

        if (process != null)
        {
            // Populate cache for future requests
            await _cache.SetProcessAsync(process);

            _logger.LogDebug(
                "Process {ProcessId} retrieved from database and cached",
                processId);
        }
        else
        {
            _logger.LogWarning(
                "Process {ProcessId} not found",
                processId);
        }

        return process;
    }

    /// <inheritdoc />
    public async Task<Process?> GetProcessByClientProcessIdAsync(
        string clientId,
        string clientProcessId,
        CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(clientId);
        ArgumentException.ThrowIfNullOrWhiteSpace(clientProcessId);

        _logger.LogDebug(
            "Retrieving process by client process ID {ClientProcessId} for client {ClientId}",
            clientProcessId,
            clientId);

        // Direct repository query (not cached)
        return await _repository.GetByClientProcessIdAsync(clientId, clientProcessId, ct);
    }

    /// <inheritdoc />
    public async Task<Process> UpdateProcessStatusAsync(
        Guid processId,
        ProcessStatus status,
        int progress = 0,
        string? currentStep = null,
        JsonDocument? result = null,
        ProcessError? error = null,
        CancellationToken ct = default)
    {
        if (processId == Guid.Empty)
        {
            throw new ArgumentException("Process ID cannot be empty", nameof(processId));
        }

        if (progress < 0 || progress > 100)
        {
            throw new ArgumentException("Progress must be between 0 and 100", nameof(progress));
        }

        _logger.LogInformation(
            "Updating process {ProcessId} to status {Status} with progress {Progress}%",
            processId,
            status,
            progress);

        var process = await _repository.GetByIdAsync(processId, ct)
            ?? throw new InvalidOperationException(
                $"Process with ID '{processId}' not found");

        var updated = process with
        {
            Status = status,
            Progress = progress,
            CurrentStep = currentStep,
            Result = result,
            Error = error,
            UpdatedAt = DateTime.UtcNow,
            CompletedAt = status is ProcessStatus.Completed or ProcessStatus.Failed
                ? DateTime.UtcNow
                : process.CompletedAt
        };

        // Write to database first
        await _repository.UpdateAsync(updated, ct);

        // Invalidate cache (write-invalidate pattern)
        // Alternative: update cache (write-through pattern) - can be configured
        await _cache.InvalidateAsync(processId);

        _logger.LogInformation(
            "Process {ProcessId} updated to status {Status}, cache invalidated",
            processId,
            status);

        return updated;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<Process>> ListProcessesAsync(
        string clientId,
        ProcessStatus? status = null,
        int skip = 0,
        int limit = 100,
        CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(clientId);

        if (skip < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(skip), "Skip cannot be negative");
        }

        if (limit < 1 || limit > 1000)
        {
            throw new ArgumentOutOfRangeException(nameof(limit), "Limit must be between 1 and 1000");
        }

        _logger.LogDebug(
            "Listing processes for client {ClientId} with status {Status}, skip {Skip}, limit {Limit}",
            clientId,
            status?.ToString() ?? "(all)",
            skip,
            limit);

        // Direct repository query
        // Note: Current IProcessRepository.GetByClientIdAsync doesn't support status filtering
        // If status filtering is needed, repository interface should be extended
        return await _repository.GetByClientIdAsync(clientId, skip, limit, ct);
    }

    /// <inheritdoc />
    public async Task<bool> CanSubmitProcessAsync(
        string clientId,
        string processType,
        CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(clientId);
        ArgumentException.ThrowIfNullOrWhiteSpace(processType);

        _logger.LogDebug(
            "Checking concurrency limits for client {ClientId}, process type {ProcessType}",
            clientId,
            processType);

        // Get effective policy for this client and process type
        var policy = await _policyProvider.GetEffectivePolicyAsync(clientId, processType, ct);

        if (policy.MaxConcurrentProcesses == null)
        {
            _logger.LogDebug(
                "No concurrency limit set for client {ClientId}, process type {ProcessType}. Allowing submission.",
                clientId,
                processType);
            return true; // No limit = unlimited
        }

        // Count active processes (Accepted, Processing)
        var activeCount = await _repository.CountActiveProcessesAsync(
            clientId,
            processType,
            ct);

        var canSubmit = activeCount < policy.MaxConcurrentProcesses.Value;

        _logger.LogDebug(
            "Client {ClientId} has {ActiveCount}/{MaxConcurrent} active processes for type {ProcessType}. Can submit: {CanSubmit}",
            clientId,
            activeCount,
            policy.MaxConcurrentProcesses.Value,
            processType,
            canSubmit);

        return canSubmit;
    }
}
