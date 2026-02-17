using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using StarGate.Core.Abstractions;

namespace StarGate.Application.Services;

/// <summary>
/// Background service that periodically refreshes the policy cache.
/// Runs continuously in the background at the configured interval.
/// </summary>
public class PolicyCacheRefreshService : BackgroundService
{
    private readonly IPolicyProvider _policyProvider;
    private readonly PolicyProviderOptions _options;
    private readonly ILogger<PolicyCacheRefreshService> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="PolicyCacheRefreshService"/> class.
    /// </summary>
    /// <param name="policyProvider">The policy provider.</param>
    /// <param name="options">The policy provider options.</param>
    /// <param name="logger">The logger.</param>
    /// <exception cref="ArgumentNullException">Thrown when any parameter is null.</exception>
    public PolicyCacheRefreshService(
        IPolicyProvider policyProvider,
        IOptions<PolicyProviderOptions> options,
        ILogger<PolicyCacheRefreshService> logger)
    {
        _policyProvider = policyProvider ?? throw new ArgumentNullException(nameof(policyProvider));
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Executes the background cache refresh at the configured interval.
    /// </summary>
    /// <param name="stoppingToken">Cancellation token to stop the service.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation(
            "Policy cache refresh service started. Interval: {IntervalMinutes} minutes",
            _options.CacheRefreshIntervalMinutes);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(
                    TimeSpan.FromMinutes(_options.CacheRefreshIntervalMinutes),
                    stoppingToken);

                _logger.LogInformation("Starting scheduled policy cache refresh...");

                await _policyProvider.RefreshPoliciesAsync(stoppingToken);

                _logger.LogInformation("Scheduled policy cache refresh completed");
            }
            catch (OperationCanceledException)
            {
                _logger.LogInformation("Policy cache refresh service stopping...");
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during scheduled policy cache refresh");
            }
        }

        _logger.LogInformation("Policy cache refresh service stopped");
    }
}
