using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MongoDB.Driver;
using StarGate.Application.Services;
using StarGate.Core.Abstractions;
using StarGate.Core.Domain.Configuration;
using StarGate.Infrastructure.Caching;
using StarGate.Infrastructure.Data.Configuration;
using StarGate.Infrastructure.Persistence.Repositories;
using StarGate.Infrastructure.Services;
using StarGate.Infrastructure.Validation;
using Xunit;

namespace StarGate.Integration.Tests.Fixtures;

/// <summary>
/// Comprehensive integration test fixture for PolicyProvider and related services.
/// Provides full DI container with MongoDB, Redis caching, and all policy services.
/// </summary>
public sealed class PolicyProviderFixture : IAsyncLifetime
{
    private const string TestDatabaseName = "stargate_policy_provider_integration_tests";
    private const string RedisConnectionString = "localhost:6379";
    
    private ServiceProvider? _serviceProvider;
    private MongoClient? _mongoClient;
    private IMongoDatabase? _database;
    
    public IPolicyProvider PolicyProvider => GetRequiredService<IPolicyProvider>();
    public IPolicyRepository PolicyRepository => GetRequiredService<IPolicyRepository>();
    public IPolicyValidator PolicyValidator => GetRequiredService<IPolicyValidator>();
    public PolicyResolutionService ResolutionService => GetRequiredService<PolicyResolutionService>();
    public PolicyCacheStatistics CacheStatistics => GetRequiredService<PolicyCacheStatistics>();
    
    /// <summary>
    /// Test data: Process type policies
    /// </summary>
    public ProcessTypePolicy OrderPolicy { get; private set; } = null!;
    public ProcessTypePolicy PaymentPolicy { get; private set; } = null!;
    public ProcessTypePolicy ShippingPolicy { get; private set; } = null!;
    
    /// <summary>
    /// Test data: Client overrides
    /// </summary>
    public ClientPolicyOverride PremiumClientOrderOverride { get; private set; } = null!;
    public ClientPolicyOverride StandardClientPaymentOverride { get; private set; } = null!;
    
    public async Task InitializeAsync()
    {
        // Setup MongoDB
        var mongoSettings = new MongoClientSettings
        {
            Server = new MongoServerAddress("localhost", 27017),
            ConnectTimeout = TimeSpan.FromSeconds(10),
            ServerSelectionTimeout = TimeSpan.FromSeconds(10)
        };
        
        _mongoClient = new MongoClient(mongoSettings);
        _database = _mongoClient.GetDatabase(TestDatabaseName);
        
        // Clean up database from previous runs
        await _mongoClient.DropDatabaseAsync(TestDatabaseName);
        _database = _mongoClient.GetDatabase(TestDatabaseName);
        
        // Configure services
        var services = new ServiceCollection();
        
        // Logging
        services.AddLogging(builder => builder
            .AddConsole()
            .SetMinimumLevel(LogLevel.Information));
        
        // MongoDB
        services.AddSingleton(_database);
        services.Configure<MongoDbOptions>(options =>
        {
            options.ConnectionString = "mongodb://localhost:27017";
            options.DatabaseName = TestDatabaseName;
        });
        
        // Redis Cache (in-memory for testing)
        services.AddMemoryCache();
        services.AddSingleton<ICacheStore, InMemoryCacheStore>();
        
        // Policy Repository
        services.AddSingleton<IPolicyRepository, MongoPolicyRepository>();
        
        // Policy Validation
        services.AddSingleton<ProcessTypePolicyValidator>();
        services.AddSingleton<ClientPolicyOverrideValidator>();
        services.AddSingleton<IPolicyValidator, PolicyValidator>();
        
        // Policy Resolution Service
        services.AddSingleton<PolicyResolutionService>();
        
        // Policy Cache Statistics
        services.AddSingleton<PolicyCacheStatistics>();
        
        // Policy Provider Options
        services.Configure<PolicyProviderOptions>(options =>
        {
            options.EnableCaching = true;
            options.CacheTtlMinutes = 30;
            options.WarmupOnStartup = false; // Manual warmup in tests
            options.DefaultTimeoutSeconds = 300;
            options.DefaultMaxRetryAttempts = 3;
            options.DefaultRetryDelaySeconds = 5;
            options.DefaultBackoffStrategy = "Exponential";
            options.DefaultRetentionDays = 30;
            options.DefaultMaxConcurrentProcesses = 10;
        });
        
        // Policy Provider
        services.AddSingleton<IPolicyProvider, PolicyProvider>();
        
        _serviceProvider = services.BuildServiceProvider();
        
        // Seed test data
        await SeedTestDataAsync();
    }
    
    private async Task SeedTestDataAsync()
    {
        var repository = PolicyRepository;
        
        // Create process type policies
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
        
        PaymentPolicy = new ProcessTypePolicy
        {
            ProcessType = "payment",
            Timeout = TimeSpan.FromMinutes(2),
            RetryPolicy = new RetryPolicy
            {
                Enabled = true,
                MaxAttempts = 5,
                InitialDelay = TimeSpan.FromSeconds(3),
                BackoffStrategy = BackoffStrategy.Exponential,
                MaxDelay = TimeSpan.FromMinutes(3)
            },
            ResultRetention = TimeSpan.FromDays(90),
            MaxConcurrentProcesses = 20,
            UpdatedAt = DateTime.UtcNow
        };
        
        ShippingPolicy = new ProcessTypePolicy
        {
            ProcessType = "shipping",
            Timeout = TimeSpan.FromMinutes(10),
            RetryPolicy = new RetryPolicy
            {
                Enabled = true,
                MaxAttempts = 2,
                InitialDelay = TimeSpan.FromSeconds(10),
                BackoffStrategy = BackoffStrategy.Linear,
                MaxDelay = TimeSpan.FromMinutes(10)
            },
            ResultRetention = TimeSpan.FromDays(60),
            MaxConcurrentProcesses = 5,
            UpdatedAt = DateTime.UtcNow
        };
        
        await repository.SaveProcessTypePolicyAsync(OrderPolicy);
        await repository.SaveProcessTypePolicyAsync(PaymentPolicy);
        await repository.SaveProcessTypePolicyAsync(ShippingPolicy);
        
        // Create client overrides
        PremiumClientOrderOverride = new ClientPolicyOverride
        {
            ClientId = "premium-client",
            ProcessType = "order",
            Timeout = TimeSpan.FromMinutes(15), // Extended timeout
            RetryPolicy = new RetryPolicy
            {
                Enabled = true,
                MaxAttempts = 5, // More retries
                InitialDelay = TimeSpan.FromSeconds(5),
                BackoffStrategy = BackoffStrategy.Exponential,
                MaxDelay = TimeSpan.FromMinutes(10)
            },
            ResultRetention = null, // Use default
            MaxConcurrentProcesses = 50, // Higher concurrency
            UpdatedAt = DateTime.UtcNow
        };
        
        StandardClientPaymentOverride = new ClientPolicyOverride
        {
            ClientId = "standard-client",
            ProcessType = "payment",
            Timeout = TimeSpan.FromMinutes(1), // Shorter timeout
            RetryPolicy = null, // Use default
            ResultRetention = TimeSpan.FromDays(30), // Shorter retention
            MaxConcurrentProcesses = null, // Use default
            UpdatedAt = DateTime.UtcNow
        };
        
        await repository.SaveClientOverrideAsync(PremiumClientOrderOverride);
        await repository.SaveClientOverrideAsync(StandardClientPaymentOverride);
    }
    
    public async Task DisposeAsync()
    {
        if (_mongoClient != null && _database != null)
        {
            await _mongoClient.DropDatabaseAsync(TestDatabaseName);
        }
        
        _serviceProvider?.Dispose();
    }
    
    /// <summary>
    /// Helper to reset all caches and statistics.
    /// </summary>
    public async Task ResetCachesAsync()
    {
        await PolicyProvider.RefreshPoliciesAsync();
        CacheStatistics.Reset();
    }
    
    /// <summary>
    /// Helper to reset database to initial seeded state.
    /// </summary>
    public async Task ResetDatabaseAsync()
    {
        if (_mongoClient != null)
        {
            await _mongoClient.DropDatabaseAsync(TestDatabaseName);
            _database = _mongoClient.GetDatabase(TestDatabaseName);
            await SeedTestDataAsync();
        }
    }
    
    private T GetRequiredService<T>() where T : notnull
    {
        return _serviceProvider?.GetRequiredService<T>()
            ?? throw new InvalidOperationException("Service provider not initialized");
    }
}

/// <summary>
/// Simple in-memory implementation of ICacheStore for testing.
/// Simulates Redis behavior without external dependencies.
/// </summary>
public class InMemoryCacheStore : ICacheStore
{
    private readonly IMemoryCache _cache;
    
    public InMemoryCacheStore(IMemoryCache cache)
    {
        _cache = cache ?? throw new ArgumentNullException(nameof(cache));
    }
    
    public Task<T?> GetAsync<T>(string key, CancellationToken ct = default) where T : class
    {
        _cache.TryGetValue(key, out T? value);
        return Task.FromResult(value);
    }
    
    public Task SetAsync<T>(string key, T value, TimeSpan? expiration = null, CancellationToken ct = default) where T : class
    {
        var options = new MemoryCacheEntryOptions();
        if (expiration.HasValue)
        {
            options.AbsoluteExpirationRelativeToNow = expiration.Value;
        }
        
        _cache.Set(key, value, options);
        return Task.CompletedTask;
    }
    
    public Task<bool> RemoveAsync(string key, CancellationToken ct = default)
    {
        _cache.Remove(key);
        return Task.FromResult(true);
    }
    
    public Task<bool> ExistsAsync(string key, CancellationToken ct = default)
    {
        var exists = _cache.TryGetValue(key, out _);
        return Task.FromResult(exists);
    }
}
