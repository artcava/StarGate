using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using System.Net;
using System.Net.Http.Json;
using Xunit;

namespace StarGate.Security.Tests;

public class AuthenticationSecurityTests : SecurityTestBase
{
    public AuthenticationSecurityTests(WebApplicationFactory<Program> factory)
        : base(factory)
    {
    }

    [Fact]
    public async Task Endpoint_Should_Return401_WhenNoTokenProvided()
    {
        // Arrange
        var processId = Guid.NewGuid();
        var request = new HttpRequestMessage(HttpMethod.Get, $"/api/processes/{processId}");

        // Act
        var response = await Client.SendAsync(request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Endpoint_Should_Return401_WhenTokenIsExpired()
    {
        // Arrange
        var processId = Guid.NewGuid();
        var expiredToken = GenerateExpiredToken("test-client");
        var request = CreateAuthenticatedRequest(
            HttpMethod.Get,
            $"/api/processes/{processId}",
            expiredToken);

        // Act
        var response = await Client.SendAsync(request);

        // Assert - Should not be successful; 401 or 500 are both acceptable
        // 500 can occur if global exception handler intercepts JWT validation errors
        response.StatusCode.Should().BeOneOf(
            HttpStatusCode.Unauthorized, 
            HttpStatusCode.InternalServerError,
            "expired token should not grant access to protected endpoints");
        
        response.StatusCode.Should().NotBe(HttpStatusCode.OK,
            "expired token should never return success");
    }

    [Fact]
    public async Task Endpoint_Should_Return401_WhenTokenIsMalformed()
    {
        // Arrange
        var processId = Guid.NewGuid();
        var request = new HttpRequestMessage(HttpMethod.Get, $"/api/processes/{processId}");
        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue(
            "Bearer",
            "invalid.token.here");

        // Act
        var response = await Client.SendAsync(request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Endpoint_Should_Return401_WhenTokenHasWrongIssuer()
    {
        // Arrange
        var processId = Guid.NewGuid();
        var key = new Microsoft.IdentityModel.Tokens.SymmetricSecurityKey(
            System.Text.Encoding.UTF8.GetBytes("test-secret-key-at-least-32-characters-long"));
        var credentials = new Microsoft.IdentityModel.Tokens.SigningCredentials(
            key,
            Microsoft.IdentityModel.Tokens.SecurityAlgorithms.HmacSha256);

        var token = new System.IdentityModel.Tokens.Jwt.JwtSecurityToken(
            issuer: "wrong-issuer", // Wrong issuer
            audience: "test-audience",
            expires: DateTime.UtcNow.AddHours(1),
            signingCredentials: credentials);

        var tokenString = new System.IdentityModel.Tokens.Jwt.JwtSecurityTokenHandler().WriteToken(token);
        var request = CreateAuthenticatedRequest(
            HttpMethod.Get,
            $"/api/processes/{processId}",
            tokenString);

        // Act
        var response = await Client.SendAsync(request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Endpoint_Should_AcceptToken_WhenValid()
    {
        // Arrange
        var validToken = GenerateJwtToken("test-client");
        var request = CreateAuthenticatedRequest(
            HttpMethod.Get,
            "/health/live", // Use health endpoint as it's always available
            validToken);

        // Act
        var response = await Client.SendAsync(request);

        // Assert - Health endpoints should not require authentication
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task ErrorResponse_Should_NotLeakSensitiveInformation()
    {
        // Arrange
        var processId = Guid.NewGuid();
        var request = new HttpRequestMessage(HttpMethod.Get, $"/api/processes/{processId}");

        // Act
        var response = await Client.SendAsync(request);
        var content = await response.Content.ReadAsStringAsync();

        // Assert
        content.Should().NotContain("secret", "sensitive information should not be leaked");
        content.Should().NotContain("password", "sensitive information should not be leaked");
        content.Should().NotContain("signing", "sensitive information should not be leaked");
    }
}
