namespace StarGate.Api.Tests.Exceptions;

using FluentAssertions;
using Microsoft.AspNetCore.Http;
using StarGate.Api.Exceptions;
using StarGate.Core.Exceptions;
using Xunit;

public class ProblemDetailsFactoryTests
{
    private readonly DefaultHttpContext _httpContext;

    public ProblemDetailsFactoryTests()
    {
        _httpContext = new DefaultHttpContext();
        _httpContext.Request.Path = "/api/test";
        _httpContext.TraceIdentifier = "test-trace-id";
    }

    [Fact]
    public void CreateProblemDetails_Should_Map_ProcessNotFoundException_To404()
    {
        // Arrange
        var exception = new ProcessNotFoundException(Guid.NewGuid());

        // Act
        var problemDetails = ProblemDetailsFactory.CreateProblemDetails(
            exception,
            _httpContext,
            includeDetails: false);

        // Assert
        problemDetails.Status.Should().Be(404);
        problemDetails.Title.Should().Be("Process Not Found");
        problemDetails.Type.Should().Be("https://tools.ietf.org/html/rfc7231#section-6.5.4");
    }

    [Fact]
    public void CreateProblemDetails_Should_Map_DuplicateProcessException_To409()
    {
        // Arrange
        var exception = new DuplicateProcessException("test-key");

        // Act
        var problemDetails = ProblemDetailsFactory.CreateProblemDetails(
            exception,
            _httpContext,
            includeDetails: false);

        // Assert
        problemDetails.Status.Should().Be(409);
        problemDetails.Title.Should().Be("Duplicate Process");
        problemDetails.Type.Should().Be("https://tools.ietf.org/html/rfc7231#section-6.5.8");
    }

    [Fact]
    public void CreateProblemDetails_Should_Map_PolicyViolationException_To429()
    {
        // Arrange
        var exception = new PolicyViolationException("Rate limit exceeded");

        // Act
        var problemDetails = ProblemDetailsFactory.CreateProblemDetails(
            exception,
            _httpContext,
            includeDetails: false);

        // Assert
        problemDetails.Status.Should().Be(429);
        problemDetails.Title.Should().Be("Policy Violation");
        problemDetails.Type.Should().Be("https://tools.ietf.org/html/rfc6585#section-4");
    }

    [Fact]
    public void CreateProblemDetails_Should_Map_DomainException_To400()
    {
        // Arrange
        var exception = new DomainException("Invalid domain operation");

        // Act
        var problemDetails = ProblemDetailsFactory.CreateProblemDetails(
            exception,
            _httpContext,
            includeDetails: false);

        // Assert
        problemDetails.Status.Should().Be(400);
        problemDetails.Title.Should().Be("Domain Error");
        problemDetails.Type.Should().Be("https://tools.ietf.org/html/rfc7231#section-6.5.1");
    }

    [Fact]
    public void CreateProblemDetails_Should_Map_ArgumentException_To400()
    {
        // Arrange
        var exception = new ArgumentException("Invalid argument provided");

        // Act
        var problemDetails = ProblemDetailsFactory.CreateProblemDetails(
            exception,
            _httpContext,
            includeDetails: false);

        // Assert
        problemDetails.Status.Should().Be(400);
        problemDetails.Title.Should().Be("Invalid Argument");
        problemDetails.Detail.Should().Be("One or more arguments are invalid");
    }

    [Fact]
    public void CreateProblemDetails_Should_Map_TimeoutException_To408()
    {
        // Arrange
        var exception = new TimeoutException("Operation timed out");

        // Act
        var problemDetails = ProblemDetailsFactory.CreateProblemDetails(
            exception,
            _httpContext,
            includeDetails: false);

        // Assert
        problemDetails.Status.Should().Be(408);
        problemDetails.Title.Should().Be("Request Timeout");
        problemDetails.Type.Should().Be("https://tools.ietf.org/html/rfc7231#section-6.5.7");
    }

    [Fact]
    public void CreateProblemDetails_Should_Map_OperationCanceledException_To499()
    {
        // Arrange
        var exception = new OperationCanceledException("Request was cancelled");

        // Act
        var problemDetails = ProblemDetailsFactory.CreateProblemDetails(
            exception,
            _httpContext,
            includeDetails: false);

        // Assert
        problemDetails.Status.Should().Be(499);
        problemDetails.Title.Should().Be("Request Cancelled");
        problemDetails.Type.Should().Be("https://httpstatuses.com/499");
    }

    [Fact]
    public void CreateProblemDetails_Should_Map_UnknownException_To500()
    {
        // Arrange
        var exception = new InvalidOperationException("Unexpected error");

        // Act
        var problemDetails = ProblemDetailsFactory.CreateProblemDetails(
            exception,
            _httpContext,
            includeDetails: false);

        // Assert
        problemDetails.Status.Should().Be(500);
        problemDetails.Title.Should().Be("Internal Server Error");
        problemDetails.Type.Should().Be("https://tools.ietf.org/html/rfc7231#section-6.6.1");
    }

    [Fact]
    public void CreateProblemDetails_Should_IncludeTraceId()
    {
        // Arrange
        var exception = new DomainException("Test error");
        var traceId = "custom-trace-id";
        _httpContext.TraceIdentifier = traceId;

        // Act
        var problemDetails = ProblemDetailsFactory.CreateProblemDetails(
            exception,
            _httpContext,
            includeDetails: false);

        // Assert
        problemDetails.Extensions.Should().ContainKey("traceId");
        problemDetails.Extensions["traceId"].Should().Be(traceId);
    }

    [Fact]
    public void CreateProblemDetails_Should_IncludeTimestamp()
    {
        // Arrange
        var exception = new DomainException("Test error");
        var beforeTimestamp = DateTime.UtcNow;

        // Act
        var problemDetails = ProblemDetailsFactory.CreateProblemDetails(
            exception,
            _httpContext,
            includeDetails: false);

        var afterTimestamp = DateTime.UtcNow;

        // Assert
        problemDetails.Extensions.Should().ContainKey("timestamp");
        var timestamp = (DateTime)problemDetails.Extensions["timestamp"]!;
        timestamp.Should().BeOnOrAfter(beforeTimestamp).And.BeOnOrBefore(afterTimestamp);
    }

    [Fact]
    public void CreateProblemDetails_Should_IncludeInstance()
    {
        // Arrange
        var exception = new DomainException("Test error");
        var path = "/api/processes/123";
        _httpContext.Request.Path = path;

        // Act
        var problemDetails = ProblemDetailsFactory.CreateProblemDetails(
            exception,
            _httpContext,
            includeDetails: false);

        // Assert
        problemDetails.Instance.Should().Be(path);
    }

    [Fact]
    public void CreateProblemDetails_Should_HideSensitiveDetails_WhenNotInDevelopment()
    {
        // Arrange
        var sensitiveMessage = "Sensitive connection string: server=prod-db";
        var exception = new InvalidOperationException(sensitiveMessage);

        // Act
        var problemDetails = ProblemDetailsFactory.CreateProblemDetails(
            exception,
            _httpContext,
            includeDetails: false);

        // Assert
        problemDetails.Detail.Should().NotContain("Sensitive");
        problemDetails.Detail.Should().Be("An unexpected error occurred. Please try again later.");
    }

    [Fact]
    public void CreateProblemDetails_Should_ShowDetails_WhenInDevelopment()
    {
        // Arrange
        var detailedMessage = "Detailed error: database connection failed at line 42";
        var exception = new InvalidOperationException(detailedMessage);

        // Act
        var problemDetails = ProblemDetailsFactory.CreateProblemDetails(
            exception,
            _httpContext,
            includeDetails: true);

        // Assert
        problemDetails.Detail.Should().Be(detailedMessage);
    }

    [Fact]
    public void CreateProblemDetails_Should_AlwaysShowDomainExceptionDetails()
    {
        // Arrange
        var domainMessage = "Invalid process state transition";
        var exception = new DomainException(domainMessage);

        // Act (even with includeDetails: false)
        var problemDetails = ProblemDetailsFactory.CreateProblemDetails(
            exception,
            _httpContext,
            includeDetails: false);

        // Assert (domain exceptions are safe to show)
        problemDetails.Detail.Should().Be(domainMessage);
    }

    [Fact]
    public void CreateProblemDetails_Should_ShowArgumentDetails_OnlyInDevelopment()
    {
        // Arrange
        var argumentMessage = "Parameter 'clientId' cannot be null";
        var exception = new ArgumentException(argumentMessage);

        // Act - Production
        var prodDetails = ProblemDetailsFactory.CreateProblemDetails(
            exception,
            _httpContext,
            includeDetails: false);

        // Act - Development
        var devDetails = ProblemDetailsFactory.CreateProblemDetails(
            exception,
            _httpContext,
            includeDetails: true);

        // Assert
        prodDetails.Detail.Should().Be("One or more arguments are invalid");
        devDetails.Detail.Should().Be(argumentMessage);
    }
}
