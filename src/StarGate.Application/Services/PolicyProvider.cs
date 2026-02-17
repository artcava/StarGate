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
    private readonly PolicyResolutionService _resolutionService;
    private readonly PolicyCacheStatistics _cacheStatistics;
    private readonly ConcurrentDictionary<string, ProcessTypePolicy> _memoryCache;
    private readonly ConcurrentDictionary<string, ClientPolicyOverride> _overrideCache;
    private readonly SemaphoreSlim _refreshLock;

    public PolicyProvider(
        IPolicyRepository policyRepository,
        ICacheStore cacheStore,
        IOptions<PolicyProviderOptions> options,
        PolicyResolutionService resolutionService,
        PolicyCacheStatistics cacheStatistics,
        ILogger<PolicyProvider> logger)
    {
        _policyRepository = policyRepository ?? throw new ArgumentNullException(nameof(policyRepository));
        _cacheStore = cacheStore ?? throw new ArgumentNullException(nameof(cacheStore));
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
        _resolutionService = resolutionService ?? throw new ArgumentNullException(nameof(resolutionService));
        _cacheStatistics = cacheStatistics ?? throw new ArgumentNullException(nameof(cacheStatistics));
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

        // Merge and create effective policy using resolution service
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
    /// Gets default policy for a process type (without client override).
    /// Used by cache warmer to preload type defaults.
    /// </summary>
    /// <param name="processType">The process type.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The type default policy.</returns>
    public async Task<ProcessTypePolicy> GetDefaultPolicyAsync(
        string processType,
        CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(processType, nameof(processType));
        return await GetTypePolicyAsync(processType, ct);
    }

    /// <summary>
    /// Gets effective policy for a client and process type.
    /// Unified access method for warm-up and regular operations.
    /// </summary>
    /// <param name="processType">The process type.</param>
    /// <param name="clientId">The client identifier.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The effective policy.</returns>
    public async Task<EffectivePolicy> GetPolicyAsync(
        string processType,
        string? clientId = null,
        CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(processType, nameof(processType));

        if (string.IsNullOrWhiteSpace(clientId))
        {
            // No client specified, return type default as effective policy
            var typePolicy = await GetTypePolicyAsync(processType, ct);
            return new EffectivePolicy
            {
                ProcessType = typePolicy.ProcessType,
                ClientId = "default",
                Timeout = typePolicy.Timeout,
                RetryPolicy = typePolicy.RetryPolicy,
                ResultRetention = typePolicy.ResultRetention,
                MaxConcurrentProcesses = typePolicy.MaxConcurrentProcesses,
                Source = new PolicySource
                {
                    TimeoutFromOverride = false,
                    RetryPolicyFromOverride = false,
                    ResultRetentionFromOverride = false,
                    ConcurrencyLimitFromOverride = false
                }
            };
        }

        return await GetEffectivePolicyAsync(clientId, processType, ct);
    }

    /// <summary>
    /// Refreshes all policies by clearing caches.
    /// Policies will be reloaded on next access (lazy loading).
    /// </summary>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public async Task RefreshPoliciesAsync(CancellationToken ct = default)
    {
        await _refreshLock.WaitAsync(ct);
        try
        {
            _logger.LogInformation("Starting policy cache refresh...");

            // Clear L1 cache (memory)
            var memoryCount = _memoryCache.Count + _overrideCache.Count;
            _memoryCache.Clear();
            _overrideCache.Clear();

            // Clear L2 cache (Redis) - pattern matching
            // Note: Requires implementation in ICacheStore for pattern-based deletion
            // For now, we rely on TTL expiration
            
            _logger.LogInformation(
                "Policy cache refresh completed. Cleared {Count} memory cache entries",
                memoryCount);
        }
        finally
        {
            _refreshLock.Release();
        }
    }

    /// <summary>
    /// Invalidates a specific policy from cache.
    /// </summary>
    /// <param name="processType">The process type.</param>
    /// <param name="clientId">Optional client identifier for client override invalidation.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public async Task InvalidatePolicyAsync(
        string processType,
        string? clientId = null,
        CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(processType, nameof(processType));

        if (string.IsNullOrWhiteSpace(clientId))
        {
            // Invalidate type default
            var cacheKey = $"type:{processType}";
            var redisCacheKey = $"policy:{cacheKey}";

            _memoryCache.TryRemove(cacheKey, out _);
            _cacheStatistics.RecordEviction();
            
            // Note: Redis deletion would require ICacheStore enhancement
            await Task.CompletedTask;

            _logger.LogInformation(
                "Invalidated type default policy: ProcessType={ProcessType}",
                processType);
        }
        else
        {
            // Invalidate client override
            var cacheKey = $"override:{clientId}:{processType}";
            var redisCacheKey = $"policy:{cacheKey}";

            _overrideCache.TryRemove(cacheKey, out _);
            _cacheStatistics.RecordEviction();

            await Task.CompletedTask;

            _logger.LogInformation(
                "Invalidated client override policy: ClientId={ClientId}, ProcessType={ProcessType}",
                clientId,
                processType);
        }
    }

    /// <summary>
    /// Gets cache statistics for monitoring and diagnostics.
    /// </summary>
    /// <returns>Current cache statistics.</returns>
    public PolicyCacheStatistics GetCacheStatistics() => _cacheStatistics;

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
            _cacheStatistics.RecordHit(cacheKey);
            _logger.LogDebug("Type policy cache hit (memory): {CacheKey}", cacheKey);
            return cachedPolicy;
        }

        // L2 Cache: Redis
        var redisCacheKey = $"policy:{cacheKey}";
        var redisPolicy = await _cacheStore.GetAsync<ProcessTypePolicy>(redisCacheKey, ct);
        if (redisPolicy != null)
        {
            _cacheStatistics.RecordHit(redisCacheKey);
            _logger.LogDebug("Type policy cache hit (Redis): {CacheKey}", cacheKey);
            _memoryCache.TryAdd(cacheKey, redisPolicy);
            return redisPolicy;
        }

        // Cache miss
        _cacheStatistics.RecordMiss(cacheKey);
        _logger.LogDebug("Type policy cache miss, loading from repository: {ProcessType}", processType);
        
        ProcessTypePolicy policy;
        try
        {
            policy = await _policyRepository.GetProcessTypePolicyAsync(processType, ct);
        }
        catch (KeyNotFoundException)
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
            _cacheStatistics.RecordHit(cacheKey);
            _logger.LogDebug("Client override cache hit (memory): {CacheKey}", cacheKey);
            return cachedOverride;
        }

        // L2 Cache: Redis
        var redisCacheKey = $"policy:{cacheKey}";
        var redisOverride = await _cacheStore.GetAsync<ClientPolicyOverride>(redisCacheKey, ct);
        if (redisOverride != null)
        {
            _cacheStatistics.RecordHit(redisCacheKey);
            _logger.LogDebug("Client override cache hit (Redis): {CacheKey}", cacheKey);
            _overrideCache.TryAdd(cacheKey, redisOverride);
            return redisOverride;
        }

        // Cache miss (not recording as miss since null is valid - no override exists)
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
    /// Uses PolicyResolutionService for validation and merge logic.
    /// </summary>
    private EffectivePolicy MergePolicies(
        ProcessTypePolicy typePolicy,
        ClientPolicyOverride? clientOverride,
        string clientId)
    {
        var hasOverride = clientOverride != null;

        // Use resolution service to merge policies
        ProcessTypePolicy resolvedPolicy = typePolicy;
        
        if (clientOverride != null)
        {
            // Validate client override first
            var overrideValidation = _resolutionService.ValidateClientOverride(clientOverride);
            if (!overrideValidation.IsValid)
            {
                _logger.LogWarning(
                    "Client override validation failed, using type default: ClientId={ClientId}, ProcessType={ProcessType}, Errors={Errors}",
                    clientId,
                    typePolicy.ProcessType,
                    overrideValidation.GetErrorMessage());
            }
            else
            {
                // Resolve policy with override
                resolvedPolicy = _resolutionService.ResolvePolicy(typePolicy, clientOverride);
                
                // Validate resolved policy
                var validationResult = _resolutionService.ValidatePolicy(resolvedPolicy);
                if (!validationResult.IsValid)
                {
                    _logger.LogError(
                        "Resolved policy validation failed, falling back to type default: ClientId={ClientId}, ProcessType={ProcessType}, Errors={Errors}",
                        clientId,
                        typePolicy.ProcessType,
                        validationResult.GetErrorMessage());
                    
                    // Fallback to type default
                    resolvedPolicy = typePolicy;
                }
            }
        }

        return new EffectivePolicy
        {
            ProcessType = resolvedPolicy.ProcessType,
            ClientId = clientId,
            Timeout = resolvedPolicy.Timeout,
            RetryPolicy = resolvedPolicy.RetryPolicy,
            ResultRetention = resolvedPolicy.ResultRetention,
            MaxConcurrentProcesses = resolvedPolicy.MaxConcurrentProcesses,
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
                Enabled = true,
                MaxAttempts = _options.DefaultMaxRetryAttempts,
                InitialDelay = TimeSpan.FromSeconds(_options.DefaultRetryDelaySeconds),
                BackoffStrategy = backoffStrategy,
                MaxDelay = TimeSpan.FromMinutes(5)
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
