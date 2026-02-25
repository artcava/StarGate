using Microsoft.AspNetCore.Authorization;
using StarGate.Api.Extensions;
using StarGate.Core.Domain;

namespace StarGate.Api.Authorization.Handlers;

/// <summary>
/// Operations that can be performed on a process.
/// </summary>
public enum ProcessOperation
{
    Read,
    Update,
    Delete
}

/// <summary>
/// Authorization handler for process resources.
/// </summary>
public class ProcessAuthorizationHandler
    : AuthorizationHandler<ProcessOperationRequirement, Process>
{
    private readonly ILogger<ProcessAuthorizationHandler> _logger;

    public ProcessAuthorizationHandler(ILogger<ProcessAuthorizationHandler> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    protected override Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        ProcessOperationRequirement requirement,
        Process resource)
    {
        var clientId = context.User.GetClientId();
        var isAdmin = context.User.HasRole(Roles.Admin);

        _logger.LogDebug(
            "Authorizing {Operation} on process {ProcessId} for client {ClientId} (IsAdmin: {IsAdmin})",
            requirement.Operation,
            resource.ProcessId,
            clientId,
            isAdmin);

        // Admins can perform any operation
        if (isAdmin)
        {
            _logger.LogDebug("Authorization succeeded: user is admin");
            context.Succeed(requirement);
            return Task.CompletedTask;
        }

        // Clients can only access their own processes
        if (clientId == resource.ClientId)
        {
            _logger.LogDebug("Authorization succeeded: client ID matches");
            context.Succeed(requirement);
        }
        else
        {
            _logger.LogWarning(
                "Authorization failed: client {ClientId} attempted to access process owned by {ProcessClientId}",
                clientId,
                resource.ClientId);
        }

        return Task.CompletedTask;
    }
}

/// <summary>
/// Authorization requirement for process operations.
/// </summary>
public class ProcessOperationRequirement : IAuthorizationRequirement
{
    public required ProcessOperation Operation { get; init; }
}
