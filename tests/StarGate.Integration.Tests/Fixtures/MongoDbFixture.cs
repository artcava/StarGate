using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Bson.Serialization.Serializers;
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
    private static bool _serializersRegistered;
    private static readonly object _lock = new();

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
        // CRITICAL: Register serializers BEFORE creating MongoClient
        // Once MongoClient is created, it captures the current serialization settings
        RegisterSerializers();

        await _mongoContainer.StartAsync();

        _mongoClient = new MongoClient(ConnectionString);
        _database = _mongoClient.GetDatabase("stargate-test");

        // Ensure indexes are created
        await CreateIndexesAsync();
    }

    private static void RegisterSerializers()
    {
        if (_serializersRegistered)
        {
            return;
        }

        lock (_lock)
        {
            if (_serializersRegistered)
            {
                return;
            }

            // Register global GuidSerializer with Unspecified representation
            // This matches BsonBinaryData behavior with BsonBinarySubType.UuidStandard
            try
            {
                BsonSerializer.RegisterSerializer(new GuidSerializer(GuidRepresentation.Unspecified));
            }
            catch (BsonSerializationException)
            {
                // Already registered - safe to ignore
            }

            // Register ProcessDocument class map
            if (!BsonClassMap.IsClassMapRegistered(typeof(ProcessDocument)))
            {
                BsonClassMap.RegisterClassMap<ProcessDocument>(cm =>
                {
                    cm.AutoMap();
                    cm.MapIdMember(c => c.ProcessId)
                        .SetSerializer(new GuidSerializer(GuidRepresentation.Unspecified));
                });
            }

            _serializersRegistered = true;
        }
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
        // GuidRepresentation is configured via [BsonGuidRepresentation] attribute
        var processCollection = _database!.GetCollection<ProcessDocument>("processes");

        // CRITICAL: MongoDB automatically creates a unique index on _id field.
        // ProcessDocument.ProcessId is marked with [BsonId], so it IS the _id field.
        // We must NEVER create an explicit index on _id or ProcessId.
        
        // Index 1: Composite unique index on ClientId + ClientProcessId (idempotency)
        // This ensures that a client cannot submit the same process twice
        await processCollection.Indexes.CreateOneAsync(
            new CreateIndexModel<ProcessDocument>(
                Builders<ProcessDocument>.IndexKeys
                    .Ascending(p => p.ClientId)
                    .Ascending(p => p.ClientProcessId),
                new CreateIndexOptions { Unique = true, Name = "idx_clientId_clientProcessId" }));

        // Index 2: Index on Status (query optimization for status-based queries)
        await processCollection.Indexes.CreateOneAsync(
            new CreateIndexModel<ProcessDocument>(
                Builders<ProcessDocument>.IndexKeys.Ascending(p => p.Status),
                new CreateIndexOptions { Name = "idx_status" }));

        // Index 3: Index on CreatedAt (query optimization for time-based queries)
        await processCollection.Indexes.CreateOneAsync(
            new CreateIndexModel<ProcessDocument>(
                Builders<ProcessDocument>.IndexKeys.Ascending(p => p.CreatedAt),
                new CreateIndexOptions { Name = "idx_createdAt" }));

        // Index 4: Unique index on IdempotencyKey (prevent duplicate submissions)
        await processCollection.Indexes.CreateOneAsync(
            new CreateIndexModel<ProcessDocument>(
                Builders<ProcessDocument>.IndexKeys.Ascending(p => p.IdempotencyKey),
                new CreateIndexOptions { Unique = true, Name = "idx_idempotencyKey" }));

        // Index 5: Composite index on ClientId + ProcessType + Status (concurrency limit queries)
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
        // Note: The Id field is already _id (ClientId:ProcessType composite)
        // This index is for querying by these fields, not for uniqueness
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
