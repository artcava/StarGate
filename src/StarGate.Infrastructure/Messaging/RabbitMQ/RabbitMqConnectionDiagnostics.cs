using Microsoft.Extensions.Logging;
using RabbitMQ.Client;
using System.Linq;
using System.Text;

namespace StarGate.Infrastructure.Messaging.RabbitMQ;

/// <summary>
/// Provides diagnostics and monitoring for RabbitMQ connections.
/// </summary>
public class RabbitMqConnectionDiagnostics
{
    private readonly IConnection _connection;
    private readonly ILogger<RabbitMqConnectionDiagnostics> _logger;

    public RabbitMqConnectionDiagnostics(
        IConnection connection,
        ILogger<RabbitMqConnectionDiagnostics> logger)
    {
        _connection = connection ?? throw new ArgumentNullException(nameof(connection));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Gets current connection status and metrics.
    /// </summary>
    public ConnectionStatus GetStatus()
    {
        var isAutoRecovering = _connection is IAutorecoveringConnection;
        var isRecoveryInProgress = isAutoRecovering && !_connection.IsOpen;

        var status = new ConnectionStatus
        {
            IsOpen = _connection.IsOpen,
            Endpoint = _connection.Endpoint?.ToString() ?? "unknown",
            ClientProvidedName = _connection.ClientProvidedName,
            KnownHosts = _connection.KnownHosts.Length,
            ServerProperties = GetServerProperties(),
            AutomaticRecoveryEnabled = isAutoRecovering,
            RecoveryInProgress = isRecoveryInProgress
        };

        return status;
    }

    /// <summary>
    /// Logs detailed connection diagnostics.
    /// </summary>
    public void LogDiagnostics()
    {
        try
        {
            var status = GetStatus();

            var diagnostics = new StringBuilder();
            diagnostics.AppendLine("RabbitMQ Connection Diagnostics:");
            diagnostics.AppendLine($"  IsOpen: {status.IsOpen}");
            diagnostics.AppendLine($"  Endpoint: {status.Endpoint}");
            diagnostics.AppendLine($"  Client Name: {status.ClientProvidedName}");
            diagnostics.AppendLine($"  Known Hosts: {status.KnownHosts}");
            diagnostics.AppendLine($"  Auto Recovery: {status.AutomaticRecoveryEnabled}");
            diagnostics.AppendLine($"  Recovery In Progress: {status.RecoveryInProgress}");
            diagnostics.AppendLine("  Server Properties:");

            foreach (var prop in status.ServerProperties)
            {
                diagnostics.AppendLine($"    {prop.Key}: {prop.Value}");
            }

            _logger.LogInformation(diagnostics.ToString());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting RabbitMQ diagnostics");
        }
    }

    private Dictionary<string, string> GetServerProperties()
    {
        var properties = new Dictionary<string, string>();

        try
        {
            if (_connection.ServerProperties != null)
            {
                foreach (var kvp in _connection.ServerProperties)
                {
                    var value = kvp.Value?.ToString() ?? "null";
                    properties[kvp.Key] = value;
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error reading server properties");
        }

        return properties;
    }

    public record ConnectionStatus
    {
        public required bool IsOpen { get; init; }
        public required string Endpoint { get; init; }
        public required string ClientProvidedName { get; init; }
        public required int KnownHosts { get; init; }
        public required Dictionary<string, string> ServerProperties { get; init; }
        public bool AutomaticRecoveryEnabled { get; init; }
        public bool RecoveryInProgress { get; init; }
    }
}
