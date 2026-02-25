using Microsoft.AspNetCore.Authorization;

namespace StarGate.Api.Authorization.Requirements;

/// <summary>
/// Requirement that the authenticated user has specific scopes.
/// </summary>
public class ScopeRequirement : IAuthorizationRequirement
{
    public required IEnumerable<string> RequiredScopes { get; init; }

    public bool RequireAllScopes { get; init; } = false;
}

/// <summary>
/// Handler for ScopeRequirement.
/// </summary>
public class ScopeRequirementHandler : AuthorizationHandler<ScopeRequirement>
{
    private readonly ILogger<ScopeRequirementHandler> _logger;

    public ScopeRequirementHandler(ILogger<ScopeRequirementHandler> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    protected override Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        ScopeRequirement requirement)
    {
        var scopes = context.User.FindAll(ClaimTypes.Scope)
            .SelectMany(c => c.Value.Split(' ', StringSplitOptions.RemoveEmptyEntries))
            .Distinct()
            .ToList();

        _logger.LogDebug(
            "Found scopes: {Scopes}",
            string.Join(", ", scopes));

        bool hasRequiredScopes = requirement.RequireAllScopes
            ? requirement.RequiredScopes.All(rs => scopes.Contains(rs, StringComparer.OrdinalIgnoreCase))
            : requirement.RequiredScopes.Any(rs => scopes.Contains(rs, StringComparer.OrdinalIgnoreCase));

        if (hasRequiredScopes)
        {
            _logger.LogDebug(
                "Scope requirement satisfied. Required: {Required}, Has: {Has}",
                string.Join(", ", requirement.RequiredScopes),
                string.Join(", ", scopes));
            context.Succeed(requirement);
        }
        else
        {
            _logger.LogWarning(
                "Scope requirement failed. Required: {Required}, Has: {Has}",
                string.Join(", ", requirement.RequiredScopes),
                string.Join(", ", scopes));
        }

        return Task.CompletedTask;
    }
}
