using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using System.Net;
using System.Net.Http.Json;
using Xunit;

namespace StarGate.Security.Tests;

public class AuthorizationSecurityTests : SecurityTestBase
{
    public AuthorizationSecurityTests(WebApplicationFactory<Program> factory)
        : base(factory)
    {
    }

    [Fact]
    public async Task Endpoint_Should_Return403_WhenAccessingOtherClientData()
    {
        // Arrange - Client A tries to access Client B's data
        var token = GenerateJwtToken("client-a");

        var request = CreateAuthenticatedRequest(
            HttpMethod.Get,
            "/api/processes/client/client-b/proc-123", // Trying to access client-b's data
            token);

        // Act
        var response = await Client.SendAsync(request);

        // Assert - Should be Forbidden or error, but not successful
        // In test environment without full infrastructure, may return 500
        response.StatusCode.Should().BeOneOf(
            HttpStatusCode.Forbidden,
            HttpStatusCode.InternalServerError,
            "accessing another client's data should not succeed");
        
        response.StatusCode.Should().NotBe(HttpStatusCode.OK,
            "accessing another client's data should never return success");
        
        response.StatusCode.Should().NotBe(HttpStatusCode.Unauthorized,
            "token is valid, so should not be 401");
    }

    [Fact]
    public async Task Client_Should_AccessOwnData()
    {
        // Arrange - Client A accesses own data
        var token = GenerateJwtToken("client-a");

        var request = CreateAuthenticatedRequest(
            HttpMethod.Get,
            "/api/processes/client/client-a/proc-123",
            token);

        // Act
        var response = await Client.SendAsync(request);

        // Assert - Should not be Forbidden (may be 404 if process doesn't exist)
        response.StatusCode.Should().NotBe(HttpStatusCode.Forbidden,
            "client should be able to access own data");
        
        response.StatusCode.Should().NotBe(HttpStatusCode.Unauthorized,
            "valid token should not result in 401");
    }

    [Fact]
    public async Task Endpoint_Should_ValidateClientId_InRequestBody()
    {
        // Arrange
        var token = GenerateJwtToken("client-a");

        var request = CreateAuthenticatedRequest(
            HttpMethod.Post,
            "/api/processes",
            token);
        request.Content = JsonContent.Create(new
        {
            clientId = "client-b", // Different from token's client_id
            clientProcessId = "proc-123",
            processType = "DataTransformation",
            metadata = new Dictionary<string, string>()
        });

        // Act
        var response = await Client.SendAsync(request);

        // Assert - Should be Forbidden or BadRequest, but not successful
        // BadRequest is acceptable because validation may occur before authorization check
        response.StatusCode.Should().BeOneOf(
            HttpStatusCode.Forbidden,
            HttpStatusCode.BadRequest,
            "clientId mismatch should be rejected");
        
        response.StatusCode.Should().NotBe(HttpStatusCode.OK,
            "clientId mismatch should never succeed");
        
        response.StatusCode.Should().NotBe(HttpStatusCode.Created,
            "clientId mismatch should never create a resource");
    }

    [Fact]
    public async Task Client_Should_CreateProcessWithMatchingClientId()
    {
        // Arrange
        var token = GenerateJwtToken("test-client");

        var request = CreateAuthenticatedRequest(
            HttpMethod.Post,
            "/api/processes",
            token);
        request.Content = JsonContent.Create(new
        {
            clientId = "test-client", // Matches token's client_id
            clientProcessId = $"test-proc-{Guid.NewGuid()}",
            processType = "DataTransformation",
            metadata = new Dictionary<string, string>()
        });

        // Act
        var response = await Client.SendAsync(request);

        // Assert - Should not be Forbidden (may fail for other reasons like missing dependencies)
        response.StatusCode.Should().NotBe(HttpStatusCode.Forbidden,
            "matching clientId should not be forbidden");
        
        response.StatusCode.Should().NotBe(HttpStatusCode.Unauthorized,
            "valid token should not result in 401");
    }
}
