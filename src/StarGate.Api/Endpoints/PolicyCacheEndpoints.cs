using Microsoft.AspNetCore.Mvc;
using StarGate.Application.Services;
using StarGate.Core.Abstractions;

namespace StarGate.Api.Endpoints;

/// <summary>
/// Endpoints for policy cache management and monitoring.
/// </summary>
public static class PolicyCacheEndpoints
{
    /// <summary>
    /// Maps policy cache management endpoints to the application.
    /// </summary>
    /// <param name="app">The endpoint route builder.</param>
    public static void MapPolicyCacheEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/policies/cache")
            .WithTags("Policy Cache Management");
            // TODO: Enable authorization when authentication is configured in Program.cs
            // To enable:
            // 1. Add authentication services in Program.cs:
            //    builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            //        .AddJwtBearer(options => { /* configure JWT */ });
            //    builder.Services.AddAuthorization();
            // 2. Add middleware in Program.cs:
            //    app.UseAuthentication();
            //    app.UseAuthorization();
            // 3. Uncomment the line below:
            // .RequireAuthorization();

        // Get cache statistics
        // Returns cache hit/miss statistics and per-key metrics for policy caching.
        group.MapGet("/statistics", async (
            [FromServices] IPolicyProvider policyProvider) =>
        {
            if (policyProvider is PolicyProvider provider)
            {
                var stats = provider.GetCacheStatistics();
                return Results.Ok(new
                {
                    hits = stats.Hits,
                    misses = stats.Misses,
                    evictions = stats.Evictions,
                    totalRequests = stats.TotalRequests,
                    hitRatio = stats.HitRatio,
                    keyStatistics = stats.GetKeyStatistics()
                });
            }

            return Results.Ok(new { message = "Statistics not available" });
        })
        .WithName("GetPolicyCacheStatistics")
        .Produces<object>(StatusCodes.Status200OK);

        // Refresh all policies
        // Clears all cached policies. Policies will be reloaded on next access.
        group.MapPost("/refresh", async (
            [FromServices] IPolicyProvider policyProvider,
            CancellationToken cancellationToken) =>
        {
            if (policyProvider is PolicyProvider provider)
            {
                await provider.RefreshPoliciesAsync(cancellationToken);
                return Results.Ok(new { message = "Policy cache refreshed successfully" });
            }

            return Results.BadRequest(new { message = "Cache refresh not supported by current provider" });
        })
        .WithName("RefreshPolicyCache")
        .Produces<object>(StatusCodes.Status200OK)
        .Produces<object>(StatusCodes.Status400BadRequest);

        // Invalidate specific policy
        // Removes a specific policy from cache.
        // If clientId is not provided, invalidates the type default policy.
        // If clientId is provided, invalidates the client override policy.
        group.MapDelete("/{processType}", async (
            string processType,
            [FromQuery] string? clientId,
            [FromServices] IPolicyProvider policyProvider,
            CancellationToken cancellationToken) =>
        {
            if (policyProvider is PolicyProvider provider)
            {
                await provider.InvalidatePolicyAsync(processType, clientId, cancellationToken);
                
                var message = string.IsNullOrWhiteSpace(clientId)
                    ? $"Policy invalidated: {processType}"
                    : $"Policy invalidated: {processType} for client {clientId}";
                
                return Results.Ok(new { message });
            }

            return Results.BadRequest(new { message = "Cache invalidation not supported by current provider" });
        })
        .WithName("InvalidatePolicy")
        .Produces<object>(StatusCodes.Status200OK)
        .Produces<object>(StatusCodes.Status400BadRequest);
    }
}
