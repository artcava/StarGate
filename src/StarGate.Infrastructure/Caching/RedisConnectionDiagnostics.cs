using Microsoft.Extensions.Logging;
using StackExchange.Redis;

namespace StarGate.Infrastructure.Caching;

/// <summary>
/// Provides diagnostics information for Redis connections.
/// Used for monitoring, troubleshooting, and connection status reporting.
/// </summary>
public class RedisConnectionDiagnostics
{
    private readonly IConnectionMultiplexer _redis;
    private readonly ILogger<RedisConnectionDiagnostics> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="RedisConnectionDiagnostics"/> class.
    /// </summary>
    /// <param name="redis">Redis connection multiplexer.</param>
    /// <param name="logger">Logger for diagnostics events.</param>
    /// <exception cref="ArgumentNullException">If redis or logger is null.</exception>
    public RedisConnectionDiagnostics(
        IConnectionMultiplexer redis,
        ILogger<RedisConnectionDiagnostics> logger)
    {
        _redis = redis ?? throw new ArgumentNullException(nameof(redis));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Gets current connection status and metrics.
    /// </summary>
    /// <returns>Connection status information including endpoints and connectivity.</returns>
    public ConnectionStatus GetStatus()
    {
        System.Net.EndPoint[] endpoints = _redis.GetEndPoints();
        List<IServer> servers = endpoints
            .Select(ep => _redis.GetServer(ep))
            .Where(s => s.IsConnected)
            .ToList();

        var status = new ConnectionStatus
        {
            IsConnected = _redis.IsConnected,
            EndpointCount = endpoints.Length,
            ConnectedServers = servers.Count,
            Endpoints = endpoints.Select(ep => new EndpointInfo
            {
                Address = ep.ToString() ?? "Unknown",
                IsConnected = servers.Any(s => s.EndPoint.Equals(ep))
            }).ToList(),
            Configuration = _redis.Configuration,
            ClientName = _redis.ClientName
        };

        return status;
    }

    /// <summary>
    /// Logs detailed connection diagnostics.
    /// Useful for troubleshooting connection issues and monitoring.
    /// </summary>
    public void LogDiagnostics()
    {
        try
        {
            ConnectionStatus status = GetStatus();

            _logger.LogInformation(
                "Redis Connection Diagnostics: "
                + "IsConnected={IsConnected}, "
                + "Endpoints={EndpointCount}, "
                + "ConnectedServers={ConnectedServers}",
                status.IsConnected,
                status.EndpointCount,
                status.ConnectedServers);

            foreach (EndpointInfo endpoint in status.Endpoints)
            {
                _logger.LogInformation(
                    "Redis Endpoint: {Address}, Connected={IsConnected}",
                    endpoint.Address,
                    endpoint.IsConnected);
            }

            IDatabase db = _redis.GetDatabase();
            _logger.LogInformation("Redis Database: {Database}", db.Database);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting Redis diagnostics");
        }
    }

    /// <summary>
    /// Represents the current status of Redis connection.
    /// </summary>
    public record ConnectionStatus
    {
        /// <summary>
        /// Gets whether the connection is currently established.
        /// </summary>
        public required bool IsConnected { get; init; }

        /// <summary>
        /// Gets the total number of configured endpoints.
        /// </summary>
        public required int EndpointCount { get; init; }

        /// <summary>
        /// Gets the number of currently connected servers.
        /// </summary>
        public required int ConnectedServers { get; init; }

        /// <summary>
        /// Gets the list of endpoint information.
        /// </summary>
        public required List<EndpointInfo> Endpoints { get; init; }

        /// <summary>
        /// Gets the connection configuration string.
        /// </summary>
        public required string Configuration { get; init; }

        /// <summary>
        /// Gets the client name used for the connection.
        /// </summary>
        public required string ClientName { get; init; }
    }

    /// <summary>
    /// Represents information about a single Redis endpoint.
    /// </summary>
    public record EndpointInfo
    {
        /// <summary>
        /// Gets the endpoint address.
        /// </summary>
        public required string Address { get; init; }

        /// <summary>
        /// Gets whether this endpoint is currently connected.
        /// </summary>
        public required bool IsConnected { get; init; }
    }
}
