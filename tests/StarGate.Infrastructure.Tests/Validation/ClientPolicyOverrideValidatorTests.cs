namespace StarGate.Infrastructure.Tests.Validation;

using FluentAssertions;
using StarGate.Core.Domain.Configuration;
using StarGate.Infrastructure.Validation;
using Xunit;

/// <summary>
/// Tests for ClientPolicyOverrideValidator.
/// Verifies validation rules with focus on conditional (optional) field validation.
/// </summary>
public class ClientPolicyOverrideValidatorTests
{
    private readonly ClientPolicyOverrideValidator _validator;

    public ClientPolicyOverrideValidatorTests()
    {
        _validator = new ClientPolicyOverrideValidator();
    }

    [Fact]
    public void Validate_Should_Succeed_WhenOverrideIsValid()
    {
        // Arrange
        var clientOverride = CreateValidOverride();

        // Act
        var result = _validator.Validate(clientOverride);

        // Assert
        result.IsValid.Should().BeTrue();
        result.Errors.Should().BeEmpty();
    }

    [Fact]
    public void Validate_Should_Succeed_WhenOnlyRequiredFieldsProvided()
    {
        // Arrange
        var clientOverride = new ClientPolicyOverride
        {
            ClientId = "client-123",
            ProcessType = "order",
            Timeout = null,
            RetryPolicy = null,
            ResultRetention = null,
            MaxConcurrentProcesses = null,
            UpdatedAt = DateTime.UtcNow
        };

        // Act
        var result = _validator.Validate(clientOverride);

        // Assert
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_Should_Fail_WhenClientIdEmpty()
    {
        // Arrange
        var clientOverride = CreateValidOverride() with { ClientId = "" };

        // Act
        var result = _validator.Validate(clientOverride);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.ErrorCode == "CLIENT_ID_REQUIRED");
    }

    [Fact]
    public void Validate_Should_Fail_WhenClientIdTooLong()
    {
        // Arrange
        var clientOverride = CreateValidOverride() with { ClientId = new string('a', 101) };

        // Act
        var result = _validator.Validate(clientOverride);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.ErrorCode == "CLIENT_ID_TOO_LONG");
    }

    [Fact]
    public void Validate_Should_Fail_WhenProcessTypeEmpty()
    {
        // Arrange
        var clientOverride = CreateValidOverride() with { ProcessType = "" };

        // Act
        var result = _validator.Validate(clientOverride);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.ErrorCode == "PROCESS_TYPE_REQUIRED");
    }

    [Fact]
    public void Validate_Should_Fail_WhenProcessTypeTooLong()
    {
        // Arrange
        var clientOverride = CreateValidOverride() with { ProcessType = new string('a', 101) };

        // Act
        var result = _validator.Validate(clientOverride);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.ErrorCode == "PROCESS_TYPE_TOO_LONG");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(3601)]
    public void Validate_Should_Fail_WhenTimeoutOutOfRange(int timeoutSeconds)
    {
        // Arrange
        var clientOverride = CreateValidOverride() with { Timeout = TimeSpan.FromSeconds(timeoutSeconds) };

        // Act
        var result = _validator.Validate(clientOverride);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.ErrorCode == "TIMEOUT_OUT_OF_RANGE");
    }

    [Theory]
    [InlineData(1)]
    [InlineData(300)]
    [InlineData(3600)]
    public void Validate_Should_Succeed_WhenTimeoutWithinRange(int timeoutSeconds)
    {
        // Arrange
        var clientOverride = CreateValidOverride() with { Timeout = TimeSpan.FromSeconds(timeoutSeconds) };

        // Act
        var result = _validator.Validate(clientOverride);

        // Assert
        result.IsValid.Should().BeTrue();
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(11)]
    public void Validate_Should_Fail_WhenMaxRetryOutOfRange(int maxRetry)
    {
        // Arrange
        var clientOverride = CreateValidOverride() with 
        { 
            RetryPolicy = new RetryPolicy 
            { 
                MaxAttempts = maxRetry, 
                BackoffStrategy = BackoffStrategy.Exponential 
            } 
        };

        // Act
        var result = _validator.Validate(clientOverride);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.ErrorCode == "MAX_RETRY_OUT_OF_RANGE");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(3)]
    [InlineData(10)]
    public void Validate_Should_Succeed_WhenMaxRetryWithinRange(int maxRetry)
    {
        // Arrange
        var clientOverride = CreateValidOverride() with 
        { 
            RetryPolicy = new RetryPolicy 
            { 
                MaxAttempts = maxRetry, 
                BackoffStrategy = BackoffStrategy.Exponential 
            } 
        };

        // Act
        var result = _validator.Validate(clientOverride);

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
        var clientOverride = CreateValidOverride() with { MaxConcurrentProcesses = maxConcurrency };

        // Act
        var result = _validator.Validate(clientOverride);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.ErrorCode == "MAX_CONCURRENCY_OUT_OF_RANGE");
    }

    [Theory]
    [InlineData(1)]
    [InlineData(10)]
    [InlineData(100)]
    public void Validate_Should_Succeed_WhenMaxConcurrencyWithinRange(int maxConcurrency)
    {
        // Arrange
        var clientOverride = CreateValidOverride() with { MaxConcurrentProcesses = maxConcurrency };

        // Act
        var result = _validator.Validate(clientOverride);

        // Assert
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_Should_Fail_WhenResultRetentionIsNegative()
    {
        // Arrange
        var clientOverride = CreateValidOverride() with { ResultRetention = TimeSpan.FromDays(-1) };

        // Act
        var result = _validator.Validate(clientOverride);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.ErrorCode == "RESULT_RETENTION_NEGATIVE");
    }

    [Fact]
    public void Validate_Should_Succeed_WhenResultRetentionIsZero()
    {
        // Arrange
        var clientOverride = CreateValidOverride() with { ResultRetention = TimeSpan.Zero };

        // Act
        var result = _validator.Validate(clientOverride);

        // Assert
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_Should_Fail_WhenUpdatedAtIsEmpty()
    {
        // Arrange
        var clientOverride = CreateValidOverride() with { UpdatedAt = default };

        // Act
        var result = _validator.Validate(clientOverride);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.ErrorCode == "UPDATED_AT_REQUIRED");
    }

    [Fact]
    public void Validate_Should_ReturnMultipleErrors_WhenMultipleFieldsInvalid()
    {
        // Arrange
        var clientOverride = CreateValidOverride() with 
        { 
            ClientId = "",
            ProcessType = "",
            Timeout = TimeSpan.FromSeconds(0),
            MaxConcurrentProcesses = 0
        };

        // Act
        var result = _validator.Validate(clientOverride);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().HaveCountGreaterOrEqualTo(4);
        result.Errors.Should().Contain(e => e.ErrorCode == "CLIENT_ID_REQUIRED");
        result.Errors.Should().Contain(e => e.ErrorCode == "PROCESS_TYPE_REQUIRED");
        result.Errors.Should().Contain(e => e.ErrorCode == "TIMEOUT_OUT_OF_RANGE");
        result.Errors.Should().Contain(e => e.ErrorCode == "MAX_CONCURRENCY_OUT_OF_RANGE");
    }

    [Fact]
    public void Validate_Should_NotValidateOptionalFields_WhenNotProvided()
    {
        // Arrange - override with no optional fields, but if they were present they would be invalid
        var clientOverride = new ClientPolicyOverride
        {
            ClientId = "client-123",
            ProcessType = "order",
            Timeout = null,
            RetryPolicy = null,
            ResultRetention = null,
            MaxConcurrentProcesses = null,
            UpdatedAt = DateTime.UtcNow
        };

        // Act
        var result = _validator.Validate(clientOverride);

        // Assert
        result.IsValid.Should().BeTrue();
        result.Errors.Should().BeEmpty();
    }

    private static ClientPolicyOverride CreateValidOverride() => new()
    {
        ClientId = "client-123",
        ProcessType = "order",
        Timeout = TimeSpan.FromMinutes(10),
        RetryPolicy = new RetryPolicy
        {
            MaxAttempts = 5,
            BackoffStrategy = BackoffStrategy.Linear
        },
        ResultRetention = TimeSpan.FromDays(60),
        MaxConcurrentProcesses = 20,
        UpdatedAt = DateTime.UtcNow
    };
}
