using Microsoft.Extensions.Diagnostics.HealthChecks;
using MongoDB.Bson;
using MongoDB.Driver;

namespace StarGate.Infrastructure.Persistence;

/// <summary>
/// Health check for MongoDB connection.
/// Used by ASP.NET Core health check middleware for monitoring and orchestration.
/// </summary>
public class MongoDbHealthCheck : IHealthCheck
{
    private readonly IMongoDatabase _database;
    private readonly ILogger<MongoDbHealthCheck> _logger;

    public MongoDbHealthCheck(
        IMongoDatabase database,
        ILogger<MongoDbHealthCheck> logger)
    {
        _database = database ?? throw new ArgumentNullException(nameof(database));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            // Execute ping command to verify connection
            var command = new BsonDocument("ping", 1);
            await _database.RunCommandAsync<BsonDocument>(command, cancellationToken: cancellationToken);

            _logger.LogDebug("MongoDB health check passed");

            var data = new Dictionary<string, object>
            {
                ["database"] = _database.DatabaseNamespace.DatabaseName,
                ["connected"] = true,
                ["timestamp"] = DateTime.UtcNow
            };

            return HealthCheckResult.Healthy(
                "MongoDB is responsive",
                data);
        }
        catch (MongoConnectionException ex)
        {
            _logger.LogError(ex, "MongoDB connection error during health check");
            return HealthCheckResult.Unhealthy(
                "MongoDB connection failed",
                ex);
        }
        catch (TimeoutException ex)
        {
            _logger.LogError(ex, "MongoDB timeout during health check");
            return HealthCheckResult.Degraded(
                "MongoDB timeout",
                ex);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "MongoDB health check failed");
            return HealthCheckResult.Unhealthy(
                "MongoDB health check failed",
                ex);
        }
    }
}
