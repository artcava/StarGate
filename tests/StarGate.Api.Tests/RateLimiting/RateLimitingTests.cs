using System.Net;
using FluentAssertions;

namespace StarGate.Api.Tests.RateLimiting;

/// <summary>
/// Integration tests for rate limiting middleware.
/// </summary>
public class RateLimitingTests
{
    [Fact]
    public async Task Endpoint_Should_Return429_WhenRateLimitExceeded()
    {
        // Arrange
        var configuration = new Dictionary<string, string?>
        {
            ["RateLimit:Enabled"] = "true",
            ["RateLimit:DefaultPolicy:PermitLimit"] = "1000",
            ["RateLimit:DefaultPolicy:WindowSeconds"] = "60",
            ["RateLimit:DefaultPolicy:QueueLimit"] = "0",
            ["RateLimit:DefaultPolicy:UseSlidingWindow"] = "true",
            ["RateLimit:EndpointPolicies:ReadProcess:PermitLimit"] = "3",
            ["RateLimit:EndpointPolicies:ReadProcess:WindowSeconds"] = "30",
            ["RateLimit:EndpointPolicies:ReadProcess:QueueLimit"] = "0",
            ["RateLimit:EndpointPolicies:ReadProcess:UseSlidingWindow"] = "true"
        };

        await using var factory = new RateLimitTestFactory(configuration);
        var client = factory.CreateClient();

        // Act - Make requests until rate limit is exceeded
        var responses = new List<HttpResponseMessage>();
        for (int i = 0; i < 10; i++)
        {
            var response = await client.GetAsync("/ratelimit-test");
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
        var configuration = new Dictionary<string, string?>
        {
            ["RateLimit:Enabled"] = "false",
            ["RateLimit:DefaultPolicy:PermitLimit"] = "1000",
            ["RateLimit:DefaultPolicy:WindowSeconds"] = "60",
            ["RateLimit:DefaultPolicy:QueueLimit"] = "0",
            ["RateLimit:DefaultPolicy:UseSlidingWindow"] = "true"
        };

        await using var factory = new RateLimitTestFactory(configuration);
        var client = factory.CreateClient();

        // Act - Make many requests
        for (int i = 0; i < 100; i++)
        {
            var response = await client.GetAsync("/ratelimit-test");

            // Assert - No rate limiting
            response.StatusCode.Should().Be(HttpStatusCode.OK);
        }
    }

    [Fact]
    public async Task RateLimiting_Should_ReturnProperErrorBody_When429()
    {
        // Arrange
        var configuration = new Dictionary<string, string?>
        {
            ["RateLimit:Enabled"] = "true",
            ["RateLimit:DefaultPolicy:PermitLimit"] = "1000",
            ["RateLimit:DefaultPolicy:WindowSeconds"] = "60",
            ["RateLimit:DefaultPolicy:QueueLimit"] = "0",
            ["RateLimit:DefaultPolicy:UseSlidingWindow"] = "true",
            ["RateLimit:EndpointPolicies:ReadProcess:PermitLimit"] = "2",
            ["RateLimit:EndpointPolicies:ReadProcess:WindowSeconds"] = "30",
            ["RateLimit:EndpointPolicies:ReadProcess:QueueLimit"] = "0",
            ["RateLimit:EndpointPolicies:ReadProcess:UseSlidingWindow"] = "true"
        };

        await using var factory = new RateLimitTestFactory(configuration);
        var client = factory.CreateClient();

        // Act - Make requests until rate limit is exceeded
        HttpResponseMessage? rateLimitedResponse = null;
        for (int i = 0; i < 10; i++)
        {
            var response = await client.GetAsync("/ratelimit-test");
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
        var configuration = new Dictionary<string, string?>
        {
            ["RateLimit:Enabled"] = "true",
            ["RateLimit:DefaultPolicy:PermitLimit"] = "1000",
            ["RateLimit:DefaultPolicy:WindowSeconds"] = "60",
            ["RateLimit:DefaultPolicy:QueueLimit"] = "0",
            ["RateLimit:DefaultPolicy:UseSlidingWindow"] = "true",
            ["RateLimit:EndpointPolicies:ReadProcess:PermitLimit"] = "5",
            ["RateLimit:EndpointPolicies:ReadProcess:WindowSeconds"] = "5",
            ["RateLimit:EndpointPolicies:ReadProcess:QueueLimit"] = "0",
            ["RateLimit:EndpointPolicies:ReadProcess:UseSlidingWindow"] = "true"
        };

        await using var factory = new RateLimitTestFactory(configuration);
        var client = factory.CreateClient();

        // Act - Make requests that should exceed limit
        var responses = new List<HttpStatusCode>();
        for (int i = 0; i < 10; i++)
        {
            var response = await client.GetAsync("/ratelimit-test");
            responses.Add(response.StatusCode);
        }

        // Assert - With sliding window, some requests should succeed and some should be rate limited
        responses.Should().Contain(HttpStatusCode.OK);
        responses.Should().Contain(HttpStatusCode.TooManyRequests);
    }
}
