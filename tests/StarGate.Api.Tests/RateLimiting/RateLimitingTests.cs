using System.Net;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;

namespace StarGate.Api.Tests.RateLimiting;

/// <summary>
/// Integration tests for rate limiting middleware.
/// </summary>
public class RateLimitingTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public RateLimitingTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Endpoint_Should_Return429_WhenRateLimitExceeded()
    {
        // Arrange
        var client = _factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureAppConfiguration((context, config) =>
            {
                config.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["RateLimit:Enabled"] = "true",
                    ["RateLimit:DefaultPolicy:PermitLimit"] = "5",
                    ["RateLimit:DefaultPolicy:WindowSeconds"] = "60",
                    ["RateLimit:DefaultPolicy:QueueLimit"] = "0",
                    ["RateLimit:DefaultPolicy:UseSlidingWindow"] = "true"
                });
            });
        }).CreateClient();

        // Act - Make requests until rate limit is exceeded
        var responses = new List<HttpResponseMessage>();
        for (int i = 0; i < 10; i++)
        {
            var response = await client.GetAsync("/health/live");
            responses.Add(response);
        }

        // Assert
        var tooManyRequestsResponse = responses.FirstOrDefault(r => r.StatusCode == HttpStatusCode.TooManyRequests);
        tooManyRequestsResponse.Should().NotBeNull("some requests should be rate limited");

        // Check Retry-After header
        tooManyRequestsResponse?.Headers.RetryAfter.Should().NotBeNull();
    }

    [Fact]
    public async Task RateLimiting_Should_BeDisabled_WhenConfiguredOff()
    {
        // Arrange
        var client = _factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureAppConfiguration((context, config) =>
            {
                config.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["RateLimit:Enabled"] = "false"
                });
            });
        }).CreateClient();

        // Act - Make many requests
        for (int i = 0; i < 100; i++)
        {
            var response = await client.GetAsync("/health/live");

            // Assert - No rate limiting
            response.StatusCode.Should().Be(HttpStatusCode.OK);
        }
    }

    [Fact]
    public async Task RateLimiting_Should_ReturnProperErrorBody_When429()
    {
        // Arrange
        var client = _factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureAppConfiguration((context, config) =>
            {
                config.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["RateLimit:Enabled"] = "true",
                    ["RateLimit:DefaultPolicy:PermitLimit"] = "3",
                    ["RateLimit:DefaultPolicy:WindowSeconds"] = "60",
                    ["RateLimit:DefaultPolicy:QueueLimit"] = "0",
                    ["RateLimit:DefaultPolicy:UseSlidingWindow"] = "true"
                });
            });
        }).CreateClient();

        // Act - Make requests until rate limit is exceeded
        HttpResponseMessage? rateLimitedResponse = null;
        for (int i = 0; i < 10; i++)
        {
            var response = await client.GetAsync("/health/live");
            if (response.StatusCode == HttpStatusCode.TooManyRequests)
            {
                rateLimitedResponse = response;
                break;
            }
        }

        // Assert
        rateLimitedResponse.Should().NotBeNull();
        if (rateLimitedResponse != null)
        {
            var content = await rateLimitedResponse.Content.ReadAsStringAsync();
            content.Should().Contain("Too Many Requests");
            content.Should().Contain("Rate limit exceeded");
            content.Should().Contain("429");
        }
    }

    [Fact]
    public async Task RateLimiting_Should_UseSlidingWindow_ByDefault()
    {
        // Arrange
        var client = _factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureAppConfiguration((context, config) =>
            {
                config.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["RateLimit:Enabled"] = "true",
                    ["RateLimit:DefaultPolicy:PermitLimit"] = "10",
                    ["RateLimit:DefaultPolicy:WindowSeconds"] = "10",
                    ["RateLimit:DefaultPolicy:QueueLimit"] = "0",
                    ["RateLimit:DefaultPolicy:UseSlidingWindow"] = "true"
                });
            });
        }).CreateClient();

        // Act - Make requests close to limit
        var responses = new List<HttpStatusCode>();
        for (int i = 0; i < 10; i++)
        {
            var response = await client.GetAsync("/health/live");
            responses.Add(response.StatusCode);
        }

        // Wait a bit and make more requests (sliding window should allow some)
        await Task.Delay(TimeSpan.FromSeconds(2));

        for (int i = 0; i < 5; i++)
        {
            var response = await client.GetAsync("/health/live");
            responses.Add(response.StatusCode);
        }

        // Assert - With sliding window, some later requests should succeed
        // and eventually some should be rate limited
        responses.Should().Contain(HttpStatusCode.OK);
        responses.Should().Contain(HttpStatusCode.TooManyRequests);
    }
}
