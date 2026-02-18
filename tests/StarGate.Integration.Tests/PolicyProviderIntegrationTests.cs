using FluentAssertions;
using StarGate.Core.Domain.Configuration;
using StarGate.Integration.Tests.Fixtures;
using Xunit;

namespace StarGate.Integration.Tests;

/// <summary>
/// Integration tests for PolicyProvider.
/// Tests end-to-end policy resolution, caching, and override precedence.
/// </summary>
public class PolicyProviderIntegrationTests : IClassFixture<PolicyProviderFixture>
{
    private readonly PolicyProviderFixture _fixture;

    public PolicyProviderIntegrationTests(PolicyProviderFixture fixture)
    {
        _fixture = fixture;
    }

    #region Policy Resolution Tests

    [Fact]
    public async Task GetEffectivePolicyAsync_Should_ReturnTypeDefault_WhenNoOverrideExists()
    {
        // Arrange
        var clientId = "regular-client";
        var processType = "shipping";

        // Act
        var effectivePolicy = await _fixture.PolicyProvider.GetEffectivePolicyAsync(
            clientId,
            processType);

        // Assert
        effectivePolicy.Should().NotBeNull();
        effectivePolicy.ProcessType.Should().Be(processType);
        effectivePolicy.ClientId.Should().Be(clientId);
        
        // Should match type default (shipping policy)
        effectivePolicy.Timeout.Should().Be(TimeSpan.FromMinutes(10));
        effectivePolicy.RetryPolicy.MaxAttempts.Should().Be(2);
        effectivePolicy.RetryPolicy.BackoffStrategy.Should().Be(BackoffStrategy.Linear);
        effectivePolicy.ResultRetention.Should().Be(TimeSpan.FromDays(60));
        effectivePolicy.MaxConcurrentProcesses.Should().Be(5);
        
        // All values from type default
        effectivePolicy.Source.TimeoutFromOverride.Should().BeFalse();
        effectivePolicy.Source.RetryPolicyFromOverride.Should().BeFalse();
        effectivePolicy.Source.ResultRetentionFromOverride.Should().BeFalse();
        effectivePolicy.Source.ConcurrencyLimitFromOverride.Should().BeFalse();
    }

    [Fact]
    public async Task GetEffectivePolicyAsync_Should_ApplyClientOverride_WhenExists()
    {
        // Arrange
        var clientId = "premium-client";
        var processType = "order";

        // Act
        var effectivePolicy = await _fixture.PolicyProvider.GetEffectivePolicyAsync(
            clientId,
            processType);

        // Assert
        effectivePolicy.Should().NotBeNull();
        effectivePolicy.ProcessType.Should().Be(processType);
        effectivePolicy.ClientId.Should().Be(clientId);
        
        // Should use override values
        effectivePolicy.Timeout.Should().Be(TimeSpan.FromMinutes(15)); // From override
        effectivePolicy.RetryPolicy.MaxAttempts.Should().Be(5); // From override
        effectivePolicy.ResultRetention.Should().Be(TimeSpan.FromDays(30)); // From type default
        effectivePolicy.MaxConcurrentProcesses.Should().Be(50); // From override
        
        // Source tracking
        effectivePolicy.Source.TimeoutFromOverride.Should().BeTrue();
        effectivePolicy.Source.RetryPolicyFromOverride.Should().BeTrue();
        effectivePolicy.Source.ResultRetentionFromOverride.Should().BeFalse(); // null in override
        effectivePolicy.Source.ConcurrencyLimitFromOverride.Should().BeTrue();
    }

    [Fact]
    public async Task GetEffectivePolicyAsync_Should_MergePartialOverride_Correctly()
    {
        // Arrange
        var clientId = "standard-client";
        var processType = "payment";

        // Act
        var effectivePolicy = await _fixture.PolicyProvider.GetEffectivePolicyAsync(
            clientId,
            processType);

        // Assert
        effectivePolicy.Should().NotBeNull();
        
        // Partial override: only timeout and retention overridden
        effectivePolicy.Timeout.Should().Be(TimeSpan.FromMinutes(1)); // From override
        effectivePolicy.RetryPolicy.MaxAttempts.Should().Be(5); // From type default
        effectivePolicy.ResultRetention.Should().Be(TimeSpan.FromDays(30)); // From override
        effectivePolicy.MaxConcurrentProcesses.Should().Be(20); // From type default
        
        // Source tracking
        effectivePolicy.Source.TimeoutFromOverride.Should().BeTrue();
        effectivePolicy.Source.RetryPolicyFromOverride.Should().BeFalse();
        effectivePolicy.Source.ResultRetentionFromOverride.Should().BeTrue();
        effectivePolicy.Source.ConcurrencyLimitFromOverride.Should().BeFalse();
    }

    [Fact]
    public async Task GetDefaultPolicyAsync_Should_ReturnTypeDefault_WithoutClientSpecifics()
    {
        // Arrange
        var processType = "order";

        // Act
        var defaultPolicy = await _fixture.PolicyProvider.GetDefaultPolicyAsync(processType);

        // Assert
        defaultPolicy.Should().NotBeNull();
        defaultPolicy.ProcessType.Should().Be(processType);
        defaultPolicy.ClientId.Should().Be("default");
        
        // Should match type default exactly
        defaultPolicy.Timeout.Should().Be(TimeSpan.FromMinutes(5));
        defaultPolicy.RetryPolicy.MaxAttempts.Should().Be(3);
        defaultPolicy.ResultRetention.Should().Be(TimeSpan.FromDays(30));
        defaultPolicy.MaxConcurrentProcesses.Should().Be(10);
        
        // All from type default
        defaultPolicy.Source.TimeoutFromOverride.Should().BeFalse();
    }

    #endregion

    #region Individual Policy Getter Tests

    [Fact]
    public async Task GetTimeoutAsync_Should_ReturnOverrideValue_WhenExists()
    {
        // Arrange
        var clientId = "premium-client";
        var processType = "order";

        // Act
        var timeout = await _fixture.PolicyProvider.GetTimeoutAsync(clientId, processType);

        // Assert
        timeout.Should().Be(TimeSpan.FromMinutes(15)); // From override
    }

    [Fact]
    public async Task GetTimeoutAsync_Should_ReturnDefaultValue_WhenNoOverride()
    {
        // Arrange
        var clientId = "regular-client";
        var processType = "payment";

        // Act
        var timeout = await _fixture.PolicyProvider.GetTimeoutAsync(clientId, processType);

        // Assert
        timeout.Should().Be(TimeSpan.FromMinutes(2)); // From type default
    }

    [Fact]
    public async Task GetRetryPolicyAsync_Should_ReturnOverrideValue_WhenExists()
    {
        // Arrange
        var clientId = "premium-client";
        var processType = "order";

        // Act
        var retryPolicy = await _fixture.PolicyProvider.GetRetryPolicyAsync(clientId, processType);

        // Assert
        retryPolicy.Should().NotBeNull();
        retryPolicy.MaxAttempts.Should().Be(5); // From override
        retryPolicy.BackoffStrategy.Should().Be(BackoffStrategy.Exponential);
    }

    [Fact]
    public async Task GetResultRetentionAsync_Should_ReturnDefaultValue_WhenOverrideIsNull()
    {
        // Arrange
        var clientId = "premium-client";
        var processType = "order";

        // Act
        var retention = await _fixture.PolicyProvider.GetResultRetentionAsync(clientId, processType);

        // Assert
        retention.Should().Be(TimeSpan.FromDays(30)); // From type default (override is null)
    }

    [Fact]
    public async Task GetConcurrencyLimitAsync_Should_ReturnOverrideValue_WhenExists()
    {
        // Arrange
        var clientId = "premium-client";
        var processType = "order";

        // Act
        var limit = await _fixture.PolicyProvider.GetConcurrencyLimitAsync(clientId, processType);

        // Assert
        limit.Should().Be(50); // From override
    }

    #endregion

    #region Caching Tests

    [Fact]
    public async Task GetEffectivePolicyAsync_Should_UseCache_OnSecondCall()
    {
        // Arrange
        await _fixture.ResetCachesAsync();
        var clientId = "cache-test-client";
        var processType = "order";

        // Act - First call (cache miss)
        var policy1 = await _fixture.PolicyProvider.GetEffectivePolicyAsync(clientId, processType);
        var stats1 = _fixture.CacheStatistics;
        var missesAfterFirst = stats1.Misses;

        // Act - Second call (cache hit)
        var policy2 = await _fixture.PolicyProvider.GetEffectivePolicyAsync(clientId, processType);
        var stats2 = _fixture.CacheStatistics;
        var hitsAfterSecond = stats2.Hits;

        // Assert
        policy1.Should().BeEquivalentTo(policy2);
        hitsAfterSecond.Should().BeGreaterThan(0); // Should have cache hits
        stats2.HitRatio.Should().BeGreaterThan(0); // Hit ratio improved
    }

    [Fact]
    public async Task RefreshPoliciesAsync_Should_ClearCache()
    {
        // Arrange
        var clientId = "refresh-test-client";
        var processType = "payment";
        
        // Warm up cache
        await _fixture.PolicyProvider.GetEffectivePolicyAsync(clientId, processType);

        // Act
        var clearedCount = await _fixture.PolicyProvider.RefreshPoliciesAsync();

        // Assert
        clearedCount.Should().BeGreaterThan(0);
        
        // Next call should be a cache miss
        await _fixture.ResetCachesAsync(); // Reset stats to verify
        await _fixture.PolicyProvider.GetEffectivePolicyAsync(clientId, processType);
        
        // Should have recorded new miss
        _fixture.CacheStatistics.Misses.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task CacheStatistics_Should_TrackHitsAndMisses_Accurately()
    {
        // Arrange
        await _fixture.ResetCachesAsync();
        var clientId = "stats-test-client";

        // Act - Multiple calls to different process types
        await _fixture.PolicyProvider.GetEffectivePolicyAsync(clientId, "order"); // Miss
        await _fixture.PolicyProvider.GetEffectivePolicyAsync(clientId, "order"); // Hit
        await _fixture.PolicyProvider.GetEffectivePolicyAsync(clientId, "payment"); // Miss
        await _fixture.PolicyProvider.GetEffectivePolicyAsync(clientId, "payment"); // Hit
        await _fixture.PolicyProvider.GetEffectivePolicyAsync(clientId, "shipping"); // Miss

        var stats = _fixture.CacheStatistics;

        // Assert
        stats.TotalRequests.Should().BeGreaterThan(0);
        stats.Hits.Should().BeGreaterThan(0);
        stats.Misses.Should().BeGreaterThan(0);
        stats.HitRatio.Should().BeGreaterThan(0).And.BeLessThanOrEqualTo(1.0);
    }

    #endregion

    #region Fallback Policy Tests

    [Fact]
    public async Task GetEffectivePolicyAsync_Should_ReturnFallback_WhenTypeNotFound()
    {
        // Arrange
        var clientId = "test-client";
        var nonExistentType = "non-existent-process-type";

        // Act
        var effectivePolicy = await _fixture.PolicyProvider.GetEffectivePolicyAsync(
            clientId,
            nonExistentType);

        // Assert - Should return fallback with default values
        effectivePolicy.Should().NotBeNull();
        effectivePolicy.ProcessType.Should().Be(nonExistentType);
        effectivePolicy.Timeout.Should().Be(TimeSpan.FromSeconds(300)); // Default from options
        effectivePolicy.RetryPolicy.MaxAttempts.Should().Be(3); // Default from options
        effectivePolicy.ResultRetention.Should().Be(TimeSpan.FromDays(30)); // Default from options
    }

    #endregion

    #region Validation Tests

    [Fact]
    public async Task GetEffectivePolicyAsync_Should_FallbackToTypeDefault_WhenOverrideIsInvalid()
    {
        // Arrange
        var invalidOverride = new ClientPolicyOverride
        {
            ClientId = "invalid-client",
            ProcessType = "order",
            Timeout = TimeSpan.FromSeconds(-10), // Invalid: negative timeout
            RetryPolicy = null,
            ResultRetention = null,
            MaxConcurrentProcesses = null,
            UpdatedAt = DateTime.UtcNow
        };
        
        await _fixture.PolicyRepository.SaveClientOverrideAsync(invalidOverride);
        await _fixture.ResetCachesAsync(); // Clear cache to force reload

        // Act
        var effectivePolicy = await _fixture.PolicyProvider.GetEffectivePolicyAsync(
            "invalid-client",
            "order");

        // Assert - Should use type default due to invalid override
        effectivePolicy.Timeout.Should().Be(TimeSpan.FromMinutes(5)); // Type default, not invalid override
    }

    #endregion

    #region Error Handling Tests

    [Fact]
    public async Task GetEffectivePolicyAsync_Should_ThrowArgumentException_WhenClientIdIsEmpty()
    {
        // Arrange
        var emptyClientId = "";
        var processType = "order";

        // Act
        Func<Task> act = async () => 
            await _fixture.PolicyProvider.GetEffectivePolicyAsync(emptyClientId, processType);

        // Assert
        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task GetEffectivePolicyAsync_Should_ThrowArgumentException_WhenProcessTypeIsEmpty()
    {
        // Arrange
        var clientId = "test-client";
        var emptyProcessType = "";

        // Act
        Func<Task> act = async () => 
            await _fixture.PolicyProvider.GetEffectivePolicyAsync(clientId, emptyProcessType);

        // Assert
        await act.Should().ThrowAsync<ArgumentException>();
    }

    #endregion
}
