using Microsoft.Extensions.Diagnostics.HealthChecks;
using StarGate.Server.Workers;

namespace StarGate.Server.HealthChecks;

/// <summary>
/// Health check for ProcessWorker.
/// </summary>
public class ProcessWorkerHealthCheck : IHealthCheck
{
    private readonly ProcessWorker _worker;

    public ProcessWorkerHealthCheck(ProcessWorker worker)
    {
        _worker = worker ?? throw new ArgumentNullException(nameof(worker));
    }

    public Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        if (_worker.IsShuttingDown)
        {
            return Task.FromResult(
                HealthCheckResult.Degraded(
                    "Worker is shutting down",
                    data: new Dictionary<string, object>
                    {
                        ["activeMessages"] = _worker.ActiveMessageCount
                    }));
        }

        var activeMessages = _worker.ActiveMessageCount;

        if (activeMessages > 100)
        {
            return Task.FromResult(
                HealthCheckResult.Degraded(
                    $"High number of active messages: {activeMessages}",
                    data: new Dictionary<string, object>
                    {
                        ["activeMessages"] = activeMessages
                    }));
        }

        return Task.FromResult(
            HealthCheckResult.Healthy(
                "Worker is running normally",
                data: new Dictionary<string, object>
                {
                    ["activeMessages"] = activeMessages
                }));
    }
}
