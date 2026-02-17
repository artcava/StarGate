using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
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

    /// <summary>
    /// Initializes a new instance of the <see cref="MongoDbIndexCreationService"/> class.
    /// </summary>
    /// <param name="database">MongoDB database instance.</param>
    /// <param name="logger">Logger for tracking index creation.</param>
    /// <exception cref="ArgumentNullException">Thrown when database or logger is null.</exception>
    public MongoDbIndexCreationService(
        IMongoDatabase database,
        ILogger<MongoDbIndexCreationService> logger)
    {
        _database = database ?? throw new ArgumentNullException(nameof(database));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc/>
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Starting MongoDB index creation...");

        try
        {
            // Create indexes for processes collection
            await MongoDbIndexes.CreateProcessIndexesAsync(_database, _logger);
            
            // Create indexes for policy collections
            await MongoDbIndexes.CreatePolicyIndexesAsync(_database, _logger);
            
            _logger.LogInformation("MongoDB index creation completed successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Error creating MongoDB indexes. Application will continue, but performance may be degraded.");
            // Don't throw - allow application to start even if index creation fails
        }
    }

    /// <inheritdoc/>
    public Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("MongoDB index creation service stopping");
        return Task.CompletedTask;
    }
}
