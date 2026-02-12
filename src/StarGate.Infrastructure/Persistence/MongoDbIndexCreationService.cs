using Microsoft.Extensions.Hosting;
using MongoDB.Driver;

namespace StarGate.Infrastructure.Persistence;

/// <summary>
/// Hosted service that creates MongoDB indexes on application startup.
/// Runs in the background to avoid blocking the main application thread.
/// </summary>
public class MongoDbIndexCreationService : IHostedService
{
    private readonly IMongoDatabase _database;
    private readonly ILogger<MongoDbIndexCreationService> _logger;

    public MongoDbIndexCreationService(
        IMongoDatabase database,
        ILogger<MongoDbIndexCreationService> logger)
    {
        _database = database ?? throw new ArgumentNullException(nameof(database));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Starting MongoDB index creation...");

        try
        {
            await MongoDbIndexes.CreateProcessIndexesAsync(_database, _logger);
            _logger.LogInformation("MongoDB index creation completed successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Error creating MongoDB indexes. Application will continue, but performance may be degraded.");
            // Don't throw - allow application to start even if index creation fails
        }

        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("MongoDB index creation service stopping");
        return Task.CompletedTask;
    }
}
