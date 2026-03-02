using Microsoft.Extensions.DependencyInjection;
using StarGate.Core.Abstractions;
using StarGate.Server.Factories;

namespace StarGate.Server.Extensions;

/// <summary>
/// Extension methods for registering process handlers.
/// </summary>
public static class ProcessHandlerServiceCollectionExtensions
{
    /// <summary>
    /// Adds process handler infrastructure to the service collection.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddProcessHandlers(this IServiceCollection services)
    {
        // Register factory as singleton
        services.AddSingleton<IProcessHandlerFactory, ProcessHandlerFactory>();

        return services;
    }

    /// <summary>
    /// Adds a custom process handler to the service collection.
    /// </summary>
    /// <typeparam name="THandler">The handler type.</typeparam>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddProcessHandler<THandler>(
        this IServiceCollection services)
        where THandler : class, IProcessHandler
    {
        services.AddTransient<THandler>();

        services.AddSingleton(provider =>
        {
            var handler = provider.GetRequiredService<THandler>();
            var factory = provider.GetRequiredService<IProcessHandlerFactory>();
            factory.RegisterHandler(handler.ProcessType, handler);
            return handler;
        });

        return services;
    }
}
