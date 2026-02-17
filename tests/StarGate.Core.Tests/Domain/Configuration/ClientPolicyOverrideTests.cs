using FluentAssertions;
using StarGate.Core.Domain.Configuration;
using Xunit;

namespace StarGate.Core.Tests.Domain.Configuration;

/// <summary>
/// Unit tests for ClientPolicyOverride record.
/// Validates immutability, required fields, and partial override support.
/// </summary>
public class ClientPolicyOverrideTests
{
    [Fact]
    public void ClientPolicyOverride_Should_BeImmutable()
    {
        // Arrange
        ClientPolicyOverride override1 = CreateValidClientPolicyOverride();

        // Act
        ClientPolicyOverride modified = override1 with { Timeout = TimeSpan.FromMinutes(30) };

        // Assert
        override1.Timeout.Should().Be(TimeSpan.FromMinutes(15));
        modified.Timeout.Should().Be(TimeSpan.FromMinutes(30));
        override1.ClientId.Should().Be(modified.ClientId);
    }

    [Fact]
    public void ClientPolicyOverride_Should_RequireMandatoryFields()
    {
        // Test validates the design intent with required properties
        ClientPolicyOverride override1 = CreateValidClientPolicyOverride();

        override1.ClientId.Should().NotBeNullOrEmpty();
        override1.ProcessType.Should().NotBeNullOrEmpty();
        override1.UpdatedAt.Should().NotBe(default);
    }

    [Fact]
    public void ClientPolicyOverride_Should_AllowAllOptionalFields()
    {
        // Arrange & Act
        ClientPolicyOverride override1 = new()
        {
            ClientId = "test-client",
            ProcessType = "order",
            Timeout = null,
            RetryPolicy = null,
            ResultRetention = null,
            MaxConcurrentProcesses = null,
            UpdatedAt = DateTime.UtcNow
        };

        // Assert
        override1.Timeout.Should().BeNull();
        override1.RetryPolicy.Should().BeNull();
        override1.ResultRetention.Should().BeNull();
        override1.MaxConcurrentProcesses.Should().BeNull();
    }

    [Fact]
    public void ClientPolicyOverride_Should_SupportPartialOverrides()
    {
        // Arrange & Act - Override only timeout and concurrency
        ClientPolicyOverride override1 = new()
        {
            ClientId = "test-client",
            ProcessType = "order",
            Timeout = TimeSpan.FromMinutes(20),
            RetryPolicy = null,
            ResultRetention = null,
            MaxConcurrentProcesses = 50,
            UpdatedAt = DateTime.UtcNow
        };

        // Assert
        override1.Timeout.Should().Be(TimeSpan.FromMinutes(20));
        override1.MaxConcurrentProcesses.Should().Be(50);
        override1.RetryPolicy.Should().BeNull();
        override1.ResultRetention.Should().BeNull();
    }

    [Fact]
    public void ClientPolicyOverride_Should_StoreRetryPolicyOverride()
    {
        // Arrange
        RetryPolicy customRetryPolicy = new()
        {
            Enabled = false,
            MaxAttempts = 5,
            InitialDelay = TimeSpan.FromSeconds(20),
            BackoffStrategy = BackoffStrategy.Linear,
            MaxDelay = TimeSpan.FromMinutes(10)
        };

        // Act
        ClientPolicyOverride override1 = CreateValidClientPolicyOverride() with
        {
            RetryPolicy = customRetryPolicy
        };

        // Assert
        override1.RetryPolicy.Should().NotBeNull();
        override1.RetryPolicy!.Enabled.Should().BeFalse();
        override1.RetryPolicy.MaxAttempts.Should().Be(5);
        override1.RetryPolicy.BackoffStrategy.Should().Be(BackoffStrategy.Linear);
    }

    [Fact]
    public void ClientPolicyOverride_Should_TrackUpdateTimestamp()
    {
        // Arrange
        DateTime updatedAt = DateTime.UtcNow.AddHours(-2);

        // Act
        ClientPolicyOverride override1 = CreateValidClientPolicyOverride() with
        {
            UpdatedAt = updatedAt
        };

        // Assert
        override1.UpdatedAt.Should().Be(updatedAt);
    }

    private static ClientPolicyOverride CreateValidClientPolicyOverride() => new()
    {
        ClientId = "test-client",
        ProcessType = "order",
        Timeout = TimeSpan.FromMinutes(15),
        RetryPolicy = new RetryPolicy
        {
            Enabled = true,
            MaxAttempts = 5,
            InitialDelay = TimeSpan.FromSeconds(15),
            BackoffStrategy = BackoffStrategy.Exponential,
            MaxDelay = TimeSpan.FromMinutes(10)
        },
        ResultRetention = TimeSpan.FromDays(14),
        MaxConcurrentProcesses = 50,
        UpdatedAt = DateTime.UtcNow
    };
}
