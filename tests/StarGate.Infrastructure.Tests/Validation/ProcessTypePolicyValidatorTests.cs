using FluentAssertions;
using StarGate.Core.Domain.Configuration;
using StarGate.Infrastructure.Validation;
using Xunit;

namespace StarGate.Infrastructure.Tests.Validation;

/// <summary>
/// Tests for ProcessTypePolicyValidator.
/// Verifies all validation rules and constraint boundaries.
/// </summary>
public class ProcessTypePolicyValidatorTests
{
    private readonly ProcessTypePolicyValidator _validator;

    public ProcessTypePolicyValidatorTests()
    {
        _validator = new ProcessTypePolicyValidator();
    }

    [Fact]
    public void Validate_Should_Succeed_WhenPolicyIsValid()
    {
        // Arrange
        var policy = CreateValidPolicy();

        // Act
        var result = _validator.Validate(policy);

        // Assert
        result.IsValid.Should().BeTrue();
        result.Errors.Should().BeEmpty();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(3601)]
    public void Validate_Should_Fail_WhenTimeoutOutOfRange(int timeoutSeconds)
    {
        // Arrange
        var policy = CreateValidPolicy() with { Timeout = TimeSpan.FromSeconds(timeoutSeconds) };

        // Act
        var result = _validator.Validate(policy);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.ErrorCode == "TIMEOUT_OUT_OF_RANGE");
    }

    [Theory]
    [InlineData(1)]    // Minimum valid
    [InlineData(300)]  // Common value (5 minutes)
    [InlineData(3600)] // Maximum valid (1 hour)
    public void Validate_Should_Succeed_WhenTimeoutWithinRange(int timeoutSeconds)
    {
        // Arrange
        var policy = CreateValidPolicy() with { Timeout = TimeSpan.FromSeconds(timeoutSeconds) };

        // Act
        var result = _validator.Validate(policy);

        // Assert
        result.IsValid.Should().BeTrue();
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(11)]
    public void Validate_Should_Fail_WhenMaxRetryOutOfRange(int maxRetry)
    {
        // Arrange
        var policy = CreateValidPolicy() with 
        { 
            RetryPolicy = CreateValidRetryPolicy() with { MaxAttempts = maxRetry }
        };

        // Act
        var result = _validator.Validate(policy);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.ErrorCode == "MAX_RETRY_OUT_OF_RANGE");
    }

    [Theory]
    [InlineData(0)]  // Minimum valid (no retry)
    [InlineData(3)]  // Common value
    [InlineData(10)] // Maximum valid
    public void Validate_Should_Succeed_WhenMaxRetryWithinRange(int maxRetry)
    {
        // Arrange
        var policy = CreateValidPolicy() with 
        { 
            RetryPolicy = CreateValidRetryPolicy() with { MaxAttempts = maxRetry }
        };

        // Act
        var result = _validator.Validate(policy);

        // Assert
        result.IsValid.Should().BeTrue();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(101)]
    public void Validate_Should_Fail_WhenMaxConcurrencyOutOfRange(int maxConcurrency)
    {
        // Arrange
        var policy = CreateValidPolicy() with { MaxConcurrentProcesses = maxConcurrency };

        // Act
        var result = _validator.Validate(policy);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.ErrorCode == "MAX_CONCURRENCY_OUT_OF_RANGE");
    }

    [Theory]
    [InlineData(1)]   // Minimum valid
    [InlineData(10)]  // Common value
    [InlineData(100)] // Maximum valid
    public void Validate_Should_Succeed_WhenMaxConcurrencyWithinRange(int maxConcurrency)
    {
        // Arrange
        var policy = CreateValidPolicy() with { MaxConcurrentProcesses = maxConcurrency };

        // Act
        var result = _validator.Validate(policy);

        // Assert
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_Should_Succeed_WhenMaxConcurrencyIsNull()
    {
        // Arrange
        var policy = CreateValidPolicy() with { MaxConcurrentProcesses = null };

        // Act
        var result = _validator.Validate(policy);

        // Assert
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_Should_Fail_WhenProcessTypeEmpty()
    {
        // Arrange
        var policy = CreateValidPolicy() with { ProcessType = "" };

        // Act
        var result = _validator.Validate(policy);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.ErrorCode == "PROCESS_TYPE_REQUIRED");
    }

    [Fact]
    public void Validate_Should_Fail_WhenProcessTypeTooLong()
    {
        // Arrange
        var policy = CreateValidPolicy() with { ProcessType = new string('a', 101) };

        // Act
        var result = _validator.Validate(policy);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.ErrorCode == "PROCESS_TYPE_TOO_LONG");
    }

    [Fact]
    public void Validate_Should_Fail_WhenRetryPolicyIsNull()
    {
        // Arrange
        var policy = CreateValidPolicy() with { RetryPolicy = null! };

        // Act
        var result = _validator.Validate(policy);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.ErrorCode == "RETRY_POLICY_REQUIRED");
    }

    [Fact]
    public void Validate_Should_Fail_WhenResultRetentionIsNegative()
    {
        // Arrange
        var policy = CreateValidPolicy() with { ResultRetention = TimeSpan.FromDays(-1) };

        // Act
        var result = _validator.Validate(policy);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.ErrorCode == "RESULT_RETENTION_NEGATIVE");
    }

    [Fact]
    public void Validate_Should_Fail_WhenUpdatedAtIsEmpty()
    {
        // Arrange
        var policy = CreateValidPolicy() with { UpdatedAt = default };

        // Act
        var result = _validator.Validate(policy);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.ErrorCode == "UPDATED_AT_REQUIRED");
    }

    [Fact]
    public void Validate_Should_ReturnMultipleErrors_WhenMultipleFieldsInvalid()
    {
        // Arrange
        var policy = CreateValidPolicy() with 
        { 
            ProcessType = "",
            Timeout = TimeSpan.FromSeconds(0),
            MaxConcurrentProcesses = 0
        };

        // Act
        var result = _validator.Validate(policy);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().HaveCountGreaterOrEqualTo(3);
        result.Errors.Should().Contain(e => e.ErrorCode == "PROCESS_TYPE_REQUIRED");
        result.Errors.Should().Contain(e => e.ErrorCode == "TIMEOUT_OUT_OF_RANGE");
        result.Errors.Should().Contain(e => e.ErrorCode == "MAX_CONCURRENCY_OUT_OF_RANGE");
    }

    private static ProcessTypePolicy CreateValidPolicy() => new()
    {
        ProcessType = "order",
        Timeout = TimeSpan.FromMinutes(5),
        RetryPolicy = CreateValidRetryPolicy(),
        ResultRetention = TimeSpan.FromDays(30),
        MaxConcurrentProcesses = 10,
        UpdatedAt = DateTime.UtcNow
    };

    private static RetryPolicy CreateValidRetryPolicy() => new()
    {
        Enabled = true,
        MaxAttempts = 3,
        InitialDelay = TimeSpan.FromSeconds(5),
        BackoffStrategy = BackoffStrategy.Exponential,
        MaxDelay = TimeSpan.FromMinutes(5)
    };
}
