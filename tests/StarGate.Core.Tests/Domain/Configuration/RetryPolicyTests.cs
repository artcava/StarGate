using FluentAssertions;
using StarGate.Core.Domain.Configuration;
using Xunit;

namespace StarGate.Core.Tests.Domain.Configuration;

/// <summary>
/// Unit tests for RetryPolicy record.
/// Validates immutability, required fields, and retry configuration.
/// </summary>
public class RetryPolicyTests
{
    [Fact]
    public void RetryPolicy_Should_BeImmutable()
    {
        // Arrange
        RetryPolicy policy = CreateValidRetryPolicy();

        // Act
        RetryPolicy modified = policy with { MaxAttempts = 5 };

        // Assert
        policy.MaxAttempts.Should().Be(3);
        modified.MaxAttempts.Should().Be(5);
        policy.Enabled.Should().Be(modified.Enabled);
    }

    [Fact]
    public void RetryPolicy_Should_RequireAllFields()
    {
        // Test validates the design intent with required properties
        RetryPolicy policy = CreateValidRetryPolicy();

        policy.Enabled.Should().BeDefined();
        policy.MaxAttempts.Should().BeGreaterThan(0);
        policy.InitialDelay.Should().BePositive();
        policy.BackoffStrategy.Should().BeDefined();
        policy.MaxDelay.Should().BePositive();
    }

    [Theory]
    [InlineData(1)]
    [InlineData(3)]
    [InlineData(10)]
    public void RetryPolicy_Should_AcceptValidMaxAttempts(int maxAttempts)
    {
        // Arrange & Act
        RetryPolicy policy = CreateValidRetryPolicy() with
        {
            MaxAttempts = maxAttempts
        };

        // Assert
        policy.MaxAttempts.Should().Be(maxAttempts);
        policy.MaxAttempts.Should().BeGreaterThan(0);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(10)]
    [InlineData(60)]
    public void RetryPolicy_Should_AcceptValidInitialDelay(int seconds)
    {
        // Arrange & Act
        RetryPolicy policy = CreateValidRetryPolicy() with
        {
            InitialDelay = TimeSpan.FromSeconds(seconds)
        };

        // Assert
        policy.InitialDelay.Should().Be(TimeSpan.FromSeconds(seconds));
        policy.InitialDelay.Should().BePositive();
    }

    [Theory]
    [InlineData(1)]
    [InlineData(5)]
    [InlineData(30)]
    public void RetryPolicy_Should_AcceptValidMaxDelay(int minutes)
    {
        // Arrange & Act
        RetryPolicy policy = CreateValidRetryPolicy() with
        {
            MaxDelay = TimeSpan.FromMinutes(minutes)
        };

        // Assert
        policy.MaxDelay.Should().Be(TimeSpan.FromMinutes(minutes));
        policy.MaxDelay.Should().BePositive();
    }

    [Theory]
    [InlineData(BackoffStrategy.None)]
    [InlineData(BackoffStrategy.Linear)]
    [InlineData(BackoffStrategy.Exponential)]
    public void RetryPolicy_Should_SupportAllBackoffStrategies(BackoffStrategy strategy)
    {
        // Arrange & Act
        RetryPolicy policy = CreateValidRetryPolicy() with
        {
            BackoffStrategy = strategy
        };

        // Assert
        policy.BackoffStrategy.Should().Be(strategy);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void RetryPolicy_Should_SupportEnabledFlag(bool enabled)
    {
        // Arrange & Act
        RetryPolicy policy = CreateValidRetryPolicy() with
        {
            Enabled = enabled
        };

        // Assert
        policy.Enabled.Should().Be(enabled);
    }

    private static RetryPolicy CreateValidRetryPolicy() => new()
    {
        Enabled = true,
        MaxAttempts = 3,
        InitialDelay = TimeSpan.FromSeconds(10),
        BackoffStrategy = BackoffStrategy.Exponential,
        MaxDelay = TimeSpan.FromMinutes(5)
    };
}
