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
        // Register ProcessService (scoped per request)
        services.AddScoped<IProcessService, ProcessService>();

        // Configure PolicyProvider options from appsettings.json
        services.Configure<PolicyProviderOptions>(options =>
        {
            var section = configuration.GetSection(PolicyProviderOptions.SectionName);
            
            options.CacheTtlMinutes = int.TryParse(section[nameof(PolicyProviderOptions.CacheTtlMinutes)], out var cacheTtl) 
                ? cacheTtl : 60;
            
            options.DefaultTimeoutSeconds = int.TryParse(section[nameof(PolicyProviderOptions.DefaultTimeoutSeconds)], out var timeout) 
                ? timeout : 300;
            
            options.DefaultMaxRetryAttempts = int.TryParse(section[nameof(PolicyProviderOptions.DefaultMaxRetryAttempts)], out var maxRetry) 
                ? maxRetry : 3;
            
            options.DefaultRetryDelaySeconds = int.TryParse(section[nameof(PolicyProviderOptions.DefaultRetryDelaySeconds)], out var retryDelay) 
                ? retryDelay : 5;
            
            options.DefaultRetentionDays = int.TryParse(section[nameof(PolicyProviderOptions.DefaultRetentionDays)], out var retention) 
                ? retention : 30;
            
            var concurrentStr = section[nameof(PolicyProviderOptions.DefaultMaxConcurrentProcesses)];
            options.DefaultMaxConcurrentProcesses = string.IsNullOrEmpty(concurrentStr) 
                ? 10 
                : int.TryParse(concurrentStr, out var concurrent) ? concurrent : 10;
            
            options.DefaultBackoffStrategy = section[nameof(PolicyProviderOptions.DefaultBackoffStrategy)] ?? "Exponential";
            
            // Cache management options
            options.CacheRefreshIntervalMinutes = int.TryParse(section[nameof(PolicyProviderOptions.CacheRefreshIntervalMinutes)], out var refreshInterval) 
                ? refreshInterval : 30;
            
            options.EnableCacheWarmup = !bool.TryParse(section[nameof(PolicyProviderOptions.EnableCacheWarmup)], out var enableWarmup) 
                || enableWarmup;
            
            options.EnableBackgroundRefresh = !bool.TryParse(section[nameof(PolicyProviderOptions.EnableBackgroundRefresh)], out var enableRefresh) 
                || enableRefresh;
            
            options.MaxMemoryCacheSize = int.TryParse(section[nameof(PolicyProviderOptions.MaxMemoryCacheSize)], out var maxCacheSize) 
                ? maxCacheSize : 1000;
        });

        // Register cache statistics service as singleton (thread-safe)
        services.AddSingleton<PolicyCacheStatistics>();

        // Register PolicyResolutionService as singleton (stateless, thread-safe)
        services.AddSingleton<PolicyResolutionService>();

        // Register PolicyProvider as singleton (thread-safe with internal caching)
        services.AddSingleton<IPolicyProvider, PolicyProvider>();

        // Register background services based on configuration
        var options = configuration
            .GetSection(PolicyProviderOptions.SectionName)
            .Get<PolicyProviderOptions>() ?? new PolicyProviderOptions();

        if (options.EnableCacheWarmup)
        {
            services.AddHostedService<PolicyCacheWarmer>();
        }

        if (options.EnableBackgroundRefresh)
        {
            services.AddHostedService<PolicyCacheRefreshService>();
        }

        return services;
    }
}
