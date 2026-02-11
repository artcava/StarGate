using FluentAssertions;
using StarGate.Core.Domain;
using System.Text.Json;
using Xunit;

namespace StarGate.Core.Tests.Domain;

/// <summary>
/// Unit tests for Process entity.
/// Validates immutability, required fields, optional fields, and data storage.
/// </summary>
public class ProcessTests
{
    [Fact]
    public void Process_Should_BeImmutable()
    {
        // Arrange
        Process process = CreateValidProcess();

        // Act
        Process modified = process with { Status = ProcessStatus.Completed };

        // Assert
        process.Status.Should().Be(ProcessStatus.Accepted);
        modified.Status.Should().Be(ProcessStatus.Completed);
        process.ProcessId.Should().Be(modified.ProcessId);
    }

    [Fact]
    public void Process_Should_RequireAllMandatoryFields()
    {
        // Arrange & Act & Assert
        Process process = CreateValidProcess();

        process.ProcessId.Should().NotBe(Guid.Empty);
        process.ClientProcessId.Should().NotBeNullOrEmpty();
        process.ProcessType.Should().NotBeNullOrEmpty();
        process.ClientId.Should().NotBeNullOrEmpty();
        process.Status.Should().BeDefined();
        process.CreatedAt.Should().NotBe(default);
        process.UpdatedAt.Should().NotBe(default);
        process.IdempotencyKey.Should().NotBeNullOrEmpty();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(50)]
    [InlineData(100)]
    public void Process_Progress_Should_BeWithinValidRange(int progress)
    {
        // Arrange & Act
        Process process = CreateValidProcess() with { Progress = progress };

        // Assert
        process.Progress.Should().BeInRange(0, 100);
    }

    [Fact]
    public void Process_Should_AllowOptionalFields()
    {
        // Arrange & Act
        Process process = new()
        {
            ProcessId = Guid.NewGuid(),
            ClientProcessId = "client-123",
            ProcessType = "order",
            ClientId = "test-client",
            Status = ProcessStatus.Accepted,
            Progress = 0,
            CurrentStep = null,
            Data = null,
            Result = null,
            Error = null,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            CompletedAt = null,
            IdempotencyKey = "key-123",
            Retryable = true
        };

        // Assert
        process.CurrentStep.Should().BeNull();
        process.Data.Should().BeNull();
        process.Result.Should().BeNull();
        process.Error.Should().BeNull();
        process.CompletedAt.Should().BeNull();
    }

    [Fact]
    public void Process_With_Should_CreateNewInstance()
    {
        // Arrange
        Process original = CreateValidProcess();
        ProcessStatus newStatus = ProcessStatus.Completed;

        // Act
        Process updated = original with
        {
            Status = newStatus,
            Progress = 100,
            CompletedAt = DateTime.UtcNow
        };

        // Assert
        original.Status.Should().Be(ProcessStatus.Accepted);
        updated.Status.Should().Be(ProcessStatus.Completed);
        updated.ProcessId.Should().Be(original.ProcessId);
        updated.CompletedAt.Should().NotBeNull();
    }

    [Fact]
    public void Process_Should_StoreData()
    {
        // Arrange
        object dataObject = new { OrderId = "ORD-001", Amount = 100.50m };
        JsonDocument data = JsonDocument.Parse(JsonSerializer.Serialize(dataObject));

        // Act
        Process process = CreateValidProcess() with { Data = data };

        // Assert
        process.Data.Should().NotBeNull();
        process.Data!.RootElement.GetProperty("OrderId").GetString().Should().Be("ORD-001");
        process.Data.RootElement.GetProperty("Amount").GetDecimal().Should().Be(100.50m);
    }

    [Fact]
    public void Process_Should_StoreResult()
    {
        // Arrange
        object resultObject = new { Status = "Success", TrackingNumber = "TRACK-123" };
        JsonDocument result = JsonDocument.Parse(JsonSerializer.Serialize(resultObject));

        // Act
        Process process = CreateValidProcess() with
        {
            Status = ProcessStatus.Completed,
            Result = result,
            CompletedAt = DateTime.UtcNow
        };

        // Assert
        process.Result.Should().NotBeNull();
        process.Result!.RootElement.GetProperty("Status").GetString().Should().Be("Success");
        process.Result.RootElement.GetProperty("TrackingNumber").GetString().Should().Be("TRACK-123");
    }

    [Fact]
    public void Process_Should_StoreErrorDetails()
    {
        // Arrange
        object detailsObject = new { Field = "Amount", Issue = "Must be positive" };
        JsonDocument details = JsonDocument.Parse(JsonSerializer.Serialize(detailsObject));
        ProcessError error = new(
            "VALIDATION_ERROR",
            "Invalid order data",
            details);

        // Act
        Process process = CreateValidProcess() with
        {
            Status = ProcessStatus.Failed,
            Error = error
        };

        // Assert
        process.Error.Should().NotBeNull();
        process.Error!.Code.Should().Be("VALIDATION_ERROR");
        process.Error.Message.Should().Be("Invalid order data");
        process.Error.Details.Should().NotBeNull();
    }

    [Fact]
    public void Process_Should_TrackTimestamps()
    {
        // Arrange
        DateTime createdAt = DateTime.UtcNow.AddMinutes(-5);
        DateTime updatedAt = DateTime.UtcNow.AddMinutes(-2);
        DateTime completedAt = DateTime.UtcNow;

        // Act
        Process process = CreateValidProcess() with
        {
            CreatedAt = createdAt,
            UpdatedAt = updatedAt,
            CompletedAt = completedAt
        };

        // Assert
        process.CreatedAt.Should().Be(createdAt);
        process.UpdatedAt.Should().Be(updatedAt);
        process.CompletedAt.Should().Be(completedAt);
        process.UpdatedAt.Should().BeAfter(process.CreatedAt);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Process_Should_SupportRetryableFlag(bool retryable)
    {
        // Arrange & Act
        Process process = CreateValidProcess() with { Retryable = retryable };

        // Assert
        process.Retryable.Should().Be(retryable);
    }

    private static Process CreateValidProcess() => new()
    {
        ProcessId = Guid.NewGuid(),
        ClientProcessId = "client-process-123",
        ProcessType = "order",
        ClientId = "test-client",
        Status = ProcessStatus.Accepted,
        Progress = 0,
        CreatedAt = DateTime.UtcNow,
        UpdatedAt = DateTime.UtcNow,
        IdempotencyKey = "idempotency-key-123",
        Retryable = true
    };
}
