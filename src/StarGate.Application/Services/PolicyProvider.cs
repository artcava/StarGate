using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using StarGate.Core.Abstractions;
using StarGate.Core.Domain.Configuration;
using System.Collections.Concurrent;

namespace StarGate.Application.Services;

/// <summary>
/// Provides process configuration policies with two-tier caching and override resolution.
/// Implements hierarchical policy resolution: client overrides take precedence over type defaults.
/// Thread-safe for concurrent access.
/// </summary>
public class PolicyProvider : IPolicyProvider
{
    private readonly IPolicyRepository _policyRepository;
    private readonly ICacheStore _cacheStore;
    private readonly ILogger<PolicyProvider> _logger;
    private readonly PolicyProviderOptions _options;
    private readonly ConcurrentDictionary<string, ProcessTypePolicy> _memoryCache;
    private readonly ConcurrentDictionary<string, ClientPolicyOverride> _overrideCache;
    private readonly SemaphoreSlim _refreshLock;

    public PolicyProvider(
        IPolicyRepository policyRepository,
        ICacheStore cacheStore,
        IOptions<PolicyProviderOptions> options,
        ILogger<PolicyProvider> logger)
    {
        _policyRepository = policyRepository ?? throw new ArgumentNullException(nameof(policyRepository));
        _cacheStore = cacheStore ?? throw new ArgumentNullException(nameof(cacheStore));
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _memoryCache = new ConcurrentDictionary<string, ProcessTypePolicy>();
        _overrideCache = new ConcurrentDictionary<string, ClientPolicyOverride>();
        _refreshLock = new SemaphoreSlim(1, 1);
    }

    /// <inheritdoc/>
    public async Task<TimeSpan> GetTimeoutAsync(
        string clientId,
        string processType,
        CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(clientId, nameof(clientId));
        ArgumentException.ThrowIfNullOrWhiteSpace(processType, nameof(processType));

        var effectivePolicy = await GetEffectivePolicyAsync(clientId, processType, ct);
        return effectivePolicy.Timeout;
    }

    /// <inheritdoc/>
    public async Task<RetryPolicy> GetRetryPolicyAsync(
        string clientId,
        string processType,
        CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(clientId, nameof(clientId));
        ArgumentException.ThrowIfNullOrWhiteSpace(processType, nameof(processType));

        var effectivePolicy = await GetEffectivePolicyAsync(clientId, processType, ct);
        return effectivePolicy.RetryPolicy;
    }

    /// <inheritdoc/>
    public async Task<TimeSpan> GetResultRetentionAsync(
        string clientId,
        string processType,
        CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(clientId, nameof(clientId));
        ArgumentException.ThrowIfNullOrWhiteSpace(processType, nameof(processType));

        var effectivePolicy = await GetEffectivePolicyAsync(clientId, processType, ct);
        return effectivePolicy.ResultRetention;
    }

    /// <inheritdoc/>
    public async Task<int?> GetConcurrencyLimitAsync(
        string clientId,
        string processType,
        CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(clientId, nameof(clientId));
        ArgumentException.ThrowIfNullOrWhiteSpace(processType, nameof(processType));

        var effectivePolicy = await GetEffectivePolicyAsync(clientId, processType, ct);
        return effectivePolicy.MaxConcurrentProcesses;
    }

    /// <inheritdoc/>
    public async Task<EffectivePolicy> GetEffectivePolicyAsync(
        string clientId,
        string processType,
        CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(clientId, nameof(clientId));
        ArgumentException.ThrowIfNullOrWhiteSpace(processType, nameof(processType));

        _logger.LogDebug(
            "Resolving effective policy for ClientId={ClientId}, ProcessType={ProcessType}",
            clientId,
            processType);

        // Get type default policy (with caching)
        var typePolicy = await GetTypePolicyAsync(processType, ct);

        // Get client override (with caching)
        var clientOverride = await GetClientOverrideAsync(clientId, processType, ct);

        // Merge and create effective policy
        var effectivePolicy = MergePolicies(typePolicy, clientOverride, clientId);

        _logger.LogDebug(
            "Resolved effective policy: ClientId={ClientId}, ProcessType={ProcessType}, " +
            "Timeout={Timeout}, MaxRetry={MaxRetry}, HasOverride={HasOverride}",
            clientId,
            processType,
            effectivePolicy.Timeout,
            effectivePolicy.RetryPolicy.MaxAttempts,
            clientOverride != null);

        return effectivePolicy;
    }

    /// <summary>
    /// Retrieves process type policy with two-tier caching.
    /// </summary>
    private async Task<ProcessTypePolicy> GetTypePolicyAsync(
        string processType,
        CancellationToken ct)
    {
        // L1 Cache: Memory
        var cacheKey = $"type:{processType}";
        if (_memoryCache.TryGetValue(cacheKey, out var cachedPolicy))
        {
            _logger.LogDebug("Type policy cache hit (memory): {CacheKey}", cacheKey);
            return cachedPolicy;
        }

        // L2 Cache: Redis
        var redisCacheKey = $"policy:{cacheKey}";
        var redisPolicy = await _cacheStore.GetAsync<ProcessTypePolicy>(redisCacheKey, ct);
        if (redisPolicy != null)
        {
            _logger.LogDebug("Type policy cache hit (Redis): {CacheKey}", cacheKey);
            _memoryCache.TryAdd(cacheKey, redisPolicy);
            return redisPolicy;
        }

        // Cache miss: Load from repository
        _logger.LogDebug("Type policy cache miss, loading from repository: {ProcessType}", processType);
        var policy = await _policyRepository.GetByProcessTypeAsync(processType, ct);

        if (policy == null)
        {
            _logger.LogWarning(
                "Process type policy not found, using fallback defaults: {ProcessType}",
                processType);
            policy = CreateFallbackPolicy(processType);
        }

        // Cache the policy
        await CachePolicyAsync(cacheKey, redisCacheKey, policy, ct);

        return policy;
    }

    /// <summary>
    /// Retrieves client override with two-tier caching.
    /// </summary>
    private async Task<ClientPolicyOverride?> GetClientOverrideAsync(
        string clientId,
        string processType,
        CancellationToken ct)
    {
        // L1 Cache: Memory
        var cacheKey = $"override:{clientId}:{processType}";
        if (_overrideCache.TryGetValue(cacheKey, out var cachedOverride))
        {
            _logger.LogDebug("Client override cache hit (memory): {CacheKey}", cacheKey);
            return cachedOverride;
        }

        // L2 Cache: Redis
        var redisCacheKey = $"policy:{cacheKey}";
        var redisOverride = await _cacheStore.GetAsync<ClientPolicyOverride>(redisCacheKey, ct);
        if (redisOverride != null)
        {
            _logger.LogDebug("Client override cache hit (Redis): {CacheKey}", cacheKey);
            _overrideCache.TryAdd(cacheKey, redisOverride);
            return redisOverride;
        }

        // Cache miss: Load from repository
        _logger.LogDebug(
            "Client override cache miss, loading from repository: ClientId={ClientId}, ProcessType={ProcessType}",
            clientId,
            processType);

        var clientOverride = await _policyRepository.GetClientOverrideAsync(clientId, processType, ct);

        if (clientOverride != null)
        {
            // Cache the override
            await CacheOverrideAsync(cacheKey, redisCacheKey, clientOverride, ct);
        }
        else
        {
            _logger.LogDebug(
                "No client override found: ClientId={ClientId}, ProcessType={ProcessType}",
                clientId,
                processType);
        }

        return clientOverride;
    }

    /// <summary>
    /// Merges type policy with client override to create effective policy.
    /// Client override values take precedence.
    /// </summary>
    private EffectivePolicy MergePolicies(
        ProcessTypePolicy typePolicy,
        ClientPolicyOverride? clientOverride,
        string clientId)
    {
        var hasOverride = clientOverride != null;

        return new EffectivePolicy
        {
            ProcessType = typePolicy.ProcessType,
            ClientId = clientId,
            Timeout = clientOverride?.Timeout ?? typePolicy.Timeout,
            RetryPolicy = clientOverride?.RetryPolicy ?? typePolicy.RetryPolicy,
            ResultRetention = clientOverride?.ResultRetention ?? typePolicy.ResultRetention,
            MaxConcurrentProcesses = clientOverride?.MaxConcurrentProcesses ?? typePolicy.MaxConcurrentProcesses,
            Source = new PolicySource
            {
                TimeoutFromOverride = clientOverride?.Timeout.HasValue ?? false,
                RetryPolicyFromOverride = clientOverride?.RetryPolicy != null,
                ResultRetentionFromOverride = clientOverride?.ResultRetention.HasValue ?? false,
                ConcurrencyLimitFromOverride = clientOverride?.MaxConcurrentProcesses.HasValue ?? false
            }
        };
    }

    /// <summary>
    /// Creates a fallback policy when type policy is not found.
    /// Ensures system resilience.
    /// </summary>
    private ProcessTypePolicy CreateFallbackPolicy(string processType)
    {
        var backoffStrategy = Enum.TryParse<BackoffStrategy>(
            _options.DefaultBackoffStrategy,
            ignoreCase: true,
            out var strategy)
            ? strategy
            : BackoffStrategy.Exponential;

        return new ProcessTypePolicy
        {
            ProcessType = processType,
            Timeout = TimeSpan.FromSeconds(_options.DefaultTimeoutSeconds),
            RetryPolicy = new RetryPolicy
            {
                MaxAttempts = _options.DefaultMaxRetryAttempts,
                InitialDelay = TimeSpan.FromSeconds(_options.DefaultRetryDelaySeconds),
                BackoffStrategy = backoffStrategy
            },
            ResultRetention = TimeSpan.FromDays(_options.DefaultRetentionDays),
            MaxConcurrentProcesses = _options.DefaultMaxConcurrentProcesses,
            UpdatedAt = DateTime.UtcNow
        };
    }

    /// <summary>
    /// Caches policy in both tiers.
    /// </summary>
    private async Task CachePolicyAsync(
        string cacheKey,
        string redisCacheKey,
        ProcessTypePolicy policy,
        CancellationToken ct)
    {
        // L1 Cache
        _memoryCache.TryAdd(cacheKey, policy);

        // L2 Cache with TTL
        var ttl = TimeSpan.FromMinutes(_options.CacheTtlMinutes);
        await _cacheStore.SetAsync(redisCacheKey, policy, ttl, ct);

        _logger.LogDebug(
            "Policy cached: {CacheKey}, TTL={TtlMinutes}min",
            cacheKey,
            _options.CacheTtlMinutes);
    }

    /// <summary>
    /// Caches client override in both tiers.
    /// </summary>
    private async Task CacheOverrideAsync(
        string cacheKey,
        string redisCacheKey,
        ClientPolicyOverride clientOverride,
        CancellationToken ct)
    {
        // L1 Cache
        _overrideCache.TryAdd(cacheKey, clientOverride);

        // L2 Cache with TTL
        var ttl = TimeSpan.FromMinutes(_options.CacheTtlMinutes);
        await _cacheStore.SetAsync(redisCacheKey, clientOverride, ttl, ct);

        _logger.LogDebug(
            "Client override cached: {CacheKey}, TTL={TtlMinutes}min",
            cacheKey,
            _options.CacheTtlMinutes);
    }
}
