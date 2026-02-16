namespace StarGate.Infrastructure.Messaging.RabbitMQ;

using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;

/// <summary>
/// Health check for RabbitMQ connection.
/// </summary>
public class RabbitMqHealthCheck : IHealthCheck
{
    private readonly IConnection _connection;
    private readonly ILogger<RabbitMqHealthCheck> _logger;

    public RabbitMqHealthCheck(
        IConnection connection,
        ILogger<RabbitMqHealthCheck> logger)
    {
        _connection = connection ?? throw new ArgumentNullException(nameof(connection));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            if (!_connection.IsOpen)
            {
                _logger.LogWarning("RabbitMQ health check: Connection is not open");
                return Task.FromResult(HealthCheckResult.Unhealthy(
                    "RabbitMQ connection is not open"));
            }

            // Try to create and close a channel to verify connection
            using var channel = _connection.CreateModel();
            
            if (!channel.IsOpen)
            {
                _logger.LogWarning("RabbitMQ health check: Channel could not be opened");
                return Task.FromResult(HealthCheckResult.Degraded(
                    "RabbitMQ channel could not be opened"));
            }

            channel.Close();

            var data = new Dictionary<string, object>
            {
                ["endpoint"] = _connection.Endpoint?.ToString() ?? "unknown",
                ["isOpen"] = _connection.IsOpen,
                ["serverProperties"] = _connection.ServerProperties?.Count ?? 0
            };

            _logger.LogDebug("RabbitMQ health check: Healthy");

            return Task.FromResult(HealthCheckResult.Healthy(
                "RabbitMQ connection is open and responsive",
                data));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "RabbitMQ health check failed");
            return Task.FromResult(HealthCheckResult.Unhealthy(
                "RabbitMQ health check failed",
                ex));
        }
    }
}
