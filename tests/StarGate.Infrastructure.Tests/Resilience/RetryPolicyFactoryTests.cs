using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Polly;
using StarGate.Infrastructure.Resilience;

namespace StarGate.Infrastructure.Tests.Resilience;

public class RetryPolicyFactoryTests
{
    private readonly RetryPolicyConfiguration _config;
    private readonly NullLogger _logger;

    public RetryPolicyFactoryTests()
    {
        _config = new RetryPolicyConfiguration
        {
            MaxRetryAttempts = 3,
            InitialDelaySeconds = 0.1,
            UseJitter = false
        };
        _logger = NullLogger.Instance;
    }

    [Fact]
    public async Task HttpRetryPolicy_Should_RetryOnHttpRequestException()
    {
        // Arrange
        var policy = RetryPolicyFactory.CreateHttpRetryPolicy(_config, _logger);
        var attemptCount = 0;

        // Act
        var act = async () => await policy.ExecuteAsync(async () =>
        {
            attemptCount++;
            await Task.CompletedTask;
            throw new HttpRequestException("Simulated failure");
        });

        // Assert
        await act.Should().ThrowAsync<HttpRequestException>();
        attemptCount.Should().Be(4); // Initial + 3 retries
    }

    [Fact]
    public async Task HttpRetryPolicy_Should_RetryOnTimeoutException()
    {
        // Arrange
        var policy = RetryPolicyFactory.CreateHttpRetryPolicy(_config, _logger);
        var attemptCount = 0;

        // Act
        var act = async () => await policy.ExecuteAsync(async () =>
        {
            attemptCount++;
            await Task.CompletedTask;
            throw new TimeoutException("Simulated timeout");
        });

        // Assert
        await act.Should().ThrowAsync<TimeoutException>();
        attemptCount.Should().Be(4); // Initial + 3 retries
    }

    [Fact]
    public async Task DatabaseRetryPolicy_Should_RetryOnTimeoutException()
    {
        // Arrange
        var policy = RetryPolicyFactory.CreateDatabaseRetryPolicy(_config, _logger);
        var attemptCount = 0;

        // Act
        var act = async () => await policy.ExecuteAsync(async () =>
        {
            attemptCount++;
            await Task.CompletedTask;
            throw new TimeoutException("Simulated timeout");
        });

        // Assert
        await act.Should().ThrowAsync<TimeoutException>();
        attemptCount.Should().Be(4); // Initial + 3 retries
    }

    [Fact]
    public async Task DatabaseRetryPolicy_Should_RetryOnIOException()
    {
        // Arrange
        var policy = RetryPolicyFactory.CreateDatabaseRetryPolicy(_config, _logger);
        var attemptCount = 0;

        // Act
        var act = async () => await policy.ExecuteAsync(async () =>
        {
            attemptCount++;
            await Task.CompletedTask;
            throw new IOException("Simulated IO error");
        });

        // Assert
        await act.Should().ThrowAsync<IOException>();
        attemptCount.Should().Be(4); // Initial + 3 retries
    }

    [Fact]
    public async Task DatabaseRetryPolicy_Should_RetryOnConnectionException()
    {
        // Arrange
        var policy = RetryPolicyFactory.CreateDatabaseRetryPolicy(_config, _logger);
        var attemptCount = 0;

        // Act
        var act = async () => await policy.ExecuteAsync(async () =>
        {
            attemptCount++;
            await Task.CompletedTask;
            throw new InvalidOperationException("Connection failed");
        });

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>();
        attemptCount.Should().Be(4); // Initial + 3 retries
    }

    [Fact]
    public async Task BrokerRetryPolicy_Should_RetryOnIOException()
    {
        // Arrange
        var policy = RetryPolicyFactory.CreateBrokerRetryPolicy(_config, _logger);
        var attemptCount = 0;

        // Act
        var act = async () => await policy.ExecuteAsync(async () =>
        {
            attemptCount++;
            await Task.CompletedTask;
            throw new IOException("Simulated IO error");
        });

        // Assert
        await act.Should().ThrowAsync<IOException>();
        attemptCount.Should().Be(4); // Initial + 3 retries
    }

    [Fact]
    public async Task GenericRetryPolicy_Should_RetryOnTransientException()
    {
        // Arrange
        var policy = RetryPolicyFactory.CreateGenericRetryPolicy(_config, _logger);
        var attemptCount = 0;

        // Act
        var act = async () => await policy.ExecuteAsync(async () =>
        {
            attemptCount++;
            await Task.CompletedTask;
            throw new TimeoutException("Simulated timeout");
        });

        // Assert
        await act.Should().ThrowAsync<TimeoutException>();
        attemptCount.Should().Be(4); // Initial + 3 retries
    }

    [Fact]
    public async Task RetryPolicy_Should_SucceedOnEventualSuccess()
    {
        // Arrange
        var policy = RetryPolicyFactory.CreateGenericRetryPolicy(_config, _logger);
        var attemptCount = 0;

        // Act
        await policy.ExecuteAsync(async () =>
        {
            attemptCount++;
            if (attemptCount < 3)
            {
                throw new TimeoutException("Transient failure");
            }
            await Task.CompletedTask;
        });

        // Assert
        attemptCount.Should().Be(3); // 2 failures + 1 success
    }

    [Fact]
    public async Task DatabaseRetryPolicy_Should_NotRetryOnNonTransientException()
    {
        // Arrange
        var policy = RetryPolicyFactory.CreateDatabaseRetryPolicy(_config, _logger);
        var attemptCount = 0;

        // Act
        var act = async () => await policy.ExecuteAsync(async () =>
        {
            attemptCount++;
            await Task.CompletedTask;
            throw new ArgumentException("Non-transient error");
        });

        // Assert
        await act.Should().ThrowAsync<ArgumentException>();
        attemptCount.Should().Be(1); // Only initial attempt, no retries
    }

    [Fact]
    public async Task GenericRetryPolicy_Should_NotRetryOnNonTransientException()
    {
        // Arrange
        var policy = RetryPolicyFactory.CreateGenericRetryPolicy(_config, _logger);
        var attemptCount = 0;

        // Act
        var act = async () => await policy.ExecuteAsync(async () =>
        {
            attemptCount++;
            await Task.CompletedTask;
            throw new ArgumentNullException("Non-transient error");
        });

        // Assert
        await act.Should().ThrowAsync<ArgumentNullException>();
        attemptCount.Should().Be(1); // Only initial attempt, no retries
    }

    [Fact]
    public async Task RetryPolicy_Should_RespectMaxRetryAttempts()
    {
        // Arrange
        var customConfig = new RetryPolicyConfiguration
        {
            MaxRetryAttempts = 5,
            InitialDelaySeconds = 0.01,
            UseJitter = false
        };
        var policy = RetryPolicyFactory.CreateGenericRetryPolicy(customConfig, _logger);
        var attemptCount = 0;

        // Act
        var act = async () => await policy.ExecuteAsync(async () =>
        {
            attemptCount++;
            await Task.CompletedTask;
            throw new TimeoutException("Always failing");
        });

        // Assert
        await act.Should().ThrowAsync<TimeoutException>();
        attemptCount.Should().Be(6); // Initial + 5 retries
    }
}
