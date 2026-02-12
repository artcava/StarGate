using FluentAssertions;
using MongoDB.Bson;
using StarGate.Core.Domain;
using StarGate.Infrastructure.Persistence;
using System.Text.Json;
using Xunit;

namespace StarGate.Infrastructure.Tests.Persistence;

public class ProcessMapperTests
{
    [Fact]
    public void MapToDocument_Should_ConvertProcessCorrectly()
    {
        // Arrange
        Process process = CreateValidProcess();

        // Act
        ProcessDocument document = ProcessMapper.MapToDocument(process);

        // Assert
        document.ProcessId.Should().Be(process.ProcessId);
        document.ClientProcessId.Should().Be(process.ClientProcessId);
        document.ProcessType.Should().Be(process.ProcessType);
        document.ClientId.Should().Be(process.ClientId);
        document.Status.Should().Be(process.Status.ToString());
        document.Progress.Should().Be(process.Progress);
        document.CreatedAt.Should().Be(process.CreatedAt);
        document.IdempotencyKey.Should().Be(process.IdempotencyKey);
    }

    [Fact]
    public void MapToDocument_Should_ThrowArgumentNull_WhenProcessIsNull()
    {
        // Act
        Action act = () => ProcessMapper.MapToDocument(null!);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void MapToDocument_Should_HandleNullData()
    {
        // Arrange
        Process process = CreateValidProcess() with { Data = null };

        // Act
        ProcessDocument document = ProcessMapper.MapToDocument(process);

        // Assert
        document.Data.Should().BeNull();
    }

    [Fact]
    public void MapToDocument_Should_SerializeJsonDocumentToBson()
    {
        // Arrange
        JsonDocument data = JsonSerializer.SerializeToDocument(new { orderId = "ORD-123", total = 99.99 });
        Process process = CreateValidProcess() with { Data = data };

        // Act
        ProcessDocument document = ProcessMapper.MapToDocument(process);

        // Assert
        document.Data.Should().NotBeNull();
        document.Data!["orderId"].Should().Be("ORD-123");
        document.Data["total"].AsDouble.Should().Be(99.99);
    }

    [Fact]
    public void MapToDocument_Should_HandleError()
    {
        // Arrange
        JsonDocument errorDetails = JsonSerializer.SerializeToDocument(new { field = "orderId" });
        ProcessError error = new(
            "VALIDATION_ERROR",
            "Invalid order data",
            errorDetails);
        Process process = CreateValidProcess() with { Error = error };

        // Act
        ProcessDocument document = ProcessMapper.MapToDocument(process);

        // Assert
        document.Error.Should().NotBeNull();
        document.Error!.Code.Should().Be("VALIDATION_ERROR");
        document.Error.Message.Should().Be("Invalid order data");
        document.Error.Details.Should().NotBeNull();
        document.Error.Details!["field"].Should().Be("orderId");
    }

    [Fact]
    public void MapToDocument_Should_HandleErrorWithNullDetails()
    {
        // Arrange
        ProcessError error = new("ERROR_CODE", "Error message", null);
        Process process = CreateValidProcess() with { Error = error };

        // Act
        ProcessDocument document = ProcessMapper.MapToDocument(process);

        // Assert
        document.Error.Should().NotBeNull();
        document.Error!.Code.Should().Be("ERROR_CODE");
        document.Error.Message.Should().Be("Error message");
        document.Error.Details.Should().BeNull();
    }

    [Fact]
    public void MapToDomain_Should_ConvertDocumentCorrectly()
    {
        // Arrange
        ProcessDocument document = CreateValidDocument();

        // Act
        Process process = ProcessMapper.MapToDomain(document);

        // Assert
        process.ProcessId.Should().Be(document.ProcessId);
        process.ClientProcessId.Should().Be(document.ClientProcessId);
        process.ProcessType.Should().Be(document.ProcessType);
        process.ClientId.Should().Be(document.ClientId);
        process.Status.Should().Be(ProcessStatus.Accepted);
        process.Progress.Should().Be(document.Progress);
        process.CreatedAt.Should().Be(document.CreatedAt);
        process.IdempotencyKey.Should().Be(document.IdempotencyKey);
    }

    [Fact]
    public void MapToDomain_Should_ThrowArgumentNull_WhenDocumentIsNull()
    {
        // Act
        Action act = () => ProcessMapper.MapToDomain(null!);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void MapToDomain_Should_HandleNullData()
    {
        // Arrange
        ProcessDocument document = CreateValidDocument() with { Data = null };

        // Act
        Process process = ProcessMapper.MapToDomain(document);

        // Assert
        process.Data.Should().BeNull();
    }

    [Fact]
    public void MapToDomain_Should_DeserializeBsonDataToJsonDocument()
    {
        // Arrange
        BsonDocument bsonData = BsonDocument.Parse("{\"orderId\":\"ORD-123\",\"total\":99.99}");
        ProcessDocument document = CreateValidDocument() with { Data = bsonData };

        // Act
        Process process = ProcessMapper.MapToDomain(document);

        // Assert
        process.Data.Should().NotBeNull();
        process.Data!.RootElement.GetProperty("orderId").GetString().Should().Be("ORD-123");
        process.Data.RootElement.GetProperty("total").GetDouble().Should().Be(99.99);
    }

    [Fact]
    public void MapToDomain_Should_HandleError()
    {
        // Arrange
        ErrorDocument errorDocument = new()
        {
            Code = "TIMEOUT_ERROR",
            Message = "Process execution timeout",
            Details = BsonDocument.Parse("{\"timeout\":300}")
        };
        ProcessDocument document = CreateValidDocument() with { Error = errorDocument };

        // Act
        Process process = ProcessMapper.MapToDomain(document);

        // Assert
        process.Error.Should().NotBeNull();
        process.Error!.Code.Should().Be("TIMEOUT_ERROR");
        process.Error.Message.Should().Be("Process execution timeout");
        process.Error.Details.Should().NotBeNull();
        process.Error.Details!.RootElement.GetProperty("timeout").GetInt32().Should().Be(300);
    }

    [Fact]
    public void MapToDomain_Should_HandleAllProcessStatuses()
    {
        // Arrange & Act & Assert
        ProcessStatus[] statuses = 
        { 
            ProcessStatus.Accepted,
            ProcessStatus.Queued,
            ProcessStatus.Running,
            ProcessStatus.Completed,
            ProcessStatus.Failed,
            ProcessStatus.Cancelled
        };

        foreach (ProcessStatus status in statuses)
        {
            ProcessDocument document = CreateValidDocument() with { Status = status.ToString() };
            Process process = ProcessMapper.MapToDomain(document);
            process.Status.Should().Be(status);
        }
    }

    [Fact]
    public void MapToDomain_Should_ThrowException_OnInvalidStatus()
    {
        // Arrange
        ProcessDocument document = CreateValidDocument() with { Status = "InvalidStatus" };

        // Act
        Action act = () => ProcessMapper.MapToDomain(document);

        // Assert
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*InvalidStatus*");
    }

    [Fact]
    public void MapToDomain_Should_BeCaseInsensitiveForStatus()
    {
        // Arrange
        ProcessDocument document = CreateValidDocument() with { Status = "accepted" }; // lowercase

        // Act
        Process process = ProcessMapper.MapToDomain(document);

        // Assert
        process.Status.Should().Be(ProcessStatus.Accepted);
    }

    [Fact]
    public void RoundTrip_Should_PreserveData()
    {
        // Arrange
        Process originalProcess = CreateValidProcess();

        // Act
        ProcessDocument document = ProcessMapper.MapToDocument(originalProcess);
        Process roundTrippedProcess = ProcessMapper.MapToDomain(document);

        // Assert
        roundTrippedProcess.ProcessId.Should().Be(originalProcess.ProcessId);
        roundTrippedProcess.ClientProcessId.Should().Be(originalProcess.ClientProcessId);
        roundTrippedProcess.Status.Should().Be(originalProcess.Status);
        roundTrippedProcess.IdempotencyKey.Should().Be(originalProcess.IdempotencyKey);
    }

    [Fact]
    public void RoundTrip_Should_PreserveComplexData()
    {
        // Arrange
        JsonDocument complexData = JsonSerializer.SerializeToDocument(new
        {
            orderId = "ORD-789",
            customer = new { id = "CUST-001", name = "John Doe" },
            items = new[] { "item1", "item2", "item3" },
            total = 299.99
        });
        Process originalProcess = CreateValidProcess() with { Data = complexData };

        // Act
        ProcessDocument document = ProcessMapper.MapToDocument(originalProcess);
        Process roundTrippedProcess = ProcessMapper.MapToDomain(document);

        // Assert
        roundTrippedProcess.Data.Should().NotBeNull();
        roundTrippedProcess.Data!.RootElement.GetProperty("orderId").GetString().Should().Be("ORD-789");
        roundTrippedProcess.Data.RootElement.GetProperty("customer").GetProperty("name").GetString().Should().Be("John Doe");
        roundTrippedProcess.Data.RootElement.GetProperty("total").GetDouble().Should().Be(299.99);
    }

    [Fact]
    public void MapToDocument_Should_HandleNullResult()
    {
        // Arrange
        Process process = CreateValidProcess() with { Result = null };

        // Act
        ProcessDocument document = ProcessMapper.MapToDocument(process);

        // Assert
        document.Result.Should().BeNull();
    }

    [Fact]
    public void MapToDocument_Should_SerializeResult()
    {
        // Arrange
        JsonDocument result = JsonSerializer.SerializeToDocument(new { success = true, orderId = "ORD-999" });
        Process process = CreateValidProcess() with { Result = result };

        // Act
        ProcessDocument document = ProcessMapper.MapToDocument(process);

        // Assert
        document.Result.Should().NotBeNull();
        document.Result!["success"].AsBoolean.Should().BeTrue();
        document.Result["orderId"].Should().Be("ORD-999");
    }

    [Fact]
    public void MapToDomain_Should_HandleNullError()
    {
        // Arrange
        ProcessDocument document = CreateValidDocument() with { Error = null };

        // Act
        Process process = ProcessMapper.MapToDomain(document);

        // Assert
        process.Error.Should().BeNull();
    }

    [Fact]
    public void RoundTrip_Should_PreserveResult()
    {
        // Arrange
        JsonDocument result = JsonSerializer.SerializeToDocument(new { status = "completed", itemCount = 42 });
        Process originalProcess = CreateValidProcess() with { Result = result };

        // Act
        ProcessDocument document = ProcessMapper.MapToDocument(originalProcess);
        Process roundTrippedProcess = ProcessMapper.MapToDomain(document);

        // Assert
        roundTrippedProcess.Result.Should().NotBeNull();
        roundTrippedProcess.Result!.RootElement.GetProperty("status").GetString().Should().Be("completed");
        roundTrippedProcess.Result.RootElement.GetProperty("itemCount").GetInt32().Should().Be(42);
    }

    private static Process CreateValidProcess() => new()
    {
        ProcessId = Guid.NewGuid(),
        ClientProcessId = $"client-{Guid.NewGuid()}",
        ProcessType = "order",
        ClientId = "test-client",
        Status = ProcessStatus.Accepted,
        Progress = 0,
        CreatedAt = DateTime.UtcNow,
        UpdatedAt = DateTime.UtcNow,
        IdempotencyKey = Guid.NewGuid().ToString(),
        Retryable = true
    };

    private static ProcessDocument CreateValidDocument() => new()
    {
        ProcessId = Guid.NewGuid(),
        ClientProcessId = $"client-{Guid.NewGuid()}",
        ProcessType = "order",
        ClientId = "test-client",
        Status = "Accepted",
        Progress = 0,
        CreatedAt = DateTime.UtcNow,
        UpdatedAt = DateTime.UtcNow,
        IdempotencyKey = Guid.NewGuid().ToString(),
        Retryable = true
    };
}
