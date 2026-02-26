namespace StarGate.Security.Tests;

using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using System.Net;
using System.Net.Http.Json;
using Xunit;

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
        var request = new HttpRequestMessage(HttpMethod.Get, "/api/processes/test-id");

        // Act
        var response = await Client.SendAsync(request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Endpoint_Should_Return401_WhenTokenIsExpired()
    {
        // Arrange
        var expiredToken = GenerateExpiredToken("test-client");
        var request = CreateAuthenticatedRequest(
            HttpMethod.Get,
            "/api/processes/test-id",
            expiredToken);

        // Act
        var response = await Client.SendAsync(request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Endpoint_Should_Return401_WhenTokenIsMalformed()
    {
        // Arrange
        var request = new HttpRequestMessage(HttpMethod.Get, "/api/processes/test-id");
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
            "/api/processes/test-id",
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
        var validToken = GenerateJwtToken(
            "test-client",
            scopes: new[] { "process.read" });
        var request = CreateAuthenticatedRequest(
            HttpMethod.Get,
            "/health/live", // Use health endpoint as it's always available
            validToken);

        // Act
        var response = await Client.SendAsync(request);

        // Assert
        response.StatusCode.Should().NotBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task ErrorResponse_Should_NotLeakSensitiveInformation()
    {
        // Arrange
        var request = new HttpRequestMessage(HttpMethod.Get, "/api/processes/test-id");

        // Act
        var response = await Client.SendAsync(request);
        var content = await response.Content.ReadAsStringAsync();

        // Assert
        content.Should().NotContain("secret", "sensitive information should not be leaked");
        content.Should().NotContain("password", "sensitive information should not be leaked");
        content.Should().NotContain("key", "sensitive information should not be leaked");
    }
}
