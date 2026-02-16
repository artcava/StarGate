using MongoDB.Driver;
using StarGate.Infrastructure.Persistence;
using Testcontainers.MongoDb;
using Xunit;

namespace StarGate.Integration.Tests.Fixtures;

/// <summary>
/// Provides a MongoDB test container for integration tests.
/// Implements IAsyncLifetime to manage container lifecycle.
/// </summary>
public class MongoDbFixture : IAsyncLifetime
{
    private readonly MongoDbContainer _mongoContainer;
    private IMongoClient? _mongoClient;
    private IMongoDatabase? _database;

    public MongoDbFixture()
    {
        _mongoContainer = new MongoDbBuilder()
            .WithImage("mongo:7.0")
            .WithPortBinding(27017, true)
            .Build();
    }

    public IMongoDatabase Database => _database 
        ?? throw new InvalidOperationException("Database not initialized. Call InitializeAsync first.");

    public string ConnectionString => _mongoContainer.GetConnectionString();

    public async Task InitializeAsync()
    {
        await _mongoContainer.StartAsync();

        _mongoClient = new MongoClient(ConnectionString);
        _database = _mongoClient.GetDatabase("stargate-test");

        // Ensure indexes are created
        await CreateIndexesAsync();
    }

    public async Task DisposeAsync()
    {
        await _mongoContainer.DisposeAsync();
    }

    public async Task ResetDatabaseAsync()
    {
        // _database is guaranteed to be non-null after InitializeAsync
        await _database!.DropCollectionAsync("processes");
        await _database!.DropCollectionAsync("processTypePolicies");
        await _database!.DropCollectionAsync("clientPolicyOverrides");
        await CreateIndexesAsync();
    }

    private async Task CreateIndexesAsync()
    {
        // _database is guaranteed to be non-null when this method is called
        var processCollection = _database!.GetCollection<ProcessDocument>("processes");

        // NOTE: MongoDB automatically creates a unique index on _id field.
        // We should NEVER explicitly create an index on _id as it's invalid.
        // Only create indexes on custom fields.

        // Index 1: Unique index on ProcessId (business key)
        await processCollection.Indexes.CreateOneAsync(
            new CreateIndexModel<ProcessDocument>(
                Builders<ProcessDocument>.IndexKeys.Ascending(p => p.ProcessId),
                new CreateIndexOptions { Unique = true, Name = "idx_processId" }));

        // Index 2: Composite unique index on ClientId + ClientProcessId (idempotency)
        await processCollection.Indexes.CreateOneAsync(
            new CreateIndexModel<ProcessDocument>(
                Builders<ProcessDocument>.IndexKeys
                    .Ascending(p => p.ClientId)
                    .Ascending(p => p.ClientProcessId),
                new CreateIndexOptions { Unique = true, Name = "idx_clientId_clientProcessId" }));

        // Index 3: Index on Status (query optimization)
        await processCollection.Indexes.CreateOneAsync(
            new CreateIndexModel<ProcessDocument>(
                Builders<ProcessDocument>.IndexKeys.Ascending(p => p.Status),
                new CreateIndexOptions { Name = "idx_status" }));

        // Index 4: Index on CreatedAt (query optimization)
        await processCollection.Indexes.CreateOneAsync(
            new CreateIndexModel<ProcessDocument>(
                Builders<ProcessDocument>.IndexKeys.Ascending(p => p.CreatedAt),
                new CreateIndexOptions { Name = "idx_createdAt" }));

        // Index 5: Unique index on IdempotencyKey (prevent duplicates)
        await processCollection.Indexes.CreateOneAsync(
            new CreateIndexModel<ProcessDocument>(
                Builders<ProcessDocument>.IndexKeys.Ascending(p => p.IdempotencyKey),
                new CreateIndexOptions { Unique = true, Name = "idx_idempotencyKey" }));

        // Index 6: Composite index on ClientId + ProcessType + Status (concurrency queries)
        await processCollection.Indexes.CreateOneAsync(
            new CreateIndexModel<ProcessDocument>(
                Builders<ProcessDocument>.IndexKeys
                    .Ascending(p => p.ClientId)
                    .Ascending(p => p.ProcessType)
                    .Ascending(p => p.Status),
                new CreateIndexOptions { Name = "idx_clientId_processType_status" }));

        // Policy collections indexes
        // Note: ProcessTypePolicyDocument uses ProcessType as _id, so no additional index needed
        
        // ClientPolicyOverrideDocument indexes
        var clientOverridesCollection = _database!.GetCollection<ClientPolicyOverrideDocument>("clientPolicyOverrides");
        
        // Index 1: Composite index on ClientId + ProcessType for queries
        // Note: The Id field is already _id (ClientId:ProcessType composite), but we need this for queries
        await clientOverridesCollection.Indexes.CreateOneAsync(
            new CreateIndexModel<ClientPolicyOverrideDocument>(
                Builders<ClientPolicyOverrideDocument>.IndexKeys
                    .Ascending(o => o.ClientId)
                    .Ascending(o => o.ProcessType),
                new CreateIndexOptions { Name = "idx_clientId_processType" }));

        // Index 2: Index on ClientId alone for listing overrides by client
        await clientOverridesCollection.Indexes.CreateOneAsync(
            new CreateIndexModel<ClientPolicyOverrideDocument>(
                Builders<ClientPolicyOverrideDocument>.IndexKeys.Ascending(o => o.ClientId),
                new CreateIndexOptions { Name = "idx_clientId" }));
    }
}
