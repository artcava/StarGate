using FluentValidation;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using StackExchange.Redis;
using StarGate.Core.Abstractions;
using StarGate.Infrastructure.Caching;
using StarGate.Infrastructure.Services;
using StarGate.Infrastructure.Validation;

namespace StarGate.Infrastructure;

/// <summary>
/// Dependency injection configuration for Infrastructure layer.
/// </summary>
public static class DependencyInjection
{
    /// <summary>
    /// Registers infrastructure services (persistence, caching, messaging).
    /// </summary>
    /// <param name="services">Service collection.</param>
    /// <param name="configuration">Application configuration.</param>
    /// <returns>Service collection for chaining.</returns>
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // Redis connection (shared singleton)
        var redisConnection = configuration.GetConnectionString("Redis") 
            ?? throw new InvalidOperationException("Redis connection string not configured");
        
        services.AddSingleton<IConnectionMultiplexer>(
            ConnectionMultiplexer.Connect(redisConnection));

        // Generic cache store
        services.AddSingleton<ICacheStore, RedisCacheStore>();

        // Policy validation services
        services.AddPolicyValidation();

        // TODO: Add other infrastructure services
        // - IStateStore implementation
        // - IPolicyRepository implementation
        // - IProcessRepository implementation
        // - IMessageBroker implementation

        return services;
    }

    /// <summary>
    /// Registers policy validation services and validators.
    /// </summary>
    /// <param name="services">Service collection.</param>
    /// <returns>Service collection for chaining.</returns>
    public static IServiceCollection AddPolicyValidation(this IServiceCollection services)
    {
        // Register all validators from the assembly containing ProcessTypePolicyValidator
        services.AddValidatorsFromAssemblyContaining<ProcessTypePolicyValidator>();

        // Register policy validator service
        services.AddSingleton<IPolicyValidator, PolicyValidator>();

        return services;
    }
}
