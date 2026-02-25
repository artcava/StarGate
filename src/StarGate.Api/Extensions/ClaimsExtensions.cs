namespace StarGate.Api.Extensions;

using System.Security.Claims;

/// <summary>
/// Extension methods for working with claims.
/// </summary>
public static class ClaimsExtensions
{
    /// <summary>
    /// Gets the client ID from the claims principal.
    /// </summary>
    public static string? GetClientId(this ClaimsPrincipal principal)
    {
        return principal.FindFirst("client_id")?.Value
            ?? principal.FindFirst("azp")?.Value  // Azure AD authorized party
            ?? principal.FindFirst(ClaimTypes.NameIdentifier)?.Value;
    }

    /// <summary>
    /// Gets the user email from the claims principal.
    /// </summary>
    public static string? GetEmail(this ClaimsPrincipal principal)
    {
        return principal.FindFirst(ClaimTypes.Email)?.Value
            ?? principal.FindFirst("email")?.Value;
    }

    /// <summary>
    /// Gets the user name from the claims principal.
    /// </summary>
    public static string? GetUserName(this ClaimsPrincipal principal)
    {
        return principal.FindFirst(ClaimTypes.Name)?.Value
            ?? principal.FindFirst("name")?.Value;
    }

    /// <summary>
    /// Gets all roles from the claims principal.
    /// </summary>
    public static IEnumerable<string> GetRoles(this ClaimsPrincipal principal)
    {
        return principal.FindAll(ClaimTypes.Role).Select(c => c.Value)
            .Concat(principal.FindAll("role").Select(c => c.Value))
            .Distinct();
    }

    /// <summary>
    /// Checks if the principal has a specific role.
    /// </summary>
    public static bool HasRole(this ClaimsPrincipal principal, string role)
    {
        return principal.GetRoles().Any(r => r.Equals(role, StringComparison.OrdinalIgnoreCase));
    }
}
