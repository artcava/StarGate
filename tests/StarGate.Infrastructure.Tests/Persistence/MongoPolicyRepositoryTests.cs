namespace StarGate.Infrastructure.Tests.Persistence;

using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using MongoDB.Driver;
using Moq;
using StarGate.Core.Domain.Configuration;
using StarGate.Infrastructure.Persistence;
using StarGate.Infrastructure.Persistence.Documents;
using Xunit;

/// <summary>
/// Unit tests for MongoPolicyRepository using mocked MongoDB driver.
/// NO actual database connection required - all operations mocked with Moq.
/// </summary>
public class MongoPolicyRepositoryTests
{
    private readonly Mock<IMongoDatabase> _databaseMock;
    private readonly Mock<IMongoCollection<ProcessTypePolicyDocument>> _policyCollectionMock;
    private readonly Mock<IMongoCollection<ClientPolicyOverrideDocument>> _overrideCollectionMock;
    private readonly MongoPolicyRepository _repository;

    public MongoPolicyRepositoryTests()
    {
        _databaseMock = new Mock<IMongoDatabase>();
        _policyCollectionMock = new Mock<IMongoCollection<ProcessTypePolicyDocument>>();
        _overrideCollectionMock = new Mock<IMongoCollection<ClientPolicyOverrideDocument>>();

        _databaseMock
            .Setup(db => db.GetCollection<ProcessTypePolicyDocument>("processTypePolicies", null))
            .Returns(_policyCollectionMock.Object);

        _databaseMock
            .Setup(db => db.GetCollection<ClientPolicyOverrideDocument>("clientPolicyOverrides", null))
            .Returns(_overrideCollectionMock.Object);

        _repository = new MongoPolicyRepository(
            _databaseMock.Object,
            NullLogger<MongoPolicyRepository>.Instance);
    }

    #region GetProcessTypePolicyAsync Tests

    [Fact]
    public async Task GetProcessTypePolicyAsync_Should_ReturnPolicy_WhenExists()
    {
        // Arrange
        var processType = "order";
        var document = CreateValidPolicyDocument() with { ProcessType = processType };

        var cursorMock = new Mock<IAsyncCursor<ProcessTypePolicyDocument>>();
        cursorMock
            .SetupSequence(c => c.MoveNextAsync(default))
            .ReturnsAsync(true)
            .ReturnsAsync(false);
        cursorMock
            .Setup(c => c.Current)
            .Returns(new[] { document });

        _policyCollectionMock
            .Setup(c => c.FindAsync(
                It.IsAny<FilterDefinition<ProcessTypePolicyDocument>>(),
                It.IsAny<FindOptions<ProcessTypePolicyDocument>>(),
                default))
            .ReturnsAsync(cursorMock.Object);

        // Act
        var result = await _repository.GetProcessTypePolicyAsync(processType);

        // Assert
        result.Should().NotBeNull();
        result.ProcessType.Should().Be(processType);
        result.Timeout.Should().Be(TimeSpan.FromSeconds(600));
        result.RetryPolicy.Enabled.Should().BeTrue();
        result.RetryPolicy.MaxAttempts.Should().Be(3);
        result.RetryPolicy.BackoffStrategy.Should().Be(BackoffStrategy.Exponential);
    }

    [Fact]
    public async Task GetProcessTypePolicyAsync_Should_ThrowException_WhenNotFound()
    {
        // Arrange
        var processType = "nonexistent";

        var cursorMock = new Mock<IAsyncCursor<ProcessTypePolicyDocument>>();
        cursorMock
            .Setup(c => c.MoveNextAsync(default))
            .ReturnsAsync(false);

        _policyCollectionMock
            .Setup(c => c.FindAsync(
                It.IsAny<FilterDefinition<ProcessTypePolicyDocument>>(),
                It.IsAny<FindOptions<ProcessTypePolicyDocument>>(),
                default))
            .ReturnsAsync(cursorMock.Object);

        // Act
        Func<Task> act = async () => await _repository.GetProcessTypePolicyAsync(processType);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage($"Process type policy '{processType}' not found");
    }

    [Fact]
    public async Task GetProcessTypePolicyAsync_Should_ThrowArgumentException_WhenProcessTypeIsEmpty()
    {
        // Act
        Func<Task> act = async () => await _repository.GetProcessTypePolicyAsync("");

        // Assert
        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task GetProcessTypePolicyAsync_Should_ThrowArgumentException_WhenProcessTypeIsWhiteSpace()
    {
        // Act
        Func<Task> act = async () => await _repository.GetProcessTypePolicyAsync("   ");

        // Assert
        await act.Should().ThrowAsync<ArgumentException>();
    }

    #endregion

    #region GetClientOverrideAsync Tests

    [Fact]
    public async Task GetClientOverrideAsync_Should_ReturnOverride_WhenExists()
    {
        // Arrange
        var clientId = "premium-client";
        var processType = "order";
        var document = CreateValidOverrideDocument() with
        {
            ClientId = clientId,
            ProcessType = processType
        };

        var cursorMock = new Mock<IAsyncCursor<ClientPolicyOverrideDocument>>();
        cursorMock
            .SetupSequence(c => c.MoveNextAsync(default))
            .ReturnsAsync(true)
            .ReturnsAsync(false);
        cursorMock
            .Setup(c => c.Current)
            .Returns(new[] { document });

        _overrideCollectionMock
            .Setup(c => c.FindAsync(
                It.IsAny<FilterDefinition<ClientPolicyOverrideDocument>>(),
                It.IsAny<FindOptions<ClientPolicyOverrideDocument>>(),
                default))
            .ReturnsAsync(cursorMock.Object);

        // Act
        var result = await _repository.GetClientOverrideAsync(clientId, processType);

        // Assert
        result.Should().NotBeNull();
        result!.ClientId.Should().Be(clientId);
        result.ProcessType.Should().Be(processType);
        result.Timeout.Should().Be(TimeSpan.FromSeconds(1800));
        result.MaxConcurrentProcesses.Should().Be(500);
    }

    [Fact]
    public async Task GetClientOverrideAsync_Should_ReturnNull_WhenNotFound()
    {
        // Arrange
        var clientId = "standard-client";
        var processType = "order";

        var cursorMock = new Mock<IAsyncCursor<ClientPolicyOverrideDocument>>();
        cursorMock
            .Setup(c => c.MoveNextAsync(default))
            .ReturnsAsync(false);

        _overrideCollectionMock
            .Setup(c => c.FindAsync(
                It.IsAny<FilterDefinition<ClientPolicyOverrideDocument>>(),
                It.IsAny<FindOptions<ClientPolicyOverrideDocument>>(),
                default))
            .ReturnsAsync(cursorMock.Object);

        // Act
        var result = await _repository.GetClientOverrideAsync(clientId, processType);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task GetClientOverrideAsync_Should_ThrowArgumentException_WhenClientIdIsEmpty()
    {
        // Act
        Func<Task> act = async () => await _repository.GetClientOverrideAsync("", "order");

        // Assert
        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task GetClientOverrideAsync_Should_ThrowArgumentException_WhenProcessTypeIsEmpty()
    {
        // Act
        Func<Task> act = async () => await _repository.GetClientOverrideAsync("client-1", "");

        // Assert
        await act.Should().ThrowAsync<ArgumentException>();
    }

    #endregion

    #region SaveProcessTypePolicyAsync Tests

    [Fact]
    public async Task SaveProcessTypePolicyAsync_Should_UpsertPolicy()
    {
        // Arrange
        var policy = CreateValidProcessTypePolicy();

        var replaceResult = new Mock<ReplaceOneResult>();
        replaceResult.Setup(r => r.IsAcknowledged).Returns(true);

        _policyCollectionMock
            .Setup(c => c.ReplaceOneAsync(
                It.IsAny<FilterDefinition<ProcessTypePolicyDocument>>(),
                It.IsAny<ProcessTypePolicyDocument>(),
                It.IsAny<ReplaceOptions>(),
                default))
            .ReturnsAsync(replaceResult.Object);

        // Act
        var result = await _repository.SaveProcessTypePolicyAsync(policy);

        // Assert
        result.Should().Be(policy);
        _policyCollectionMock.Verify(
            c => c.ReplaceOneAsync(
                It.IsAny<FilterDefinition<ProcessTypePolicyDocument>>(),
                It.Is<ProcessTypePolicyDocument>(d =>
                    d.ProcessType == policy.ProcessType &&
                    d.TimeoutSeconds == 600),
                It.Is<ReplaceOptions>(o => o.IsUpsert),
                default),
            Times.Once);
    }

    [Fact]
    public async Task SaveProcessTypePolicyAsync_Should_ThrowArgumentNullException_WhenPolicyIsNull()
    {
        // Act
        Func<Task> act = async () => await _repository.SaveProcessTypePolicyAsync(null!);

        // Assert
        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    #endregion

    #region SaveClientOverrideAsync Tests

    [Fact]
    public async Task SaveClientOverrideAsync_Should_UpsertOverride()
    {
        // Arrange
        var override_ = CreateValidClientOverride();

        var replaceResult = new Mock<ReplaceOneResult>();
        replaceResult.Setup(r => r.IsAcknowledged).Returns(true);

        _overrideCollectionMock
            .Setup(c => c.ReplaceOneAsync(
                It.IsAny<FilterDefinition<ClientPolicyOverrideDocument>>(),
                It.IsAny<ClientPolicyOverrideDocument>(),
                It.IsAny<ReplaceOptions>(),
                default))
            .ReturnsAsync(replaceResult.Object);

        // Act
        var result = await _repository.SaveClientOverrideAsync(override_);

        // Assert
        result.Should().Be(override_);
        _overrideCollectionMock.Verify(
            c => c.ReplaceOneAsync(
                It.IsAny<FilterDefinition<ClientPolicyOverrideDocument>>(),
                It.Is<ClientPolicyOverrideDocument>(d =>
                    d.ClientId == override_.ClientId &&
                    d.ProcessType == override_.ProcessType),
                It.Is<ReplaceOptions>(o => o.IsUpsert),
                default),
            Times.Once);
    }

    [Fact]
    public async Task SaveClientOverrideAsync_Should_ThrowArgumentNullException_WhenOverrideIsNull()
    {
        // Act
        Func<Task> act = async () => await _repository.SaveClientOverrideAsync(null!);

        // Assert
        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    #endregion

    #region DeleteClientOverrideAsync Tests

    [Fact]
    public async Task DeleteClientOverrideAsync_Should_ReturnTrue_WhenDeleted()
    {
        // Arrange
        var clientId = "premium-client";
        var processType = "order";

        var deleteResult = new Mock<DeleteResult>();
        deleteResult.Setup(r => r.DeletedCount).Returns(1);

        _overrideCollectionMock
            .Setup(c => c.DeleteOneAsync(
                It.IsAny<FilterDefinition<ClientPolicyOverrideDocument>>(),
                default))
            .ReturnsAsync(deleteResult.Object);

        // Act
        var result = await _repository.DeleteClientOverrideAsync(clientId, processType);

        // Assert
        result.Should().BeTrue();
        _overrideCollectionMock.Verify(
            c => c.DeleteOneAsync(
                It.IsAny<FilterDefinition<ClientPolicyOverrideDocument>>(),
                default),
            Times.Once);
    }

    [Fact]
    public async Task DeleteClientOverrideAsync_Should_ReturnFalse_WhenNotFound()
    {
        // Arrange
        var clientId = "nonexistent-client";
        var processType = "order";

        var deleteResult = new Mock<DeleteResult>();
        deleteResult.Setup(r => r.DeletedCount).Returns(0);

        _overrideCollectionMock
            .Setup(c => c.DeleteOneAsync(
                It.IsAny<FilterDefinition<ClientPolicyOverrideDocument>>(),
                default))
            .ReturnsAsync(deleteResult.Object);

        // Act
        var result = await _repository.DeleteClientOverrideAsync(clientId, processType);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task DeleteClientOverrideAsync_Should_ThrowArgumentException_WhenClientIdIsEmpty()
    {
        // Act
        Func<Task> act = async () => await _repository.DeleteClientOverrideAsync("", "order");

        // Assert
        await act.Should().ThrowAsync<ArgumentException>();
    }

    #endregion

    #region ListProcessTypePoliciesAsync Tests

    [Fact]
    public async Task ListProcessTypePoliciesAsync_Should_ReturnAllPolicies()
    {
        // Arrange
        var documents = new[]
        {
            CreateValidPolicyDocument() with { ProcessType = "order" },
            CreateValidPolicyDocument() with { ProcessType = "payment" },
            CreateValidPolicyDocument() with { ProcessType = "shipment" }
        };

        var cursorMock = new Mock<IAsyncCursor<ProcessTypePolicyDocument>>();
        cursorMock
            .SetupSequence(c => c.MoveNextAsync(default))
            .ReturnsAsync(true)
            .ReturnsAsync(false);
        cursorMock
            .Setup(c => c.Current)
            .Returns(documents);

        _policyCollectionMock
            .Setup(c => c.FindAsync(
                It.IsAny<FilterDefinition<ProcessTypePolicyDocument>>(),
                It.IsAny<FindOptions<ProcessTypePolicyDocument>>(),
                default))
            .ReturnsAsync(cursorMock.Object);

        // Act
        var result = await _repository.ListProcessTypePoliciesAsync();

        // Assert
        result.Should().HaveCount(3);
        result.Select(p => p.ProcessType).Should().BeEquivalentTo(new[] { "order", "payment", "shipment" });
    }

    [Fact]
    public async Task ListProcessTypePoliciesAsync_Should_ReturnEmptyList_WhenNoPolicies()
    {
        // Arrange
        var cursorMock = new Mock<IAsyncCursor<ProcessTypePolicyDocument>>();
        cursorMock
            .Setup(c => c.MoveNextAsync(default))
            .ReturnsAsync(false);

        _policyCollectionMock
            .Setup(c => c.FindAsync(
                It.IsAny<FilterDefinition<ProcessTypePolicyDocument>>(),
                It.IsAny<FindOptions<ProcessTypePolicyDocument>>(),
                default))
            .ReturnsAsync(cursorMock.Object);

        // Act
        var result = await _repository.ListProcessTypePoliciesAsync();

        // Assert
        result.Should().BeEmpty();
    }

    #endregion

    #region ListClientOverridesAsync Tests

    [Fact]
    public async Task ListClientOverridesAsync_Should_ReturnClientOverrides()
    {
        // Arrange
        var clientId = "premium-client";
        var documents = new[]
        {
            CreateValidOverrideDocument() with { ClientId = clientId, ProcessType = "order" },
            CreateValidOverrideDocument() with { ClientId = clientId, ProcessType = "payment" }
        };

        var cursorMock = new Mock<IAsyncCursor<ClientPolicyOverrideDocument>>();
        cursorMock
            .SetupSequence(c => c.MoveNextAsync(default))
            .ReturnsAsync(true)
            .ReturnsAsync(false);
        cursorMock
            .Setup(c => c.Current)
            .Returns(documents);

        _overrideCollectionMock
            .Setup(c => c.FindAsync(
                It.IsAny<FilterDefinition<ClientPolicyOverrideDocument>>(),
                It.IsAny<FindOptions<ClientPolicyOverrideDocument>>(),
                default))
            .ReturnsAsync(cursorMock.Object);

        // Act
        var result = await _repository.ListClientOverridesAsync(clientId);

        // Assert
        result.Should().HaveCount(2);
        result.Should().AllSatisfy(o => o.ClientId.Should().Be(clientId));
        result.Select(o => o.ProcessType).Should().BeEquivalentTo(new[] { "order", "payment" });
    }

    [Fact]
    public async Task ListClientOverridesAsync_Should_ThrowArgumentException_WhenClientIdIsEmpty()
    {
        // Act
        Func<Task> act = async () => await _repository.ListClientOverridesAsync("");

        // Assert
        await act.Should().ThrowAsync<ArgumentException>();
    }

    #endregion

    #region Helper Methods

    private static ProcessTypePolicyDocument CreateValidPolicyDocument() => new()
    {
        ProcessType = "order",
        TimeoutSeconds = 600,
        RetryEnabled = true,
        RetryMaxAttempts = 3,
        RetryInitialDelaySeconds = 10,
        RetryBackoffStrategy = "Exponential",
        RetryMaxDelaySeconds = 300,
        ResultRetentionDays = 7,
        MaxConcurrentProcesses = 100,
        UpdatedAt = DateTime.UtcNow
    };

    private static ClientPolicyOverrideDocument CreateValidOverrideDocument() => new()
    {
        ClientId = "premium-client",
        ProcessType = "order",
        TimeoutSeconds = 1800,
        ResultRetentionDays = 30,
        MaxConcurrentProcesses = 500,
        UpdatedAt = DateTime.UtcNow
    };

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

    #endregion
}
