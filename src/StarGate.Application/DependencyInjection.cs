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
        services.Configure<PolicyProviderOptions>(options =>
        {
            var section = configuration.GetSection(PolicyProviderOptions.SectionName);
            options.CacheTtlMinutes = section.GetValue<int>(nameof(PolicyProviderOptions.CacheTtlMinutes), 60);
            options.DefaultTimeoutSeconds = section.GetValue<int>(nameof(PolicyProviderOptions.DefaultTimeoutSeconds), 300);
            options.DefaultMaxRetryAttempts = section.GetValue<int>(nameof(PolicyProviderOptions.DefaultMaxRetryAttempts), 3);
            options.DefaultRetryDelaySeconds = section.GetValue<int>(nameof(PolicyProviderOptions.DefaultRetryDelaySeconds), 5);
            options.DefaultRetentionDays = section.GetValue<int>(nameof(PolicyProviderOptions.DefaultRetentionDays), 30);
            options.DefaultMaxConcurrentProcesses = section.GetValue<int?>(nameof(PolicyProviderOptions.DefaultMaxConcurrentProcesses), 10);
            options.DefaultBackoffStrategy = section.GetValue<string>(nameof(PolicyProviderOptions.DefaultBackoffStrategy), "Exponential") ?? "Exponential";
        });

        // Register PolicyProvider as singleton (thread-safe with internal caching)
        services.AddSingleton<IPolicyProvider, PolicyProvider>();

        return services;
    }
}
