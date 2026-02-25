namespace StarGate.Api.Extensions;

using Microsoft.AspNetCore.Authorization;
using StarGate.Api.Authorization;
using StarGate.Api.Authorization.Handlers;
using StarGate.Api.Authorization.Requirements;
using System.Security.Claims;

/// <summary>
/// Extension methods for configuring authorization.
/// </summary>
public static class AuthorizationExtensions
{
    /// <summary>
    /// Adds authorization policies to the application.
    /// </summary>
    public static IServiceCollection AddAuthorizationPolicies(this IServiceCollection services)
    {
        services.AddAuthorization(options =>
        {
            // Policy: CreateProcess
            options.AddPolicy(Policies.CreateProcess, policy =>
            {
                policy.RequireAuthenticatedUser();
                policy.AddRequirements(new ClientIdRequirement { AllowNullClientId = false });
                policy.AddRequirements(new ScopeRequirement
                {
                    RequiredScopes = new[] { "process.create", "process.write" },
                    RequireAllScopes = false
                });
            });

            // Policy: ReadOwnProcesses
            options.AddPolicy(Policies.ReadOwnProcesses, policy =>
            {
                policy.RequireAuthenticatedUser();
                policy.AddRequirements(new ClientIdRequirement { AllowNullClientId = false });
                policy.AddRequirements(new ScopeRequirement
                {
                    RequiredScopes = new[] { "process.read", "process.write" },
                    RequireAllScopes = false
                });
            });

            // Policy: ReadAllProcesses (admin only)
            options.AddPolicy(Policies.ReadAllProcesses, policy =>
            {
                policy.RequireAuthenticatedUser();
                policy.RequireRole(Roles.Admin);
            });

            // Policy: AdminOnly
            options.AddPolicy(Policies.AdminOnly, policy =>
            {
                policy.RequireAuthenticatedUser();
                policy.RequireRole(Roles.Admin);
            });
        });

        // Register authorization handlers
        services.AddScoped<IAuthorizationHandler, ClientIdRequirementHandler>();
        services.AddScoped<IAuthorizationHandler, ScopeRequirementHandler>();
        services.AddScoped<IAuthorizationHandler, ProcessAuthorizationHandler>();

        return services;
    }

    /// <summary>
    /// Authorizes access to a process resource.
    /// </summary>
    public static async Task<bool> AuthorizeProcessAccessAsync(
        this IAuthorizationService authorizationService,
        ClaimsPrincipal user,
        Core.Domain.Process process,
        ProcessOperation operation)
    {
        var requirement = new ProcessOperationRequirement { Operation = operation };
        var result = await authorizationService.AuthorizeAsync(user, process, requirement);
        return result.Succeeded;
    }
}
