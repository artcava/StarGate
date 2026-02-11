using FluentAssertions;
using StarGate.Core.Abstractions;
using StarGate.Core.Domain.Configuration;
using Xunit;

namespace StarGate.Core.Tests.Abstractions;

/// <summary>
/// Unit tests for EffectivePolicy and PolicySource record types.
/// Verifies immutability and property behavior.
/// </summary>
public class EffectivePolicyTests
{
    [Fact]
    public void EffectivePolicy_Should_BeImmutable()
    {
        // Arrange
        EffectivePolicy policy = CreateValidEffectivePolicy();

        // Act
        EffectivePolicy modified = policy with { Timeout = TimeSpan.FromMinutes(20) };

        // Assert
        policy.Timeout.Should().Be(TimeSpan.FromMinutes(10));
        modified.Timeout.Should().Be(TimeSpan.FromMinutes(20));
    }

    [Fact]
    public void PolicySource_Should_TrackOverrideOrigin()
    {
        // Arrange & Act
        PolicySource source = new()
        {
            TimeoutFromOverride = true,
            RetryPolicyFromOverride = false,
            ResultRetentionFromOverride = true,
            ConcurrencyLimitFromOverride = false
        };

        // Assert
        source.TimeoutFromOverride.Should().BeTrue();
        source.RetryPolicyFromOverride.Should().BeFalse();
    }

    private static EffectivePolicy CreateValidEffectivePolicy() => new()
    {
        ProcessType = "order",
        ClientId = "test-client",
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
        Source = new PolicySource
        {
            TimeoutFromOverride = false,
            RetryPolicyFromOverride = false,
            ResultRetentionFromOverride = false,
            ConcurrencyLimitFromOverride = false
        }
    };
}
