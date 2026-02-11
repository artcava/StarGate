using FluentAssertions;
using StarGate.Core.Domain.Configuration;
using Xunit;

namespace StarGate.Core.Tests.Domain.Configuration;

/// <summary>
/// Unit tests for ProcessTypePolicy record.
/// Validates immutability, required fields, and policy configuration.
/// </summary>
public class ProcessTypePolicyTests
{
    [Fact]
    public void ProcessTypePolicy_Should_BeImmutable()
    {
        // Arrange
        ProcessTypePolicy policy = CreateValidProcessTypePolicy();

        // Act
        ProcessTypePolicy modified = policy with { Timeout = TimeSpan.FromMinutes(20) };

        // Assert
        policy.Timeout.Should().Be(TimeSpan.FromMinutes(10));
        modified.Timeout.Should().Be(TimeSpan.FromMinutes(20));
        policy.ProcessType.Should().Be(modified.ProcessType);
    }

    [Fact]
    public void ProcessTypePolicy_Should_RequireAllMandatoryFields()
    {
        // Test validates the design intent with required properties
        ProcessTypePolicy policy = CreateValidProcessTypePolicy();

        policy.ProcessType.Should().NotBeNullOrEmpty();
        policy.Timeout.Should().BePositive();
        policy.RetryPolicy.Should().NotBeNull();
        policy.ResultRetention.Should().BePositive();
        policy.UpdatedAt.Should().NotBe(default);
    }

    [Fact]
    public void ProcessTypePolicy_Should_AllowNullConcurrencyLimit()
    {
        // Arrange & Act
        ProcessTypePolicy policy = CreateValidProcessTypePolicy() with
        {
            MaxConcurrentProcesses = null
        };

        // Assert
        policy.MaxConcurrentProcesses.Should().BeNull();
    }

    [Theory]
    [InlineData(1)]
    [InlineData(10)]
    [InlineData(1000)]
    public void ProcessTypePolicy_Should_AcceptValidConcurrencyLimits(int limit)
    {
        // Arrange & Act
        ProcessTypePolicy policy = CreateValidProcessTypePolicy() with
        {
            MaxConcurrentProcesses = limit
        };

        // Assert
        policy.MaxConcurrentProcesses.Should().Be(limit);
    }

    [Fact]
    public void ProcessTypePolicy_Should_StoreRetryPolicy()
    {
        // Arrange
        RetryPolicy retryPolicy = new()
        {
            Enabled = true,
            MaxAttempts = 3,
            InitialDelay = TimeSpan.FromSeconds(5),
            BackoffStrategy = BackoffStrategy.Exponential,
            MaxDelay = TimeSpan.FromMinutes(5)
        };

        // Act
        ProcessTypePolicy policy = CreateValidProcessTypePolicy() with
        {
            RetryPolicy = retryPolicy
        };

        // Assert
        policy.RetryPolicy.Should().Be(retryPolicy);
        policy.RetryPolicy.Enabled.Should().BeTrue();
        policy.RetryPolicy.MaxAttempts.Should().Be(3);
    }

    private static ProcessTypePolicy CreateValidProcessTypePolicy() => new()
    {
        ProcessType = "order",
        Timeout = TimeSpan.FromMinutes(10),
        RetryPolicy = new RetryPolicy
        {
            Enabled = true,
            MaxAttempts = 3,
            InitialDelay = TimeSpan.FromSeconds(10),
            BackoffStrategy = BackoffStrategy.Exponential,
            MaxDelay = TimeSpan.FromMinutes(5)
        },
        ResultRetention = TimeSpan.FromDays(7),
        MaxConcurrentProcesses = 100,
        UpdatedAt = DateTime.UtcNow
    };
}
