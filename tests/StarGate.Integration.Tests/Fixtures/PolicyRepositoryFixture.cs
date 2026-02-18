using MongoDB.Driver;
using StarGate.Core.Abstractions;
using StarGate.Core.Domain.Configuration;
using StarGate.Infrastructure.Persistence;
using Microsoft.Extensions.Logging.Abstractions;

namespace StarGate.Integration.Tests.Fixtures;

/// <summary>
/// Integration test fixture for policy repository testing.
/// Provides MongoDB test environment with cleanup.
/// </summary>
public sealed class PolicyRepositoryFixture : IAsyncLifetime
{
    private const string _testDatabaseName = "stargate_policy_integration_tests";
    
    private MongoClient? _mongoClient;
    private IMongoDatabase? _database;
    
    public IPolicyRepository PolicyRepository { get; private set; } = null!;
    
    /// <summary>
    /// Test policy instances for reuse across tests.
    /// </summary>
    public ProcessTypePolicy OrderPolicy { get; private set; } = null!;
    public ProcessTypePolicy ShippingPolicy { get; private set; } = null!;
    
    public async Task InitializeAsync()
    {
        // Connect to MongoDB (using default local connection)
        var mongoSettings = new MongoClientSettings
        {
            Server = new MongoServerAddress("localhost", 27017),
            ConnectTimeout = TimeSpan.FromSeconds(10),
            ServerSelectionTimeout = TimeSpan.FromSeconds(10)
        };
        
        _mongoClient = new MongoClient(mongoSettings);
        _database = _mongoClient.GetDatabase(_testDatabaseName);
        
        // Clean up database from previous runs
        await _mongoClient.DropDatabaseAsync(_testDatabaseName);
        
        // Create fresh database
        _database = _mongoClient.GetDatabase(_testDatabaseName);
        
        // Create repository instance
        PolicyRepository = new MongoPolicyRepository(
            _database,
            NullLogger<MongoPolicyRepository>.Instance);
        
        // Seed test data
        await SeedTestDataAsync();
    }
    
    private async Task SeedTestDataAsync()
    {
        // Create default order policy
        OrderPolicy = new ProcessTypePolicy
        {
            ProcessType = "order",
            Timeout = TimeSpan.FromMinutes(5),
            RetryPolicy = new RetryPolicy
            {
                Enabled = true,
                MaxAttempts = 3,
                InitialDelay = TimeSpan.FromSeconds(5),
                BackoffStrategy = BackoffStrategy.Exponential,
                MaxDelay = TimeSpan.FromMinutes(5)
            },
            ResultRetention = TimeSpan.FromDays(30),
            MaxConcurrentProcesses = 10,
            UpdatedAt = DateTime.UtcNow
        };
        
        // Create default shipping policy
        ShippingPolicy = new ProcessTypePolicy
        {
            ProcessType = "shipping",
            Timeout = TimeSpan.FromMinutes(10),
            RetryPolicy = new RetryPolicy
            {
                Enabled = true,
                MaxAttempts = 5,
                InitialDelay = TimeSpan.FromSeconds(10),
                BackoffStrategy = BackoffStrategy.Linear,
                MaxDelay = TimeSpan.FromMinutes(10)
            },
            ResultRetention = TimeSpan.FromDays(60),
            MaxConcurrentProcesses = 5,
            UpdatedAt = DateTime.UtcNow
        };
        
        // Save to database
        await PolicyRepository.SaveProcessTypePolicyAsync(OrderPolicy);
        await PolicyRepository.SaveProcessTypePolicyAsync(ShippingPolicy);
    }
    
    public async Task DisposeAsync()
    {
        if (_mongoClient != null && _database != null)
        {
            await _mongoClient.DropDatabaseAsync(_testDatabaseName);
        }
    }
    
    /// <summary>
    /// Helper to reset database to initial seeded state.
    /// </summary>
    public async Task ResetDatabaseAsync()
    {
        if (_mongoClient != null)
        {
            await _mongoClient.DropDatabaseAsync(_testDatabaseName);
            _database = _mongoClient.GetDatabase(_testDatabaseName);
            await SeedTestDataAsync();
        }
    }
}
