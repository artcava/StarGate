using MongoDB.Driver;

namespace StarGate.Infrastructure.Persistence;

/// <summary>
/// Configures MongoDB indexes for the processes collection.
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
    /// Helper method to create an index with error handling and logging.
    /// </summary>
    private static async Task CreateIndexAsync(
        IMongoCollection<ProcessDocument> collection,
        IndexKeysDefinition<ProcessDocument> keys,
        CreateIndexOptions options,
        ILogger logger)
    {
        try
        {
            var indexModel = new CreateIndexModel<ProcessDocument>(keys, options);
            var indexName = await collection.Indexes.CreateOneAsync(indexModel);

            logger.LogDebug(
                "Index '{IndexName}' created successfully",
                options.Name ?? indexName);
        }
        catch (MongoCommandException ex) when (ex.CodeName == "IndexOptionsConflict")
        {
            logger.LogWarning(
                "Index '{IndexName}' already exists with different options, skipping",
                options.Name);
        }
        catch (MongoCommandException ex) when (ex.CodeName == "IndexAlreadyExists")
        {
            logger.LogDebug(
                "Index '{IndexName}' already exists, skipping",
                options.Name);
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
