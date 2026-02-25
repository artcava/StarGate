using Microsoft.AspNetCore.Authorization;

namespace StarGate.Api.Authorization.Requirements;

/// <summary>
/// Requirement that the authenticated user has a valid client ID claim.
/// </summary>
public class ClientIdRequirement : IAuthorizationRequirement
{
    public bool AllowNullClientId { get; init; }
}

/// <summary>
/// Handler for ClientIdRequirement.
/// </summary>
public class ClientIdRequirementHandler : AuthorizationHandler<ClientIdRequirement>
{
    private readonly ILogger<ClientIdRequirementHandler> _logger;

    public ClientIdRequirementHandler(ILogger<ClientIdRequirementHandler> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    protected override Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        ClientIdRequirement requirement)
    {
        var clientId = context.User.FindFirst(ClaimTypes.ClientId)?.Value
            ?? context.User.FindFirst("azp")?.Value
            ?? context.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;

        if (!string.IsNullOrWhiteSpace(clientId))
        {
            _logger.LogDebug("ClientId requirement satisfied: {ClientId}", clientId);
            context.Succeed(requirement);
        }
        else if (requirement.AllowNullClientId)
        {
            _logger.LogDebug("ClientId requirement satisfied (null allowed)");
            context.Succeed(requirement);
        }
        else
        {
            _logger.LogWarning("ClientId requirement failed: no client_id claim found");
        }

        return Task.CompletedTask;
    }
}
