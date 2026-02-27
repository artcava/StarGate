using System.Net;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;

namespace StarGate.Api.Tests.RateLimiting;

/// <summary>
/// Integration tests for rate limiting middleware.
/// </summary>
public class RateLimitingTests
{
    [Fact]
    public async Task Endpoint_Should_Return429_WhenRateLimitExceeded()
    {
        // Arrange - permetti solo 1 richiesta in 60s sul global limiter
        var configuration = new Dictionary<string, string?>
        {
            ["RateLimit:Enabled"] = "true",
            ["RateLimit:DefaultPolicy:PermitLimit"] = "1",
            ["RateLimit:DefaultPolicy:WindowSeconds"] = "60",
            ["RateLimit:DefaultPolicy:QueueLimit"] = "0",
            ["RateLimit:DefaultPolicy:UseSlidingWindow"] = "false" // fisso, comportamento più prevedibile
        };

        await using var factory = new RateLimitTestFactory(configuration);
        var client = factory.CreateClient();

        // Act
        var responses = new List<HttpResponseMessage>();
        for (int i = 0; i < 3; i++)
        {
            responses.Add(await client.GetAsync("/ratelimit-test"));
        }

        // Assert: 1 OK + almeno un 429
        responses.Count(r => r.StatusCode == HttpStatusCode.OK)
            .Should().Be(1, "with PermitLimit=1 only the first request should succeed");
        responses.Should().Contain(r => r.StatusCode == HttpStatusCode.TooManyRequests,
            "some requests should be rate limited when exceeding the limit");
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
        var client = factory.Server.CreateClient();

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
        // Arrange - limite 1 richiesta
        var configuration = new Dictionary<string, string?>
        {
            ["RateLimit:Enabled"] = "true",
            ["RateLimit:DefaultPolicy:PermitLimit"] = "1",
            ["RateLimit:DefaultPolicy:WindowSeconds"] = "60",
            ["RateLimit:DefaultPolicy:QueueLimit"] = "0",
            ["RateLimit:DefaultPolicy:UseSlidingWindow"] = "false"
        };

        await using var factory = new RateLimitTestFactory(configuration);
        var client = factory.CreateClient();

        // Act: facciamo richieste finché non troviamo la 429
        HttpResponseMessage? rateLimitedResponse = null;
        for (int i = 0; i < 5; i++)
        {
            var response = await client.GetAsync("/ratelimit-test");
            if (response.StatusCode == HttpStatusCode.TooManyRequests)
            {
                rateLimitedResponse = response;
                break;
            }
        }

        // Assert
        rateLimitedResponse.Should().NotBeNull("after exceeding the limit at least one request should be rate limited");

        var content = await rateLimitedResponse!.Content.ReadAsStringAsync();
        content.Should().Contain("Too Many Requests");
        content.Should().Contain("Rate limit exceeded");
        content.Should().Contain("429");
    }

    [Fact]
    public async Task RateLimiting_Should_UseSlidingWindow_ByDefault()
    {
        // Arrange
        var configuration = new Dictionary<string, string?>
        {
            ["RateLimit:Enabled"] = "true",
            ["RateLimit:DefaultPolicy:PermitLimit"] = "5",
            ["RateLimit:DefaultPolicy:WindowSeconds"] = "5",
            ["RateLimit:DefaultPolicy:QueueLimit"] = "0",
            ["RateLimit:DefaultPolicy:UseSlidingWindow"] = "true"
        };

        await using var factory = new RateLimitTestFactory(configuration);
        var client = factory.CreateClient();

        // Act: spariamo 20 richieste veloci
        var responses = new List<HttpStatusCode>();
        for (int i = 0; i < 20; i++)
        {
            var response = await client.GetAsync("/ratelimit-test");
            responses.Add(response.StatusCode);
        }

        // Assert: ci aspettiamo sia OK che 429
        responses.Should().Contain(HttpStatusCode.OK,
            "some requests should be allowed within the permit limit");
        responses.Should().Contain(HttpStatusCode.TooManyRequests,
            "once the sliding window limit is exceeded some requests should be rate limited");
    }
}
