namespace StarGate.Security.Tests;

using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using System.Net;
using System.Net.Http.Json;
using Xunit;

public class AuthorizationSecurityTests : SecurityTestBase
{
    public AuthorizationSecurityTests(WebApplicationFactory<Program> factory)
        : base(factory)
    {
    }

    [Fact]
    public async Task Endpoint_Should_Return403_WhenMissingRequiredScope()
    {
        // Arrange
        var token = GenerateJwtToken(
            "test-client",
            scopes: new[] { "process.read" }); // Missing process.write scope

        var request = CreateAuthenticatedRequest(
            HttpMethod.Post,
            "/api/processes",
            token);
        request.Content = JsonContent.Create(new
        {
            clientId = "test-client",
            clientProcessId = "proc-123",
            type = "DataTransformation",
            priority = 5
        });

        // Act
        var response = await Client.SendAsync(request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Endpoint_Should_Return403_WhenAccessingOtherClientData()
    {
        // Arrange
        var token = GenerateJwtToken(
            "client-a",
            scopes: new[] { "process.read" });

        var request = CreateAuthenticatedRequest(
            HttpMethod.Get,
            "/api/processes/client/client-b/proc-123", // Trying to access client-b's data
            token);

        // Act
        var response = await Client.SendAsync(request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task AdminUser_Should_AccessAnyClientData()
    {
        // Arrange
        var adminToken = GenerateJwtToken(
            "admin-client",
            roles: new[] { "admin" },
            scopes: new[] { "process.read", "admin" });

        var request = CreateAuthenticatedRequest(
            HttpMethod.Get,
            "/api/processes/client/any-client/proc-123",
            adminToken);

        // Act
        var response = await Client.SendAsync(request);

        // Assert
        // Should not be 403 Forbidden (may be 404 if process doesn't exist)
        response.StatusCode.Should().NotBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Endpoint_Should_ValidateClientId_InRequestBody()
    {
        // Arrange
        var token = GenerateJwtToken(
            "client-a",
            scopes: new[] { "process.write" });

        var request = CreateAuthenticatedRequest(
            HttpMethod.Post,
            "/api/processes",
            token);
        request.Content = JsonContent.Create(new
        {
            clientId = "client-b", // Different from token's client_id
            clientProcessId = "proc-123",
            type = "DataTransformation",
            priority = 5
        });

        // Act
        var response = await Client.SendAsync(request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }
}
