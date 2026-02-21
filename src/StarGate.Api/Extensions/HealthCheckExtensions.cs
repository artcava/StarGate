using HealthChecks.UI.Client;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using StarGate.Api.HealthChecks;

namespace StarGate.Api.Extensions;

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

        // Add MongoDB health check ONLY if connection string is configured
        var mongoConnectionString = configuration.GetConnectionString("MongoDB");
        if (!string.IsNullOrWhiteSpace(mongoConnectionString))
        {
            try
            {
                healthChecksBuilder.AddMongoDb(
                    mongoConnectionString,
                    name: "mongodb",
                    failureStatus: HealthStatus.Unhealthy,
                    tags: new[] { "db", "mongodb", "ready" },
                    timeout: TimeSpan.FromSeconds(3));
            }
            catch
            {
                // Ignore MongoDB health check registration errors in test environments
            }
        }

        // Add Redis health check ONLY if connection string is configured
        var redisConnectionString = configuration.GetConnectionString("Redis");
        if (!string.IsNullOrWhiteSpace(redisConnectionString))
        {
            try
            {
                healthChecksBuilder.AddRedis(
                    redisConnectionString,
                    name: "redis",
                    failureStatus: HealthStatus.Degraded, // Degraded instead of Unhealthy (cache is optional)
                    tags: new[] { "cache", "redis", "ready" },
                    timeout: TimeSpan.FromSeconds(3));
            }
            catch
            {
                // Ignore Redis health check registration errors in test environments
            }
        }

        // Add RabbitMQ health check ONLY if configuration is present
        var rabbitMqConfig = configuration.GetSection("RabbitMQ");
        var hostName = rabbitMqConfig.GetValue<string>("HostName");
        var port = rabbitMqConfig.GetValue<int>("Port");
        var userName = rabbitMqConfig.GetValue<string>("UserName");
        var password = rabbitMqConfig.GetValue<string>("Password");
        var virtualHost = rabbitMqConfig.GetValue<string>("VirtualHost") ?? "/";

        if (!string.IsNullOrWhiteSpace(hostName) && 
            !string.IsNullOrWhiteSpace(userName) && 
            !string.IsNullOrWhiteSpace(password))
        {
            try
            {
                var rabbitMqConnectionString = $"amqp://{userName}:{password}@{hostName}:{port}{virtualHost}";
                healthChecksBuilder.AddRabbitMQ(
                    rabbitMqConnectionString,
                    name: "rabbitmq",
                    failureStatus: HealthStatus.Unhealthy,
                    tags: new[] { "messagebroker", "rabbitmq", "ready" },
                    timeout: TimeSpan.FromSeconds(3));
            }
            catch
            {
                // Ignore RabbitMQ health check registration errors in test environments
            }
        }

        // Always add custom health checks (these work without external dependencies)
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
