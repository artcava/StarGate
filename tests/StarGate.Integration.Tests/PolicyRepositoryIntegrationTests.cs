using FluentAssertions;
using StarGate.Core.Domain.Configuration;
using StarGate.Integration.Tests.Fixtures;
using Xunit;

namespace StarGate.Integration.Tests;

/// <summary>
/// Integration tests for policy repository.
/// Tests MongoDB persistence of process type policies and client overrides.
/// </summary>
public class PolicyRepositoryIntegrationTests : IClassFixture<PolicyRepositoryFixture>
{
    private readonly PolicyRepositoryFixture _fixture;

    public PolicyRepositoryIntegrationTests(PolicyRepositoryFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task GetProcessTypePolicyAsync_Should_ReturnPolicy_WhenExists()
    {
        // Arrange
        var processType = "order";

        // Act
        var policy = await _fixture.PolicyRepository.GetProcessTypePolicyAsync(processType);

        // Assert
        policy.Should().NotBeNull();
        policy.ProcessType.Should().Be(processType);
        policy.Timeout.Should().Be(TimeSpan.FromMinutes(5));
        policy.RetryPolicy.Should().NotBeNull();
        policy.RetryPolicy.Enabled.Should().BeTrue();
        policy.RetryPolicy.MaxAttempts.Should().Be(3);
        policy.ResultRetention.Should().Be(TimeSpan.FromDays(30));
        policy.MaxConcurrentProcesses.Should().Be(10);
    }

    [Fact]
    public async Task GetProcessTypePolicyAsync_Should_ThrowKeyNotFoundException_WhenNotExists()
    {
        // Arrange
        var nonExistentProcessType = "nonexistent";

        // Act
        Func<Task> act = async () => 
            await _fixture.PolicyRepository.GetProcessTypePolicyAsync(nonExistentProcessType);

        // Assert
        await act.Should().ThrowAsync<KeyNotFoundException>();
    }

    [Fact]
    public async Task SaveProcessTypePolicyAsync_Should_CreateNewPolicy()
    {
        // Arrange
        var newPolicy = new ProcessTypePolicy
        {
            ProcessType = "invoice",
            Timeout = TimeSpan.FromMinutes(15),
            RetryPolicy = new RetryPolicy
            {
                Enabled = true,
                MaxAttempts = 2,
                InitialDelay = TimeSpan.FromSeconds(3),
                BackoffStrategy = BackoffStrategy.Exponential,
                MaxDelay = TimeSpan.FromMinutes(3)
            },
            ResultRetention = TimeSpan.FromDays(90),
            MaxConcurrentProcesses = 20,
            UpdatedAt = DateTime.UtcNow
        };

        // Act
        var savedPolicy = await _fixture.PolicyRepository.SaveProcessTypePolicyAsync(newPolicy);

        // Assert
        savedPolicy.Should().NotBeNull();
        savedPolicy.ProcessType.Should().Be("invoice");
        savedPolicy.Timeout.Should().Be(TimeSpan.FromMinutes(15));
        
        // Verify it can be retrieved
        var retrievedPolicy = await _fixture.PolicyRepository.GetProcessTypePolicyAsync("invoice");
        retrievedPolicy.Should().BeEquivalentTo(savedPolicy);
    }

    [Fact]
    public async Task SaveProcessTypePolicyAsync_Should_UpdateExistingPolicy()
    {
        // Arrange
        var originalPolicy = await _fixture.PolicyRepository.GetProcessTypePolicyAsync("order");
        var updatedPolicy = originalPolicy with
        {
            Timeout = TimeSpan.FromMinutes(10), // Changed from 5 to 10
            MaxConcurrentProcesses = 20,        // Changed from 10 to 20
            UpdatedAt = DateTime.UtcNow
        };

        // Act
        var savedPolicy = await _fixture.PolicyRepository.SaveProcessTypePolicyAsync(updatedPolicy);

        // Assert
        savedPolicy.Timeout.Should().Be(TimeSpan.FromMinutes(10));
        savedPolicy.MaxConcurrentProcesses.Should().Be(20);
        savedPolicy.UpdatedAt.Should().BeAfter(originalPolicy.UpdatedAt);
        
        // Verify changes persisted
        var retrievedPolicy = await _fixture.PolicyRepository.GetProcessTypePolicyAsync("order");
        retrievedPolicy.Timeout.Should().Be(TimeSpan.FromMinutes(10));
    }

    [Fact]
    public async Task ListProcessTypePoliciesAsync_Should_ReturnAllPolicies()
    {
        // Act
        var policies = await _fixture.PolicyRepository.ListProcessTypePoliciesAsync();

        // Assert
        policies.Should().NotBeEmpty();
        policies.Should().HaveCount(2); // order and shipping from seed data
        policies.Should().Contain(p => p.ProcessType == "order");
        policies.Should().Contain(p => p.ProcessType == "shipping");
    }

    [Fact]
    public async Task GetAllTypeDefaultsAsync_Should_ReturnAllPolicies()
    {
        // Act
        var policies = await _fixture.PolicyRepository.GetAllTypeDefaultsAsync();

        // Assert
        policies.Should().NotBeEmpty();
        policies.Should().HaveCount(2);
        policies.Should().Contain(p => p.ProcessType == "order");
        policies.Should().Contain(p => p.ProcessType == "shipping");
    }

    [Fact]
    public async Task SaveClientOverrideAsync_Should_CreateOverride()
    {
        // Arrange
        var clientOverride = new ClientPolicyOverride
        {
            ClientId = "premium-client",
            ProcessType = "order",
            Timeout = TimeSpan.FromMinutes(15), // Override: longer timeout
            RetryPolicy = new RetryPolicy
            {
                Enabled = true,
                MaxAttempts = 5, // Override: more retries
                InitialDelay = TimeSpan.FromSeconds(5),
                BackoffStrategy = BackoffStrategy.Exponential,
                MaxDelay = TimeSpan.FromMinutes(5)
            },
            ResultRetention = null, // Use default
            MaxConcurrentProcesses = 50, // Override: higher concurrency
            UpdatedAt = DateTime.UtcNow
        };

        // Act
        var savedOverride = await _fixture.PolicyRepository.SaveClientOverrideAsync(clientOverride);

        // Assert
        savedOverride.Should().NotBeNull();
        savedOverride.ClientId.Should().Be("premium-client");
        savedOverride.ProcessType.Should().Be("order");
        savedOverride.Timeout.Should().Be(TimeSpan.FromMinutes(15));
        savedOverride.MaxConcurrentProcesses.Should().Be(50);
    }

    [Fact]
    public async Task GetClientOverrideAsync_Should_ReturnOverride_WhenExists()
    {
        // Arrange
        var clientId = "test-client";
        var processType = "order";
        var clientOverride = new ClientPolicyOverride
        {
            ClientId = clientId,
            ProcessType = processType,
            Timeout = TimeSpan.FromMinutes(20),
            RetryPolicy = null,
            ResultRetention = null,
            MaxConcurrentProcesses = null,
            UpdatedAt = DateTime.UtcNow
        };
        await _fixture.PolicyRepository.SaveClientOverrideAsync(clientOverride);

        // Act
        var retrievedOverride = await _fixture.PolicyRepository.GetClientOverrideAsync(clientId, processType);

        // Assert
        retrievedOverride.Should().NotBeNull();
        retrievedOverride!.ClientId.Should().Be(clientId);
        retrievedOverride.ProcessType.Should().Be(processType);
        retrievedOverride.Timeout.Should().Be(TimeSpan.FromMinutes(20));
    }

    [Fact]
    public async Task GetClientOverrideAsync_Should_ReturnNull_WhenNotExists()
    {
        // Arrange
        var nonExistentClientId = "nonexistent-client";
        var processType = "order";

        // Act
        var result = await _fixture.PolicyRepository.GetClientOverrideAsync(nonExistentClientId, processType);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task DeleteClientOverrideAsync_Should_ReturnTrue_WhenExists()
    {
        // Arrange
        var clientId = "deletable-client";
        var processType = "order";
        var clientOverride = new ClientPolicyOverride
        {
            ClientId = clientId,
            ProcessType = processType,
            Timeout = TimeSpan.FromMinutes(25),
            RetryPolicy = null,
            ResultRetention = null,
            MaxConcurrentProcesses = null,
            UpdatedAt = DateTime.UtcNow
        };
        await _fixture.PolicyRepository.SaveClientOverrideAsync(clientOverride);

        // Act
        var deleted = await _fixture.PolicyRepository.DeleteClientOverrideAsync(clientId, processType);

        // Assert
        deleted.Should().BeTrue();
        
        // Verify it's gone
        var retrievedOverride = await _fixture.PolicyRepository.GetClientOverrideAsync(clientId, processType);
        retrievedOverride.Should().BeNull();
    }

    [Fact]
    public async Task DeleteClientOverrideAsync_Should_ReturnFalse_WhenNotExists()
    {
        // Arrange
        var nonExistentClientId = "nonexistent-client";
        var processType = "order";

        // Act
        var deleted = await _fixture.PolicyRepository.DeleteClientOverrideAsync(nonExistentClientId, processType);

        // Assert
        deleted.Should().BeFalse();
    }

    [Fact]
    public async Task ListClientOverridesAsync_Should_ReturnAllOverridesForClient()
    {
        // Arrange
        var clientId = "multi-override-client";
        
        var override1 = new ClientPolicyOverride
        {
            ClientId = clientId,
            ProcessType = "order",
            Timeout = TimeSpan.FromMinutes(30),
            RetryPolicy = null,
            ResultRetention = null,
            MaxConcurrentProcesses = null,
            UpdatedAt = DateTime.UtcNow
        };
        
        var override2 = new ClientPolicyOverride
        {
            ClientId = clientId,
            ProcessType = "shipping",
            Timeout = TimeSpan.FromMinutes(40),
            RetryPolicy = null,
            ResultRetention = null,
            MaxConcurrentProcesses = null,
            UpdatedAt = DateTime.UtcNow
        };
        
        await _fixture.PolicyRepository.SaveClientOverrideAsync(override1);
        await _fixture.PolicyRepository.SaveClientOverrideAsync(override2);

        // Act
        var overrides = await _fixture.PolicyRepository.ListClientOverridesAsync(clientId);

        // Assert
        overrides.Should().HaveCount(2);
        overrides.Should().Contain(o => o.ProcessType == "order");
        overrides.Should().Contain(o => o.ProcessType == "shipping");
    }

    [Fact]
    public async Task GetAllClientOverridesAsync_Should_ReturnAllOverrides()
    {
        // Arrange
        var override1 = new ClientPolicyOverride
        {
            ClientId = "client-1",
            ProcessType = "order",
            Timeout = TimeSpan.FromMinutes(35),
            RetryPolicy = null,
            ResultRetention = null,
            MaxConcurrentProcesses = null,
            UpdatedAt = DateTime.UtcNow
        };
        
        var override2 = new ClientPolicyOverride
        {
            ClientId = "client-2",
            ProcessType = "shipping",
            Timeout = TimeSpan.FromMinutes(45),
            RetryPolicy = null,
            ResultRetention = null,
            MaxConcurrentProcesses = null,
            UpdatedAt = DateTime.UtcNow
        };
        
        await _fixture.PolicyRepository.SaveClientOverrideAsync(override1);
        await _fixture.PolicyRepository.SaveClientOverrideAsync(override2);

        // Act
        var allOverrides = await _fixture.PolicyRepository.GetAllClientOverridesAsync();

        // Assert
        allOverrides.Should().NotBeEmpty();
        allOverrides.Should().Contain(o => o.ClientId == "client-1" && o.ProcessType == "order");
        allOverrides.Should().Contain(o => o.ClientId == "client-2" && o.ProcessType == "shipping");
    }
}
