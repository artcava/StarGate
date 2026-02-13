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

        // Unique index on ProcessId
        await processCollection.Indexes.CreateOneAsync(
            new CreateIndexModel<ProcessDocument>(
                Builders<ProcessDocument>.IndexKeys.Ascending(p => p.ProcessId),
                new CreateIndexOptions { Unique = true }));

        // Composite unique index on ClientId + ClientProcessId
        await processCollection.Indexes.CreateOneAsync(
            new CreateIndexModel<ProcessDocument>(
                Builders<ProcessDocument>.IndexKeys
                    .Ascending(p => p.ClientId)
                    .Ascending(p => p.ClientProcessId),
                new CreateIndexOptions { Unique = true }));

        // Index on Status
        await processCollection.Indexes.CreateOneAsync(
            new CreateIndexModel<ProcessDocument>(
                Builders<ProcessDocument>.IndexKeys.Ascending(p => p.Status)));

        // Index on CreatedAt
        await processCollection.Indexes.CreateOneAsync(
            new CreateIndexModel<ProcessDocument>(
                Builders<ProcessDocument>.IndexKeys.Ascending(p => p.CreatedAt)));

        // Unique index on IdempotencyKey
        await processCollection.Indexes.CreateOneAsync(
            new CreateIndexModel<ProcessDocument>(
                Builders<ProcessDocument>.IndexKeys.Ascending(p => p.IdempotencyKey),
                new CreateIndexOptions { Unique = true }));
    }
}
