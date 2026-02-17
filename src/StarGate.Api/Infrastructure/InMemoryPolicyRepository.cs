using StarGate.Core.Abstractions;
using StarGate.Core.Domain.Configuration;
using System.Collections.Concurrent;

namespace StarGate.Api.Infrastructure;

/// <summary>
/// In-memory implementation of IPolicyRepository for testing.
/// TODO: Replace with actual repository implementation from Infrastructure layer.
/// </summary>
public class InMemoryPolicyRepository : IPolicyRepository
{
    private readonly ConcurrentDictionary<string, ProcessTypePolicy> _typePolicies = new();
    private readonly ConcurrentDictionary<string, ClientPolicyOverride> _clientOverrides = new();

    public InMemoryPolicyRepository()
    {
        // Seed with sample data
        SeedSampleData();
    }

    public Task<ProcessTypePolicy> GetProcessTypePolicyAsync(string processType, CancellationToken ct = default)
    {
        if (_typePolicies.TryGetValue(processType, out var policy))
        {
            return Task.FromResult(policy);
        }

        throw new KeyNotFoundException($"Process type policy not found: {processType}");
    }

    public Task<ClientPolicyOverride?> GetClientOverrideAsync(
        string clientId,
        string processType,
        CancellationToken ct = default)
    {
        var key = $"{clientId}:{processType}";
        _clientOverrides.TryGetValue(key, out var clientOverride);
        return Task.FromResult(clientOverride);
    }

    public Task<ProcessTypePolicy> SaveProcessTypePolicyAsync(
        ProcessTypePolicy policy,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(policy);
        
        // Create new instance with updated timestamp (UpdatedAt is init-only)
        var updatedPolicy = new ProcessTypePolicy
        {
            ProcessType = policy.ProcessType,
            Timeout = policy.Timeout,
            RetryPolicy = policy.RetryPolicy,
            ResultRetention = policy.ResultRetention,
            MaxConcurrentProcesses = policy.MaxConcurrentProcesses,
            UpdatedAt = DateTime.UtcNow
        };
        
        _typePolicies[updatedPolicy.ProcessType] = updatedPolicy;
        
        return Task.FromResult(updatedPolicy);
    }

    public Task<ClientPolicyOverride> SaveClientOverrideAsync(
        ClientPolicyOverride @override,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(@override);
        
        // Create new instance with updated timestamp (UpdatedAt is init-only)
        var updatedOverride = new ClientPolicyOverride
        {
            ClientId = @override.ClientId,
            ProcessType = @override.ProcessType,
            Timeout = @override.Timeout,
            RetryPolicy = @override.RetryPolicy,
            ResultRetention = @override.ResultRetention,
            MaxConcurrentProcesses = @override.MaxConcurrentProcesses,
            UpdatedAt = DateTime.UtcNow
        };
        
        var key = $"{updatedOverride.ClientId}:{updatedOverride.ProcessType}";
        _clientOverrides[key] = updatedOverride;
        
        return Task.FromResult(updatedOverride);
    }

    public Task<bool> DeleteClientOverrideAsync(
        string clientId,
        string processType,
        CancellationToken ct = default)
    {
        var key = $"{clientId}:{processType}";
        var removed = _clientOverrides.TryRemove(key, out _);
        return Task.FromResult(removed);
    }

    public Task<IReadOnlyList<ProcessTypePolicy>> ListProcessTypePoliciesAsync(
        CancellationToken ct = default)
    {
        var policies = _typePolicies.Values.ToList();
        return Task.FromResult<IReadOnlyList<ProcessTypePolicy>>(policies);
    }

    public Task<IReadOnlyList<ClientPolicyOverride>> ListClientOverridesAsync(
        string clientId,
        CancellationToken ct = default)
    {
        var overrides = _clientOverrides.Values
            .Where(o => o.ClientId == clientId)
            .ToList();
        return Task.FromResult<IReadOnlyList<ClientPolicyOverride>>(overrides);
    }

    public Task<IReadOnlyList<ProcessTypePolicy>> GetAllTypeDefaultsAsync(
        CancellationToken ct = default)
    {
        var policies = _typePolicies.Values.ToList();
        return Task.FromResult<IReadOnlyList<ProcessTypePolicy>>(policies);
    }

    public Task<IReadOnlyList<ClientPolicyOverride>> GetAllClientOverridesAsync(
        CancellationToken ct = default)
    {
        var overrides = _clientOverrides.Values.ToList();
        return Task.FromResult<IReadOnlyList<ClientPolicyOverride>>(overrides);
    }

    private void SeedSampleData()
    {
        // Sample process type policies
        _typePolicies["order"] = new ProcessTypePolicy
        {
            ProcessType = "order",
            Timeout = TimeSpan.FromMinutes(5),
            RetryPolicy = new RetryPolicy
            {
                Enabled = true,
                MaxAttempts = 3,
                InitialDelay = TimeSpan.FromSeconds(5),
                BackoffStrategy = BackoffStrategy.Exponential,
                MaxDelay = TimeSpan.FromMinutes(5)
            },
            ResultRetention = TimeSpan.FromDays(30),
            MaxConcurrentProcesses = 10,
            UpdatedAt = DateTime.UtcNow
        };

        _typePolicies["payment"] = new ProcessTypePolicy
        {
            ProcessType = "payment",
            Timeout = TimeSpan.FromMinutes(2),
            RetryPolicy = new RetryPolicy
            {
                Enabled = true,
                MaxAttempts = 5,
                InitialDelay = TimeSpan.FromSeconds(2),
                BackoffStrategy = BackoffStrategy.Linear,
                MaxDelay = TimeSpan.FromMinutes(1)
            },
            ResultRetention = TimeSpan.FromDays(90),
            MaxConcurrentProcesses = 20,
            UpdatedAt = DateTime.UtcNow
        };

        // Sample client overrides
        _clientOverrides["client-vip:order"] = new ClientPolicyOverride
        {
            ClientId = "client-vip",
            ProcessType = "order",
            Timeout = TimeSpan.FromMinutes(10),
            MaxConcurrentProcesses = 50,
            UpdatedAt = DateTime.UtcNow
        };
    }
}

/// <summary>
/// In-memory implementation of ICacheStore for testing.
/// TODO: Replace with actual Redis implementation from Infrastructure layer.
/// </summary>
public class InMemoryCacheStore : ICacheStore
{
    private readonly ConcurrentDictionary<string, (object Value, DateTime Expiry)> _cache = new();

    public Task<T?> GetAsync<T>(string key, CancellationToken ct = default) where T : class
    {
        if (_cache.TryGetValue(key, out var entry))
        {
            if (entry.Expiry > DateTime.UtcNow)
            {
                return Task.FromResult(entry.Value as T);
            }

            // Expired, remove it
            _cache.TryRemove(key, out _);
        }

        return Task.FromResult<T?>(null);
    }

    public Task SetAsync<T>(
        string key,
        T value,
        TimeSpan ttl,
        CancellationToken ct = default) where T : class
    {
        var expiry = DateTime.UtcNow.Add(ttl);
        _cache[key] = (value, expiry);
        return Task.CompletedTask;
    }

    public Task DeleteAsync(string key, CancellationToken ct = default)
    {
        _cache.TryRemove(key, out _);
        return Task.CompletedTask;
    }

    public Task DeleteByPatternAsync(string pattern, CancellationToken ct = default)
    {
        // Simple pattern matching: support "prefix:*" patterns
        var prefix = pattern.Replace("*", "");
        var keysToRemove = _cache.Keys.Where(k => k.StartsWith(prefix)).ToList();
        
        foreach (var key in keysToRemove)
        {
            _cache.TryRemove(key, out _);
        }
        
        return Task.CompletedTask;
    }

    public Task<bool> ExistsAsync(string key, CancellationToken ct = default)
    {
        if (_cache.TryGetValue(key, out var entry))
        {
            if (entry.Expiry > DateTime.UtcNow)
            {
                return Task.FromResult(true);
            }

            // Expired
            _cache.TryRemove(key, out _);
        }

        return Task.FromResult(false);
    }
}
