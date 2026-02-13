namespace StarGate.Integration.Tests.Caching;

using FluentAssertions;
using StarGate.Core.Domain;
using StarGate.Integration.Tests.Fixtures;
using Xunit;

public class RedisStateStoreIntegrationTests : IClassFixture<RedisFixture>, IAsyncLifetime
{
    private readonly RedisFixture _fixture;

    public RedisStateStoreIntegrationTests(RedisFixture fixture)
    {
        _fixture = fixture;
    }

    public Task InitializeAsync() => Task.CompletedTask;

    public async Task DisposeAsync()
    {
        await _fixture.FlushDatabaseAsync();
    }

    [Fact]
    public async Task SetProcessAsync_Should_CacheProcess_InRedis()
    {
        // Arrange
        var process = CreateValidProcess();

        // Act
        await _fixture.StateStore.SetProcessAsync(process);

        // Assert
        var retrieved = await _fixture.StateStore.GetProcessAsync(process.ProcessId);
        retrieved.Should().NotBeNull();
        retrieved!.ProcessId.Should().Be(process.ProcessId);
        retrieved.ClientProcessId.Should().Be(process.ClientProcessId);
        retrieved.Status.Should().Be(process.Status);
    }

    [Fact]
    public async Task GetProcessAsync_Should_ReturnNull_WhenNotCached()
    {
        // Act
        var result = await _fixture.StateStore.GetProcessAsync(Guid.NewGuid());

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task InvalidateAsync_Should_RemoveFromCache()
    {
        // Arrange
        var process = CreateValidProcess();
        await _fixture.StateStore.SetProcessAsync(process);

        // Verify cached
        var cached = await _fixture.StateStore.GetProcessAsync(process.ProcessId);
        cached.Should().NotBeNull();

        // Act
        await _fixture.StateStore.InvalidateAsync(process.ProcessId);

        // Assert
        var afterInvalidation = await _fixture.StateStore.GetProcessAsync(process.ProcessId);
        afterInvalidation.Should().BeNull();
    }

    [Fact]
    public async Task ExistsAsync_Should_ReturnTrue_WhenCached()
    {
        // Arrange
        var process = CreateValidProcess();
        await _fixture.StateStore.SetProcessAsync(process);

        // Act
        var exists = await _fixture.StateStore.ExistsAsync(process.ProcessId);

        // Assert
        exists.Should().BeTrue();
    }

    [Fact]
    public async Task ExistsAsync_Should_ReturnFalse_WhenNotCached()
    {
        // Act
        var exists = await _fixture.StateStore.ExistsAsync(Guid.NewGuid());

        // Assert
        exists.Should().BeFalse();
    }

    [Fact]
    public async Task TTL_Should_ExpireCache_AfterConfiguredTime()
    {
        // Arrange
        var process = CreateValidProcess();
        await _fixture.StateStore.SetProcessAsync(process);

        // Verify initially cached
        var cached = await _fixture.StateStore.GetProcessAsync(process.ProcessId);
        cached.Should().NotBeNull();

        // Act - Wait for TTL expiration (30 seconds + buffer)
        await Task.Delay(TimeSpan.FromSeconds(35));

        // Assert
        var afterExpiration = await _fixture.StateStore.GetProcessAsync(process.ProcessId);
        afterExpiration.Should().BeNull();
    }

    [Fact]
    public async Task SetProcessAsync_Should_SerializeComplexData()
    {
        // Arrange
        var complexData = new
        {
            orderId = "ORD-12345",
            customer = new
            {
                id = "CUST-001",
                name = "John Doe",
                emails = new[] { "john@example.com", "doe@example.com" }
            },
            items = new[]
            {
                new { sku = "SKU-001", quantity = 10, price = 99.99m },
                new { sku = "SKU-002", quantity = 5, price = 49.99m }
            },
            metadata = new Dictionary<string, string>
            {
                ["source"] = "web",
                ["priority"] = "high",
                ["timestamp"] = DateTime.UtcNow.ToString("O")
            }
        };

        var process = CreateValidProcess() with { Data = complexData };

        // Act
        await _fixture.StateStore.SetProcessAsync(process);

        // Assert
        var retrieved = await _fixture.StateStore.GetProcessAsync(process.ProcessId);
        retrieved.Should().NotBeNull();
        retrieved!.Data.Should().NotBeNull();

        var dataJson = retrieved.Data.ToString();
        dataJson.Should().Contain("ORD-12345");
        dataJson.Should().Contain("John Doe");
        dataJson.Should().Contain("SKU-001");
    }

    [Fact]
    public async Task SetProcessAsync_Should_HandleError_Serialization()
    {
        // Arrange
        var error = new ProcessError(
            "VALIDATION_ERROR",
            "Invalid order data",
            new { field = "orderId", value = "" });

        var process = CreateValidProcess() with
        {
            Status = ProcessStatus.Failed,
            Error = error
        };

        // Act
        await _fixture.StateStore.SetProcessAsync(process);

        // Assert
        var retrieved = await _fixture.StateStore.GetProcessAsync(process.ProcessId);
        retrieved.Should().NotBeNull();
        retrieved!.Error.Should().NotBeNull();
        retrieved.Error!.Code.Should().Be("VALIDATION_ERROR");
        retrieved.Error.Message.Should().Be("Invalid order data");
    }

    [Fact]
    public async Task ConcurrentOperations_Should_NotCorruptCache()
    {
        // Arrange
        var processes = Enumerable.Range(0, 10)
            .Select(_ => CreateValidProcess())
            .ToList();

        // Act - Concurrent writes
        var writeTasks = processes.Select(p =>
            _fixture.StateStore.SetProcessAsync(p));
        await Task.WhenAll(writeTasks);

        // Assert - All processes cached
        var readTasks = processes.Select(p =>
            _fixture.StateStore.GetProcessAsync(p.ProcessId));
        var results = await Task.WhenAll(readTasks);

        results.Should().AllSatisfy(r => r.Should().NotBeNull());
        results.Select(r => r!.ProcessId)
            .Should().BeEquivalentTo(processes.Select(p => p.ProcessId));
    }

    [Fact]
    public async Task ConcurrentInvalidation_Should_NotThrow()
    {
        // Arrange
        var process = CreateValidProcess();
        await _fixture.StateStore.SetProcessAsync(process);

        // Act - Concurrent invalidations
        var tasks = Enumerable.Range(0, 10).Select(_ =>
            _fixture.StateStore.InvalidateAsync(process.ProcessId));

        Func<Task> act = async () => await Task.WhenAll(tasks);

        // Assert
        await act.Should().NotThrowAsync();

        var exists = await _fixture.StateStore.ExistsAsync(process.ProcessId);
        exists.Should().BeFalse();
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
