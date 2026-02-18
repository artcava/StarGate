using MongoDB.Driver;
using StarGate.Core.Abstractions;
using StarGate.Core.Domain.Configuration;
using StarGate.Infrastructure.Persistence;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace StarGate.Integration.Tests.Fixtures;

/// <summary>
/// Integration test fixture for policy repository testing.
/// Uses Testcontainers for MongoDB via MongoDbFixture.
/// </summary>
public sealed class PolicyRepositoryFixture : IAsyncLifetime
{
    private readonly MongoDbFixture _mongoFixture;
    
    public IPolicyRepository PolicyRepository { get; private set; } = null!;
    
    /// <summary>
    /// Test policy instances for reuse across tests.
    /// </summary>
    public ProcessTypePolicy OrderPolicy { get; private set; } = null!;
    public ProcessTypePolicy ShippingPolicy { get; private set; } = null!;

    public PolicyRepositoryFixture()
    {
        _mongoFixture = new MongoDbFixture();
    }
    
    public async Task InitializeAsync()
    {
        // Initialize Testcontainers MongoDB
        await _mongoFixture.InitializeAsync();
        
        // Create repository instance using Testcontainers database
        PolicyRepository = new MongoPolicyRepository(
            _mongoFixture.Database,
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
        await _mongoFixture.DisposeAsync();
    }
    
    /// <summary>
    /// Helper to reset database to initial seeded state.
    /// </summary>
    public async Task ResetDatabaseAsync()
    {
        await _mongoFixture.ResetDatabaseAsync();
        await SeedTestDataAsync();
    }
}
