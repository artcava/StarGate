using Microsoft.Extensions.Logging;
using MongoDB.Driver;

namespace StarGate.Infrastructure.Persistence;

/// <summary>
/// Configures MongoDB indexes for collections.
/// Indexes are critical for query performance and data integrity.
/// </summary>
public static class MongoDbIndexes
{
    /// <summary>
    /// Creates all required indexes for the processes collection.
    /// Should be called during application startup or database migration.
    /// </summary>
    /// <param name="database">MongoDB database instance.</param>
    /// <param name="logger">Logger for tracking index creation.</param>
    public static async Task CreateProcessIndexesAsync(
        IMongoDatabase database,
        ILogger logger)
    {
        ArgumentNullException.ThrowIfNull(database);
        ArgumentNullException.ThrowIfNull(logger);

        var collection = database.GetCollection<ProcessDocument>("processes");

        logger.LogInformation("Creating MongoDB indexes for processes collection...");

        try
        {
            // Index 1: Unique index on ProcessId (Primary Key)
            await CreateIndexAsync(
                collection,
                Builders<ProcessDocument>.IndexKeys.Ascending(p => p.ProcessId),
                new CreateIndexOptions
                {
                    Name = "idx_processId",
                    Unique = true
                },
                logger);

            // Index 2: Composite unique index on ClientId + ClientProcessId (Idempotency)
            await CreateIndexAsync(
                collection,
                Builders<ProcessDocument>.IndexKeys
                    .Ascending(p => p.ClientId)
                    .Ascending(p => p.ClientProcessId),
                new CreateIndexOptions
                {
                    Name = "idx_clientId_clientProcessId",
                    Unique = true
                },
                logger);

            // Index 3: Index on Status (Query optimization)
            await CreateIndexAsync(
                collection,
                Builders<ProcessDocument>.IndexKeys.Ascending(p => p.Status),
                new CreateIndexOptions
                {
                    Name = "idx_status"
                },
                logger);

            // Index 4: Index on CreatedAt (Query optimization)
            await CreateIndexAsync(
                collection,
                Builders<ProcessDocument>.IndexKeys.Ascending(p => p.CreatedAt),
                new CreateIndexOptions
                {
                    Name = "idx_createdAt"
                },
                logger);

            // Index 5: Unique index on IdempotencyKey (Prevent duplicates)
            await CreateIndexAsync(
                collection,
                Builders<ProcessDocument>.IndexKeys.Ascending(p => p.IdempotencyKey),
                new CreateIndexOptions
                {
                    Name = "idx_idempotencyKey",
                    Unique = true
                },
                logger);

            // Index 6: Composite index on ClientId + ProcessType + Status (Concurrency limit queries)
            await CreateIndexAsync(
                collection,
                Builders<ProcessDocument>.IndexKeys
                    .Ascending(p => p.ClientId)
                    .Ascending(p => p.ProcessType)
                    .Ascending(p => p.Status),
                new CreateIndexOptions
                {
                    Name = "idx_clientId_processType_status"
                },
                logger);

            logger.LogInformation("MongoDB indexes created successfully for processes collection");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error creating MongoDB indexes for processes collection");
            throw;
        }
    }

    /// <summary>
    /// Creates all required indexes for policy collections.
    /// Should be called during application startup or database migration.
    /// </summary>
    /// <param name="database">MongoDB database instance.</param>
    /// <param name="logger">Logger for tracking index creation.</param>
    public static async Task CreatePolicyIndexesAsync(
        IMongoDatabase database,
        ILogger logger)
    {
        ArgumentNullException.ThrowIfNull(database);
        ArgumentNullException.ThrowIfNull(logger);

        var processTypePolicies = database.GetCollection<ProcessTypePolicyDocument>("processTypePolicies");
        var clientOverrides = database.GetCollection<ClientPolicyOverrideDocument>("clientPolicyOverrides");

        logger.LogInformation("Creating MongoDB indexes for policy collections...");

        try
        {
            // ProcessType is already the primary key (_id) for process type policies
            // No additional index needed

            // Index 1: Compound unique index on ClientId + ProcessType for client overrides
            await CreateIndexAsync(
                clientOverrides,
                Builders<ClientPolicyOverrideDocument>.IndexKeys
                    .Ascending(o => o.ClientId)
                    .Ascending(o => o.ProcessType),
                new CreateIndexOptions
                {
                    Name = "idx_clientId_processType",
                    Unique = true
                },
                logger);

            // Index 2: Index on ClientId for listing overrides by client
            await CreateIndexAsync(
                clientOverrides,
                Builders<ClientPolicyOverrideDocument>.IndexKeys.Ascending(o => o.ClientId),
                new CreateIndexOptions
                {
                    Name = "idx_clientId"
                },
                logger);

            logger.LogInformation("MongoDB indexes created successfully for policy collections");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error creating MongoDB indexes for policy collections");
            throw;
        }
    }

    /// <summary>
    /// Helper method to create an index with error handling and logging.
    /// </summary>
    private static async Task CreateIndexAsync<TDocument>(
        IMongoCollection<TDocument> collection,
        IndexKeysDefinition<TDocument> keys,
        CreateIndexOptions options,
        ILogger logger)
    {
        try
        {
            var indexModel = new CreateIndexModel<TDocument>(keys, options);
            var indexName = await collection.Indexes.CreateOneAsync(indexModel);

            logger.LogDebug(
                "Index '{IndexName}' created successfully on collection '{CollectionName}'",
                options.Name ?? indexName,
                collection.CollectionNamespace.CollectionName);
        }
        catch (MongoCommandException ex) when (ex.CodeName == "IndexOptionsConflict")
        {
            logger.LogWarning(
                "Index '{IndexName}' already exists with different options on collection '{CollectionName}', skipping",
                options.Name,
                collection.CollectionNamespace.CollectionName);
        }
        catch (MongoCommandException ex) when (ex.CodeName == "IndexAlreadyExists")
        {
            logger.LogDebug(
                "Index '{IndexName}' already exists on collection '{CollectionName}', skipping",
                options.Name,
                collection.CollectionNamespace.CollectionName);
        }
    }

    /// <summary>
    /// Lists all indexes in the processes collection.
    /// Useful for debugging and verification.
    /// </summary>
    /// <param name="database">MongoDB database instance.</param>
    /// <param name="logger">Logger for output.</param>
    public static async Task ListProcessIndexesAsync(
        IMongoDatabase database,
        ILogger logger)
    {
        ArgumentNullException.ThrowIfNull(database);
        ArgumentNullException.ThrowIfNull(logger);

        var collection = database.GetCollection<ProcessDocument>("processes");

        logger.LogInformation("Listing indexes for processes collection:");

        using var cursor = await collection.Indexes.ListAsync();
        var indexes = await cursor.ToListAsync();

        foreach (var index in indexes)
        {
            logger.LogInformation(
                "Index: {IndexName}, Keys: {Keys}",
                index["name"],
                index["key"]);
        }
    }
}
