using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using StarGate.Api.Middleware;
using StarGate.Core.Exceptions;
using System.Text.Json;
using Xunit;

namespace StarGate.Api.Tests.Middleware;

public class GlobalExceptionHandlerMiddlewareTests
{
    private readonly Mock<IHostEnvironment> _environmentMock;
    private readonly GlobalExceptionHandlerMiddleware _middleware;
    private readonly DefaultHttpContext _httpContext;

    public GlobalExceptionHandlerMiddlewareTests()
    {
        _environmentMock = new Mock<IHostEnvironment>();
        _environmentMock.Setup(e => e.EnvironmentName).Returns(Environments.Production);

        _middleware = new GlobalExceptionHandlerMiddleware(
            NullLogger<GlobalExceptionHandlerMiddleware>.Instance,
            _environmentMock.Object);

        _httpContext = new DefaultHttpContext();
        _httpContext.Response.Body = new MemoryStream();
    }

    [Fact]
    public async Task TryHandleAsync_Should_Return404_ForProcessNotFoundException()
    {
        // Arrange
        var exception = new ProcessNotFoundException(Guid.NewGuid());

        // Act
        var handled = await _middleware.TryHandleAsync(
            _httpContext,
            exception,
            CancellationToken.None);

        // Assert
        handled.Should().BeTrue();
        _httpContext.Response.StatusCode.Should().Be(404);
        _httpContext.Response.ContentType.Should().Be("application/problem+json");
    }

    [Fact]
    public async Task TryHandleAsync_Should_Return409_ForDuplicateProcessException()
    {
        // Arrange
        var exception = new DuplicateProcessException("idempotency-key");

        // Act
        var handled = await _middleware.TryHandleAsync(
            _httpContext,
            exception,
            CancellationToken.None);

        // Assert
        handled.Should().BeTrue();
        _httpContext.Response.StatusCode.Should().Be(409);
    }

    [Fact]
    public async Task TryHandleAsync_Should_Return429_ForPolicyViolationException()
    {
        // Arrange
        var exception = new PolicyViolationException("test-client", "order", "Max concurrent executions exceeded");

        // Act
        var handled = await _middleware.TryHandleAsync(
            _httpContext,
            exception,
            CancellationToken.None);

        // Assert
        handled.Should().BeTrue();
        _httpContext.Response.StatusCode.Should().Be(429);
    }

    [Fact]
    public async Task TryHandleAsync_Should_HandleDomainException()
    {
        // Arrange
        // Use DuplicateProcessException which is a concrete DomainException
        var exception = new DuplicateProcessException("test-key");

        // Act
        var handled = await _middleware.TryHandleAsync(
            _httpContext,
            exception,
            CancellationToken.None);

        // Assert
        handled.Should().BeTrue();
        _httpContext.Response.StatusCode.Should().Be(409);
    }

    [Fact]
    public async Task TryHandleAsync_Should_Return400_ForArgumentException()
    {
        // Arrange
        var exception = new ArgumentException("Invalid argument");

        // Act
        var handled = await _middleware.TryHandleAsync(
            _httpContext,
            exception,
            CancellationToken.None);

        // Assert
        handled.Should().BeTrue();
        _httpContext.Response.StatusCode.Should().Be(400);
    }

    [Fact]
    public async Task TryHandleAsync_Should_Return408_ForTimeoutException()
    {
        // Arrange
        var exception = new TimeoutException("Request timed out");

        // Act
        var handled = await _middleware.TryHandleAsync(
            _httpContext,
            exception,
            CancellationToken.None);

        // Assert
        handled.Should().BeTrue();
        _httpContext.Response.StatusCode.Should().Be(408);
    }

    [Fact]
    public async Task TryHandleAsync_Should_Return499_ForOperationCanceledException()
    {
        // Arrange
        var exception = new OperationCanceledException("Operation was cancelled");

        // Act
        var handled = await _middleware.TryHandleAsync(
            _httpContext,
            exception,
            CancellationToken.None);

        // Assert
        handled.Should().BeTrue();
        _httpContext.Response.StatusCode.Should().Be(499);
    }

    [Fact]
    public async Task TryHandleAsync_Should_Return500_ForUnhandledException()
    {
        // Arrange
        var exception = new InvalidOperationException("Something went wrong");

        // Act
        var handled = await _middleware.TryHandleAsync(
            _httpContext,
            exception,
            CancellationToken.None);

        // Assert
        handled.Should().BeTrue();
        _httpContext.Response.StatusCode.Should().Be(500);
    }

    [Fact]
    public async Task TryHandleAsync_Should_IncludeTraceId_InResponse()
    {
        // Arrange
        var traceId = "test-trace-id";
        _httpContext.TraceIdentifier = traceId;
        var exception = new ProcessNotFoundException(Guid.NewGuid());

        // Act
        await _middleware.TryHandleAsync(
            _httpContext,
            exception,
            CancellationToken.None);

        // Read response body
        _httpContext.Response.Body.Seek(0, SeekOrigin.Begin);
        var reader = new StreamReader(_httpContext.Response.Body);
        var responseBody = await reader.ReadToEndAsync();
        var problemDetails = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(responseBody);

        // Assert
        problemDetails.Should().ContainKey("traceId");
        problemDetails!["traceId"].GetString().Should().Be(traceId);
    }

    [Fact]
    public async Task TryHandleAsync_Should_IncludeTimestamp_InResponse()
    {
        // Arrange
        var exception = new ProcessNotFoundException(Guid.NewGuid());

        // Act
        await _middleware.TryHandleAsync(
            _httpContext,
            exception,
            CancellationToken.None);

        // Read response body
        _httpContext.Response.Body.Seek(0, SeekOrigin.Begin);
        var reader = new StreamReader(_httpContext.Response.Body);
        var responseBody = await reader.ReadToEndAsync();
        var problemDetails = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(responseBody);

        // Assert
        problemDetails.Should().ContainKey("timestamp");
    }

    [Fact]
    public async Task TryHandleAsync_Should_NotIncludeDetails_InProduction()
    {
        // Arrange
        _environmentMock.Setup(e => e.EnvironmentName).Returns(Environments.Production);
        var exception = new InvalidOperationException("Sensitive error message");

        // Act
        await _middleware.TryHandleAsync(
            _httpContext,
            exception,
            CancellationToken.None);

        // Read response body
        _httpContext.Response.Body.Seek(0, SeekOrigin.Begin);
        var reader = new StreamReader(_httpContext.Response.Body);
        var responseBody = await reader.ReadToEndAsync();

        // Assert
        responseBody.Should().NotContain("Sensitive error message");
        responseBody.Should().Contain("An unexpected error occurred");
    }

    [Fact]
    public async Task TryHandleAsync_Should_IncludeDetails_InDevelopment()
    {
        // Arrange
        _environmentMock.Setup(e => e.EnvironmentName).Returns(Environments.Development);
        var middleware = new GlobalExceptionHandlerMiddleware(
            NullLogger<GlobalExceptionHandlerMiddleware>.Instance,
            _environmentMock.Object);

        var exception = new InvalidOperationException("Detailed error message");

        // Act
        await middleware.TryHandleAsync(
            _httpContext,
            exception,
            CancellationToken.None);

        // Read response body
        _httpContext.Response.Body.Seek(0, SeekOrigin.Begin);
        var reader = new StreamReader(_httpContext.Response.Body);
        var responseBody = await reader.ReadToEndAsync();

        // Assert
        responseBody.Should().Contain("Detailed error message");
    }

    [Fact]
    public async Task TryHandleAsync_Should_ReturnValidRFC7807Format()
    {
        // Arrange
        var exception = new ProcessNotFoundException(Guid.NewGuid());
        _httpContext.Request.Path = "/api/processes/123";

        // Act
        await _middleware.TryHandleAsync(
            _httpContext,
            exception,
            CancellationToken.None);

        // Read response body
        _httpContext.Response.Body.Seek(0, SeekOrigin.Begin);
        var reader = new StreamReader(_httpContext.Response.Body);
        var responseBody = await reader.ReadToEndAsync();
        var problemDetails = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(responseBody);

        // Assert RFC 7807 required fields
        problemDetails.Should().ContainKey("type");
        problemDetails.Should().ContainKey("title");
        problemDetails.Should().ContainKey("status");
        problemDetails.Should().ContainKey("detail");
        problemDetails.Should().ContainKey("instance");
        
        problemDetails!["instance"].GetString().Should().Be("/api/processes/123");
        problemDetails!["status"].GetInt32().Should().Be(404);
    }

    [Fact]
    public async Task TryHandleAsync_Should_ReturnJsonContentType()
    {
        // Arrange
        var exception = new ProcessNotFoundException(Guid.NewGuid());

        // Act
        await _middleware.TryHandleAsync(
            _httpContext,
            exception,
            CancellationToken.None);

        // Assert
        _httpContext.Response.ContentType.Should().Be("application/problem+json");
    }
}
