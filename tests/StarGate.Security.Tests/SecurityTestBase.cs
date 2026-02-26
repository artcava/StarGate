using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
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
    
    private const string TestSecretKey = "test-secret-key-at-least-32-characters-long-for-jwt-signing";
    private const string TestIssuer = "test-issuer";
    private const string TestAudience = "test-audience";

    protected SecurityTestBase(WebApplicationFactory<Program> factory)
    {
        // Configure test factory with proper JWT settings
        Factory = factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureAppConfiguration((context, config) =>
            {
                // Add in-memory configuration that matches JwtOptions structure
                // JwtOptions.SectionName = "Jwt" (not "Authentication:Jwt")
                config.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    // JWT Configuration
                    ["Jwt:Issuer"] = TestIssuer,
                    ["Jwt:Audience"] = TestAudience,
                    ["Jwt:SecretKey"] = TestSecretKey,
                    ["Jwt:ValidateLifetime"] = "true",
                    ["Jwt:RequireHttpsMetadata"] = "false",
                    ["Jwt:ClockSkew"] = "00:00:30",
                    
                    // Rate Limiting Configuration (relaxed for tests)
                    ["RateLimiting:GlobalFixedWindow:PermitLimit"] = "1000",
                    ["RateLimiting:GlobalFixedWindow:Window"] = "00:01:00",
                    ["RateLimiting:CreateProcess:PermitLimit"] = "100",
                    ["RateLimiting:CreateProcess:Window"] = "00:01:00",
                    ["RateLimiting:ReadProcess:PermitLimit"] = "200",
                    ["RateLimiting:ReadProcess:Window"] = "00:01:00",
                    
                    // CORS Configuration (permissive for tests)
                    ["Cors:AllowedOrigins:0"] = "https://localhost:3000",
                    ["Cors:AllowedOrigins:1"] = "http://localhost:3000",
                    ["Cors:AllowCredentials"] = "true"
                });
            });
            
            builder.UseEnvironment("Test");
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

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(TestSecretKey));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: TestIssuer,
            audience: TestAudience,
            claims: claims,
            expires: DateTime.UtcNow.Add(expiration ?? TimeSpan.FromHours(1)),
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    protected string GenerateExpiredToken(string clientId)
    {
        return GenerateJwtToken(clientId, expiration: TimeSpan.FromSeconds(-10));
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
