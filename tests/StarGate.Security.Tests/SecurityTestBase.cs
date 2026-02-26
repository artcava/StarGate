using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace StarGate.Security.Tests;

/// <summary>
/// Base class for security tests providing JWT token generation and common utilities.
/// </summary>
public abstract class SecurityTestBase : IClassFixture<WebApplicationFactory<Program>>
{
    protected WebApplicationFactory<Program> Factory { get; }
    protected HttpClient Client { get; }

    protected SecurityTestBase(WebApplicationFactory<Program> factory)
    {
        // Configure test factory with in-memory configuration
        Factory = factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureAppConfiguration((context, config) =>
            {
                config.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Authentication:Schemes:Bearer:ValidIssuer"] = "test-issuer",
                    ["Authentication:Schemes:Bearer:ValidAudience"] = "test-audience",
                    ["Authentication:Schemes:Bearer:SigningKey"] = "test-secret-key-at-least-32-characters-long",
                    ["RateLimiting:GlobalFixedWindow:PermitLimit"] = "100",
                    ["RateLimiting:GlobalFixedWindow:Window"] = "00:01:00"
                });
            });
        });
        
        Client = Factory.CreateClient();
    }

    protected string GenerateJwtToken(
        string clientId,
        string[]? roles = null,
        string[]? scopes = null,
        TimeSpan? expiration = null)
    {
        var claims = new List<Claim>
        {
            new Claim("client_id", clientId),
            new Claim(JwtRegisteredClaimNames.Sub, clientId),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        };

        if (roles != null)
        {
            foreach (var role in roles)
            {
                claims.Add(new Claim(ClaimTypes.Role, role));
            }
        }

        if (scopes != null)
        {
            claims.Add(new Claim("scope", string.Join(" ", scopes)));
        }

        var key = new SymmetricSecurityKey(
            Encoding.UTF8.GetBytes("test-secret-key-at-least-32-characters-long"));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: "test-issuer",
            audience: "test-audience",
            claims: claims,
            expires: DateTime.UtcNow.Add(expiration ?? TimeSpan.FromHours(1)),
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    protected string GenerateExpiredToken(string clientId)
    {
        return GenerateJwtToken(clientId, expiration: TimeSpan.FromSeconds(-1));
    }

    protected HttpRequestMessage CreateAuthenticatedRequest(
        HttpMethod method,
        string uri,
        string token)
    {
        var request = new HttpRequestMessage(method, uri);
        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
        return request;
    }
}
