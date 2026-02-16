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
    /// <param name="services">Service collection.
    /// <param name="configuration">Application configuration.</param>
    /// <returns>Service collection for chaining.</returns>
    public static IServiceCollection AddApplicationServices(
        this IServiceCollection services,
        Microsoft.Extensions.Configuration.IConfiguration configuration)
    {
        // Configure PolicyProvider options from appsettings.json
        // Note: NOT importing Microsoft.Extensions.Configuration namespace to avoid overload ambiguity
        // The Configure<T>(IConfiguration) extension method comes from Microsoft.Extensions.Options.ConfigurationExtensions
        services.Configure<PolicyProviderOptions>(
            configuration.GetSection(PolicyProviderOptions.SectionName));

        // Register PolicyProvider as singleton (thread-safe with internal caching)
        services.AddSingleton<IPolicyProvider, PolicyProvider>();

        return services;
    }
}
