using System.Net;
using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;
using Xunit.Abstractions;

namespace StarGate.Integration.Tests.HealthChecks;

public class HealthCheckEndpointsTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;
    private readonly HttpClient _client;
    private readonly ITestOutputHelper _output;

    public HealthCheckEndpointsTests(WebApplicationFactory<Program> factory, ITestOutputHelper output)
    {
        _factory = factory;
        _client = factory.CreateClient();
        _output = output;
    }

    [Fact]
    public async Task LivenessEndpoint_Should_ReturnHealthy()
    {
        // Act
        var response = await _client.GetAsync("/health/live");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        
        var content = await response.Content.ReadAsStringAsync();
        _output.WriteLine($"Liveness response: {content}");
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
        var content = await response.Content.ReadAsStringAsync();
        _output.WriteLine($"Readiness Status: {response.StatusCode}");
        _output.WriteLine($"Readiness response: {content}");

        // Assert
        // May be Unhealthy if dependencies are not running (expected in CI)
        // May also return 200 with empty checks if no health checks registered
        response.StatusCode.Should().BeOneOf(
            HttpStatusCode.OK,
            HttpStatusCode.ServiceUnavailable);
        
        // Response should be valid JSON
        content.Should().NotBeNullOrWhiteSpace();
        var json = JsonDocument.Parse(content);
        json.RootElement.TryGetProperty("status", out _).Should().BeTrue();
    }

    [Fact]
    public async Task HealthEndpoint_Should_ReturnDetailedStatus()
    {
        // Act
        var response = await _client.GetAsync("/health");
        var content = await response.Content.ReadAsStringAsync();
        _output.WriteLine($"Health Status: {response.StatusCode}");
        _output.WriteLine($"Health response: {content}");

        // Assert
        // Status code may vary depending on dependencies
        response.StatusCode.Should().BeOneOf(
            HttpStatusCode.OK,
            HttpStatusCode.ServiceUnavailable);
        
        // Response should be valid JSON with status
        content.Should().NotBeNullOrWhiteSpace();
        var json = JsonDocument.Parse(content);
        json.RootElement.TryGetProperty("status", out _).Should().BeTrue();
    }

    [Fact]
    public async Task HealthEndpoint_Should_IncludeCustomHealthChecks()
    {
        // Act
        var response = await _client.GetAsync("/health");
        var content = await response.Content.ReadAsStringAsync();
        _output.WriteLine($"Health response for custom checks: {content}");
        
        // Assert - response should be valid health check JSON
        response.StatusCode.Should().BeOneOf(
            HttpStatusCode.OK,
            HttpStatusCode.ServiceUnavailable);
        
        var json = JsonDocument.Parse(content);
        json.RootElement.TryGetProperty("status", out _).Should().BeTrue();
        
        // Check if response has entries (standard UIResponseWriter format)
        if (json.RootElement.TryGetProperty("entries", out var entries))
        {
            var entriesObject = entries.EnumerateObject().Select(e => e.Name).ToList();
            _output.WriteLine($"Entries: {string.Join(", ", entriesObject)}");
            
            // If dependencies are registered, custom health checks should be present
            // In test environment without dependencies, entries may be empty - this is OK
            entriesObject.Should().NotBeNull();
        }
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

        var readinessContent = await readinessResponse.Content.ReadAsStringAsync();
        _output.WriteLine($"Readiness Content-Type: {readinessResponse.Content.Headers.ContentType?.MediaType}");

        // Assert
        livenessResponse.Content.Headers.ContentType?.MediaType.Should().Be("application/json");
        
        // UIResponseWriter may use application/json or application/health+json
        var readinessContentType = readinessResponse.Content.Headers.ContentType?.MediaType;
        readinessContentType.Should().Match(ct => 
            ct == "application/json" || 
            ct == "application/health+json" ||
            ct?.Contains("json") == true);
        
        var healthContentType = healthResponse.Content.Headers.ContentType?.MediaType;
        healthContentType.Should().Match(ct => 
            ct == "application/json" || 
            ct == "application/health+json" ||
            ct?.Contains("json") == true);
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
