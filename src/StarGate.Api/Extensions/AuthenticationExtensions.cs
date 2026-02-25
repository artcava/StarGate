namespace StarGate.Api.Extensions;

using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using StarGate.Api.Configuration;
using System.Text;

/// <summary>
/// Extension methods for configuring authentication.
/// </summary>
public static class AuthenticationExtensions
{
    /// <summary>
    /// Adds JWT authentication to the application.
    /// </summary>
    public static IServiceCollection AddJwtAuthentication(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var jwtOptions = configuration.GetSection(JwtOptions.SectionName).Get<JwtOptions>()
            ?? throw new InvalidOperationException("JWT configuration is required");

        services.Configure<JwtOptions>(configuration.GetSection(JwtOptions.SectionName));

        services.AddAuthentication(options =>
        {
            options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
            options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
        })
        .AddJwtBearer(options =>
        {
            options.RequireHttpsMetadata = jwtOptions.RequireHttpsMetadata;
            options.SaveToken = true;
            options.TokenValidationParameters = CreateTokenValidationParameters(jwtOptions);

            // Configure events for logging and custom error handling
            options.Events = new JwtBearerEvents
            {
                OnAuthenticationFailed = context =>
                {
                    var logger = context.HttpContext.RequestServices
                        .GetRequiredService<ILogger<JwtBearerEvents>>();

                    logger.LogWarning(
                        context.Exception,
                        "JWT authentication failed: {Error}",
                        context.Exception.Message);

                    return Task.CompletedTask;
                },
                OnTokenValidated = context =>
                {
                    var logger = context.HttpContext.RequestServices
                        .GetRequiredService<ILogger<JwtBearerEvents>>();

                    var clientId = context.Principal?.FindFirst("client_id")?.Value
                        ?? context.Principal?.FindFirst("azp")?.Value  // Azure AD
                        ?? "unknown";

                    logger.LogDebug(
                        "JWT token validated for client: {ClientId}",
                        clientId);

                    return Task.CompletedTask;
                },
                OnChallenge = context =>
                {
                    var logger = context.HttpContext.RequestServices
                        .GetRequiredService<ILogger<JwtBearerEvents>>();

                    logger.LogWarning(
                        "JWT authentication challenge: {Error}, {ErrorDescription}",
                        context.Error,
                        context.ErrorDescription);

                    return Task.CompletedTask;
                }
            };
        });

        return services;
    }

    private static TokenValidationParameters CreateTokenValidationParameters(JwtOptions options)
    {
        var parameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = options.ValidateLifetime,
            ValidateIssuerSigningKey = true,
            ValidIssuer = options.Issuer,
            ValidAudience = options.Audience,
            ClockSkew = options.ClockSkew
        };

        // Use secret key for development/testing
        if (!string.IsNullOrWhiteSpace(options.SecretKey))
        {
            var key = Encoding.UTF8.GetBytes(options.SecretKey);
            parameters.IssuerSigningKey = new SymmetricSecurityKey(key);
        }
        // Use authority for production (e.g., Azure AD)
        else if (!string.IsNullOrWhiteSpace(options.Authority))
        {
            // Authority will be used automatically by JwtBearer middleware
            // to download signing keys from .well-known/openid-configuration
        }
        else
        {
            throw new InvalidOperationException(
                "Either SecretKey or Authority must be configured for JWT authentication");
        }

        return parameters;
    }

    /// <summary>
    /// Configures Swagger to use JWT authentication.
    /// </summary>
    public static IServiceCollection AddSwaggerWithJwt(this IServiceCollection services)
    {
        services.AddSwaggerGen(options =>
        {
            options.AddSecurityDefinition("Bearer", new Microsoft.OpenApi.Models.OpenApiSecurityScheme
            {
                Name = "Authorization",
                Type = Microsoft.OpenApi.Models.SecuritySchemeType.Http,
                Scheme = "bearer",
                BearerFormat = "JWT",
                In = Microsoft.OpenApi.Models.ParameterLocation.Header,
                Description = "JWT Authorization header using the Bearer scheme. Example: \"Authorization: Bearer {token}\""
            });

            options.AddSecurityRequirement(new Microsoft.OpenApi.Models.OpenApiSecurityRequirement
            {
                {
                    new Microsoft.OpenApi.Models.OpenApiSecurityScheme
                    {
                        Reference = new Microsoft.OpenApi.Models.OpenApiReference
                        {
                            Type = Microsoft.OpenApi.Models.ReferenceType.SecurityScheme,
                            Id = "Bearer"
                        }
                    },
                    Array.Empty<string>()
                }
            });
        });

        return services;
    }
}
