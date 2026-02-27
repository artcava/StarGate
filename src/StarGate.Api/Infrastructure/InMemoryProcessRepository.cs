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

    public Task<Process> CreateAsync(Process process, CancellationToken cancellationToken = default)
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

    public Task<Process> UpdateAsync(Process process, CancellationToken cancellationToken = default)
    {
        _processesById[process.ProcessId] = process;
        return Task.FromResult(process);
    }

    public Task<Process?> GetByIdAsync(Guid processId, CancellationToken cancellationToken = default)
    {
        _processesById.TryGetValue(processId, out var process);
        return Task.FromResult(process);
    }

    public Task<Process?> GetByClientProcessIdAsync(
        string clientId,
        string clientProcessId,
        CancellationToken cancellationToken = default)
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
        CancellationToken cancellationToken = default)
    {
        var key = GetIdempotencyKey(clientId, idempotencyKey);
        
        if (_processesByIdempotencyKey.TryGetValue(key, out var processId))
        {
            _processesById.TryGetValue(processId, out var process);
            return Task.FromResult(process);
        }

        return Task.FromResult<Process?>(null);
    }

    public Task<IEnumerable<Process>> GetByClientIdAsync(
        string clientId,
        CancellationToken cancellationToken = default)
    {
        var processes = _processesById.Values
            .Where(p => p.ClientId == clientId)
            .ToList();

        return Task.FromResult<IEnumerable<Process>>(processes);
    }

    public Task<IEnumerable<Process>> GetByStatusAsync(
        ProcessStatus status,
        CancellationToken cancellationToken = default)
    {
        var processes = _processesById.Values
            .Where(p => p.Status == status)
            .ToList();

        return Task.FromResult<IEnumerable<Process>>(processes);
    }

    public Task<bool> DeleteAsync(Guid processId, CancellationToken cancellationToken = default)
    {
        if (_processesById.TryRemove(processId, out var process))
        {
            var clientKey = GetClientKey(process.ClientId, process.ClientProcessId);
            _processesByClientId.TryRemove(clientKey, out _);

            var idempotencyKey = GetIdempotencyKey(process.ClientId, process.IdempotencyKey);
            _processesByIdempotencyKey.TryRemove(idempotencyKey, out _);

            return Task.FromResult(true);
        }

        return Task.FromResult(false);
    }

    private static string GetClientKey(string clientId, string clientProcessId) 
        => $"{clientId}:{clientProcessId}";

    private static string GetIdempotencyKey(string clientId, string idempotencyKey) 
        => $"{clientId}:{idempotencyKey}";
}
