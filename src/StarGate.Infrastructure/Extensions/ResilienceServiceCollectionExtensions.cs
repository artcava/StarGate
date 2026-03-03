using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Polly;
using StarGate.Infrastructure.Resilience;

namespace StarGate.Infrastructure.Extensions;

/// <summary>
/// Extension methods for registering resilience policies.
/// </summary>
public static class ResilienceServiceCollectionExtensions
{
    /// <summary>
    /// Adds resilience policies to the service collection.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configuration">Application configuration.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddResiliencePolicies(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // Register retry policy configuration
        services.Configure<RetryPolicyConfiguration>(
            configuration.GetSection("Resilience:Retry"));

        // Register database retry policy as singleton
        services.AddSingleton(provider =>
        {
            var config = provider.GetRequiredService<IOptions<RetryPolicyConfiguration>>().Value;
            var logger = provider.GetRequiredService<ILogger<RetryPolicyConfiguration>>();
            return RetryPolicyFactory.CreateDatabaseRetryPolicy(config, logger);
        });

        // Register broker retry policy as singleton
        services.AddSingleton(provider =>
        {
            var config = provider.GetRequiredService<IOptions<RetryPolicyConfiguration>>().Value;
            var logger = provider.GetRequiredService<ILogger<RetryPolicyConfiguration>>();
            return RetryPolicyFactory.CreateBrokerRetryPolicy(config, logger);
        });

        return services;
    }

    /// <summary>
    /// Adds HTTP client with retry policy.
    /// </summary>
    /// <typeparam name="TClient">HTTP client interface type.</typeparam>
    /// <param name="services">The service collection.</param>
    /// <param name="name">HTTP client name.</param>
    /// <returns>HTTP client builder for further configuration.</returns>
    public static IHttpClientBuilder AddHttpClientWithRetry<TClient>(
        this IServiceCollection services,
        string name)
        where TClient : class
    {
        return services
            .AddHttpClient<TClient>(name)
            .AddPolicyHandler((provider, request) =>
            {
                var config = provider.GetRequiredService<IOptions<RetryPolicyConfiguration>>().Value;
                var logger = provider.GetRequiredService<ILogger<TClient>>();
                return RetryPolicyFactory.CreateHttpRetryPolicy(config, logger);
            });
    }
}
