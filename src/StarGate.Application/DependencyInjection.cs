using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using StarGate.Application.Services;
using StarGate.Core.Abstractions;

namespace StarGate.Application;

/// <summary>
/// Dependency injection configuration for Application layer.
/// </summary>
public static class DependencyInjection
{
    /// <summary>
    /// Registers application services in the DI container.
    /// </summary>
    /// <param name="services">Service collection.</param>
    /// <param name="configuration">Application configuration.</param>
    /// <returns>Service collection for chaining.</returns>
    public static IServiceCollection AddApplicationServices(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // Configure PolicyProvider options from appsettings.json
        // Uses IConfiguration.GetSection which returns IConfiguration, compatible with Configure<T>
        services.Configure<PolicyProviderOptions>(
            configuration.GetSection(PolicyProviderOptions.SectionName));

        // Register PolicyProvider as singleton (thread-safe with internal caching)
        services.AddSingleton<IPolicyProvider, PolicyProvider>();

        return services;
    }
}
