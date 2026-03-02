using StarGate.Core.Abstractions;
using StarGate.Core.Domain;
using StarGate.Integration.Tests.Fixtures;
using Xunit;

namespace StarGate.Integration.Tests.Infrastructure;

/// <summary>
/// Base class for MongoDB repository integration tests.
/// Provides common infrastructure and helper methods.
/// </summary>
public abstract class MongoRepositoryTestBase : IClassFixture<MongoDbFixture>, IAsyncLifetime
{
    private readonly MongoDbFixture _fixture;

    /// <summary>
    /// Gets the process repository instance for testing.
    /// </summary>
    protected IProcessRepository Repository { get; }

    /// <summary>
    /// Gets the MongoDB fixture.
    /// </summary>
    protected MongoDbFixture Fixture => _fixture;

    protected MongoRepositoryTestBase(MongoDbFixture fixture, IProcessRepository repository)
    {
        _fixture = fixture ?? throw new ArgumentNullException(nameof(fixture));
        Repository = repository ?? throw new ArgumentNullException(nameof(repository));
    }

    /// <summary>
    /// Called before each test method.
    /// Override to add custom initialization logic.
    /// </summary>
    public virtual Task InitializeAsync() => Task.CompletedTask;

    /// <summary>
    /// Called after each test method.
    /// Resets the database to ensure test isolation.
    /// </summary>
    public virtual async Task DisposeAsync()
    {
        await _fixture.ResetDatabaseAsync();
    }

    /// <summary>
    /// Creates a valid test process with default values.
    /// </summary>
    /// <param name="status">Process status. Default is Accepted.</param>
    /// <param name="timeoutAt">Optional timeout timestamp.</param>
    /// <returns>A valid Process instance ready for testing.</returns>
    protected static Process CreateTestProcess(
        ProcessStatus status = ProcessStatus.Accepted,
        DateTime? timeoutAt = null)
    {
        return new Process
        {
            ProcessId = Guid.NewGuid(),
            ClientProcessId = $"client-{Guid.NewGuid()}",
            ProcessType = "test-order",
            ClientId = "test-client",
            Status = status,
            Progress = 0,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            TimeoutAt = timeoutAt,
            IdempotencyKey = Guid.NewGuid().ToString(),
            Retryable = true
        };
    }

    /// <summary>
    /// Creates a test process with custom properties.
    /// </summary>
    /// <param name="configure">Action to configure the process.</param>
    /// <returns>A configured Process instance.</returns>
    protected static Process CreateTestProcess(Action<Process> configure)
    {
        var process = CreateTestProcess();
        configure(process);
        return process;
    }
}
