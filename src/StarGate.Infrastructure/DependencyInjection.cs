using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MongoDB.Driver;
using StackExchange.Redis;
using StarGate.Core.Abstractions;
using StarGate.Infrastructure.Caching;
using StarGate.Infrastructure.Persistence;

namespace StarGate.Infrastructure;

/// <summary>
/// Extension methods for configuring Infrastructure services.
/// Registers MongoDB, Redis, repositories, and related infrastructure components.
/// </summary>
public static class DependencyInjection
{
    /// <summary>
    /// Adds Infrastructure services to the service collection.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configuration">Application configuration.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // ============================================
        // MongoDB Configuration
        // ============================================
        MongoDbOptions mongoOptions = configuration
            .GetSection(MongoDbOptions.SectionName)
            .Get<MongoDbOptions>() ?? throw new InvalidOperationException(
                $"MongoDB configuration section '{MongoDbOptions.SectionName}' not found in appsettings");

        services.Configure<MongoDbOptions>(
            configuration.GetSection(MongoDbOptions.SectionName));

        // Register MongoDB client as singleton (connection pooling)
        services.AddSingleton<IMongoClient>(sp =>
        {
            MongoClientSettings settings = MongoClientSettings.FromConnectionString(mongoOptions.ConnectionString);
            settings.ConnectTimeout = TimeSpan.FromMilliseconds(mongoOptions.ConnectionTimeoutMs);
            settings.ServerSelectionTimeout = TimeSpan.FromMilliseconds(mongoOptions.ServerSelectionTimeoutMs);
            settings.ApplicationName = "StarGate";

            ILogger<MongoClient> logger = sp.GetRequiredService<ILogger<MongoClient>>();
            logger.LogInformation(
                "Initializing MongoDB client: Database={DatabaseName}",
                mongoOptions.DatabaseName);

            return new MongoClient(settings);
        });

        // Register MongoDB database as singleton
        services.AddSingleton<IMongoDatabase>(sp =>
        {
            IMongoClient client = sp.GetRequiredService<IMongoClient>();
            IMongoDatabase database = client.GetDatabase(mongoOptions.DatabaseName);

            ILogger<IMongoDatabase> logger = sp.GetRequiredService<ILogger<IMongoDatabase>>();
            logger.LogInformation(
                "MongoDB database '{DatabaseName}' initialized",
                mongoOptions.DatabaseName);

            return database;
        });

        // Register repositories
        services.AddScoped<IProcessRepository, MongoProcessRepository>();
        services.AddScoped<IPolicyRepository, MongoPolicyRepository>();

        // Add MongoDB health check
        services.AddHealthChecks()
            .AddCheck<MongoDbHealthCheck>(
                name: "mongodb",
                failureStatus: HealthStatus.Unhealthy,
                tags: new[] { "database", "mongodb", "ready" });

        // Create indexes on startup if configured
        if (mongoOptions.CreateIndexesOnStartup)
        {
            services.AddHostedService<MongoDbIndexCreationService>();
        }

        // ============================================
        // Redis Cache Configuration
        // ============================================
        RedisOptions? redisOptions = configuration
            .GetSection(RedisOptions.SectionName)
            .Get<RedisOptions>();

        // Cache lock manager (always registered for stampede prevention)
        services.AddSingleton<CacheLockManager>();

        if (redisOptions?.Enabled == true)
        {
            services.Configure<RedisOptions>(
                configuration.GetSection(RedisOptions.SectionName));

            // Register cache metrics for observability
            services.AddSingleton<CacheMetrics>();

            // Register Redis connection multiplexer as singleton
            services.AddSingleton<IConnectionMultiplexer>(sp =>
            {
                // Use non-generic ILogger since RedisConnectionFactory is static
                ILogger logger = sp.GetRequiredService<ILoggerFactory>().CreateLogger("RedisConnectionFactory");
                return RedisConnectionFactory.CreateConnection(
                    redisOptions.ConnectionString,
                    logger);
            });

            // Register Redis state store with metrics
            services.AddSingleton<IStateStore>(sp =>
            {
                IConnectionMultiplexer redis = sp.GetRequiredService<IConnectionMultiplexer>();
                ILogger<RedisStateStore> logger = sp.GetRequiredService<ILogger<RedisStateStore>>();
                CacheMetrics metrics = sp.GetRequiredService<CacheMetrics>();
                TimeSpan ttl = TimeSpan.FromSeconds(redisOptions.DefaultTtlSeconds);

                logger.LogInformation(
                    "Redis cache enabled with TTL {TtlSeconds}s and metrics collection",
                    redisOptions.DefaultTtlSeconds);

                return new RedisStateStore(redis, logger, ttl, metrics);
            });
        }
        else
        {
            // Null object pattern - no-op cache when Redis is disabled
            services.AddSingleton<IStateStore, NullStateStore>();
        }

        return services;
    }
}
