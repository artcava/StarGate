using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using System.Net;
using System.Net.Http.Json;
using Xunit;

namespace StarGate.Security.Tests;

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
    [InlineData("")]
    public async Task Endpoint_Should_RejectMaliciousInput(string maliciousInput)
    {
        // Arrange
        var token = GenerateJwtToken("test-client");

        var request = CreateAuthenticatedRequest(
            HttpMethod.Post,
            "/api/processes",
            token);
        request.Content = JsonContent.Create(new
        {
            clientId = "test-client",
            clientProcessId = maliciousInput,
            processType = "DataTransformation",
            metadata = new Dictionary<string, string>()
        });

        // Act
        var response = await Client.SendAsync(request);

        // Assert - Should reject with BadRequest
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest,
            $"malicious input '{maliciousInput}' should be rejected");
    }

    [Fact]
    public async Task Endpoint_Should_ValidateStringLength()
    {
        // Arrange
        var token = GenerateJwtToken("test-client");

        var request = CreateAuthenticatedRequest(
            HttpMethod.Post,
            "/api/processes",
            token);
        request.Content = JsonContent.Create(new
        {
            clientId = "test-client",
            clientProcessId = new string('x', 300), // Exceeds max length
            processType = "DataTransformation",
            metadata = new Dictionary<string, string>()
        });

        // Act
        var response = await Client.SendAsync(request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest,
            "overly long input should be rejected");
    }

    [Fact]
    public async Task Endpoint_Should_RequireValidProcessType()
    {
        // Arrange
        var token = GenerateJwtToken("test-client");

        var request = CreateAuthenticatedRequest(
            HttpMethod.Post,
            "/api/processes",
            token);
        request.Content = JsonContent.Create(new
        {
            clientId = "test-client",
            clientProcessId = "valid-id",
            processType = "InvalidType12345", // Invalid process type
            metadata = new Dictionary<string, string>()
        });

        // Act
        var response = await Client.SendAsync(request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest,
            "invalid process type should be rejected");
    }

    [Fact]
    public async Task Endpoint_Should_AcceptValidInput()
    {
        // Arrange
        var token = GenerateJwtToken("test-client");

        var request = CreateAuthenticatedRequest(
            HttpMethod.Post,
            "/api/processes",
            token);
        request.Content = JsonContent.Create(new
        {
            clientId = "test-client",
            clientProcessId = $"valid-proc-{Guid.NewGuid()}",
            processType = "DataTransformation",
            metadata = new Dictionary<string, string>
            {
                ["source"] = "test",
                ["target"] = "validation"
            }
        });

        // Act
        var response = await Client.SendAsync(request);

        // Assert - Should not be BadRequest (may fail for other reasons like missing services)
        response.StatusCode.Should().NotBe(HttpStatusCode.BadRequest,
            "valid input should pass validation");
    }
}
