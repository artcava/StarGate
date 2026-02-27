using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using StarGate.Application.Services;
using StarGate.Core.Abstractions;

namespace StarGate.Application.Extensions;

/// <summary>
/// Extension methods for registering Application Layer services in the DI container.
/// </summary>
public static class ApplicationServiceExtensions
{
    /// <summary>
    /// Registers Application Layer services to the DI container.
    /// </summary>
    /// <param name="services">Service collection.</param>
    /// <param name="configuration">Configuration instance.</param>
    /// <returns>Service collection for method chaining.</returns>
    public static IServiceCollection AddApplicationServices(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        // Register ProcessService as Scoped (one instance per HTTP request)
        // Scoped is appropriate for services that maintain state during request processing
        services.AddScoped<IProcessService, ProcessService>();

        return services;
    }
}
