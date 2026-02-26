namespace StarGate.Security.Tests;

using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using System.Net;
using Xunit;

public class RateLimitingSecurityTests : SecurityTestBase
{
    public RateLimitingSecurityTests(WebApplicationFactory<Program> factory)
        : base(factory)
    {
    }

    [Fact]
    public async Task Endpoint_Should_Return429_WhenRateLimitExceeded()
    {
        // Arrange
        var token = GenerateJwtToken(
            "test-client",
            scopes: new[] { "process.read" });

        var responses = new List<HttpResponseMessage>();

        // Act - Make many requests quickly
        for (int i = 0; i < 150; i++)
        {
            var request = CreateAuthenticatedRequest(
                HttpMethod.Get,
                "/health/live",
                token);
            var response = await Client.SendAsync(request);
            responses.Add(response);
        }

        // Assert
        var tooManyRequests = responses.Where(r => r.StatusCode == HttpStatusCode.TooManyRequests).ToList();
        tooManyRequests.Should().NotBeEmpty("some requests should be rate limited");
    }

    [Fact]
    public async Task RateLimitResponse_Should_IncludeRetryAfterHeader()
    {
        // Arrange
        var token = GenerateJwtToken(
            "test-client",
            scopes: new[] { "process.read" });

        // Act - Exceed rate limit
        HttpResponseMessage? rateLimitedResponse = null;
        for (int i = 0; i < 200; i++)
        {
            var request = CreateAuthenticatedRequest(
                HttpMethod.Get,
                "/health/live",
                token);
            var response = await Client.SendAsync(request);
            
            if (response.StatusCode == HttpStatusCode.TooManyRequests)
            {
                rateLimitedResponse = response;
                break;
            }
        }

        // Assert
        rateLimitedResponse.Should().NotBeNull();
        rateLimitedResponse!.Headers.Should().ContainKey("Retry-After");
    }

    [Fact]
    public async Task DifferentClients_Should_HaveIndependentRateLimits()
    {
        // Arrange
        var tokenA = GenerateJwtToken("client-a", scopes: new[] { "process.read" });
        var tokenB = GenerateJwtToken("client-b", scopes: new[] { "process.read" });

        // Act - Exhaust rate limit for client A
        for (int i = 0; i < 150; i++)
        {
            var request = CreateAuthenticatedRequest(HttpMethod.Get, "/health/live", tokenA);
            await Client.SendAsync(request);
        }

        // Try with client B
        var requestB = CreateAuthenticatedRequest(HttpMethod.Get, "/health/live", tokenB);
        var responseB = await Client.SendAsync(requestB);

        // Assert - Client B should not be rate limited
        responseB.StatusCode.Should().Be(HttpStatusCode.OK);
    }
}
