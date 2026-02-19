namespace StarGate.Api.HealthChecks;

using Microsoft.Extensions.Diagnostics.HealthChecks;
using StarGate.Core.Abstractions;

/// <summary>
/// Health check for ProcessService availability.
/// </summary>
public class ProcessServiceHealthCheck : IHealthCheck
{
    private readonly IProcessRepository _repository;
    private readonly ILogger<ProcessServiceHealthCheck> _logger;

    public ProcessServiceHealthCheck(
        IProcessRepository repository,
        ILogger<ProcessServiceHealthCheck> logger)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogDebug("Executing ProcessService health check");

            // Test repository connectivity with a simple operation
            // This verifies MongoDB connection and basic query capability
            var testProcessId = Guid.NewGuid();
            var process = await _repository.GetByIdAsync(testProcessId, cancellationToken);

            // If we get here without exception, the service is healthy
            // (process will be null, which is expected for random GUID)
            return HealthCheckResult.Healthy("ProcessService is operational");
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("ProcessService health check cancelled");
            return HealthCheckResult.Unhealthy("ProcessService health check was cancelled");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "ProcessService health check failed");
            return HealthCheckResult.Unhealthy(
                "ProcessService is not operational",
                ex,
                new Dictionary<string, object>
                {
                    ["error"] = ex.Message,
                    ["type"] = ex.GetType().Name
                });
        }
    }
}
