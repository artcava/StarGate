using Microsoft.Extensions.Diagnostics.HealthChecks;
using Polly.CircuitBreaker;
using StarGate.Infrastructure.Resilience;

namespace StarGate.Server.HealthChecks;

/// <summary>
/// Health check that monitors circuit breaker states.
/// </summary>
public class CircuitBreakerHealthCheck : IHealthCheck
{
    private readonly CircuitBreakerStateService _stateService;

    /// <summary>
    /// Initializes a new instance of the <see cref="CircuitBreakerHealthCheck"/> class.
    /// </summary>
    /// <param name="stateService">Circuit breaker state service.</param>
    /// <exception cref="ArgumentNullException">Thrown when stateService is null.</exception>
    public CircuitBreakerHealthCheck(CircuitBreakerStateService stateService)
    {
        _stateService = stateService ?? throw new ArgumentNullException(nameof(stateService));
    }

    /// <summary>
    /// Runs the health check to monitor circuit breaker states.
    /// </summary>
    /// <param name="context">Health check context.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Health check result indicating circuit breaker status.</returns>
    public Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        var states = _stateService.GetAllStates();

        if (states.Count == 0)
        {
            return Task.FromResult(
                HealthCheckResult.Healthy(
                    "No circuit breakers configured"));
        }

        var openCircuits = states.Where(kvp => kvp.Value == CircuitState.Open).ToList();
        var halfOpenCircuits = states.Where(kvp => kvp.Value == CircuitState.HalfOpen).ToList();

        var data = new Dictionary<string, object>();
        foreach (var (name, state) in states)
        {
            data[name] = state.ToString();
        }

        if (openCircuits.Any())
        {
            var openNames = string.Join(", ", openCircuits.Select(kvp => kvp.Key));
            return Task.FromResult(
                HealthCheckResult.Unhealthy(
                    $"Circuit breakers open: {openNames}",
                    data: data));
        }

        if (halfOpenCircuits.Any())
        {
            var halfOpenNames = string.Join(", ", halfOpenCircuits.Select(kvp => kvp.Key));
            return Task.FromResult(
                HealthCheckResult.Degraded(
                    $"Circuit breakers half-open: {halfOpenNames}",
                    data: data));
        }

        return Task.FromResult(
            HealthCheckResult.Healthy(
                "All circuit breakers closed",
                data: data));
    }
}
