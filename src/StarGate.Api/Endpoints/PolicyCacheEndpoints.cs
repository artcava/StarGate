namespace StarGate.Api.Endpoints;

using Microsoft.AspNetCore.Mvc;
using StarGate.Application.Services;
using StarGate.Core.Abstractions;

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
            .WithTags("Policy Cache Management")
            .RequireAuthorization();

        // Get cache statistics
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
        .WithOpenApi(operation =>
        {
            operation.Summary = "Get cache statistics";
            operation.Description = "Returns cache hit/miss statistics and per-key metrics for policy caching.";
            return operation;
        });

        // Refresh all policies
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
        .WithOpenApi(operation =>
        {
            operation.Summary = "Refresh policy cache";
            operation.Description = "Clears all cached policies. Policies will be reloaded on next access.";
            return operation;
        });

        // Invalidate specific policy
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
        .WithOpenApi(operation =>
        {
            operation.Summary = "Invalidate specific policy";
            operation.Description = "Removes a specific policy from cache. " +
                "If clientId is not provided, invalidates the type default policy. " +
                "If clientId is provided, invalidates the client override policy.";
            return operation;
        });
    }
}
