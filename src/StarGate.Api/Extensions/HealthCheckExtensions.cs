namespace StarGate.Api.Extensions;

using HealthChecks.UI.Client;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using StarGate.Api.HealthChecks;

/// <summary>
/// Extension methods for configuring health checks.
/// </summary>
public static class HealthCheckExtensions
{
    /// <summary>
    /// Adds all health checks for the application.
    /// </summary>
    public static IServiceCollection AddApplicationHealthChecks(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var healthChecksBuilder = services.AddHealthChecks();

        // Add MongoDB health check if connection string is configured
        var mongoConnectionString = configuration.GetConnectionString("MongoDB");
        if (!string.IsNullOrWhiteSpace(mongoConnectionString))
        {
            healthChecksBuilder.AddMongoDb(
                mongoConnectionString,
                name: "mongodb",
                failureStatus: HealthStatus.Unhealthy,
                tags: new[] { "db", "mongodb", "ready" },
                timeout: TimeSpan.FromSeconds(3));
        }

        // Add Redis health check if connection string is configured
        var redisConnectionString = configuration.GetConnectionString("Redis");
        if (!string.IsNullOrWhiteSpace(redisConnectionString))
        {
            healthChecksBuilder.AddRedis(
                redisConnectionString,
                name: "redis",
                failureStatus: HealthStatus.Degraded, // Degraded instead of Unhealthy (cache is optional)
                tags: new[] { "cache", "redis", "ready" },
                timeout: TimeSpan.FromSeconds(3));
        }

        // Add RabbitMQ health check if connection string is configured
        var rabbitMqConnectionString = configuration.GetConnectionString("RabbitMQ");
        if (!string.IsNullOrWhiteSpace(rabbitMqConnectionString))
        {
            healthChecksBuilder.AddRabbitMQ(
                rabbitMqConnectionString,
                name: "rabbitmq",
                failureStatus: HealthStatus.Unhealthy,
                tags: new[] { "messagebroker", "rabbitmq", "ready" },
                timeout: TimeSpan.FromSeconds(3));
        }
        else
        {
            // Fallback: build connection string from RabbitMQ configuration section
            var rabbitMqConfig = configuration.GetSection("RabbitMQ");
            var hostName = rabbitMqConfig.GetValue<string>("HostName");
            var port = rabbitMqConfig.GetValue<int>("Port");
            var userName = rabbitMqConfig.GetValue<string>("UserName");
            var password = rabbitMqConfig.GetValue<string>("Password");
            var virtualHost = rabbitMqConfig.GetValue<string>("VirtualHost") ?? "/";

            if (!string.IsNullOrWhiteSpace(hostName))
            {
                var connectionString = $"amqp://{userName}:{password}@{hostName}:{port}{virtualHost}";
                healthChecksBuilder.AddRabbitMQ(
                    connectionString,
                    name: "rabbitmq",
                    failureStatus: HealthStatus.Unhealthy,
                    tags: new[] { "messagebroker", "rabbitmq", "ready" },
                    timeout: TimeSpan.FromSeconds(3));
            }
        }

        // Add custom health checks
        healthChecksBuilder.AddCheck<ProcessServiceHealthCheck>(
            "process-service",
            failureStatus: HealthStatus.Unhealthy,
            tags: new[] { "service", "ready" },
            timeout: TimeSpan.FromSeconds(5));

        healthChecksBuilder.AddCheck<PolicyProviderHealthCheck>(
            "policy-provider",
            failureStatus: HealthStatus.Degraded,
            tags: new[] { "service", "ready" },
            timeout: TimeSpan.FromSeconds(5));

        return services;
    }

    /// <summary>
    /// Maps health check endpoints.
    /// </summary>
    public static IEndpointRouteBuilder MapHealthCheckEndpoints(this IEndpointRouteBuilder app)
    {
        // Liveness endpoint - always returns 200 if API is running
        app.MapHealthChecks("/health/live", new HealthCheckOptions
        {
            Predicate = _ => false, // No checks, just returns if API is alive
            ResponseWriter = async (context, report) =>
            {
                context.Response.ContentType = "application/json";
                await context.Response.WriteAsJsonAsync(new
                {
                    status = "Healthy",
                    timestamp = DateTime.UtcNow
                });
            }
        })
        .WithName("Liveness")
        .WithTags("Health")
        .AllowAnonymous(); // No authentication required

        // Readiness endpoint - checks all dependencies
        app.MapHealthChecks("/health/ready", new HealthCheckOptions
        {
            Predicate = check => check.Tags.Contains("ready"),
            ResponseWriter = UIResponseWriter.WriteHealthCheckUIResponse
        })
        .WithName("Readiness")
        .WithTags("Health")
        .AllowAnonymous();

        // Detailed health endpoint (for monitoring/debugging)
        app.MapHealthChecks("/health", new HealthCheckOptions
        {
            ResponseWriter = UIResponseWriter.WriteHealthCheckUIResponse
        })
        .WithName("Health")
        .WithTags("Health")
        .AllowAnonymous();

        return app;
    }
}
