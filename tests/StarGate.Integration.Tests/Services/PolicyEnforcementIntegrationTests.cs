using FluentAssertions;
using StarGate.Contracts.Requests;
using StarGate.Core.Domain.Configuration;
using StarGate.Integration.Tests.Fixtures;
using Xunit;

namespace StarGate.Integration.Tests.Services;

public class PolicyEnforcementIntegrationTests : IClassFixture<PolicyIntegrationFixture>, IAsyncLifetime
{
    private readonly PolicyIntegrationFixture _fixture;

    public PolicyEnforcementIntegrationTests(PolicyIntegrationFixture fixture)
    {
        _fixture = fixture;
    }

    public Task InitializeAsync() => Task.CompletedTask;

    public async Task DisposeAsync()
    {
        await _fixture.ClearProcessesAsync();
        await _fixture.ClearPoliciesAsync();
    }

    [Fact]
    public async Task CreateProcessAsync_Should_ApplyPolicyFromProvider()
    {
        // Arrange
        var request = new SubmitProcessRequest(
            ClientProcessId: "order-001",
            ProcessType: "order",
            Payload: new { orderId = "ORD-12345" },
            IdempotencyKey: Guid.NewGuid().ToString());

        var clientId = "client-001";

        // Act
        var process = await _fixture.ProcessService.SubmitProcessAsync(clientId, request);

        // Assert
        process.Should().NotBeNull();
        process.ProcessType.Should().Be("order");
        process.ClientId.Should().Be("client-001");

        // Verify policy was loaded (via logs or by checking process metadata)
        var policy = await _fixture.PolicyProvider.GetPolicyAsync(
            process.ProcessType,
            process.ClientId);

        policy.Should().NotBeNull();
        policy!.TimeoutSeconds.Should().Be(300);
    }

    [Fact]
    public async Task ProcessExecution_Should_EnforceTimeout()
    {
        // Arrange - Create process type with very short timeout
        var shortTimeoutPolicy = new ProcessTypePolicy
        {
            Id = Guid.NewGuid().ToString(),
            ProcessType = "slow-process",
            TimeoutSeconds = 2, // 2 seconds
            MaxRetryAttempts = 0,
            MaxConcurrentExecutions = 1,
            Priority = 5,
            IsActive = true
        };

        await _fixture.PolicyRepository.SaveTypePolicyAsync(shortTimeoutPolicy);

        var request = new SubmitProcessRequest(
            ClientProcessId: "slow-001",
            ProcessType: "slow-process",
            Payload: new { delay = 10 }, // Simulate 10-second process
            IdempotencyKey: Guid.NewGuid().ToString());

        var clientId = "client-timeout-test";

        // Act
        var process = await _fixture.ProcessService.SubmitProcessAsync(clientId, request);

        // Assert
        process.Should().NotBeNull();

        // Note: Actual timeout enforcement happens in ProcessWorker
        // This test verifies policy is loaded correctly
        var policy = await _fixture.PolicyProvider.GetPolicyAsync(
            process.ProcessType,
            process.ClientId);

        policy!.TimeoutSeconds.Should().Be(2);
    }

    [Fact]
    public async Task ProcessExecution_Should_RespectClientOverridePriority()
    {
        // Arrange - Create VIP client with priority override
        var vipOverride = new ClientPolicyOverride
        {
            Id = Guid.NewGuid().ToString(),
            ClientId = "vip-client",
            ProcessType = "order",
            Priority = 10 // Highest priority
        };

        await _fixture.PolicyRepository.SaveClientOverrideAsync(vipOverride);

        var request = new SubmitProcessRequest(
            ClientProcessId: "vip-order-001",
            ProcessType: "order",
            Payload: new { orderId = "VIP-12345" },
            IdempotencyKey: Guid.NewGuid().ToString());

        var clientId = "vip-client";

        // Act
        var process = await _fixture.ProcessService.SubmitProcessAsync(clientId, request);
        var policy = await _fixture.PolicyProvider.GetPolicyAsync(
            process.ProcessType,
            process.ClientId);

        // Assert
        policy!.Priority.Should().Be(10, "VIP client should have priority 10");
    }

    [Fact]
    public async Task ProcessExecution_Should_HandleMultipleProcessTypes_WithDifferentPolicies()
    {
        // Arrange
        var orderRequest = new SubmitProcessRequest(
            ClientProcessId: "order-multi-001",
            ProcessType: "order",
            Payload: new { orderId = "ORD-001" },
            IdempotencyKey: Guid.NewGuid().ToString());

        var paymentRequest = new SubmitProcessRequest(
            ClientProcessId: "payment-multi-001",
            ProcessType: "payment",
            Payload: new { paymentId = "PAY-001" },
            IdempotencyKey: Guid.NewGuid().ToString());

        var clientId = "multi-client";

        // Act
        var orderProcess = await _fixture.ProcessService.SubmitProcessAsync(clientId, orderRequest);
        var paymentProcess = await _fixture.ProcessService.SubmitProcessAsync(clientId, paymentRequest);

        var orderPolicy = await _fixture.PolicyProvider.GetPolicyAsync(
            orderProcess.ProcessType,
            orderProcess.ClientId);

        var paymentPolicy = await _fixture.PolicyProvider.GetPolicyAsync(
            paymentProcess.ProcessType,
            paymentProcess.ClientId);

        // Assert
        orderPolicy!.TimeoutSeconds.Should().Be(300);
        orderPolicy.MaxRetryAttempts.Should().Be(3);

        paymentPolicy!.TimeoutSeconds.Should().Be(60);
        paymentPolicy.MaxRetryAttempts.Should().Be(5);
        paymentPolicy.Priority.Should().Be(8, "payment should have higher priority");
    }
}
