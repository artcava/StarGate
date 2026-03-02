using System.Collections.Concurrent;
using StarGate.Core.Abstractions;
using StarGate.Core.Domain;

namespace StarGate.Api.Infrastructure;

/// <summary>
/// Temporary in-memory implementation of IProcessRepository for development and testing.
/// Thread-safe using ConcurrentDictionary.
/// </summary>
public class InMemoryProcessRepository : IProcessRepository
{
    private readonly ConcurrentDictionary<Guid, Process> _processesById = new();
    private readonly ConcurrentDictionary<string, Guid> _processesByClientId = new();
    private readonly ConcurrentDictionary<string, Guid> _processesByIdempotencyKey = new();

    public Task<Process> CreateAsync(Process process, CancellationToken ct = default)
    {
        if (!_processesById.TryAdd(process.ProcessId, process))
        {
            throw new InvalidOperationException($"Process with ID {process.ProcessId} already exists");
        }

        var clientKey = GetClientKey(process.ClientId, process.ClientProcessId);
        _processesByClientId.TryAdd(clientKey, process.ProcessId);

        var idempotencyKey = GetIdempotencyKey(process.ClientId, process.IdempotencyKey);
        _processesByIdempotencyKey.TryAdd(idempotencyKey, process.ProcessId);

        return Task.FromResult(process);
    }

    public Task<Process> UpdateAsync(Process process, CancellationToken ct = default)
    {
        _processesById[process.ProcessId] = process;
        return Task.FromResult(process);
    }

    public Task<Process?> GetByIdAsync(Guid processId, CancellationToken ct = default)
    {
        _processesById.TryGetValue(processId, out var process);
        return Task.FromResult(process);
    }

    public Task<Process?> GetByClientProcessIdAsync(
        string clientId,
        string clientProcessId,
        CancellationToken ct = default)
    {
        var clientKey = GetClientKey(clientId, clientProcessId);
        
        if (_processesByClientId.TryGetValue(clientKey, out var processId))
        {
            _processesById.TryGetValue(processId, out var process);
            return Task.FromResult(process);
        }

        return Task.FromResult<Process?>(null);
    }

    public Task<Process?> GetByIdempotencyKeyAsync(
        string clientId,
        string idempotencyKey,
        CancellationToken ct = default)
    {
        var key = GetIdempotencyKey(clientId, idempotencyKey);
        
        if (_processesByIdempotencyKey.TryGetValue(key, out var processId))
        {
            _processesById.TryGetValue(processId, out var process);
            return Task.FromResult(process);
        }

        return Task.FromResult<Process?>(null);
    }

    public Task<IReadOnlyList<Process>> GetByStatusAsync(
        ProcessStatus status,
        int limit = 100,
        CancellationToken ct = default)
    {
        var processes = _processesById.Values
            .Where(p => p.Status == status)
            .OrderBy(p => p.CreatedAt)
            .Take(Math.Min(limit, 1000))
            .ToList();

        return Task.FromResult<IReadOnlyList<Process>>(processes);
    }

    public Task<IReadOnlyList<Process>> GetByClientIdAsync(
        string clientId,
        int skip = 0,
        int limit = 100,
        CancellationToken ct = default)
    {
        var processes = _processesById.Values
            .Where(p => p.ClientId == clientId)
            .OrderByDescending(p => p.CreatedAt)
            .Skip(skip)
            .Take(Math.Min(limit, 1000))
            .ToList();

        return Task.FromResult<IReadOnlyList<Process>>(processes);
    }

    public Task<int> CountActiveProcessesAsync(
        string clientId,
        string processType,
        CancellationToken ct = default)
    {
        var count = _processesById.Values
            .Count(p => 
                p.ClientId == clientId && 
                p.ProcessType == processType &&
                (p.Status == ProcessStatus.Accepted || p.Status == ProcessStatus.Processing));

        return Task.FromResult(count);
    }

    public Task<int> CountRunningProcessesByTypeAsync(
        string processType,
        string clientId,
        CancellationToken ct = default)
    {
        var count = _processesById.Values
            .Count(p => 
                p.ProcessType == processType &&
                p.ClientId == clientId &&
                (p.Status == ProcessStatus.Accepted || p.Status == ProcessStatus.Processing));

        return Task.FromResult(count);
    }

    public Task<IReadOnlyList<Process>> GetExpiredProcessesAsync(
        DateTime expirationDate,
        CancellationToken ct = default)
    {
        var terminalStatuses = new[] 
        { 
            ProcessStatus.Completed, 
            ProcessStatus.Failed 
        };

        var processes = _processesById.Values
            .Where(p => 
                terminalStatuses.Contains(p.Status) &&
                p.RetentionExpiresAt.HasValue &&
                p.RetentionExpiresAt.Value <= expirationDate)
            .Take(1000)
            .ToList();

        return Task.FromResult<IReadOnlyList<Process>>(processes);
    }

    public Task<IReadOnlyList<Process>> GetTimedOutProcessesAsync(
        CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;
        var activeStatuses = new[] 
        { 
            ProcessStatus.Accepted, 
            ProcessStatus.Processing,
            ProcessStatus.Retrying
        };

        var processes = _processesById.Values
            .Where(p => 
                activeStatuses.Contains(p.Status) &&
                p.TimeoutAt.HasValue &&
                p.TimeoutAt.Value < now)
            .Take(100)
            .ToList();

        return Task.FromResult<IReadOnlyList<Process>>(processes);
    }

    private static string GetClientKey(string clientId, string clientProcessId) 
        => $"{clientId}:{clientProcessId}";

    private static string GetIdempotencyKey(string clientId, string idempotencyKey) 
        => $"{clientId}:{idempotencyKey}";
}
