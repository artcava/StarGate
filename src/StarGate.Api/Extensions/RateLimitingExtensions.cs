using System.Globalization;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;
using StarGate.Api.Configuration;

namespace StarGate.Api.Extensions;

/// <summary>
/// Extension methods for configuring rate limiting.
/// </summary>
public static class RateLimitingExtensions
{
    /// <summary>
    /// Adds rate limiting to the application.
    /// </summary>
    public static IServiceCollection AddApiRateLimiting(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var rateLimitOptions = configuration.GetSection(RateLimitOptions.SectionName).Get<RateLimitOptions>()
            ?? throw new InvalidOperationException("RateLimit configuration is required");

        if (!rateLimitOptions.Enabled)
        {
            return services;
        }

        services.Configure<RateLimitOptions>(configuration.GetSection(RateLimitOptions.SectionName));

        services.AddRateLimiter(options =>
        {
            // Set rejection status code
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

            // Configure global limiter
            options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
            {
                // HARDCODE temporaneo per test: usa sempre lo stesso partition key
                var clientId = "test-client-fixed";

                // Originale (commentato temporaneamente):
                // Partition by client ID from JWT
                // var clientId = context.User.GetClientId() ?? context.Connection.RemoteIpAddress?.ToString() ?? "anonymous";

                return RateLimitPartition.GetSlidingWindowLimiter(
                    clientId,
                    _ => new SlidingWindowRateLimiterOptions
                    {
                        PermitLimit = rateLimitOptions.DefaultPolicy.PermitLimit,
                        Window = TimeSpan.FromSeconds(rateLimitOptions.DefaultPolicy.WindowSeconds),
                        SegmentsPerWindow = 10,
                        QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                        QueueLimit = rateLimitOptions.DefaultPolicy.QueueLimit
                    });
            });

            // Add policy for process creation (more restrictive)
            options.AddPolicy("CreateProcess", context =>
            {
                // HARDCODE temporaneo per test: usa sempre lo stesso partition key
                var clientId = "test-client-fixed";

                // Originale (commentato temporaneamente):
                // var clientId = context.User.GetClientId() ?? context.Connection.RemoteIpAddress?.ToString() ?? "anonymous";

                var policy = rateLimitOptions.EndpointPolicies.GetValueOrDefault("CreateProcess")
                    ?? rateLimitOptions.DefaultPolicy;

                if (policy.UseSlidingWindow)
                {
                    return RateLimitPartition.GetSlidingWindowLimiter(
                        clientId,
                        _ => new SlidingWindowRateLimiterOptions
                        {
                            PermitLimit = policy.PermitLimit,
                            Window = TimeSpan.FromSeconds(policy.WindowSeconds),
                            SegmentsPerWindow = 10,
                            QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                            QueueLimit = policy.QueueLimit
                        });
                }
                else
                {
                    return RateLimitPartition.GetFixedWindowLimiter(
                        clientId,
                        _ => new FixedWindowRateLimiterOptions
                        {
                            PermitLimit = policy.PermitLimit,
                            Window = TimeSpan.FromSeconds(policy.WindowSeconds),
                            QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                            QueueLimit = policy.QueueLimit
                        });
                }
            });

            //// Add policy for reading processes (less restrictive)
            //options.AddPolicy("ReadProcess", context =>
            //{
            //    var clientId = context.User.GetClientId() ?? context.Connection.RemoteIpAddress?.ToString() ?? "anonymous";

            //    var policy = rateLimitOptions.EndpointPolicies.GetValueOrDefault("ReadProcess")
            //        ?? rateLimitOptions.DefaultPolicy;

            //    return RateLimitPartition.GetSlidingWindowLimiter(
            //        clientId,
            //        _ => new SlidingWindowRateLimiterOptions
            //        {
            //            PermitLimit = policy.PermitLimit,
            //            Window = TimeSpan.FromSeconds(policy.WindowSeconds),
            //            SegmentsPerWindow = 10,
            //            QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
            //            QueueLimit = policy.QueueLimit
            //        });
            //});
            // Add policy for reading processes (less restrictive)
            options.AddPolicy("ReadProcess", context =>
            {
                // HARDCODE temporaneo per test: usa sempre lo stesso partition key
                var clientId = "test-client-fixed";

                // Originale (commentato temporaneamente):
                // var clientId = context.User.GetClientId() ?? context.Connection.RemoteIpAddress?.ToString() ?? "anonymous";

                var policy = rateLimitOptions.EndpointPolicies.GetValueOrDefault("ReadProcess")
                    ?? rateLimitOptions.DefaultPolicy;

                return RateLimitPartition.GetSlidingWindowLimiter(
                    clientId,
                    _ => new SlidingWindowRateLimiterOptions
                    {
                        PermitLimit = policy.PermitLimit,
                        Window = TimeSpan.FromSeconds(policy.WindowSeconds),
                        SegmentsPerWindow = 10,
                        QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                        QueueLimit = policy.QueueLimit
                    });
            });

            // Configure response on rate limit exceeded
            options.OnRejected = async (context, cancellationToken) =>
            {
                context.HttpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;

                double? retryAfterSeconds = null;

                if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
                {
                    retryAfterSeconds = retryAfter.TotalSeconds;
                    context.HttpContext.Response.Headers.RetryAfter = retryAfter.TotalSeconds.ToString(CultureInfo.InvariantCulture);
                }

                var loggerFactory = context.HttpContext.RequestServices.GetRequiredService<ILoggerFactory>();
                var logger = loggerFactory.CreateLogger("RateLimiting");
                var clientId = context.HttpContext.User.GetClientId() ?? "anonymous";

                logger.LogWarning(
                    "Rate limit exceeded for client {ClientId} on endpoint {Endpoint}",
                    clientId,
                    context.HttpContext.Request.Path);

                await context.HttpContext.Response.WriteAsJsonAsync(
                    new
                    {
                        type = "https://tools.ietf.org/html/rfc6585#section-4",
                        title = "Too Many Requests",
                        status = StatusCodes.Status429TooManyRequests,
                        detail = "Rate limit exceeded. Please try again later.",
                        retryAfter = retryAfterSeconds
                    },
                    cancellationToken: cancellationToken);
            };
        });

        return services;
    }
}
