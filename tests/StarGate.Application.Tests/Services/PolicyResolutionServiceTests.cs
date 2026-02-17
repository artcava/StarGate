using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using StarGate.Application.Services;
using StarGate.Core.Domain.Configuration;

namespace StarGate.Application.Tests.Services;

/// <summary>
/// Unit tests for PolicyResolutionService.
/// Tests policy resolution, validation, comparison, and edge cases.
/// </summary>
public class PolicyResolutionServiceTests
{
    private readonly PolicyResolutionService _service;

    public PolicyResolutionServiceTests()
    {
        _service = new PolicyResolutionService(NullLogger<PolicyResolutionService>.Instance);
    }

    #region ResolvePolicy Tests

    [Fact]
    public void ResolvePolicy_Should_ReturnTypeDefault_WhenNoOverride()
    {
        // Arrange
        var typeDefault = CreateDefaultPolicy("order");

        // Act
        var result = _service.ResolvePolicy(typeDefault, clientOverride: null);

        // Assert
        result.Should().BeEquivalentTo(typeDefault);
    }

    [Fact]
    public void ResolvePolicy_Should_ApplyTimeoutOverride()
    {
        // Arrange
        var typeDefault = CreateDefaultPolicy("order");
        var clientOverride = new ClientPolicyOverride
        {
            ClientId = "client-123",
            ProcessType = "order",
            Timeout = TimeSpan.FromMinutes(10), // Override: 10 min instead of 5 min
            UpdatedAt = DateTime.UtcNow
        };

        // Act
        var result = _service.ResolvePolicy(typeDefault, clientOverride);

        // Assert
        result.Timeout.Should().Be(TimeSpan.FromMinutes(10));
        result.RetryPolicy.Should().BeEquivalentTo(typeDefault.RetryPolicy);
        result.ResultRetention.Should().Be(typeDefault.ResultRetention);
    }

    [Fact]
    public void ResolvePolicy_Should_ApplyRetryPolicyOverride()
    {
        // Arrange
        var typeDefault = CreateDefaultPolicy("order");
        var customRetryPolicy = new RetryPolicy
        {
            Enabled = true,
            MaxAttempts = 5,
            InitialDelay = TimeSpan.FromSeconds(10),
            BackoffStrategy = BackoffStrategy.Linear,
            MaxDelay = TimeSpan.FromMinutes(10)
        };
        var clientOverride = new ClientPolicyOverride
        {
            ClientId = "client-123",
            ProcessType = "order",
            RetryPolicy = customRetryPolicy,
            UpdatedAt = DateTime.UtcNow
        };

        // Act
        var result = _service.ResolvePolicy(typeDefault, clientOverride);

        // Assert
        result.RetryPolicy.Should().BeEquivalentTo(customRetryPolicy);
        result.Timeout.Should().Be(typeDefault.Timeout);
    }

    [Fact]
    public void ResolvePolicy_Should_ApplyAllOverrides()
    {
        // Arrange
        var typeDefault = CreateDefaultPolicy("order");
        var customRetryPolicy = new RetryPolicy
        {
            Enabled = false,
            MaxAttempts = 0,
            InitialDelay = TimeSpan.Zero,
            BackoffStrategy = BackoffStrategy.Linear,
            MaxDelay = TimeSpan.Zero
        };
        var clientOverride = new ClientPolicyOverride
        {
            ClientId = "client-123",
            ProcessType = "order",
            Timeout = TimeSpan.FromMinutes(10),
            RetryPolicy = customRetryPolicy,
            ResultRetention = TimeSpan.FromDays(90),
            MaxConcurrentProcesses = 20,
            UpdatedAt = DateTime.UtcNow
        };

        // Act
        var result = _service.ResolvePolicy(typeDefault, clientOverride);

        // Assert
        result.Timeout.Should().Be(TimeSpan.FromMinutes(10));
        result.RetryPolicy.Should().BeEquivalentTo(customRetryPolicy);
        result.ResultRetention.Should().Be(TimeSpan.FromDays(90));
        result.MaxConcurrentProcesses.Should().Be(20);
    }

    [Fact]
    public void ResolvePolicy_Should_ThrowArgumentNull_WhenTypeDefaultNull()
    {
        // Arrange
        var clientOverride = new ClientPolicyOverride
        {
            ClientId = "client-123",
            ProcessType = "order",
            Timeout = TimeSpan.FromMinutes(10),
            UpdatedAt = DateTime.UtcNow
        };

        // Act
        Action act = () => _service.ResolvePolicy(null!, clientOverride);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    #endregion

    #region ValidatePolicy Tests

    [Fact]
    public void ValidatePolicy_Should_ReturnValid_ForValidPolicy()
    {
        // Arrange
        var policy = CreateDefaultPolicy("order");

        // Act
        var result = _service.ValidatePolicy(policy);

        // Assert
        result.IsValid.Should().BeTrue();
        result.Errors.Should().BeEmpty();
    }

    [Fact]
    public void ValidatePolicy_Should_ReturnInvalid_WhenTimeoutTooLarge()
    {
        // Arrange
        var policy = CreateDefaultPolicy("order");
        policy = policy with { Timeout = TimeSpan.FromHours(25) }; // Exceeds 24 hours

        // Act
        var result = _service.ValidatePolicy(policy);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Contains("Timeout") && e.Contains("24 hours"));
    }

    [Fact]
    public void ValidatePolicy_Should_ReturnInvalid_WhenTimeoutNegative()
    {
        // Arrange
        var policy = CreateDefaultPolicy("order");
        policy = policy with { Timeout = TimeSpan.FromSeconds(-10) };

        // Act
        var result = _service.ValidatePolicy(policy);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Contains("Timeout") && e.Contains("positive"));
    }

    [Fact]
    public void ValidatePolicy_Should_ReturnInvalid_WhenMaxRetryAttemptsNegative()
    {
        // Arrange
        var policy = CreateDefaultPolicy("order");
        var invalidRetryPolicy = policy.RetryPolicy! with { MaxAttempts = -1 };
        policy = policy with { RetryPolicy = invalidRetryPolicy };

        // Act
        var result = _service.ValidatePolicy(policy);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Contains("MaxAttempts") && e.Contains("negative"));
    }

    [Fact]
    public void ValidatePolicy_Should_ReturnInvalid_WhenMaxRetryAttemptsExceedsLimit()
    {
        // Arrange
        var policy = CreateDefaultPolicy("order");
        var invalidRetryPolicy = policy.RetryPolicy! with { MaxAttempts = 20 }; // Exceeds max of 10
        policy = policy with { RetryPolicy = invalidRetryPolicy };

        // Act
        var result = _service.ValidatePolicy(policy);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Contains("MaxAttempts") && e.Contains("10"));
    }

    [Fact]
    public void ValidatePolicy_Should_ReturnInvalid_WhenRetentionExceedsLimit()
    {
        // Arrange
        var policy = CreateDefaultPolicy("order");
        policy = policy with { ResultRetention = TimeSpan.FromDays(400) }; // Exceeds 365 days

        // Act
        var result = _service.ValidatePolicy(policy);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Contains("ResultRetention") && e.Contains("365"));
    }

    [Fact]
    public void ValidatePolicy_Should_ReturnInvalid_WhenMaxConcurrentProcessesNegative()
    {
        // Arrange
        var policy = CreateDefaultPolicy("order");
        policy = policy with { MaxConcurrentProcesses = -5 };

        // Act
        var result = _service.ValidatePolicy(policy);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Contains("MaxConcurrentProcesses") && e.Contains("positive"));
    }

    [Fact]
    public void ValidatePolicy_Should_ReturnInvalid_WhenMaxConcurrentProcessesExceedsLimit()
    {
        // Arrange
        var policy = CreateDefaultPolicy("order");
        policy = policy with { MaxConcurrentProcesses = 2000 }; // Exceeds 1000

        // Act
        var result = _service.ValidatePolicy(policy);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Contains("MaxConcurrentProcesses") && e.Contains("1000"));
    }

    [Fact]
    public void ValidatePolicy_Should_ReturnMultipleErrors_WhenMultipleFieldsInvalid()
    {
        // Arrange
        var policy = CreateDefaultPolicy("order");
        var invalidRetryPolicy = policy.RetryPolicy! with { MaxAttempts = 20 };
        policy = policy with
        {
            Timeout = TimeSpan.FromSeconds(-1),
            RetryPolicy = invalidRetryPolicy,
            MaxConcurrentProcesses = 2000
        };

        // Act
        var result = _service.ValidatePolicy(policy);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().HaveCountGreaterOrEqualTo(3);
    }

    [Fact]
    public void ValidatePolicy_Should_ReturnInvalid_WhenProcessTypeEmpty()
    {
        // Arrange
        var policy = CreateDefaultPolicy("");

        // Act
        var result = _service.ValidatePolicy(policy);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Contains("ProcessType"));
    }

    [Fact]
    public void ValidatePolicy_Should_ThrowArgumentNull_WhenPolicyNull()
    {
        // Act
        Action act = () => _service.ValidatePolicy(null!);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    #endregion

    #region ValidateClientOverride Tests

    [Fact]
    public void ValidateClientOverride_Should_ReturnValid_ForValidOverride()
    {
        // Arrange
        var clientOverride = new ClientPolicyOverride
        {
            ClientId = "client-123",
            ProcessType = "order",
            Timeout = TimeSpan.FromMinutes(10),
            UpdatedAt = DateTime.UtcNow
        };

        // Act
        var result = _service.ValidateClientOverride(clientOverride);

        // Assert
        result.IsValid.Should().BeTrue();
        result.Errors.Should().BeEmpty();
    }

    [Fact]
    public void ValidateClientOverride_Should_ReturnInvalid_WhenTimeoutInvalid()
    {
        // Arrange
        var clientOverride = new ClientPolicyOverride
        {
            ClientId = "client-123",
            ProcessType = "order",
            Timeout = TimeSpan.FromSeconds(-100),
            UpdatedAt = DateTime.UtcNow
        };

        // Act
        var result = _service.ValidateClientOverride(clientOverride);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Contains("Timeout") && e.Contains("positive"));
    }

    [Fact]
    public void ValidateClientOverride_Should_ReturnInvalid_WhenClientIdEmpty()
    {
        // Arrange
        var clientOverride = new ClientPolicyOverride
        {
            ClientId = "",
            ProcessType = "order",
            Timeout = TimeSpan.FromMinutes(5),
            UpdatedAt = DateTime.UtcNow
        };

        // Act
        var result = _service.ValidateClientOverride(clientOverride);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Contains("ClientId"));
    }

    [Fact]
    public void ValidateClientOverride_Should_ReturnInvalid_WhenProcessTypeEmpty()
    {
        // Arrange
        var clientOverride = new ClientPolicyOverride
        {
            ClientId = "client-123",
            ProcessType = "",
            Timeout = TimeSpan.FromMinutes(5),
            UpdatedAt = DateTime.UtcNow
        };

        // Act
        var result = _service.ValidateClientOverride(clientOverride);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Contains("ProcessType"));
    }

    [Fact]
    public void ValidateClientOverride_Should_ThrowArgumentNull_WhenOverrideNull()
    {
        // Act
        Action act = () => _service.ValidateClientOverride(null!);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    #endregion

    #region ComparePolicies Tests

    [Fact]
    public void ComparePolicies_Should_ReturnNoDifferences_WhenIdentical()
    {
        // Arrange
        var policy1 = CreateDefaultPolicy("order");
        var policy2 = CreateDefaultPolicy("order");

        // Act
        var result = _service.ComparePolicies(policy1, policy2);

        // Assert
        result.HasDifferences.Should().BeFalse();
        result.Differences.Should().BeEmpty();
    }

    [Fact]
    public void ComparePolicies_Should_ReturnDifferences_WhenTimeoutDifferent()
    {
        // Arrange
        var policy1 = CreateDefaultPolicy("order");
        var policy2 = CreateDefaultPolicy("order");
        policy2 = policy2 with { Timeout = TimeSpan.FromMinutes(10) };

        // Act
        var result = _service.ComparePolicies(policy1, policy2);

        // Assert
        result.HasDifferences.Should().BeTrue();
        result.Differences.Should().Contain(d => d.Contains("Timeout"));
    }

    [Fact]
    public void ComparePolicies_Should_ReturnDifferences_WhenRetryPolicyDifferent()
    {
        // Arrange
        var policy1 = CreateDefaultPolicy("order");
        var policy2 = CreateDefaultPolicy("order");
        var differentRetryPolicy = policy2.RetryPolicy! with { MaxAttempts = 5 };
        policy2 = policy2 with { RetryPolicy = differentRetryPolicy };

        // Act
        var result = _service.ComparePolicies(policy1, policy2);

        // Assert
        result.HasDifferences.Should().BeTrue();
        result.Differences.Should().Contain(d => d.Contains("RetryPolicy"));
    }

    [Fact]
    public void ComparePolicies_Should_ReturnDifferences_WhenRetentionDifferent()
    {
        // Arrange
        var policy1 = CreateDefaultPolicy("order");
        var policy2 = CreateDefaultPolicy("order");
        policy2 = policy2 with { ResultRetention = TimeSpan.FromDays(90) };

        // Act
        var result = _service.ComparePolicies(policy1, policy2);

        // Assert
        result.HasDifferences.Should().BeTrue();
        result.Differences.Should().Contain(d => d.Contains("ResultRetention"));
    }

    [Fact]
    public void ComparePolicies_Should_ReturnDifferences_WhenConcurrencyDifferent()
    {
        // Arrange
        var policy1 = CreateDefaultPolicy("order");
        var policy2 = CreateDefaultPolicy("order");
        policy2 = policy2 with { MaxConcurrentProcesses = 20 };

        // Act
        var result = _service.ComparePolicies(policy1, policy2);

        // Assert
        result.HasDifferences.Should().BeTrue();
        result.Differences.Should().Contain(d => d.Contains("MaxConcurrentProcesses"));
    }

    [Fact]
    public void ComparePolicies_Should_ReturnMultipleDifferences_WhenMultipleFieldsDifferent()
    {
        // Arrange
        var policy1 = CreateDefaultPolicy("order");
        var policy2 = CreateDefaultPolicy("order");
        policy2 = policy2 with
        {
            Timeout = TimeSpan.FromMinutes(10),
            ResultRetention = TimeSpan.FromDays(90),
            MaxConcurrentProcesses = 20
        };

        // Act
        var result = _service.ComparePolicies(policy1, policy2);

        // Assert
        result.HasDifferences.Should().BeTrue();
        result.Differences.Should().HaveCount(3);
    }

    [Fact]
    public void ComparePolicies_Should_ThrowArgumentNull_WhenBaselineNull()
    {
        // Arrange
        var policy = CreateDefaultPolicy("order");

        // Act
        Action act = () => _service.ComparePolicies(null!, policy);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void ComparePolicies_Should_ThrowArgumentNull_WhenComparisonNull()
    {
        // Arrange
        var policy = CreateDefaultPolicy("order");

        // Act
        Action act = () => _service.ComparePolicies(policy, null!);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    #endregion

    #region HasEffectiveOverride Tests

    [Fact]
    public void HasEffectiveOverride_Should_ReturnTrue_WhenTimeoutOverrideChangesValue()
    {
        // Arrange
        var typeDefault = CreateDefaultPolicy("order");
        var clientOverride = new ClientPolicyOverride
        {
            ClientId = "client-123",
            ProcessType = "order",
            Timeout = TimeSpan.FromMinutes(10), // Different from default
            UpdatedAt = DateTime.UtcNow
        };

        // Act
        var result = _service.HasEffectiveOverride(typeDefault, clientOverride);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void HasEffectiveOverride_Should_ReturnTrue_WhenRetryPolicyOverrideChangesValue()
    {
        // Arrange
        var typeDefault = CreateDefaultPolicy("order");
        var differentRetryPolicy = typeDefault.RetryPolicy! with { MaxAttempts = 5 };
        var clientOverride = new ClientPolicyOverride
        {
            ClientId = "client-123",
            ProcessType = "order",
            RetryPolicy = differentRetryPolicy,
            UpdatedAt = DateTime.UtcNow
        };

        // Act
        var result = _service.HasEffectiveOverride(typeDefault, clientOverride);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void HasEffectiveOverride_Should_ReturnFalse_WhenOverrideMatchesDefault()
    {
        // Arrange
        var typeDefault = CreateDefaultPolicy("order");
        var clientOverride = new ClientPolicyOverride
        {
            ClientId = "client-123",
            ProcessType = "order",
            Timeout = TimeSpan.FromMinutes(5), // Same as default
            UpdatedAt = DateTime.UtcNow
        };

        // Act
        var result = _service.HasEffectiveOverride(typeDefault, clientOverride);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void HasEffectiveOverride_Should_ReturnFalse_WhenNoOverridesProvided()
    {
        // Arrange
        var typeDefault = CreateDefaultPolicy("order");
        var clientOverride = new ClientPolicyOverride
        {
            ClientId = "client-123",
            ProcessType = "order",
            UpdatedAt = DateTime.UtcNow
        };

        // Act
        var result = _service.HasEffectiveOverride(typeDefault, clientOverride);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void HasEffectiveOverride_Should_ThrowArgumentNull_WhenTypeDefaultNull()
    {
        // Arrange
        var clientOverride = new ClientPolicyOverride
        {
            ClientId = "client-123",
            ProcessType = "order",
            Timeout = TimeSpan.FromMinutes(10),
            UpdatedAt = DateTime.UtcNow
        };

        // Act
        Action act = () => _service.HasEffectiveOverride(null!, clientOverride);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void HasEffectiveOverride_Should_ThrowArgumentNull_WhenClientOverrideNull()
    {
        // Arrange
        var typeDefault = CreateDefaultPolicy("order");

        // Act
        Action act = () => _service.HasEffectiveOverride(typeDefault, null!);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    #endregion

    #region Helper Methods

    private static ProcessTypePolicy CreateDefaultPolicy(string processType) => new()
    {
        ProcessType = processType,
        Timeout = TimeSpan.FromMinutes(5),
        RetryPolicy = new RetryPolicy
        {
            Enabled = true,
            MaxAttempts = 3,
            InitialDelay = TimeSpan.FromSeconds(5),
            BackoffStrategy = BackoffStrategy.Exponential,
            MaxDelay = TimeSpan.FromMinutes(5)
        },
        ResultRetention = TimeSpan.FromDays(30),
        MaxConcurrentProcesses = 10,
        UpdatedAt = DateTime.UtcNow
    };

    #endregion
}
