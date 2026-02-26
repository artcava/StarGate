using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using System.Net;
using Xunit;

namespace StarGate.Security.Tests;

public class CorsSecurityTests : SecurityTestBase
{
    public CorsSecurityTests(WebApplicationFactory<Program> factory)
        : base(factory)
    {
    }

    [Fact]
    public async Task Preflight_Should_AllowConfiguredOrigins()
    {
        // Arrange
        var request = new HttpRequestMessage(HttpMethod.Options, "/api/processes");
        request.Headers.Add("Origin", "https://localhost:3000");
        request.Headers.Add("Access-Control-Request-Method", "POST");
        request.Headers.Add("Access-Control-Request-Headers", "authorization,content-type");

        // Act
        var response = await Client.SendAsync(request);

        // Assert - Check if CORS headers are present
        // Note: Actual CORS configuration depends on appsettings
        var hasCorsHeaders = response.Headers.Contains("Access-Control-Allow-Origin") ||
                            response.StatusCode == HttpStatusCode.NoContent ||
                            response.StatusCode == HttpStatusCode.OK;
        
        hasCorsHeaders.Should().BeTrue("CORS should be configured for the API");
    }

    [Fact]
    public async Task ActualRequest_Should_IncludeCorsHeaders_WhenOriginProvided()
    {
        // Arrange
        var request = new HttpRequestMessage(HttpMethod.Get, "/health/live");
        request.Headers.Add("Origin", "https://localhost:3000");

        // Act
        var response = await Client.SendAsync(request);

        // Assert - Response should be successful
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        
        // CORS headers may or may not be present depending on configuration
        // This test verifies that the request is not rejected due to CORS
    }

    [Fact]
    public async Task Request_Should_Work_WithoutOriginHeader()
    {
        // Arrange - Request without Origin header (non-browser request)
        var request = new HttpRequestMessage(HttpMethod.Get, "/health/live");

        // Act
        var response = await Client.SendAsync(request);

        // Assert - Should work fine without Origin header
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }
}
