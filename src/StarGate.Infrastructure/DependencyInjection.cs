using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using MongoDB.Driver;
using StarGate.Core.Abstractions;
using StarGate.Infrastructure.Persistence;

namespace StarGate.Infrastructure;

/// <summary>
/// Extension methods for configuring Infrastructure services.
/// Registers MongoDB, repositories, and related infrastructure components.
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
        // Bind MongoDB configuration
        var mongoOptions = configuration
            .GetSection(MongoDbOptions.SectionName)
            .Get<MongoDbOptions>();

        if (mongoOptions == null)
        {
            throw new InvalidOperationException(
                $"MongoDB configuration section '{MongoDbOptions.SectionName}' not found in appsettings");
        }

        services.Configure<MongoDbOptions>(
            configuration.GetSection(MongoDbOptions.SectionName));

        // Register MongoDB client as singleton (connection pooling)
        services.AddSingleton<IMongoClient>(sp =>
        {
            var settings = MongoClientSettings.FromConnectionString(mongoOptions.ConnectionString);
            settings.ConnectTimeout = TimeSpan.FromMilliseconds(mongoOptions.ConnectionTimeoutMs);
            settings.ServerSelectionTimeout = TimeSpan.FromMilliseconds(mongoOptions.ServerSelectionTimeoutMs);
            settings.ApplicationName = "StarGate";

            var logger = sp.GetRequiredService<ILogger<MongoClient>>();
            logger.LogInformation(
                "Initializing MongoDB client: Database={DatabaseName}",
                mongoOptions.DatabaseName);

            return new MongoClient(settings);
        });

        // Register MongoDB database as singleton
        services.AddSingleton<IMongoDatabase>(sp =>
        {
            var client = sp.GetRequiredService<IMongoClient>();
            var database = client.GetDatabase(mongoOptions.DatabaseName);

            var logger = sp.GetRequiredService<ILogger<IMongoDatabase>>();
            logger.LogInformation(
                "MongoDB database '{DatabaseName}' initialized",
                mongoOptions.DatabaseName);

            return database;
        });

        // Register repositories
        services.AddScoped<IProcessRepository, MongoProcessRepository>();

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

        return services;
    }
}
