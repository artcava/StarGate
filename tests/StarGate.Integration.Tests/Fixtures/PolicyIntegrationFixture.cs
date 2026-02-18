using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MongoDB.Driver;
using StarGate.Core.Abstractions;
using StarGate.Core.Domain.Configuration;
using StarGate.Infrastructure.Persistence;
using StarGate.Infrastructure.Persistence.MongoDB;
using StarGate.Infrastructure.Services;
using Testcontainers.MongoDb;
using Testcontainers.RabbitMq;
using Xunit;

namespace StarGate.Integration.Tests.Fixtures;

/// <summary>
/// Integration test fixture providing MongoDB, RabbitMQ, and policy infrastructure.
/// Implements IAsyncLifetime to manage container lifecycle and service provider setup.
/// </summary>
public class PolicyIntegrationFixture : IAsyncLifetime
{
    private readonly MongoDbContainer _mongoContainer;
    private readonly RabbitMqContainer _rabbitMqContainer;
    private IServiceProvider? _serviceProvider;

    public PolicyIntegrationFixture()
    {
        _mongoContainer = new MongoDbBuilder()
            .WithImage("mongo:7.0")
            .Build();

        _rabbitMqContainer = new RabbitMqBuilder()
            .WithImage("rabbitmq:3.12-management-alpine")
            .Build();
    }

    public IPolicyProvider PolicyProvider => GetRequiredService<IPolicyProvider>();
    public IPolicyRepository PolicyRepository => GetRequiredService<IPolicyRepository>();
    public IProcessRepository ProcessRepository => GetRequiredService<IProcessRepository>();
    public IProcessService ProcessService => GetRequiredService<IProcessService>();
    public string MongoConnectionString => _mongoContainer.GetConnectionString();
    public string RabbitMqConnectionString => _rabbitMqContainer.GetConnectionString();

    public async Task InitializeAsync()
    {
        await _mongoContainer.StartAsync();
        await _rabbitMqContainer.StartAsync();

        var services = new ServiceCollection();

        // Configure MongoDB with explicit GuidRepresentation
        var mongoSettings = MongoClientSettings.FromConnectionString(MongoConnectionString);
#pragma warning disable CS0618 // GuidRepresentation is obsolete but required for MongoDB.Driver 2.28.0
        mongoSettings.GuidRepresentation = MongoDB.Bson.GuidRepresentation.Standard;
#pragma warning restore CS0618

        var mongoClient = new MongoClient(mongoSettings);
        var database = mongoClient.GetDatabase("stargate_test");
        services.AddSingleton(database);

        // Register repositories
        services.AddSingleton<IProcessRepository, MongoProcessRepository>();
        services.AddSingleton<IPolicyRepository, MongoPolicyRepository>();

        // Register services
        services.AddMemoryCache();
        services.AddSingleton<IPolicyProvider, CachedPolicyProvider>();
        services.AddSingleton<IPolicyValidator, PolicyValidator>();
        services.AddSingleton<IProcessService, ProcessService>();

        // Register logging
        services.AddLogging(builder => builder.AddConsole().SetMinimumLevel(LogLevel.Information));

        _serviceProvider = services.BuildServiceProvider();

        // Seed default policies
        await SeedDefaultPoliciesAsync();
    }

    public async Task DisposeAsync()
    {
        if (_serviceProvider is IDisposable disposable)
        {
            disposable.Dispose();
        }

        await _mongoContainer.DisposeAsync();
        await _rabbitMqContainer.DisposeAsync();
    }

    private T GetRequiredService<T>() where T : notnull
    {
        return _serviceProvider?.GetRequiredService<T>()
            ?? throw new InvalidOperationException("Service provider not initialized");
    }

    private async Task SeedDefaultPoliciesAsync()
    {
        var repository = PolicyRepository;

        // Seed process type policies
        var orderPolicy = new ProcessTypePolicy
        {
            Id = Guid.NewGuid().ToString(),
            ProcessType = "order",
            TimeoutSeconds = 300,
            MaxRetryAttempts = 3,
            MaxConcurrentExecutions = 10,
            Priority = 5,
            IsActive = true
        };

        var paymentPolicy = new ProcessTypePolicy
        {
            Id = Guid.NewGuid().ToString(),
            ProcessType = "payment",
            TimeoutSeconds = 60,
            MaxRetryAttempts = 5,
            MaxConcurrentExecutions = 20,
            Priority = 8,
            IsActive = true
        };

        await repository.SaveTypePolicyAsync(orderPolicy);
        await repository.SaveTypePolicyAsync(paymentPolicy);
    }

    /// <summary>
    /// Clears all policies from the repository and reseeds defaults.
    /// </summary>
    public async Task ClearPoliciesAsync()
    {
        var database = GetRequiredService<IMongoDatabase>();
        await database.DropCollectionAsync("processTypePolicies");
        await database.DropCollectionAsync("clientPolicyOverrides");
        await SeedDefaultPoliciesAsync();
    }

    /// <summary>
    /// Clears all processes from the repository.
    /// </summary>
    public async Task ClearProcessesAsync()
    {
        var database = GetRequiredService<IMongoDatabase>();
        await database.DropCollectionAsync("processes");
    }
}
