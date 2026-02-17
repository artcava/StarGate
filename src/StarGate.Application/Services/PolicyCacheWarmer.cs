namespace StarGate.Application.Services;

using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using StarGate.Core.Abstractions;

/// <summary>
/// Background service that warms up the policy cache on startup.
/// Preloads all type default policies and client override policies into memory cache.
/// </summary>
public class PolicyCacheWarmer : IHostedService
{
    private readonly IPolicyRepository _policyRepository;
    private readonly IPolicyProvider _policyProvider;
    private readonly ILogger<PolicyCacheWarmer> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="PolicyCacheWarmer"/> class.
    /// </summary>
    /// <param name="policyRepository">The policy repository.</param>
    /// <param name="policyProvider">The policy provider.</param>
    /// <param name="logger">The logger.</param>
    /// <exception cref="ArgumentNullException">Thrown when any parameter is null.</exception>
    public PolicyCacheWarmer(
        IPolicyRepository policyRepository,
        IPolicyProvider policyProvider,
        ILogger<PolicyCacheWarmer> logger)
    {
        _policyRepository = policyRepository ?? throw new ArgumentNullException(nameof(policyRepository));
        _policyProvider = policyProvider ?? throw new ArgumentNullException(nameof(policyProvider));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Starts the cache warm-up process.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Starting policy cache warm-up...");

        try
        {
            var startTime = DateTime.UtcNow;

            // Load all type default policies
            var typeDefaults = await _policyRepository.GetAllTypeDefaultsAsync(cancellationToken);
            _logger.LogInformation(
                "Loading {Count} type default policies into cache",
                typeDefaults.Count);

            foreach (var policy in typeDefaults)
            {
                try
                {
                    await _policyProvider.GetDefaultPolicyAsync(
                        policy.ProcessType,
                        cancellationToken);
                }
                catch (Exception ex)
                {
                    _logger.LogError(
                        ex,
                        "Failed to warm up cache for ProcessType={ProcessType}",
                        policy.ProcessType);
                }
            }

            // Load frequently used client overrides (if any)
            var clientOverrides = await _policyRepository.GetAllClientOverridesAsync(cancellationToken);
            _logger.LogInformation(
                "Loading {Count} client override policies into cache",
                clientOverrides.Count);

            foreach (var clientOverride in clientOverrides)
            {
                try
                {
                    await _policyProvider.GetPolicyAsync(
                        clientOverride.ProcessType,
                        clientOverride.ClientId,
                        cancellationToken);
                }
                catch (Exception ex)
                {
                    _logger.LogError(
                        ex,
                        "Failed to warm up cache for ClientId={ClientId}, ProcessType={ProcessType}",
                        clientOverride.ClientId,
                        clientOverride.ProcessType);
                }
            }

            var duration = DateTime.UtcNow - startTime;
            _logger.LogInformation(
                "Policy cache warm-up completed in {DurationMs}ms",
                duration.TotalMilliseconds);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Policy cache warm-up failed");
        }
    }

    /// <summary>
    /// Stops the cache warmer (no-op as warm-up runs once on startup).
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A completed task.</returns>
    public Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Policy cache warmer stopped");
        return Task.CompletedTask;
    }
}
