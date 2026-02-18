using FluentAssertions;
using StarGate.Core.Domain.Configuration;
using StarGate.Integration.Tests.Fixtures;
using Xunit;

namespace StarGate.Integration.Tests.Services;

public class PolicyProviderIntegrationTests : IClassFixture<PolicyIntegrationFixture>, IAsyncLifetime
{
    private readonly PolicyIntegrationFixture _fixture;

    public PolicyProviderIntegrationTests(PolicyIntegrationFixture fixture)
    {
        _fixture = fixture;
    }

    public Task InitializeAsync() => Task.CompletedTask;

    public async Task DisposeAsync()
    {
        await _fixture.ClearPoliciesAsync();
    }

    [Fact]
    public async Task GetPolicyAsync_Should_ReturnTypeDefaultPolicy_WhenNoClientOverride()
    {
        // Arrange
        var processType = "order";
        var clientId = "client-001";

        // Act
        var policy = await _fixture.PolicyProvider.GetPolicyAsync(
            processType,
            clientId);

        // Assert
        policy.Should().NotBeNull();
        policy!.TimeoutSeconds.Should().Be(300);
        policy.MaxRetryAttempts.Should().Be(3);
        policy.MaxConcurrentExecutions.Should().Be(10);
        policy.Priority.Should().Be(5);
    }

    [Fact]
    public async Task GetPolicyAsync_Should_ApplyClientOverride_WhenConfigured()
    {
        // Arrange
        var processType = "order";
        var clientId = "premium-client";

        var clientOverride = new ClientPolicyOverride
        {
            Id = Guid.NewGuid().ToString(),
            ClientId = clientId,
            ProcessType = processType,
            TimeoutSeconds = 600, // Override: 10 minutes instead of 5
            MaxRetryAttempts = 5, // Override: 5 retries instead of 3
            MaxConcurrentExecutions = 20 // Override: 20 concurrent instead of 10
        };

        await _fixture.PolicyRepository.SaveClientOverrideAsync(clientOverride);

        // Act
        var policy = await _fixture.PolicyProvider.GetPolicyAsync(
            processType,
            clientId);

        // Assert
        policy.Should().NotBeNull();
        policy!.TimeoutSeconds.Should().Be(600, "client override should be applied");
        policy.MaxRetryAttempts.Should().Be(5, "client override should be applied");
        policy.MaxConcurrentExecutions.Should().Be(20, "client override should be applied");
        policy.Priority.Should().Be(5, "priority not overridden, should use type default");
    }

    [Fact]
    public async Task GetPolicyAsync_Should_CacheResolvedPolicy()
    {
        // Arrange
        var processType = "order";
        var clientId = "client-002";

        // Act - First call
        var policy1 = await _fixture.PolicyProvider.GetPolicyAsync(
            processType,
            clientId);

        // Act - Second call (should hit cache)
        var policy2 = await _fixture.PolicyProvider.GetPolicyAsync(
            processType,
            clientId);

        // Assert
        policy1.Should().NotBeNull();
        policy2.Should().NotBeNull();
        policy1.Should().BeSameAs(policy2, "cached policy should be returned");
    }

    [Fact]
    public async Task GetPolicyAsync_Should_InvalidateCache_WhenPolicyUpdated()
    {
        // Arrange
        var processType = "payment";
        var clientId = "client-003";

        // Get initial policy (caches it)
        var initialPolicy = await _fixture.PolicyProvider.GetPolicyAsync(
            processType,
            clientId);

        // Update type policy
        var typePolicy = await _fixture.PolicyRepository.GetTypePolicyAsync(processType);
        typePolicy = typePolicy! with { TimeoutSeconds = 120 };
        await _fixture.PolicyRepository.SaveTypePolicyAsync(typePolicy);

        // Invalidate cache
        await _fixture.PolicyProvider.InvalidateCacheAsync(processType, clientId);

        // Act - Get policy again (should reload from repository)
        var updatedPolicy = await _fixture.PolicyProvider.GetPolicyAsync(
            processType,
            clientId);

        // Assert
        initialPolicy!.TimeoutSeconds.Should().Be(60);
        updatedPolicy!.TimeoutSeconds.Should().Be(120, "updated policy should be loaded");
    }

    [Fact]
    public async Task GetPolicyAsync_Should_ReturnNull_WhenProcessTypeNotFound()
    {
        // Arrange
        var processType = "nonexistent-type";
        var clientId = "client-004";

        // Act
        var policy = await _fixture.PolicyProvider.GetPolicyAsync(
            processType,
            clientId);

        // Assert
        policy.Should().BeNull();
    }

    [Fact]
    public async Task GetPolicyAsync_Should_IgnoreInvalidClientOverride()
    {
        // Arrange
        var processType = "order";
        var clientId = "invalid-override-client";

        // Create invalid override (timeout out of range)
        var invalidOverride = new ClientPolicyOverride
        {
            Id = Guid.NewGuid().ToString(),
            ClientId = clientId,
            ProcessType = processType,
            TimeoutSeconds = 5000 // Invalid: exceeds max 3600
        };

        await _fixture.PolicyRepository.SaveClientOverrideAsync(invalidOverride);

        // Act
        var policy = await _fixture.PolicyProvider.GetPolicyAsync(
            processType,
            clientId);

        // Assert - Should use type default, ignoring invalid override
        policy.Should().NotBeNull();
        policy!.TimeoutSeconds.Should().Be(300, "should use type default due to invalid override");
    }
}
