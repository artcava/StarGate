using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using StarGate.Api.Configuration;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace StarGate.Api.Services;

/// <summary>
/// Service for generating JWT tokens for development and testing purposes.
/// </summary>
public interface IJwtTokenGenerator
{
    /// <summary>
    /// Generates a JWT token with the specified claims.
    /// </summary>
    public string GenerateToken(string clientId, IEnumerable<string> processTypes, int expirationMinutes = 60);
}

/// <summary>
/// Implementation of JWT token generator.
/// </summary>
public class JwtTokenGenerator : IJwtTokenGenerator
{
    private readonly JwtOptions _jwtOptions;
    private readonly ILogger<JwtTokenGenerator> _logger;

    public JwtTokenGenerator(
        IOptions<JwtOptions> jwtOptions,
        ILogger<JwtTokenGenerator> logger)
    {
        _jwtOptions = jwtOptions.Value;
        _logger = logger;
    }

    public string GenerateToken(string clientId, IEnumerable<string> processTypes, int expirationMinutes = 60)
    {
        if (string.IsNullOrWhiteSpace(_jwtOptions.SecretKey))
        {
            throw new InvalidOperationException(
                "SecretKey must be configured in JWT options to generate tokens");
        }

        var securityKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_jwtOptions.SecretKey));
        var credentials = new SigningCredentials(securityKey, SecurityAlgorithms.HmacSha256);

        var claims = new List<Claim>
        {
            new("client_id", clientId),
            new(JwtRegisteredClaimNames.Sub, clientId),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new(JwtRegisteredClaimNames.Iat, DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString(), ClaimValueTypes.Integer64)
        };

        // Add process types as multiple claims
        foreach (var processType in processTypes)
        {
            claims.Add(new Claim("process_type", processType));
        }

        var token = new JwtSecurityToken(
            issuer: _jwtOptions.Issuer,
            audience: _jwtOptions.Audience,
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(expirationMinutes),
            signingCredentials: credentials
        );

        var tokenString = new JwtSecurityTokenHandler().WriteToken(token);

        _logger.LogInformation(
            "Generated JWT token for client {ClientId} with process types [{ProcessTypes}], expires in {ExpirationMinutes} minutes",
            clientId,
            string.Join(", ", processTypes),
            expirationMinutes);

        return tokenString;
    }
}
