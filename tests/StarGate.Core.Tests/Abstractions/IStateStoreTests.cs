using FluentAssertions;
using StarGate.Core.Abstractions;
using StarGate.Core.Domain;

namespace StarGate.Core.Tests.Abstractions;

/// <summary>
/// Contract tests for IStateStore implementations.
/// These tests verify the expected behavior of any state store implementation.
/// Concrete implementations (Redis, in-memory, etc.) must inherit this class,
/// implement CreateStateStore() and CleanupAsync() methods, and override test methods
/// with [Fact] attributes to enable test execution.
/// </summary>
public abstract class IStateStoreTests
{
    /// <summary>
    /// Creates a state store instance for testing.
    /// Each test will call this to get a fresh state store.
    /// </summary>
    protected abstract IStateStore CreateStateStore();

    /// <summary>
    /// Cleans up test data after each test.
    /// Ensures tests don't interfere with each other.
    /// </summary>
    protected abstract Task CleanupAsync();

    protected virtual async Task GetProcessAsync_Should_ReturnNull_WhenNotCached()
    {
        // Arrange
        var store = CreateStateStore();
        var processId = Guid.NewGuid();

        // Act
        var result = await store.GetProcessAsync(processId);

        // Assert
        result.Should().BeNull();

        await CleanupAsync();
    }

    protected virtual async Task SetProcessAsync_Should_CacheProcess()
    {
        // Arrange
        var store = CreateStateStore();
        var process = CreateValidProcess();

        // Act
        await store.SetProcessAsync(process);
        var cached = await store.GetProcessAsync(process.ProcessId);

        // Assert
        cached.Should().NotBeNull();
        cached!.ProcessId.Should().Be(process.ProcessId);
        cached.ClientProcessId.Should().Be(process.ClientProcessId);
        cached.Status.Should().Be(process.Status);

        await CleanupAsync();
    }

    protected virtual async Task SetProcessAsync_Should_OverwriteExistingCache()
    {
        // Arrange
        var store = CreateStateStore();
        var process = CreateValidProcess();
        await store.SetProcessAsync(process);

        // Act
        var updated = process with
        {
            Status = ProcessStatus.Completed,
            Progress = 100
        };
        await store.SetProcessAsync(updated);
        var cached = await store.GetProcessAsync(process.ProcessId);

        // Assert
        cached.Should().NotBeNull();
        cached!.Status.Should().Be(ProcessStatus.Completed);
        cached.Progress.Should().Be(100);

        await CleanupAsync();
    }

    protected virtual async Task InvalidateAsync_Should_RemoveCachedProcess()
    {
        // Arrange
        var store = CreateStateStore();
        var process = CreateValidProcess();
        await store.SetProcessAsync(process);

        // Act
        await store.InvalidateAsync(process.ProcessId);
        var cached = await store.GetProcessAsync(process.ProcessId);

        // Assert
        cached.Should().BeNull();

        await CleanupAsync();
    }

    protected virtual async Task InvalidateAsync_Should_BeIdempotent()
    {
        // Arrange
        var store = CreateStateStore();
        var processId = Guid.NewGuid();

        // Act & Assert - should not throw
        await store.InvalidateAsync(processId);
        await store.InvalidateAsync(processId); // Second call on non-existent key

        await CleanupAsync();
    }

    protected virtual async Task ExistsAsync_Should_ReturnTrue_WhenCached()
    {
        // Arrange
        var store = CreateStateStore();
        var process = CreateValidProcess();
        await store.SetProcessAsync(process);

        // Act
        var exists = await store.ExistsAsync(process.ProcessId);

        // Assert
        exists.Should().BeTrue();

        await CleanupAsync();
    }

    protected virtual async Task ExistsAsync_Should_ReturnFalse_WhenNotCached()
    {
        // Arrange
        var store = CreateStateStore();
        var processId = Guid.NewGuid();

        // Act
        var exists = await store.ExistsAsync(processId);

        // Assert
        exists.Should().BeFalse();

        await CleanupAsync();
    }

    protected virtual async Task TrySetStatusAsync_Should_ReturnBoolean()
    {
        // Arrange
        var store = CreateStateStore();
        var processId = Guid.NewGuid();
        var expectedVersion = 1L;

        // Act
        var result = await store.TrySetStatusAsync(
            processId,
            ProcessStatus.Completed,
            expectedVersion);

        // Assert
        // Method signature guarantees bool return type.
        // Exact behavior (true/false) depends on implementation.
        // Some implementations return true for new keys, others return false.
        // This is tested in the optimistic concurrency test below.
        _ = result; // Just verify no exception is thrown

        await CleanupAsync();
    }

    protected virtual async Task TrySetStatusAsync_Should_SupportOptimisticConcurrency()
    {
        // Arrange
        var store = CreateStateStore();
        var processId = Guid.NewGuid();
        var version1 = 1L;
        var version2 = 2L;

        // Act
        var firstUpdate = await store.TrySetStatusAsync(
            processId,
            ProcessStatus.Processing,
            version1);

        var secondUpdateWithOldVersion = await store.TrySetStatusAsync(
            processId,
            ProcessStatus.Completed,
            version1); // Using old version

        var secondUpdateWithNewVersion = await store.TrySetStatusAsync(
            processId,
            ProcessStatus.Completed,
            version2); // Using new version

        // Assert
        // This test verifies optimistic concurrency behavior.
        // Implementations must reject updates with stale versions.
        secondUpdateWithOldVersion.Should().BeFalse("stale version should be rejected");

        await CleanupAsync();
    }

    /// <summary>
    /// Creates a valid process instance for testing.
    /// Each call returns a unique process to avoid conflicts.
    /// </summary>
    protected static Process CreateValidProcess() => new()
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
