using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Polly;
using Polly.Wrap;
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

        // Register circuit breaker configuration
        services.Configure<CircuitBreakerConfiguration>(
            configuration.GetSection("Resilience:CircuitBreaker"));

        // Register wrapped resilience policies (circuit breaker + retry)
        services.AddSingleton(provider =>
        {
            var retryConfig = provider.GetRequiredService<IOptions<RetryPolicyConfiguration>>().Value;
            var circuitConfig = provider.GetRequiredService<IOptions<CircuitBreakerConfiguration>>().Value;
            var logger = provider.GetRequiredService<ILogger<RetryPolicyConfiguration>>();
            return ResiliencePolicyWrapper.CreateDatabaseResiliencePolicy(retryConfig, circuitConfig, logger);
        });

        services.AddSingleton(provider =>
        {
            var retryConfig = provider.GetRequiredService<IOptions<RetryPolicyConfiguration>>().Value;
            var circuitConfig = provider.GetRequiredService<IOptions<CircuitBreakerConfiguration>>().Value;
            var logger = provider.GetRequiredService<ILogger<RetryPolicyConfiguration>>();
            return ResiliencePolicyWrapper.CreateBrokerResiliencePolicy(retryConfig, circuitConfig, logger);
        });

        // Register HTTP resilience policy factory as singleton
        services.AddSingleton(provider =>
        {
            var retryConfig = provider.GetRequiredService<IOptions<RetryPolicyConfiguration>>().Value;
            var circuitConfig = provider.GetRequiredService<IOptions<CircuitBreakerConfiguration>>().Value;
            var loggerFactory = provider.GetRequiredService<ILoggerFactory>();
            
            // Return a factory function that creates HTTP resilience policies with appropriate logger
            return new Func<ILogger, AsyncPolicyWrap<HttpResponseMessage>>(
                logger => ResiliencePolicyWrapper.CreateHttpResiliencePolicy(retryConfig, circuitConfig, logger));
        });

        return services;
    }

    /// <summary>
    /// Adds HTTP client without automatic resilience policy.
    /// Consumers should inject AsyncPolicyWrap&lt;HttpResponseMessage&gt; and wrap calls manually.
    /// </summary>
    /// <typeparam name="TClient">HTTP client interface type.</typeparam>
    /// <param name="services">The service collection.</param>
    /// <param name="name">HTTP client name.</param>
    /// <returns>HTTP client builder for further configuration.</returns>
    /// <remarks>
    /// To use resilience policies:
    /// 1. Inject AsyncPolicyWrap&lt;HttpResponseMessage&gt; via factory
    /// 2. Wrap HTTP calls: await policy.ExecuteAsync(() => httpClient.SendAsync(request))
    /// </remarks>
    public static IHttpClientBuilder AddHttpClientWithResilience<TClient>(
        this IServiceCollection services,
        string name)
        where TClient : class
    {
        return services.AddHttpClient<TClient>(name);
    }
}
