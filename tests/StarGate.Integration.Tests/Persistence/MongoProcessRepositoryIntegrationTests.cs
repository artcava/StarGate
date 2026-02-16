using System.Text.Json;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using StarGate.Core.Domain;
using StarGate.Infrastructure.Persistence;
using StarGate.Integration.Tests.Fixtures;
using Xunit;

namespace StarGate.Integration.Tests.Persistence;

[Trait("Category", "Integration")]
public class MongoProcessRepositoryIntegrationTests : IClassFixture<MongoDbFixture>, IAsyncLifetime
{
    private readonly MongoDbFixture _fixture;
    private readonly MongoProcessRepository _repository;

    public MongoProcessRepositoryIntegrationTests(MongoDbFixture fixture)
    {
        _fixture = fixture;
        _repository = new MongoProcessRepository(
            _fixture.Database,
            NullLogger<MongoProcessRepository>.Instance);
        
        // TEMPORARY DEBUG: Print connection string for MongoDB Compass
        Console.WriteLine($"\n\n=== MONGODB CONNECTION STRING ===");
        Console.WriteLine(_fixture.ConnectionString);
        Console.WriteLine($"Database: stargate-test");
        Console.WriteLine($"Collection: processes");
        Console.WriteLine($"==================================\n\n");
    }

    public Task InitializeAsync() => Task.CompletedTask;

    public async Task DisposeAsync()
    {
        await _fixture.ResetDatabaseAsync();
    }

    [Fact]
    public async Task CreateAsync_Should_PersistProcess_InDatabase()
    {
        // Arrange
        var process = CreateValidProcess();

        // Act
        var created = await _repository.CreateAsync(process);

        // Assert
        created.Should().NotBeNull();
        created.ProcessId.Should().Be(process.ProcessId);
        
        // DEBUG: Print ProcessId for manual inspection
        Console.WriteLine($"Created ProcessId: {process.ProcessId}");
        Console.WriteLine($"Waiting 5 seconds for manual inspection...");
        await Task.Delay(5000); // Wait to allow manual inspection

        // Verify persistence
        var retrieved = await _repository.GetByIdAsync(process.ProcessId);
        retrieved.Should().NotBeNull();
        retrieved!.ProcessId.Should().Be(process.ProcessId);
        retrieved.ClientProcessId.Should().Be(process.ClientProcessId);
        retrieved.Status.Should().Be(process.Status);
    }

    [Fact]
    public async Task CreateAsync_Should_ThrowException_WhenDuplicateProcessId()
    {
        // Arrange
        var process = CreateValidProcess();
        await _repository.CreateAsync(process);

        var duplicate = process with { IdempotencyKey = Guid.NewGuid().ToString() };

        // Act
        Func<Task> act = async () => await _repository.CreateAsync(duplicate);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage($"Process with ID '{process.ProcessId}' already exists");
    }

    [Fact]
    public async Task CreateAsync_Should_ThrowException_WhenDuplicateClientProcessId()
    {
        // Arrange
        var process = CreateValidProcess();
        await _repository.CreateAsync(process);

        var duplicate = process with 
        { 
            ProcessId = Guid.NewGuid(),
            IdempotencyKey = Guid.NewGuid().ToString()
        };

        // Act
        Func<Task> act = async () => await _repository.CreateAsync(duplicate);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*already exists*");
    }

    [Fact]
    public async Task CreateAsync_Should_ThrowException_WhenDuplicateIdempotencyKey()
    {
        // Arrange
        var process = CreateValidProcess();
        await _repository.CreateAsync(process);

        var duplicate = process with 
        { 
            ProcessId = Guid.NewGuid(),
            ClientProcessId = $"client-{Guid.NewGuid()}"
        };

        // Act
        Func<Task> act = async () => await _repository.CreateAsync(duplicate);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task GetByIdAsync_Should_ReturnNull_WhenProcessNotFound()
    {
        // Arrange
        var nonExistentId = Guid.NewGuid();

        // Act
        var result = await _repository.GetByIdAsync(nonExistentId);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task GetByClientProcessIdAsync_Should_ReturnProcess_WhenExists()
    {
        // Arrange
        var process = CreateValidProcess();
        await _repository.CreateAsync(process);

        // Act
        var result = await _repository.GetByClientProcessIdAsync(
            process.ClientId,
            process.ClientProcessId);

        // Assert
        result.Should().NotBeNull();
        result!.ProcessId.Should().Be(process.ProcessId);
        result.ClientId.Should().Be(process.ClientId);
        result.ClientProcessId.Should().Be(process.ClientProcessId);
    }

    [Fact]
    public async Task GetByClientProcessIdAsync_Should_ReturnNull_WhenNotFound()
    {
        // Act
        var result = await _repository.GetByClientProcessIdAsync(
            "nonexistent-client",
            "nonexistent-process");

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task UpdateAsync_Should_ModifyExistingProcess()
    {
        // Arrange
        var process = CreateValidProcess();
        await _repository.CreateAsync(process);

        var updated = process with
        {
            Status = ProcessStatus.Processing,
            Progress = 50,
            CurrentStep = "processing-step-1",
            UpdatedAt = DateTime.UtcNow
        };

        // Act
        var result = await _repository.UpdateAsync(updated);

        // Assert
        result.Status.Should().Be(ProcessStatus.Processing);
        result.Progress.Should().Be(50);
        result.CurrentStep.Should().Be("processing-step-1");

        // Verify persistence
        var retrieved = await _repository.GetByIdAsync(process.ProcessId);
        retrieved!.Status.Should().Be(ProcessStatus.Processing);
        retrieved.Progress.Should().Be(50);
    }

    [Fact]
    public async Task UpdateAsync_Should_ThrowException_WhenProcessNotFound()
    {
        // Arrange
        var process = CreateValidProcess();

        // Act
        Func<Task> act = async () => await _repository.UpdateAsync(process);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage($"Process with ID '{process.ProcessId}' not found");
    }

    [Fact]
    public async Task CreateAsync_Should_SerializeComplexData_Correctly()
    {
        // Arrange
        var complexDataJson = @"{
            ""orderId"": ""ORD-12345"",
            ""customer"": {
                ""id"": ""CUST-001"",
                ""name"": ""John Doe"",
                ""email"": ""john@example.com""
            },
            ""items"": [
                { ""sku"": ""SKU-001"", ""quantity"": 10, ""price"": 99.99 },
                { ""sku"": ""SKU-002"", ""quantity"": 5, ""price"": 49.99 }
            ],
            ""metadata"": {
                ""source"": ""web"",
                ""priority"": ""high""
            }
        }";

        var complexData = JsonDocument.Parse(complexDataJson);
        var process = CreateValidProcess() with { Data = complexData };

        // Act
        await _repository.CreateAsync(process);

        // Assert
        var retrieved = await _repository.GetByIdAsync(process.ProcessId);
        retrieved.Should().NotBeNull();
        retrieved!.Data.Should().NotBeNull();
        
        var dataString = retrieved.Data!.RootElement.GetRawText();
        dataString.Should().Contain("orderId");
        dataString.Should().Contain("customer");
        dataString.Should().Contain("items");
    }

    [Fact]
    public async Task UpdateAsync_Should_HandleError_Correctly()
    {
        // Arrange
        var process = CreateValidProcess();
        await _repository.CreateAsync(process);

        var errorDetailsJson = @"{ ""field"": ""orderId"", ""value"": """" }";
        var errorDetails = JsonDocument.Parse(errorDetailsJson);
        
        var error = new ProcessError(
            "VALIDATION_ERROR",
            "Invalid order data",
            errorDetails);

        var updated = process with
        {
            Status = ProcessStatus.Failed,
            Error = error,
            CompletedAt = DateTime.UtcNow
        };

        // Act
        await _repository.UpdateAsync(updated);

        // Assert
        var retrieved = await _repository.GetByIdAsync(process.ProcessId);
        retrieved!.Status.Should().Be(ProcessStatus.Failed);
        retrieved.Error.Should().NotBeNull();
        retrieved.Error!.Code.Should().Be("VALIDATION_ERROR");
        retrieved.Error.Message.Should().Be("Invalid order data");
        retrieved.Error.Details.Should().NotBeNull();
    }

    [Fact]
    public async Task ConcurrentCreates_Should_HandleRaceCondition_WithIdempotencyKey()
    {
        // Arrange
        var idempotencyKey = Guid.NewGuid().ToString();
        var process1 = CreateValidProcess() with { IdempotencyKey = idempotencyKey };
        var process2 = CreateValidProcess() with 
        { 
            ProcessId = Guid.NewGuid(),
            IdempotencyKey = idempotencyKey 
        };

        // Act
        var tasks = new[]
        {
            Task.Run(async () => await _repository.CreateAsync(process1)),
            Task.Run(async () => await _repository.CreateAsync(process2))
        };

        var results = await Task.WhenAll(tasks.Select(async t =>
        {
            try
            {
                await t;
                return true;
            }
            catch (InvalidOperationException)
            {
                return false;
            }
        }));

        // Assert
        results.Count(r => r).Should().Be(1, "only one process should succeed");
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
}
