using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Moq;
using StarGate.Api.Models;
using StarGate.Contracts.Requests;
using StarGate.Core.Abstractions;
using StarGate.Core.Domain;
using StarGate.Core.Exceptions;
using System.Reflection;
using System.Security.Claims;

namespace StarGate.Api.Tests.Endpoints;

public class ProcessEndpointsTests : EndpointTestBase
{
    private readonly Mock<IProcessService> _processServiceMock;
    private readonly Mock<ILogger<Program>> _loggerMock;

    public ProcessEndpointsTests()
    {
        _processServiceMock = new Mock<IProcessService>();
        _loggerMock = new Mock<ILogger<Program>>();
    }

    #region CreateProcessAsync Tests

    [Fact]
    public async Task CreateProcessAsync_Should_ReturnCreated_WhenRequestIsValid()
    {
        // Arrange
        var request = new CreateProcessRequest
        {
            ClientId = "test-client",
            ClientProcessId = "client-proc-123",
            ProcessType = "DataTransformation",
            IdempotencyKey = "idempotency-123",
            Metadata = new Dictionary<string, string>
            {
                ["inputFile"] = "data.csv",
                ["outputFormat"] = "json"
            }
        };

        var expectedProcess = new Process
        {
            ProcessId = Guid.NewGuid(),
            ClientId = request.ClientId,
            ClientProcessId = request.ClientProcessId,
            ProcessType = request.ProcessType,
            Status = ProcessStatus.Accepted,
            Progress = 0,
            Retryable = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            RetryCount = 0,
            MaxRetries = 3,
            IdempotencyKey = request.IdempotencyKey
        };

        _processServiceMock
            .Setup(s => s.GetProcessByClientProcessIdAsync(
                request.ClientId,
                request.ClientProcessId,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((Process?)null);

        _processServiceMock
            .Setup(s => s.SubmitProcessAsync(
                request.ClientId,
                It.IsAny<SubmitProcessRequest>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedProcess);

        var user = CreateDefaultUser(request.ClientId);

        // Act
        var result = await InvokeCreateProcessAsync(request, user);

        // Assert
        var statusCode = GetStatusCode(result);
        statusCode.Should().Be(StatusCodes.Status201Created);

        var response = GetResultValue<ProcessResponse>(result);
        response.Should().NotBeNull();
        response.ProcessId.Should().Be(expectedProcess.ProcessId);
        response.ClientId.Should().Be(request.ClientId);
        response.ClientProcessId.Should().Be(request.ClientProcessId);
        response.Status.Should().Be("Accepted");
    }

    [Fact]
    public async Task CreateProcessAsync_Should_ReturnConflict_WhenDuplicateProcessExists()
    {
        // Arrange
        var request = new CreateProcessRequest
        {
            ClientId = "test-client",
            ClientProcessId = "client-proc-123",
            ProcessType = "DataTransformation",
            IdempotencyKey = "idempotency-123"
        };

        var existingProcess = new Process
        {
            ProcessId = Guid.NewGuid(),
            ClientId = request.ClientId,
            ClientProcessId = request.ClientProcessId,
            ProcessType = request.ProcessType,
            Status = ProcessStatus.Accepted,
            CreatedAt = DateTime.UtcNow.AddMinutes(-5),
            UpdatedAt = DateTime.UtcNow.AddMinutes(-5),
            IdempotencyKey = request.IdempotencyKey
        };

        _processServiceMock
            .Setup(s => s.GetProcessByClientProcessIdAsync(
                request.ClientId,
                request.ClientProcessId,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingProcess);

        var user = CreateDefaultUser(request.ClientId);

        // Act
        var result = await InvokeCreateProcessAsync(request, user);

        // Assert
        var statusCode = GetStatusCode(result);
        statusCode.Should().Be(StatusCodes.Status409Conflict);
    }

    [Fact]
    public async Task CreateProcessAsync_Should_ReturnForbidden_WhenClientIdMismatch()
    {
        // Arrange
        var request = new CreateProcessRequest
        {
            ClientId = "different-client",
            ClientProcessId = "client-proc-123",
            ProcessType = "DataTransformation",
            IdempotencyKey = "idempotency-123"
        };

        var user = CreateDefaultUser("test-client");

        // Act
        var result = await InvokeCreateProcessAsync(request, user);

        // Assert
        var statusCode = GetStatusCode(result);
        statusCode.Should().Be(StatusCodes.Status403Forbidden);
    }

    [Fact]
    public async Task CreateProcessAsync_Should_CallService_WithCorrectParameters()
    {
        // Arrange
        var request = new CreateProcessRequest
        {
            ClientId = "test-client",
            ClientProcessId = "client-proc-123",
            ProcessType = "DataTransformation",
            IdempotencyKey = "idempotency-123",
            Metadata = new Dictionary<string, string> { ["key"] = "value" }
        };

        var expectedProcess = new Process
        {
            ProcessId = Guid.NewGuid(),
            ClientId = request.ClientId,
            ClientProcessId = request.ClientProcessId,
            ProcessType = request.ProcessType,
            Status = ProcessStatus.Accepted,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            IdempotencyKey = request.IdempotencyKey
        };

        _processServiceMock
            .Setup(s => s.GetProcessByClientProcessIdAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((Process?)null);

        _processServiceMock
            .Setup(s => s.SubmitProcessAsync(
                It.IsAny<string>(),
                It.IsAny<SubmitProcessRequest>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedProcess);

        var user = CreateDefaultUser(request.ClientId);

        // Act
        await InvokeCreateProcessAsync(request, user);

        // Assert
        _processServiceMock.Verify(
            s => s.SubmitProcessAsync(
                request.ClientId,
                It.Is<SubmitProcessRequest>(r =>
                    r.ProcessType == request.ProcessType &&
                    r.ClientProcessId == request.ClientProcessId &&
                    r.IdempotencyKey == request.IdempotencyKey),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task CreateProcessAsync_Should_ReturnTooManyRequests_WhenPolicyViolationOccurs()
    {
        // Arrange
        var request = new CreateProcessRequest
        {
            ClientId = "test-client",
            ClientProcessId = "client-proc-123",
            ProcessType = "DataTransformation",
            IdempotencyKey = "idempotency-123"
        };

        _processServiceMock
            .Setup(s => s.GetProcessByClientProcessIdAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((Process?)null);

        _processServiceMock
            .Setup(s => s.SubmitProcessAsync(
                It.IsAny<string>(),
                It.IsAny<SubmitProcessRequest>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new PolicyViolationException("Rate limit exceeded for client test-client and process type DataTransformation"));

        var user = CreateDefaultUser(request.ClientId);

        // Act
        var result = await InvokeCreateProcessAsync(request, user);

        // Assert
        var statusCode = GetStatusCode(result);
        statusCode.Should().Be(StatusCodes.Status429TooManyRequests);
    }

    [Fact]
    public async Task CreateProcessAsync_Should_ReturnInternalServerError_WhenUnexpectedExceptionOccurs()
    {
        // Arrange
        var request = new CreateProcessRequest
        {
            ClientId = "test-client",
            ClientProcessId = "client-proc-123",
            ProcessType = "DataTransformation",
            IdempotencyKey = "idempotency-123"
        };

        _processServiceMock
            .Setup(s => s.GetProcessByClientProcessIdAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("Database connection failed"));

        var user = CreateDefaultUser(request.ClientId);

        // Act
        var result = await InvokeCreateProcessAsync(request, user);

        // Assert
        var statusCode = GetStatusCode(result);
        statusCode.Should().Be(StatusCodes.Status500InternalServerError);
    }

    [Fact]
    public async Task CreateProcessAsync_Should_MapMetadataToPayload_Correctly()
    {
        // Arrange
        var metadata = new Dictionary<string, string>
        {
            ["inputFile"] = "data.csv",
            ["outputFormat"] = "json",
            ["priority"] = "high"
        };

        var request = new CreateProcessRequest
        {
            ClientId = "test-client",
            ClientProcessId = "client-proc-123",
            ProcessType = "DataTransformation",
            IdempotencyKey = "idempotency-123",
            Metadata = metadata
        };

        var expectedProcess = new Process
        {
            ProcessId = Guid.NewGuid(),
            ClientId = request.ClientId,
            ClientProcessId = request.ClientProcessId,
            ProcessType = request.ProcessType,
            Status = ProcessStatus.Accepted,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            IdempotencyKey = request.IdempotencyKey
        };

        _processServiceMock
            .Setup(s => s.GetProcessByClientProcessIdAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((Process?)null);

        _processServiceMock
            .Setup(s => s.SubmitProcessAsync(
                It.IsAny<string>(),
                It.IsAny<SubmitProcessRequest>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedProcess);

        var user = CreateDefaultUser(request.ClientId);

        // Act
        await InvokeCreateProcessAsync(request, user);

        // Assert
        _processServiceMock.Verify(
            s => s.SubmitProcessAsync(
                request.ClientId,
                It.Is<SubmitProcessRequest>(r =>
                    (r.Payload as Dictionary<string, string>) != null &&
                    (r.Payload as Dictionary<string, string>)!.Count == metadata.Count &&
                    (r.Payload as Dictionary<string, string>)!.ContainsKey("inputFile") &&
                    (r.Payload as Dictionary<string, string>)!["inputFile"] == "data.csv" &&
                    (r.Payload as Dictionary<string, string>)!.ContainsKey("outputFormat") &&
                    (r.Payload as Dictionary<string, string>)!["outputFormat"] == "json" &&
                    (r.Payload as Dictionary<string, string>)!.ContainsKey("priority") &&
                    (r.Payload as Dictionary<string, string>)!["priority"] == "high"),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task CreateProcessAsync_Should_HandleNullMetadata_Gracefully()
    {
        // Arrange
        var request = new CreateProcessRequest
        {
            ClientId = "test-client",
            ClientProcessId = "client-proc-123",
            ProcessType = "DataTransformation",
            IdempotencyKey = "idempotency-123",
            Metadata = null
        };

        var expectedProcess = new Process
        {
            ProcessId = Guid.NewGuid(),
            ClientId = request.ClientId,
            ClientProcessId = request.ClientProcessId,
            ProcessType = request.ProcessType,
            Status = ProcessStatus.Accepted,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            IdempotencyKey = request.IdempotencyKey
        };

        _processServiceMock
            .Setup(s => s.GetProcessByClientProcessIdAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((Process?)null);

        _processServiceMock
            .Setup(s => s.SubmitProcessAsync(
                It.IsAny<string>(),
                It.IsAny<SubmitProcessRequest>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedProcess);

        var user = CreateDefaultUser(request.ClientId);

        // Act
        var result = await InvokeCreateProcessAsync(request, user);

        // Assert
        var statusCode = GetStatusCode(result);
        statusCode.Should().Be(StatusCodes.Status201Created);

        _processServiceMock.Verify(
            s => s.SubmitProcessAsync(
                request.ClientId,
                It.Is<SubmitProcessRequest>(r =>
                    (r.Payload as Dictionary<string, string>) != null &&
                    (r.Payload as Dictionary<string, string>)!.Count == 0),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    #endregion

    #region GetProcessByIdAsync Tests

    [Fact]
    public async Task GetProcessByIdAsync_Should_ReturnOk_WhenProcessExists()
    {
        // Arrange
        var processId = Guid.NewGuid();
        var process = new Process
        {
            ProcessId = processId,
            ClientId = "test-client",
            ClientProcessId = "client-proc-123",
            ProcessType = "DataTransformation",
            Status = ProcessStatus.Processing,
            Progress = 50,
            CreatedAt = DateTime.UtcNow.AddMinutes(-10),
            UpdatedAt = DateTime.UtcNow,
            IdempotencyKey = "idempotency-123"
        };

        _processServiceMock
            .Setup(s => s.GetProcessByIdAsync(processId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(process);

        // Act
        var result = await InvokeGetProcessByIdAsync(processId);

        // Assert
        var statusCode = GetStatusCode(result);
        statusCode.Should().Be(StatusCodes.Status200OK);

        var response = GetResultValue<ProcessResponse>(result);
        response.Should().NotBeNull();
        response.ProcessId.Should().Be(processId);
        response.Status.Should().Be("Processing");
        response.Progress.Should().Be(50);
    }

    [Fact]
    public async Task GetProcessByIdAsync_Should_ReturnNotFound_WhenProcessDoesNotExist()
    {
        // Arrange
        var processId = Guid.NewGuid();

        _processServiceMock
            .Setup(s => s.GetProcessByIdAsync(processId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Process?)null);

        // Act
        var result = await InvokeGetProcessByIdAsync(processId);

        // Assert
        var statusCode = GetStatusCode(result);
        statusCode.Should().Be(StatusCodes.Status404NotFound);
    }

    [Fact]
    public async Task GetProcessByIdAsync_Should_ReturnInternalServerError_WhenExceptionOccurs()
    {
        // Arrange
        var processId = Guid.NewGuid();

        _processServiceMock
            .Setup(s => s.GetProcessByIdAsync(processId, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("Database error"));

        // Act
        var result = await InvokeGetProcessByIdAsync(processId);

        // Assert
        var statusCode = GetStatusCode(result);
        statusCode.Should().Be(StatusCodes.Status500InternalServerError);
    }

    [Fact]
    public async Task GetProcessByIdAsync_Should_CallService_WithCorrectProcessId()
    {
        // Arrange
        var processId = Guid.NewGuid();
        var process = new Process
        {
            ProcessId = processId,
            ClientId = "test-client",
            ClientProcessId = "client-proc-123",
            ProcessType = "DataTransformation",
            Status = ProcessStatus.Completed,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            IdempotencyKey = "idempotency-123"
        };

        _processServiceMock
            .Setup(s => s.GetProcessByIdAsync(processId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(process);

        // Act
        await InvokeGetProcessByIdAsync(processId);

        // Assert
        _processServiceMock.Verify(
            s => s.GetProcessByIdAsync(processId, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    #endregion

    #region GetProcessByClientIdAsync Tests

    [Fact]
    public async Task GetProcessByClientIdAsync_Should_ReturnOk_WhenProcessExists()
    {
        // Arrange
        var clientId = "test-client";
        var clientProcessId = "order-123";
        var process = new Process
        {
            ProcessId = Guid.NewGuid(),
            ClientId = clientId,
            ClientProcessId = clientProcessId,
            ProcessType = "OrderProcessing",
            Status = ProcessStatus.Completed,
            Progress = 100,
            CreatedAt = DateTime.UtcNow.AddHours(-1),
            UpdatedAt = DateTime.UtcNow,
            IdempotencyKey = "idempotency-order-123"
        };

        _processServiceMock
            .Setup(s => s.GetProcessByClientProcessIdAsync(
                clientId,
                clientProcessId,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(process);

        var user = CreateDefaultUser(clientId);

        // Act
        var result = await InvokeGetProcessByClientIdAsync(clientId, clientProcessId, user);

        // Assert
        var statusCode = GetStatusCode(result);
        statusCode.Should().Be(StatusCodes.Status200OK);

        var response = GetResultValue<ProcessResponse>(result);
        response.Should().NotBeNull();
        response.ClientId.Should().Be(clientId);
        response.ClientProcessId.Should().Be(clientProcessId);
        response.Status.Should().Be("Completed");
    }

    [Fact]
    public async Task GetProcessByClientIdAsync_Should_ReturnNotFound_WhenProcessDoesNotExist()
    {
        // Arrange
        var clientId = "test-client";
        var clientProcessId = "order-999";

        _processServiceMock
            .Setup(s => s.GetProcessByClientProcessIdAsync(
                clientId,
                clientProcessId,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((Process?)null);

        var user = CreateDefaultUser(clientId);

        // Act
        var result = await InvokeGetProcessByClientIdAsync(clientId, clientProcessId, user);

        // Assert
        var statusCode = GetStatusCode(result);
        statusCode.Should().Be(StatusCodes.Status404NotFound);
    }

    [Fact]
    public async Task GetProcessByClientIdAsync_Should_ReturnForbidden_WhenClientIdMismatch()
    {
        // Arrange
        var clientId = "different-client";
        var clientProcessId = "order-123";
        var user = CreateDefaultUser("test-client");

        // Act
        var result = await InvokeGetProcessByClientIdAsync(clientId, clientProcessId, user);

        // Assert
        var statusCode = GetStatusCode(result);
        statusCode.Should().Be(StatusCodes.Status403Forbidden);
    }

    [Fact]
    public async Task GetProcessByClientIdAsync_Should_ReturnInternalServerError_WhenExceptionOccurs()
    {
        // Arrange
        var clientId = "test-client";
        var clientProcessId = "order-123";

        _processServiceMock
            .Setup(s => s.GetProcessByClientProcessIdAsync(
                clientId,
                clientProcessId,
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("Database error"));

        var user = CreateDefaultUser(clientId);

        // Act
        var result = await InvokeGetProcessByClientIdAsync(clientId, clientProcessId, user);

        // Assert
        var statusCode = GetStatusCode(result);
        statusCode.Should().Be(StatusCodes.Status500InternalServerError);
    }

    [Fact]
    public async Task GetProcessByClientIdAsync_Should_CallService_WithCorrectParameters()
    {
        // Arrange
        var clientId = "test-client";
        var clientProcessId = "order-123";
        var process = new Process
        {
            ProcessId = Guid.NewGuid(),
            ClientId = clientId,
            ClientProcessId = clientProcessId,
            ProcessType = "OrderProcessing",
            Status = ProcessStatus.Accepted,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            IdempotencyKey = "idempotency-order-123"
        };

        _processServiceMock
            .Setup(s => s.GetProcessByClientProcessIdAsync(
                clientId,
                clientProcessId,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(process);

        var user = CreateDefaultUser(clientId);

        // Act
        await InvokeGetProcessByClientIdAsync(clientId, clientProcessId, user);

        // Assert
        _processServiceMock.Verify(
            s => s.GetProcessByClientProcessIdAsync(
                clientId,
                clientProcessId,
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    #endregion

    #region Helper Methods

    private async Task<IResult> InvokeCreateProcessAsync(
        CreateProcessRequest request,
        ClaimsPrincipal user)
    {
        var endpointType = typeof(StarGate.Api.Endpoints.ProcessEndpoints);
        var method = endpointType.GetMethod(
            "CreateProcessAsync",
            BindingFlags.NonPublic | BindingFlags.Static);

        if (method is null)
        {
            throw new InvalidOperationException("CreateProcessAsync method not found");
        }

        var result = await (Task<IResult>)method.Invoke(
            null,
            new object[] { request, _processServiceMock.Object, _loggerMock.Object, user, CancellationToken.None })!;

        return result;
    }

    private async Task<IResult> InvokeGetProcessByIdAsync(Guid processId)
    {
        var endpointType = typeof(StarGate.Api.Endpoints.ProcessEndpoints);
        var method = endpointType.GetMethod(
            "GetProcessByIdAsync",
            BindingFlags.NonPublic | BindingFlags.Static);

        if (method is null)
        {
            throw new InvalidOperationException("GetProcessByIdAsync method not found");
        }

        var result = await (Task<IResult>)method.Invoke(
            null,
            new object[] { processId, _processServiceMock.Object, _loggerMock.Object, CancellationToken.None })!;

        return result;
    }

    private async Task<IResult> InvokeGetProcessByClientIdAsync(
        string clientId,
        string clientProcessId,
        ClaimsPrincipal user)
    {
        var endpointType = typeof(StarGate.Api.Endpoints.ProcessEndpoints);
        var method = endpointType.GetMethod(
            "GetProcessByClientIdAsync",
            BindingFlags.NonPublic | BindingFlags.Static);

        if (method is null)
        {
            throw new InvalidOperationException("GetProcessByClientIdAsync method not found");
        }

        var result = await (Task<IResult>)method.Invoke(
            null,
            new object[] { clientId, clientProcessId, _processServiceMock.Object, _loggerMock.Object, user, CancellationToken.None })!;

        return result;
    }

    #endregion
}
