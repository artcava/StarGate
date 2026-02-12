namespace StarGate.Infrastructure.Persistence;

using Microsoft.Extensions.Logging;
using MongoDB.Driver;
using StarGate.Infrastructure.Persistence.Documents;

/// <summary>
/// MongoDB database context for StarGate.
/// Manages database connections and index creation.
/// </summary>
public class MongoDbContext
{
    private readonly IMongoDatabase _database;
    private readonly ILogger<MongoDbContext> _logger;

    public MongoDbContext(
        IMongoDatabase database,
        ILogger<MongoDbContext> logger)
    {
        _database = database ?? throw new ArgumentNullException(nameof(database));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public IMongoDatabase Database => _database;

    /// <summary>
    /// Ensures MongoDB indexes are created for policy collections.
    /// Should be called during application startup.
    /// </summary>
    public async Task EnsurePolicyIndexesAsync(CancellationToken ct = default)
    {
        var clientOverrides = _database.GetCollection<ClientPolicyOverrideDocument>("clientPolicyOverrides");

        // Compound unique index for client overrides: clientId + processType
        await clientOverrides.Indexes.CreateOneAsync(
            new CreateIndexModel<ClientPolicyOverrideDocument>(
                Builders<ClientPolicyOverrideDocument>.IndexKeys
                    .Ascending(o => o.ClientId)
                    .Ascending(o => o.ProcessType),
                new CreateIndexOptions { Unique = true }),
            cancellationToken: ct);

        // Index on ClientId for listing overrides by client
        await clientOverrides.Indexes.CreateOneAsync(
            new CreateIndexModel<ClientPolicyOverrideDocument>(
                Builders<ClientPolicyOverrideDocument>.IndexKeys.Ascending(o => o.ClientId)),
            cancellationToken: ct);

        _logger.LogInformation("Policy MongoDB indexes created successfully");
    }

    /// <summary>
    /// Ensures MongoDB indexes are created for process collections.
    /// Should be called during application startup.
    /// </summary>
    public async Task EnsureProcessIndexesAsync(CancellationToken ct = default)
    {
        // This will be implemented in the Process repository issue
        // Placeholder for future implementation
        _logger.LogInformation("Process MongoDB indexes would be created here");
        await Task.CompletedTask;
    }
}
