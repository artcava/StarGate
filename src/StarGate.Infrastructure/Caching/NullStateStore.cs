using StarGate.Core.Abstractions;
using StarGate.Core.Domain;

namespace StarGate.Infrastructure.Caching;

/// <summary>
/// Null object implementation of IStateStore.
/// Used when Redis is disabled or unavailable.
/// Provides no-op implementations that always indicate cache miss.
/// </summary>
public class NullStateStore : IStateStore
{
    /// <inheritdoc />
    public Task<Process?> GetProcessAsync(Guid processId)
    {
        return Task.FromResult<Process?>(null);
    }

    /// <inheritdoc />
    public Task SetProcessAsync(Process process)
    {
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task InvalidateAsync(Guid processId)
    {
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task<bool> ExistsAsync(Guid processId)
    {
        return Task.FromResult(false);
    }

    /// <inheritdoc />
    public Task<bool> TrySetStatusAsync(
        Guid processId,
        ProcessStatus status,
        long expectedVersion)
    {
        return Task.FromResult(false);
    }
}
