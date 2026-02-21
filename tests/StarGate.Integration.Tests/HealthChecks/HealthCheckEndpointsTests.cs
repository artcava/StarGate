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
        response.StatusCode.Should().BeOneOf(
            HttpStatusCode.OK,
            HttpStatusCode.ServiceUnavailable);
        
        content.Should().Contain("status");
        
        // If we get a response, it should have the expected structure
        if (!string.IsNullOrWhiteSpace(content))
        {
            var json = JsonDocument.Parse(content);
            json.RootElement.TryGetProperty("status", out _).Should().BeTrue();
            // Note: entries structure may vary based on UIResponseWriter format
        }
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
        
        content.Should().Contain("status");
        
        // Verify JSON structure
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
        
        // Assert
        var json = JsonDocument.Parse(content);
        
        // The UIResponseWriter format uses "entries" for health check results
        // Log the actual structure to understand the format
        _output.WriteLine($"Root properties: {string.Join(", ", json.RootElement.EnumerateObject().Select(p => p.Name))}");
        
        // Check if response has entries (standard format) or results (alternative format)
        var hasEntries = json.RootElement.TryGetProperty("entries", out var entries);
        var hasResults = json.RootElement.TryGetProperty("results", out var results);
        
        if (hasEntries)
        {
            var entriesObject = entries.EnumerateObject().Select(e => e.Name).ToList();
            _output.WriteLine($"Entries: {string.Join(", ", entriesObject)}");
            
            // Should include our custom health checks
            entriesObject.Should().Contain("process-service");
            entriesObject.Should().Contain("policy-provider");
        }
        else if (hasResults)
        {
            var resultsArray = results.EnumerateObject().Select(e => e.Name).ToList();
            _output.WriteLine($"Results: {string.Join(", ", resultsArray)}");
            
            resultsArray.Should().Contain("process-service");
            resultsArray.Should().Contain("policy-provider");
        }
        else
        {
            // Fail with helpful message
            Assert.Fail($"Response does not contain 'entries' or 'results' property. Actual content: {content}");
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
        _output.WriteLine($"Readiness content: {readinessContent}");

        // Assert
        livenessResponse.Content.Headers.ContentType?.MediaType.Should().Be("application/json");
        
        // UIResponseWriter uses application/json
        var readinessContentType = readinessResponse.Content.Headers.ContentType?.MediaType;
        readinessContentType.Should().Match(ct => ct == "application/json" || ct == "application/health+json");
        
        var healthContentType = healthResponse.Content.Headers.ContentType?.MediaType;
        healthContentType.Should().Match(ct => ct == "application/json" || ct == "application/health+json");
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
