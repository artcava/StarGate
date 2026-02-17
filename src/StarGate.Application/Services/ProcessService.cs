using Microsoft.Extensions.Logging;
using StarGate.Core.Abstractions;
using StarGate.Core.Domain;
using StarGate.Core.Exceptions;

namespace StarGate.Application.Services;

/// <summary>
/// Application service for process lifecycle management.
/// Orchestrates process creation, retrieval, and policy enforcement.
/// </summary>
public class ProcessService
{
    private readonly IProcessRepository _processRepository;
    private readonly IPolicyProvider _policyProvider;
    private readonly IMessageBroker _messageBroker;
    private readonly ILogger<ProcessService> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="ProcessService"/> class.
    /// </summary>
    /// <param name="processRepository">Repository for process persistence.</param>
    /// <param name="policyProvider">Provider for policy resolution.</param>
    /// <param name="messageBroker">Message broker for async process execution.</param>
    /// <param name="logger">Logger instance.</param>
    public ProcessService(
        IProcessRepository processRepository,
        IPolicyProvider policyProvider,
        IMessageBroker messageBroker,
        ILogger<ProcessService> logger)
    {
        _processRepository = processRepository ?? throw new ArgumentNullException(nameof(processRepository));
        _policyProvider = policyProvider ?? throw new ArgumentNullException(nameof(policyProvider));
        _messageBroker = messageBroker ?? throw new ArgumentNullException(nameof(messageBroker));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Creates a new process with policy-driven configuration.
    /// Enforces idempotency, concurrency limits, and applies timeout, retry, and retention policies.
    /// </summary>
    /// <param name="clientId">Client identifier.</param>
    /// <param name="processType">Type of process (e.g., "order", "shipping").</param>
    /// <param name="clientProcessId">Client-provided process identifier.</param>
    /// <param name="idempotencyKey">Idempotency key to prevent duplicate submissions.</param>
    /// <param name="metadata">Optional metadata dictionary.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The created or existing process.</returns>
    /// <exception cref="ArgumentException">If required parameters are null or whitespace.</exception>
    /// <exception cref="PolicyViolationException">If concurrent execution limit is exceeded.</exception>
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
            "Creating process: ClientId={ClientId}, ProcessType={ProcessType}, ClientProcessId={ClientProcessId}",
            clientId,
            processType,
            clientProcessId);

        // Get policy for this client and process type
        var policy = await _policyProvider.GetPolicyAsync(
            processType,
            clientId,
            cancellationToken);

        _logger.LogDebug(
            "Applied policy for ProcessType={ProcessType}, ClientId={ClientId}: Timeout={TimeoutSeconds}s, MaxRetries={MaxRetryAttempts}, RetryDelay={RetryDelaySeconds}s, MaxConcurrent={MaxConcurrentExecutions}",
            processType,
            clientId,
            policy.TimeoutSeconds,
            policy.MaxRetryAttempts,
            policy.RetryDelaySeconds,
            policy.MaxConcurrentExecutions);

        // Check for idempotency (duplicate)
        var existingProcess = await _processRepository.GetByIdempotencyKeyAsync(
            idempotencyKey,
            cancellationToken);

        if (existingProcess != null)
        {
            _logger.LogInformation(
                "Process with IdempotencyKey={IdempotencyKey} already exists: {ProcessId}",
                idempotencyKey,
                existingProcess.ProcessId);
            return existingProcess;
        }

        // Check concurrent execution limit
        var runningProcessesCount = await _processRepository.CountRunningProcessesByTypeAsync(
            processType,
            clientId,
            cancellationToken);

        if (runningProcessesCount >= policy.MaxConcurrentExecutions)
        {
            _logger.LogWarning(
                "Max concurrent executions reached for ProcessType={ProcessType}, ClientId={ClientId}: {Count}/{Max}",
                processType,
                clientId,
                runningProcessesCount,
                policy.MaxConcurrentExecutions);

            throw new PolicyViolationException(
                $"Maximum concurrent executions limit reached: {runningProcessesCount}/{policy.MaxConcurrentExecutions}");
        }

        // Create process with policy-driven configuration
        var now = DateTime.UtcNow;
        var process = new Process
        {
            ProcessId = Guid.NewGuid(),
            ClientId = clientId,
            ProcessType = processType,
            ClientProcessId = clientProcessId,
            IdempotencyKey = idempotencyKey,
            Status = ProcessStatus.Accepted,
            Progress = 0,
            Retryable = policy.MaxRetryAttempts > 0,
            CreatedAt = now,
            UpdatedAt = now,
            
            // Policy-driven fields
            TimeoutAt = now.AddSeconds(policy.TimeoutSeconds),
            RetryCount = 0,
            MaxRetries = policy.MaxRetryAttempts,
            RetentionExpiresAt = now.AddDays(policy.RetentionDays)
        };

        // Save to repository
        await _processRepository.CreateAsync(process, cancellationToken);

        _logger.LogInformation(
            "Process created: {ProcessId}, TimeoutAt={TimeoutAt}, RetentionExpiresAt={RetentionExpiresAt}",
            process.ProcessId,
            process.TimeoutAt,
            process.RetentionExpiresAt);

        // Publish to message broker for async processing
        var queueName = $"stargate.{processType}";
        await _messageBroker.PublishAsync(queueName, process, cancellationToken);

        return process;
    }

    /// <summary>
    /// Retrieves a process by its unique identifier.
    /// </summary>
    /// <param name="processId">Process identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The process entity.</returns>
    /// <exception cref="InvalidOperationException">If process not found.</exception>
    public async Task<Process> GetProcessAsync(
        Guid processId,
        CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Retrieving process: {ProcessId}", processId);

        var process = await _processRepository.GetByIdAsync(processId, cancellationToken);

        if (process == null)
        {
            throw new InvalidOperationException($"Process with ID '{processId}' not found");
        }

        return process;
    }

    /// <summary>
    /// Retrieves a process by client ID and client process ID.
    /// </summary>
    /// <param name="clientId">Client identifier.</param>
    /// <param name="clientProcessId">Client-provided process identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The process entity.</returns>
    /// <exception cref="ArgumentException">If required parameters are null or whitespace.</exception>
    /// <exception cref="InvalidOperationException">If process not found.</exception>
    public async Task<Process> GetProcessByClientIdAsync(
        string clientId,
        string clientProcessId,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(clientId);
        ArgumentException.ThrowIfNullOrWhiteSpace(clientProcessId);

        _logger.LogDebug(
            "Retrieving process by client: ClientId={ClientId}, ClientProcessId={ClientProcessId}",
            clientId,
            clientProcessId);

        var process = await _processRepository.GetByClientProcessIdAsync(
            clientId,
            clientProcessId,
            cancellationToken);

        if (process == null)
        {
            throw new InvalidOperationException(
                $"Process with ClientId '{clientId}' and ClientProcessId '{clientProcessId}' not found");
        }

        return process;
    }

    /// <summary>
    /// Determines if a process should be retried based on its current state and policy.
    /// Uses live policy to allow runtime policy updates to affect retry decisions.
    /// </summary>
    /// <param name="processId">Process identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if the process should be retried, false otherwise.</returns>
    public async Task<bool> ShouldRetryProcessAsync(
        Guid processId,
        CancellationToken cancellationToken = default)
    {
        var process = await GetProcessAsync(processId, cancellationToken);

        // Get current policy (may have changed since process creation)
        var policy = await _policyProvider.GetPolicyAsync(
            process.ProcessType,
            process.ClientId,
            cancellationToken);

        var shouldRetry = process.Retryable &&
                         process.RetryCount < policy.MaxRetryAttempts &&
                         process.Status == ProcessStatus.Failed;

        _logger.LogDebug(
            "Retry evaluation for {ProcessId}: ShouldRetry={ShouldRetry}, RetryCount={RetryCount}, MaxRetries={MaxRetries}",
            processId,
            shouldRetry,
            process.RetryCount,
            policy.MaxRetryAttempts);

        return shouldRetry;
    }

    /// <summary>
    /// Gets the retry delay in seconds for a specific process type and client.
    /// </summary>
    /// <param name="processType">Process type.</param>
    /// <param name="clientId">Client identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Retry delay in seconds.</returns>
    public async Task<int> GetRetryDelaySecondsAsync(
        string processType,
        string clientId,
        CancellationToken cancellationToken = default)
    {
        var policy = await _policyProvider.GetPolicyAsync(
            processType,
            clientId,
            cancellationToken);

        return policy.RetryDelaySeconds;
    }

    /// <summary>
    /// Checks if a process has timed out based on its TimeoutAt timestamp.
    /// Only applies to processes in Accepted or Running status.
    /// </summary>
    /// <param name="processId">Process identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if the process has timed out, false otherwise.</returns>
    public async Task<bool> IsProcessTimedOutAsync(
        Guid processId,
        CancellationToken cancellationToken = default)
    {
        var process = await GetProcessAsync(processId, cancellationToken);

        if (!process.TimeoutAt.HasValue)
        {
            return false;
        }

        var isTimedOut = DateTime.UtcNow > process.TimeoutAt.Value &&
                        (process.Status == ProcessStatus.Running ||
                         process.Status == ProcessStatus.Accepted);

        if (isTimedOut)
        {
            _logger.LogWarning(
                "Process {ProcessId} timed out: TimeoutAt={TimeoutAt}, Status={Status}",
                processId,
                process.TimeoutAt.Value,
                process.Status);
        }

        return isTimedOut;
    }

    /// <summary>
    /// Gets processes that have expired based on their retention policy.
    /// Returns processes in terminal states (Completed, Failed, Cancelled) where
    /// RetentionExpiresAt is less than or equal to the current time.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>List of expired processes ready for cleanup.</returns>
    public async Task<IReadOnlyList<Process>> GetExpiredProcessesAsync(
        CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Retrieving expired processes for cleanup");

        var expiredProcesses = await _processRepository.GetExpiredProcessesAsync(
            DateTime.UtcNow,
            cancellationToken);

        _logger.LogInformation(
            "Found {Count} expired processes ready for cleanup",
            expiredProcesses.Count);

        return expiredProcesses;
    }
}
