namespace StarGate.Security.Tests;

using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using System.Net;
using Xunit;

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
        request.Headers.Add("Origin", "https://app.example.com");
        request.Headers.Add("Access-Control-Request-Method", "POST");
        request.Headers.Add("Access-Control-Request-Headers", "authorization");

        // Act
        var response = await Client.SendAsync(request);

        // Assert
        response.Headers.Should().ContainKey("Access-Control-Allow-Origin");
    }

    [Fact]
    public async Task Preflight_Should_RejectUnauthorizedOrigins()
    {
        // Arrange
        var request = new HttpRequestMessage(HttpMethod.Options, "/api/processes");
        request.Headers.Add("Origin", "https://malicious-site.com");
        request.Headers.Add("Access-Control-Request-Method", "POST");

        // Act
        var response = await Client.SendAsync(request);

        // Assert - Should not include CORS headers for unauthorized origin
        if (response.Headers.Contains("Access-Control-Allow-Origin"))
        {
            var allowedOrigin = response.Headers.GetValues("Access-Control-Allow-Origin").First();
            allowedOrigin.Should().NotBe("https://malicious-site.com");
        }
    }

    [Fact]
    public async Task CorsHeaders_Should_BePresent_InActualRequests()
    {
        // Arrange
        var request = new HttpRequestMessage(HttpMethod.Get, "/health/live");
        request.Headers.Add("Origin", "https://app.example.com");

        // Act
        var response = await Client.SendAsync(request);

        // Assert
        response.Headers.Should().ContainKey("Access-Control-Allow-Origin");
    }
}
