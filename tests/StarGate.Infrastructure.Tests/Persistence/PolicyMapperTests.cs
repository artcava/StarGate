using FluentAssertions;
using MongoDB.Bson;
using StarGate.Core.Domain.Configuration;
using StarGate.Infrastructure.Persistence;
using Xunit;

namespace StarGate.Infrastructure.Tests.Persistence;

public class PolicyMapperTests
{
    #region ProcessTypePolicy Tests

    [Fact]
    public void MapToDocument_ProcessTypePolicy_Should_ConvertCorrectly()
    {
        // Arrange
        ProcessTypePolicy policy = CreateValidProcessTypePolicy();

        // Act
        ProcessTypePolicyDocument document = PolicyMapper.MapToDocument(policy);

        // Assert
        document.ProcessType.Should().Be(policy.ProcessType);
        document.Timeout.Should().Be(policy.Timeout);
        document.RetryPolicy.Enabled.Should().Be(policy.RetryPolicy.Enabled);
        document.RetryPolicy.MaxAttempts.Should().Be(policy.RetryPolicy.MaxAttempts);
        document.ResultRetention.Should().Be(policy.ResultRetention);
        document.MaxConcurrentProcesses.Should().Be(policy.MaxConcurrentProcesses);
        document.UpdatedAt.Should().Be(policy.UpdatedAt);
    }

    [Fact]
    public void MapToDocument_ProcessTypePolicy_Should_ThrowArgumentNull_WhenPolicyIsNull()
    {
        // Act
        Action act = () => PolicyMapper.MapToDocument((ProcessTypePolicy)null!);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void MapToDomain_ProcessTypePolicy_Should_ConvertCorrectly()
    {
        // Arrange
        ProcessTypePolicyDocument document = CreateValidProcessTypePolicyDocument();

        // Act
        ProcessTypePolicy policy = PolicyMapper.MapToDomain(document);

        // Assert
        policy.ProcessType.Should().Be(document.ProcessType);
        policy.Timeout.Should().Be(document.Timeout);
        policy.RetryPolicy.Enabled.Should().Be(document.RetryPolicy.Enabled);
        policy.ResultRetention.Should().Be(document.ResultRetention);
        policy.MaxConcurrentProcesses.Should().Be(document.MaxConcurrentProcesses);
        policy.UpdatedAt.Should().Be(document.UpdatedAt);
    }

    [Fact]
    public void MapToDomain_ProcessTypePolicy_Should_ThrowArgumentNull_WhenDocumentIsNull()
    {
        // Act
        Action act = () => PolicyMapper.MapToDomain((ProcessTypePolicyDocument)null!);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void RoundTrip_ProcessTypePolicy_Should_PreserveData()
    {
        // Arrange
        ProcessTypePolicy original = CreateValidProcessTypePolicy();

        // Act
        ProcessTypePolicyDocument document = PolicyMapper.MapToDocument(original);
        ProcessTypePolicy roundTripped = PolicyMapper.MapToDomain(document);

        // Assert
        roundTripped.ProcessType.Should().Be(original.ProcessType);
        roundTripped.Timeout.Should().Be(original.Timeout);
        roundTripped.RetryPolicy.MaxAttempts.Should().Be(original.RetryPolicy.MaxAttempts);
        roundTripped.RetryPolicy.BackoffStrategy.Should().Be(original.RetryPolicy.BackoffStrategy);
    }

    #endregion

    #region ClientPolicyOverride Tests

    [Fact]
    public void MapToDocument_ClientOverride_Should_ConvertCorrectly()
    {
        // Arrange
        ClientPolicyOverride clientOverride = CreateValidClientOverride();

        // Act
        ClientPolicyOverrideDocument document = PolicyMapper.MapToDocument(clientOverride);

        // Assert
        document.ClientId.Should().Be(clientOverride.ClientId);
        document.ProcessType.Should().Be(clientOverride.ProcessType);
        document.Timeout.Should().Be(clientOverride.Timeout);
        document.RetryPolicy.Should().NotBeNull();
        document.RetryPolicy!.MaxAttempts.Should().Be(clientOverride.RetryPolicy!.MaxAttempts);
        document.ResultRetention.Should().Be(clientOverride.ResultRetention);
        document.MaxConcurrentProcesses.Should().Be(clientOverride.MaxConcurrentProcesses);
    }

    [Fact]
    public void MapToDocument_ClientOverride_Should_ThrowArgumentNull_WhenOverrideIsNull()
    {
        // Act
        Action act = () => PolicyMapper.MapToDocument((ClientPolicyOverride)null!);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void MapToDocument_ClientOverride_Should_HandleNullRetryPolicy()
    {
        // Arrange
        ClientPolicyOverride clientOverride = new()
        {
            ClientId = "test-client",
            ProcessType = "order",
            Timeout = TimeSpan.FromMinutes(20),
            RetryPolicy = null,
            ResultRetention = TimeSpan.FromDays(30),
            MaxConcurrentProcesses = 200,
            UpdatedAt = DateTime.UtcNow
        };

        // Act
        ClientPolicyOverrideDocument document = PolicyMapper.MapToDocument(clientOverride);

        // Assert
        document.RetryPolicy.Should().BeNull();
    }

    [Fact]
    public void MapToDocument_ClientOverride_Should_SetEmptyObjectId()
    {
        // Arrange
        ClientPolicyOverride clientOverride = CreateValidClientOverride();

        // Act
        ClientPolicyOverrideDocument document = PolicyMapper.MapToDocument(clientOverride);

        // Assert
        // ObjectId.Empty is a placeholder - repository will manage the actual ID
        document.Id.Should().Be(ObjectId.Empty);
    }

    [Fact]
    public void MapToDocument_ClientOverride_Should_HandleNullableFields()
    {
        // Arrange
        ClientPolicyOverride clientOverride = new()
        {
            ClientId = "test-client",
            ProcessType = "order",
            Timeout = null,
            RetryPolicy = null,
            ResultRetention = null,
            MaxConcurrentProcesses = null,
            UpdatedAt = DateTime.UtcNow
        };

        // Act
        ClientPolicyOverrideDocument document = PolicyMapper.MapToDocument(clientOverride);

        // Assert
        document.Timeout.Should().BeNull();
        document.RetryPolicy.Should().BeNull();
        document.ResultRetention.Should().BeNull();
        document.MaxConcurrentProcesses.Should().BeNull();
    }

    [Fact]
    public void MapToDomain_ClientOverride_Should_ConvertCorrectly()
    {
        // Arrange
        ClientPolicyOverrideDocument document = CreateValidClientOverrideDocument();

        // Act
        ClientPolicyOverride clientOverride = PolicyMapper.MapToDomain(document);

        // Assert
        clientOverride.ClientId.Should().Be(document.ClientId);
        clientOverride.ProcessType.Should().Be(document.ProcessType);
        clientOverride.Timeout.Should().Be(document.Timeout);
        clientOverride.RetryPolicy.Should().NotBeNull();
        clientOverride.ResultRetention.Should().Be(document.ResultRetention);
    }

    [Fact]
    public void MapToDomain_ClientOverride_Should_ThrowArgumentNull_WhenDocumentIsNull()
    {
        // Act
        Action act = () => PolicyMapper.MapToDomain((ClientPolicyOverrideDocument)null!);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void RoundTrip_ClientOverride_Should_PreserveData()
    {
        // Arrange
        ClientPolicyOverride original = CreateValidClientOverride();

        // Act
        ClientPolicyOverrideDocument document = PolicyMapper.MapToDocument(original);
        ClientPolicyOverride roundTripped = PolicyMapper.MapToDomain(document);

        // Assert
        roundTripped.ClientId.Should().Be(original.ClientId);
        roundTripped.ProcessType.Should().Be(original.ProcessType);
        roundTripped.Timeout.Should().Be(original.Timeout);
        roundTripped.RetryPolicy!.MaxAttempts.Should().Be(original.RetryPolicy!.MaxAttempts);
    }

    #endregion

    #region RetryPolicy Tests

    [Fact]
    public void MapToDomain_RetryPolicy_Should_HandleAllBackoffStrategies()
    {
        // Arrange & Act & Assert
        BackoffStrategy[] strategies =
        {
            BackoffStrategy.Linear,
            BackoffStrategy.Exponential
        };

        foreach (BackoffStrategy strategy in strategies)
        {
            ProcessTypePolicy policy = new()
            {
                ProcessType = "test",
                Timeout = TimeSpan.FromMinutes(10),
                RetryPolicy = new RetryPolicy
                {
                    Enabled = true,
                    MaxAttempts = 3,
                    InitialDelay = TimeSpan.FromSeconds(10),
                    BackoffStrategy = strategy,
                    MaxDelay = TimeSpan.FromMinutes(5)
                },
                ResultRetention = TimeSpan.FromDays(7),
                MaxConcurrentProcesses = 100,
                UpdatedAt = DateTime.UtcNow
            };

            ProcessTypePolicyDocument document = PolicyMapper.MapToDocument(policy);
            ProcessTypePolicy roundTripped = PolicyMapper.MapToDomain(document);

            roundTripped.RetryPolicy.BackoffStrategy.Should().Be(strategy);
        }
    }

    [Fact]
    public void MapToDomain_RetryPolicy_Should_ThrowException_OnInvalidBackoffStrategy()
    {
        // Arrange
        ProcessTypePolicyDocument document = CreateValidProcessTypePolicyDocument();
        document.RetryPolicy.BackoffStrategy = "InvalidStrategy";

        // Act
        Action act = () => PolicyMapper.MapToDomain(document);

        // Assert
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*InvalidStrategy*");
    }

    [Fact]
    public void MapToDomain_RetryPolicy_Should_BeCaseInsensitiveForBackoffStrategy()
    {
        // Arrange
        ProcessTypePolicyDocument document = CreateValidProcessTypePolicyDocument();
        document.RetryPolicy.BackoffStrategy = "exponential"; // lowercase

        // Act
        ProcessTypePolicy policy = PolicyMapper.MapToDomain(document);

        // Assert
        policy.RetryPolicy.BackoffStrategy.Should().Be(BackoffStrategy.Exponential);
    }

    [Fact]
    public void MapToDocument_RetryPolicy_Should_SerializeBackoffStrategyAsString()
    {
        // Arrange
        ProcessTypePolicy policy = CreateValidProcessTypePolicy();

        // Act
        ProcessTypePolicyDocument document = PolicyMapper.MapToDocument(policy);

        // Assert
        document.RetryPolicy.BackoffStrategy.Should().Be("Exponential");
    }

    #endregion

    #region TimeSpan and DateTime Tests

    [Fact]
    public void MapToDocument_Should_PreserveTimeSpanValues()
    {
        // Arrange
        ProcessTypePolicy policy = new()
        {
            ProcessType = "test",
            Timeout = TimeSpan.FromMinutes(15),
            RetryPolicy = new RetryPolicy
            {
                Enabled = true,
                MaxAttempts = 3,
                InitialDelay = TimeSpan.FromSeconds(5),
                BackoffStrategy = BackoffStrategy.Exponential,
                MaxDelay = TimeSpan.FromMinutes(10)
            },
            ResultRetention = TimeSpan.FromDays(30),
            MaxConcurrentProcesses = 100,
            UpdatedAt = DateTime.UtcNow
        };

        // Act
        ProcessTypePolicyDocument document = PolicyMapper.MapToDocument(policy);

        // Assert
        document.Timeout.Should().Be(TimeSpan.FromMinutes(15));
        document.RetryPolicy.InitialDelay.Should().Be(TimeSpan.FromSeconds(5));
        document.RetryPolicy.MaxDelay.Should().Be(TimeSpan.FromMinutes(10));
        document.ResultRetention.Should().Be(TimeSpan.FromDays(30));
    }

    [Fact]
    public void MapToDocument_Should_PreserveDateTimeValues()
    {
        // Arrange
        DateTime updatedAt = new(2026, 2, 12, 9, 0, 0, DateTimeKind.Utc);
        ProcessTypePolicy policy = new()
        {
            ProcessType = "test",
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
            UpdatedAt = updatedAt
        };

        // Act
        ProcessTypePolicyDocument document = PolicyMapper.MapToDocument(policy);

        // Assert
        document.UpdatedAt.Should().Be(updatedAt);
    }

    #endregion

    #region Helper Methods

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

    private static ProcessTypePolicyDocument CreateValidProcessTypePolicyDocument() => new()
    {
        ProcessType = "order",
        Timeout = TimeSpan.FromMinutes(10),
        RetryPolicy = new RetryPolicyDocument
        {
            Enabled = true,
            MaxAttempts = 3,
            InitialDelay = TimeSpan.FromSeconds(10),
            BackoffStrategy = "Exponential",
            MaxDelay = TimeSpan.FromMinutes(5)
        },
        ResultRetention = TimeSpan.FromDays(7),
        MaxConcurrentProcesses = 100,
        UpdatedAt = DateTime.UtcNow
    };

    private static ClientPolicyOverride CreateValidClientOverride() => new()
    {
        ClientId = "test-client",
        ProcessType = "order",
        Timeout = TimeSpan.FromMinutes(20),
        RetryPolicy = new RetryPolicy
        {
            Enabled = true,
            MaxAttempts = 5,
            InitialDelay = TimeSpan.FromSeconds(5),
            BackoffStrategy = BackoffStrategy.Linear,
            MaxDelay = TimeSpan.FromMinutes(10)
        },
        ResultRetention = TimeSpan.FromDays(30),
        MaxConcurrentProcesses = 200,
        UpdatedAt = DateTime.UtcNow
    };

    private static ClientPolicyOverrideDocument CreateValidClientOverrideDocument() => new()
    {
        Id = ObjectId.GenerateNewId(),
        ClientId = "test-client",
        ProcessType = "order",
        Timeout = TimeSpan.FromMinutes(20),
        RetryPolicy = new RetryPolicyDocument
        {
            Enabled = true,
            MaxAttempts = 5,
            InitialDelay = TimeSpan.FromSeconds(5),
            BackoffStrategy = "Linear",
            MaxDelay = TimeSpan.FromMinutes(10)
        },
        ResultRetention = TimeSpan.FromDays(30),
        MaxConcurrentProcesses = 200,
        UpdatedAt = DateTime.UtcNow
    };

    #endregion
}
