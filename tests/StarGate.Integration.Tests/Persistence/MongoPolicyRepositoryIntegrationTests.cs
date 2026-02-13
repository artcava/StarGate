using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using MongoDB.Driver;
using StarGate.Core.Domain.Configuration;
using StarGate.Infrastructure.Persistence;
using StarGate.Integration.Tests.Fixtures;
using Xunit;

namespace StarGate.Integration.Tests.Persistence;

public class MongoPolicyRepositoryIntegrationTests : IClassFixture<MongoDbFixture>, IAsyncLifetime
{
    private readonly MongoDbFixture _fixture;
    private readonly MongoPolicyRepository _repository;

    public MongoPolicyRepositoryIntegrationTests(MongoDbFixture fixture)
    {
        _fixture = fixture;
        _repository = new MongoPolicyRepository(
            _fixture.Database,
            NullLogger<MongoPolicyRepository>.Instance);
    }

    public Task InitializeAsync() => Task.CompletedTask;

    public async Task DisposeAsync()
    {
        await _fixture.ResetDatabaseAsync();
    }

    [Fact]
    public async Task GetProcessTypePolicyAsync_Should_ReturnPolicy_WhenExists()
    {
        // Arrange
        var policy = CreateValidProcessTypePolicy();
        await SeedProcessTypePolicyAsync(policy);

        // Act
        var result = await _repository.GetProcessTypePolicyAsync(policy.ProcessType);

        // Assert
        result.Should().NotBeNull();
        result.ProcessType.Should().Be(policy.ProcessType);
        result.Timeout.Should().Be(policy.Timeout);
        result.RetryPolicy.Enabled.Should().Be(policy.RetryPolicy.Enabled);
        result.RetryPolicy.MaxAttempts.Should().Be(policy.RetryPolicy.MaxAttempts);
        result.RetryPolicy.InitialDelay.Should().Be(policy.RetryPolicy.InitialDelay);
        result.RetryPolicy.BackoffStrategy.Should().Be(policy.RetryPolicy.BackoffStrategy);
        result.RetryPolicy.MaxDelay.Should().Be(policy.RetryPolicy.MaxDelay);
        result.ResultRetention.Should().Be(policy.ResultRetention);
        result.MaxConcurrentProcesses.Should().Be(policy.MaxConcurrentProcesses);
    }

    [Fact]
    public async Task GetProcessTypePolicyAsync_Should_ThrowException_WhenNotFound()
    {
        // Act
        Func<Task> act = async () => await _repository.GetProcessTypePolicyAsync("nonexistent");

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Process type policy 'nonexistent' not found");
    }

    [Fact]
    public async Task GetClientOverrideAsync_Should_ReturnOverride_WhenExists()
    {
        // Arrange
        var clientOverride = CreateValidClientOverride();
        await SeedClientOverrideAsync(clientOverride);

        // Act
        var result = await _repository.GetClientOverrideAsync(
            clientOverride.ClientId,
            clientOverride.ProcessType);

        // Assert
        result.Should().NotBeNull();
        result!.ClientId.Should().Be(clientOverride.ClientId);
        result.ProcessType.Should().Be(clientOverride.ProcessType);
        result.Timeout.Should().Be(clientOverride.Timeout);
        result.ResultRetention.Should().Be(clientOverride.ResultRetention);
        result.MaxConcurrentProcesses.Should().Be(clientOverride.MaxConcurrentProcesses);
    }

    [Fact]
    public async Task GetClientOverrideAsync_Should_ReturnNull_WhenNotFound()
    {
        // Act
        var result = await _repository.GetClientOverrideAsync("nonexistent-client", "order");

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task GetProcessTypePolicyAsync_Should_HandleComplexRetryPolicy()
    {
        // Arrange
        var policy = new ProcessTypePolicy
        {
            ProcessType = "complex-order",
            Timeout = TimeSpan.FromHours(2),
            RetryPolicy = new RetryPolicy
            {
                Enabled = true,
                MaxAttempts = 5,
                InitialDelay = TimeSpan.FromSeconds(30),
                BackoffStrategy = BackoffStrategy.Exponential,
                MaxDelay = TimeSpan.FromMinutes(30)
            },
            ResultRetention = TimeSpan.FromDays(90),
            MaxConcurrentProcesses = 1000,
            UpdatedAt = DateTime.UtcNow
        };
        await SeedProcessTypePolicyAsync(policy);

        // Act
        var result = await _repository.GetProcessTypePolicyAsync(policy.ProcessType);

        // Assert
        result.Should().NotBeNull();
        result.RetryPolicy.BackoffStrategy.Should().Be(BackoffStrategy.Exponential);
        result.RetryPolicy.MaxAttempts.Should().Be(5);
        result.Timeout.Should().Be(TimeSpan.FromHours(2));
    }

    [Fact]
    public async Task GetClientOverrideAsync_Should_HandleNullableFields()
    {
        // Arrange
        var clientOverride = new ClientPolicyOverride
        {
            ClientId = "basic-client",
            ProcessType = "order",
            Timeout = TimeSpan.FromMinutes(15),
            ResultRetention = null,
            MaxConcurrentProcesses = null,
            UpdatedAt = DateTime.UtcNow
        };
        await SeedClientOverrideAsync(clientOverride);

        // Act
        var result = await _repository.GetClientOverrideAsync(
            clientOverride.ClientId,
            clientOverride.ProcessType);

        // Assert
        result.Should().NotBeNull();
        result!.Timeout.Should().Be(TimeSpan.FromMinutes(15));
        result.ResultRetention.Should().BeNull();
        result.MaxConcurrentProcesses.Should().BeNull();
    }

    [Fact]
    public async Task GetProcessTypePolicyAsync_Should_HandleDisabledRetryPolicy()
    {
        // Arrange
        var policy = new ProcessTypePolicy
        {
            ProcessType = "no-retry-process",
            Timeout = TimeSpan.FromMinutes(5),
            RetryPolicy = new RetryPolicy
            {
                Enabled = false,
                MaxAttempts = 0,
                InitialDelay = TimeSpan.Zero,
                BackoffStrategy = BackoffStrategy.Linear,
                MaxDelay = TimeSpan.Zero
            },
            ResultRetention = TimeSpan.FromDays(1),
            MaxConcurrentProcesses = 10,
            UpdatedAt = DateTime.UtcNow
        };
        await SeedProcessTypePolicyAsync(policy);

        // Act
        var result = await _repository.GetProcessTypePolicyAsync(policy.ProcessType);

        // Assert
        result.Should().NotBeNull();
        result.RetryPolicy.Enabled.Should().BeFalse();
        result.RetryPolicy.MaxAttempts.Should().Be(0);
        result.RetryPolicy.BackoffStrategy.Should().Be(BackoffStrategy.Linear);
    }

    private async Task SeedProcessTypePolicyAsync(ProcessTypePolicy policy)
    {
        var collection = _fixture.Database.GetCollection<ProcessTypePolicyDocument>("processTypePolicies");
        var document = PolicyMapper.MapToDocument(policy);
        await collection.InsertOneAsync(document);
    }

    private async Task SeedClientOverrideAsync(ClientPolicyOverride clientOverride)
    {
        var collection = _fixture.Database.GetCollection<ClientPolicyOverrideDocument>("clientPolicyOverrides");
        var document = PolicyMapper.MapToDocument(clientOverride);
        await collection.InsertOneAsync(document);
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

    private static ClientPolicyOverride CreateValidClientOverride() => new()
    {
        ClientId = "premium-client",
        ProcessType = "order",
        Timeout = TimeSpan.FromMinutes(30),
        ResultRetention = TimeSpan.FromDays(30),
        MaxConcurrentProcesses = 500,
        UpdatedAt = DateTime.UtcNow
    };
}
