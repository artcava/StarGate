namespace StarGate.Security.Tests;

using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using System.Net;
using System.Net.Http.Json;
using Xunit;

public class InputValidationSecurityTests : SecurityTestBase
{
    public InputValidationSecurityTests(WebApplicationFactory<Program> factory)
        : base(factory)
    {
    }

    [Theory]
    [InlineData("<script>alert('xss')</script>")]
    [InlineData("'; DROP TABLE Processes; --")]
    [InlineData("../../../etc/passwd")]
    public async Task Endpoint_Should_RejectMaliciousInput(string maliciousInput)
    {
        // Arrange
        var token = GenerateJwtToken(
            "test-client",
            scopes: new[] { "process.write" });

        var request = CreateAuthenticatedRequest(
            HttpMethod.Post,
            "/api/processes",
            token);
        request.Content = JsonContent.Create(new
        {
            clientId = "test-client",
            clientProcessId = maliciousInput,
            type = "DataTransformation",
            priority = 5
        });

        // Act
        var response = await Client.SendAsync(request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Endpoint_Should_ValidateStringLength()
    {
        // Arrange
        var token = GenerateJwtToken(
            "test-client",
            scopes: new[] { "process.write" });

        var request = CreateAuthenticatedRequest(
            HttpMethod.Post,
            "/api/processes",
            token);
        request.Content = JsonContent.Create(new
        {
            clientId = "test-client",
            clientProcessId = new string('x', 300), // Exceeds max length
            type = "DataTransformation",
            priority = 5
        });

        // Act
        var response = await Client.SendAsync(request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(0)]
    [InlineData(11)]
    public async Task Endpoint_Should_ValidatePriorityRange(int invalidPriority)
    {
        // Arrange
        var token = GenerateJwtToken(
            "test-client",
            scopes: new[] { "process.write" });

        var request = CreateAuthenticatedRequest(
            HttpMethod.Post,
            "/api/processes",
            token);
        request.Content = JsonContent.Create(new
        {
            clientId = "test-client",
            clientProcessId = "proc-123",
            type = "DataTransformation",
            priority = invalidPriority
        });

        // Act
        var response = await Client.SendAsync(request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }
}
