namespace StarGate.Api.Extensions;

using Microsoft.AspNetCore.Cors.Infrastructure;
using StarGate.Api.Configuration;

/// <summary>
/// Extension methods for configuring CORS.
/// </summary>
public static class CorsExtensions
{
    public const string DefaultPolicyName = "DefaultCorsPolicy";
    public const string DevelopmentPolicyName = "DevelopmentCorsPolicy";

    /// <summary>
    /// Adds CORS configuration to the application.
    /// </summary>
    public static IServiceCollection AddApiCors(
        this IServiceCollection services,
        IConfiguration configuration,
        IWebHostEnvironment environment)
    {
        var corsOptions = configuration.GetSection(CorsOptions.SectionName).Get<CorsOptions>()
            ?? new CorsOptions();

        if (!corsOptions.Enabled)
        {
            return services;
        }

        services.Configure<CorsOptions>(configuration.GetSection(CorsOptions.SectionName));

        services.AddCors(options =>
        {
            // Default policy for production
            options.AddPolicy(DefaultPolicyName, builder =>
            {
                ConfigureCorsPolicy(builder, corsOptions, environment);
            });

            // Development policy (more permissive)
            if (environment.IsDevelopment())
            {
                options.AddPolicy(DevelopmentPolicyName, builder =>
                {
                    builder.AllowAnyOrigin()
                        .AllowAnyMethod()
                        .AllowAnyHeader();
                });
            }
        });

        return services;
    }

    private static void ConfigureCorsPolicy(
        CorsPolicyBuilder builder,
        CorsOptions options,
        IWebHostEnvironment environment)
    {
        // Configure origins
        if (environment.IsDevelopment() && options.AllowAnyOrigin)
        {
            builder.AllowAnyOrigin();
        }
        else if (options.AllowedOrigins.Any())
        {
            builder.WithOrigins(options.AllowedOrigins.ToArray())
                .SetIsOriginAllowedToAllowWildcardSubdomains();
        }
        else
        {
            throw new InvalidOperationException(
                "CORS is enabled but no allowed origins are configured. " +
                "Configure AllowedOrigins or set AllowAnyOrigin=true for development.");
        }

        // Configure methods
        if (options.AllowedMethods.Contains("*"))
        {
            builder.AllowAnyMethod();
        }
        else
        {
            builder.WithMethods(options.AllowedMethods.ToArray());
        }

        // Configure headers
        if (options.AllowedHeaders.Contains("*"))
        {
            builder.AllowAnyHeader();
        }
        else
        {
            builder.WithHeaders(options.AllowedHeaders.ToArray());
        }

        // Configure exposed headers
        if (options.ExposedHeaders.Any())
        {
            builder.WithExposedHeaders(options.ExposedHeaders.ToArray());
        }

        // Configure credentials
        if (options.AllowCredentials)
        {
            if (!options.AllowAnyOrigin && !environment.IsDevelopment())
            {
                builder.AllowCredentials();
            }
            // Note: AllowCredentials() cannot be used with AllowAnyOrigin()
        }

        // Configure preflight cache
        builder.SetPreflightMaxAge(TimeSpan.FromSeconds(options.PreflightMaxAgeSeconds));
    }

    /// <summary>
    /// Uses CORS middleware with the default policy.
    /// </summary>
    public static IApplicationBuilder UseApiCors(this IApplicationBuilder app)
    {
        app.UseCors(DefaultPolicyName);
        return app;
    }
}
