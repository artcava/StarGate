namespace StarGate.Api.Configuration;

/// <summary>
/// Configuration options for JWT authentication.
/// </summary>
public class JwtOptions
{
    public const string SectionName = "Jwt";

    /// <summary>
    /// JWT issuer (who issued the token).
    /// </summary>
    public required string Issuer { get; init; }

    /// <summary>
    /// JWT audience (who the token is intended for).
    /// </summary>
    public required string Audience { get; init; }

    /// <summary>
    /// Secret key for token validation (for development/testing).
    /// In production, use certificate-based validation.
    /// </summary>
    public string? SecretKey { get; init; }

    /// <summary>
    /// Authority URL for token validation (e.g., Azure AD endpoint).
    /// </summary>
    public string? Authority { get; init; }

    /// <summary>
    /// Whether to require HTTPS for metadata endpoint.
    /// </summary>
    public bool RequireHttpsMetadata { get; init; } = true;

    /// <summary>
    /// Whether to validate token lifetime.
    /// </summary>
    public bool ValidateLifetime { get; init; } = true;

    /// <summary>
    /// Clock skew for token expiration validation.
    /// </summary>
    public TimeSpan ClockSkew { get; init; } = TimeSpan.FromMinutes(5);
}
