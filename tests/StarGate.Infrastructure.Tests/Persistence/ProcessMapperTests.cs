namespace StarGate.Infrastructure.Tests.Persistence;

using FluentAssertions;
using MongoDB.Bson;
using StarGate.Core.Domain;
using StarGate.Infrastructure.Persistence;
using Xunit;

public class ProcessMapperTests
{
    [Fact]
    public void MapToDocument_Should_ConvertProcessCorrectly()
    {
        // Arrange
        var process = CreateValidProcess();

        // Act
        var document = ProcessMapper.MapToDocument(process);

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
        var process = CreateValidProcess() with { Data = null };

        // Act
        var document = ProcessMapper.MapToDocument(process);

        // Assert
        document.Data.Should().BeNull();
    }

    [Fact]
    public void MapToDocument_Should_SerializeObjectDataToBson()
    {
        // Arrange
        var data = new { orderId = "ORD-123", items = new[] { "item1", "item2" } };
        var process = CreateValidProcess() with { Data = data };

        // Act
        var document = ProcessMapper.MapToDocument(process);

        // Assert
        document.Data.Should().NotBeNull();
        document.Data!["orderId"].Should().Be("ORD-123");
        document.Data["items"].Should().BeOfType<BsonArray>();
    }

    [Fact]
    public void MapToDocument_Should_ParseStringJsonToBson()
    {
        // Arrange
        var jsonString = "{\"orderId\":\"ORD-456\",\"total\":99.99}";
        var process = CreateValidProcess() with { Data = jsonString };

        // Act
        var document = ProcessMapper.MapToDocument(process);

        // Assert
        document.Data.Should().NotBeNull();
        document.Data!["orderId"].Should().Be("ORD-456");
        document.Data["total"].AsDouble.Should().Be(99.99);
    }

    [Fact]
    public void MapToDocument_Should_HandleEmptyString()
    {
        // Arrange
        var process = CreateValidProcess() with { Data = "" };

        // Act
        var document = ProcessMapper.MapToDocument(process);

        // Assert
        document.Data.Should().BeNull();
    }

    [Fact]
    public void MapToDocument_Should_HandleWhitespaceString()
    {
        // Arrange
        var process = CreateValidProcess() with { Data = "   " };

        // Act
        var document = ProcessMapper.MapToDocument(process);

        // Assert
        document.Data.Should().BeNull();
    }

    [Fact]
    public void MapToDocument_Should_HandleError()
    {
        // Arrange
        var error = new ProcessError(
            "VALIDATION_ERROR",
            "Invalid order data",
            new { field = "orderId" });
        var process = CreateValidProcess() with { Error = error };

        // Act
        var document = ProcessMapper.MapToDocument(process);

        // Assert
        document.Error.Should().NotBeNull();
        document.Error!.Code.Should().Be("VALIDATION_ERROR");
        document.Error.Message.Should().Be("Invalid order data");
        document.Error.Details.Should().NotBeNull();
    }

    [Fact]
    public void MapToDocument_Should_HandleErrorWithNullDetails()
    {
        // Arrange
        var error = new ProcessError("ERROR_CODE", "Error message", null);
        var process = CreateValidProcess() with { Error = error };

        // Act
        var document = ProcessMapper.MapToDocument(process);

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
        var document = CreateValidDocument();

        // Act
        var process = ProcessMapper.MapToDomain(document);

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
        var document = CreateValidDocument() with { Data = null };

        // Act
        var process = ProcessMapper.MapToDomain(document);

        // Assert
        process.Data.Should().BeNull();
    }

    [Fact]
    public void MapToDomain_Should_DeserializeBsonDataToJson()
    {
        // Arrange
        var bsonData = BsonDocument.Parse("{\"orderId\":\"ORD-123\",\"total\":99.99}");
        var document = CreateValidDocument() with { Data = bsonData };

        // Act
        var process = ProcessMapper.MapToDomain(document);

        // Assert
        process.Data.Should().NotBeNull();
        process.Data.Should().Contain("orderId");
        process.Data.Should().Contain("ORD-123");
    }

    [Fact]
    public void MapToDomain_Should_HandleError()
    {
        // Arrange
        var errorDocument = new ErrorDocument
        {
            Code = "TIMEOUT_ERROR",
            Message = "Process execution timeout",
            Details = BsonDocument.Parse("{\"timeout\":300}")
        };
        var document = CreateValidDocument() with { Error = errorDocument };

        // Act
        var process = ProcessMapper.MapToDomain(document);

        // Assert
        process.Error.Should().NotBeNull();
        process.Error!.Code.Should().Be("TIMEOUT_ERROR");
        process.Error.Message.Should().Be("Process execution timeout");
        process.Error.Details.Should().NotBeNull();
    }

    [Fact]
    public void MapToDomain_Should_HandleAllProcessStatuses()
    {
        // Arrange & Act & Assert
        var statuses = new[] 
        { 
            ProcessStatus.Accepted,
            ProcessStatus.Queued,
            ProcessStatus.Running,
            ProcessStatus.Completed,
            ProcessStatus.Failed,
            ProcessStatus.Cancelled
        };

        foreach (var status in statuses)
        {
            var document = CreateValidDocument() with { Status = status.ToString() };
            var process = ProcessMapper.MapToDomain(document);
            process.Status.Should().Be(status);
        }
    }

    [Fact]
    public void MapToDomain_Should_ThrowException_OnInvalidStatus()
    {
        // Arrange
        var document = CreateValidDocument() with { Status = "InvalidStatus" };

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
        var document = CreateValidDocument() with { Status = "accepted" }; // lowercase

        // Act
        var process = ProcessMapper.MapToDomain(document);

        // Assert
        process.Status.Should().Be(ProcessStatus.Accepted);
    }

    [Fact]
    public void RoundTrip_Should_PreserveData()
    {
        // Arrange
        var originalProcess = CreateValidProcess();

        // Act
        var document = ProcessMapper.MapToDocument(originalProcess);
        var roundTrippedProcess = ProcessMapper.MapToDomain(document);

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
        var complexData = new
        {
            orderId = "ORD-789",
            customer = new { id = "CUST-001", name = "John Doe" },
            items = new[] { "item1", "item2", "item3" },
            total = 299.99
        };
        var originalProcess = CreateValidProcess() with { Data = complexData };

        // Act
        var document = ProcessMapper.MapToDocument(originalProcess);
        var roundTrippedProcess = ProcessMapper.MapToDomain(document);

        // Assert
        roundTrippedProcess.Data.Should().NotBeNull();
        roundTrippedProcess.Data.Should().Contain("ORD-789");
        roundTrippedProcess.Data.Should().Contain("John Doe");
        roundTrippedProcess.Data.Should().Contain("item1");
    }

    [Fact]
    public void MapToDocument_Should_HandleNullResult()
    {
        // Arrange
        var process = CreateValidProcess() with { Result = null };

        // Act
        var document = ProcessMapper.MapToDocument(process);

        // Assert
        document.Result.Should().BeNull();
    }

    [Fact]
    public void MapToDocument_Should_SerializeResult()
    {
        // Arrange
        var result = new { success = true, orderId = "ORD-999" };
        var process = CreateValidProcess() with { Result = result };

        // Act
        var document = ProcessMapper.MapToDocument(process);

        // Assert
        document.Result.Should().NotBeNull();
        document.Result!["success"].AsBoolean.Should().BeTrue();
        document.Result["orderId"].Should().Be("ORD-999");
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
