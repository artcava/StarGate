namespace StarGate.Api.HealthChecks;

using Microsoft.Extensions.Diagnostics.HealthChecks;
using StarGate.Core.Abstractions;

/// <summary>
/// Health check for PolicyProvider availability.
/// </summary>
public class PolicyProviderHealthCheck : IHealthCheck
{
    private readonly IPolicyProvider _policyProvider;
    private readonly ILogger<PolicyProviderHealthCheck> _logger;

    public PolicyProviderHealthCheck(
        IPolicyProvider policyProvider,
        ILogger<PolicyProviderHealthCheck> logger)
    {
        _policyProvider = policyProvider ?? throw new ArgumentNullException(nameof(policyProvider));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogDebug("Executing PolicyProvider health check");

            // Test policy provider with a test query
            // This verifies policy repository connectivity and cache
            var policy = await _policyProvider.GetPolicyAsync(
                "health-check-test",
                "health-check-client",
                cancellationToken);

            // Policy may be null (no policy configured for test type), which is fine
            return HealthCheckResult.Healthy("PolicyProvider is operational");
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("PolicyProvider health check cancelled");
            return HealthCheckResult.Unhealthy("PolicyProvider health check was cancelled");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "PolicyProvider health check failed");
            return HealthCheckResult.Unhealthy(
                "PolicyProvider is not operational",
                ex,
                new Dictionary<string, object>
                {
                    ["error"] = ex.Message,
                    ["type"] = ex.GetType().Name
                });
        }
    }
}
