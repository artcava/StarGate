using System.Net;
using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace StarGate.Integration.Tests.HealthChecks;

public class HealthCheckEndpointsTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;
    private readonly HttpClient _client;

    public HealthCheckEndpointsTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task LivenessEndpoint_Should_ReturnHealthy()
    {
        // Act
        var response = await _client.GetAsync("/health/live");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        
        var content = await response.Content.ReadAsStringAsync();
        content.Should().Contain("Healthy");
        content.Should().Contain("timestamp");
        
        // Verify JSON structure
        var json = JsonDocument.Parse(content);
        json.RootElement.GetProperty("status").GetString().Should().Be("Healthy");
        json.RootElement.TryGetProperty("timestamp", out _).Should().BeTrue();
    }

    [Fact]
    public async Task ReadinessEndpoint_Should_CheckDependencies()
    {
        // Act
        var response = await _client.GetAsync("/health/ready");

        // Assert
        // May be Unhealthy if dependencies are not running (expected in CI)
        response.StatusCode.Should().BeOneOf(
            HttpStatusCode.OK,
            HttpStatusCode.ServiceUnavailable);
        
        var content = await response.Content.ReadAsStringAsync();
        content.Should().Contain("status");
        
        // If we get a response, it should have the expected structure
        if (!string.IsNullOrWhiteSpace(content))
        {
            var json = JsonDocument.Parse(content);
            json.RootElement.TryGetProperty("status", out _).Should().BeTrue();
            json.RootElement.TryGetProperty("entries", out _).Should().BeTrue();
        }
    }

    [Fact]
    public async Task HealthEndpoint_Should_ReturnDetailedStatus()
    {
        // Act
        var response = await _client.GetAsync("/health");

        // Assert
        // Status code may vary depending on dependencies
        response.StatusCode.Should().BeOneOf(
            HttpStatusCode.OK,
            HttpStatusCode.ServiceUnavailable);
        
        var content = await response.Content.ReadAsStringAsync();
        content.Should().Contain("entries");
        content.Should().Contain("status");
        
        // Verify JSON structure
        var json = JsonDocument.Parse(content);
        json.RootElement.TryGetProperty("status", out _).Should().BeTrue();
        json.RootElement.TryGetProperty("entries", out var entries).Should().BeTrue();
        
        // Should have at least the custom health checks
        var entriesObject = entries.EnumerateObject().ToList();
        entriesObject.Should().NotBeEmpty();
    }

    [Fact]
    public async Task HealthEndpoint_Should_IncludeCustomHealthChecks()
    {
        // Act
        var response = await _client.GetAsync("/health");
        var content = await response.Content.ReadAsStringAsync();
        
        // Assert
        var json = JsonDocument.Parse(content);
        json.RootElement.TryGetProperty("entries", out var entries).Should().BeTrue();
        
        var entriesObject = entries.EnumerateObject().Select(e => e.Name).ToList();
        
        // Should include our custom health checks
        entriesObject.Should().Contain("process-service");
        entriesObject.Should().Contain("policy-provider");
    }

    [Fact]
    public async Task HealthEndpoints_Should_BeAccessibleWithoutAuthentication()
    {
        // This test verifies that health endpoints don't require authentication
        // by ensuring they don't return 401 Unauthorized
        
        // Act
        var livenessResponse = await _client.GetAsync("/health/live");
        var readinessResponse = await _client.GetAsync("/health/ready");
        var healthResponse = await _client.GetAsync("/health");

        // Assert
        livenessResponse.StatusCode.Should().NotBe(HttpStatusCode.Unauthorized);
        readinessResponse.StatusCode.Should().NotBe(HttpStatusCode.Unauthorized);
        healthResponse.StatusCode.Should().NotBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task HealthEndpoints_Should_ReturnJsonContentType()
    {
        // Act
        var livenessResponse = await _client.GetAsync("/health/live");
        var readinessResponse = await _client.GetAsync("/health/ready");
        var healthResponse = await _client.GetAsync("/health");

        // Assert
        livenessResponse.Content.Headers.ContentType?.MediaType.Should().Be("application/json");
        readinessResponse.Content.Headers.ContentType?.MediaType.Should().Be("application/json");
        healthResponse.Content.Headers.ContentType?.MediaType.Should().Be("application/json");
    }

    [Fact]
    public async Task LivenessEndpoint_Should_BeFastAndNotCheckDependencies()
    {
        // Act
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        var response = await _client.GetAsync("/health/live");
        stopwatch.Stop();

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        
        // Liveness should be very fast since it doesn't check dependencies
        // Allow 1 second for network overhead and processing
        stopwatch.ElapsedMilliseconds.Should().BeLessThan(1000);
    }
}
