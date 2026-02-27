using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using StarGate.Application.Services;
using StarGate.Core.Abstractions;

namespace StarGate.Application;

/// <summary>
/// Extension methods for registering Application layer services.
/// </summary>
public static class ApplicationServiceExtensions
{
    /// <summary>
    /// Registers Application layer services in the dependency injection container.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configuration">Application configuration.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddApplicationServices(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // Register ProcessService (scoped per request)
        services.AddScoped<IProcessService, ProcessService>();

        return services;
    }
}
