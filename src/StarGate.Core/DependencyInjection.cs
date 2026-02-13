using Microsoft.Extensions.DependencyInjection;
using StarGate.Core.Abstractions;
using StarGate.Core.Services;

namespace StarGate.Core;

/// <summary>
/// Extension methods for configuring Core services.
/// Registers application services, handlers, and business logic components.
/// </summary>
public static class DependencyInjection
{
    /// <summary>
    /// Adds Core services to the service collection.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddCore(this IServiceCollection services)
    {
        // Register process service (scoped per request)
        services.AddScoped<IProcessService, ProcessService>();

        return services;
    }
}
