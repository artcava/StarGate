using FluentAssertions;
using FluentValidation;
using FluentValidation.Results;
using Microsoft.Extensions.Logging;
using NSubstitute;
using StarGate.Core.Domain.Configuration;
using StarGate.Infrastructure.Services;
using Xunit;

namespace StarGate.Infrastructure.Tests.Services;

/// <summary>
/// Tests for PolicyValidator service.
/// Verifies integration of FluentValidation validators with custom validation result model.
/// </summary>
public class PolicyValidatorTests
{
    private readonly IValidator<ProcessTypePolicy> _typePolicyValidator;
    private readonly IValidator<ClientPolicyOverride> _clientOverrideValidator;
    private readonly ILogger<PolicyValidator> _logger;
    private readonly PolicyValidator _policyValidator;

    public PolicyValidatorTests()
    {
        _typePolicyValidator = Substitute.For<IValidator<ProcessTypePolicy>>();
        _clientOverrideValidator = Substitute.For<IValidator<ClientPolicyOverride>>();
        _logger = Substitute.For<ILogger<PolicyValidator>>();
        
        _policyValidator = new PolicyValidator(
            _typePolicyValidator,
            _clientOverrideValidator,
            _logger);
    }

    #region Constructor Tests

    [Fact]
    public void Constructor_Should_ThrowArgumentNullException_WhenTypePolicyValidatorIsNull()
    {
        // Act & Assert
        var act = () => new PolicyValidator(null!, _clientOverrideValidator, _logger);
        act.Should().Throw<ArgumentNullException>().WithParameterName("typePolicyValidator");
    }

    [Fact]
    public void Constructor_Should_ThrowArgumentNullException_WhenClientOverrideValidatorIsNull()
    {
        // Act & Assert
        var act = () => new PolicyValidator(_typePolicyValidator, null!, _logger);
        act.Should().Throw<ArgumentNullException>().WithParameterName("clientOverrideValidator");
    }

    [Fact]
    public void Constructor_Should_ThrowArgumentNullException_WhenLoggerIsNull()
    {
        // Act & Assert
        var act = () => new PolicyValidator(_typePolicyValidator, _clientOverrideValidator, null!);
        act.Should().Throw<ArgumentNullException>().WithParameterName("logger");
    }

    #endregion

    #region ValidateTypePolicy Tests

    [Fact]
    public void ValidateTypePolicy_Should_ThrowArgumentNullException_WhenPolicyIsNull()
    {
        // Act & Assert
        var act = () => _policyValidator.ValidateTypePolicy(null!);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void ValidateTypePolicy_Should_ReturnSuccess_WhenValidationSucceeds()
    {
        // Arrange
        var policy = CreateValidTypePolicy();
        var validationResult = new ValidationResult();
        _typePolicyValidator.Validate(policy).Returns(validationResult);

        // Act
        var result = _policyValidator.ValidateTypePolicy(policy);

        // Assert
        result.IsValid.Should().BeTrue();
        result.Errors.Should().BeEmpty();
    }

    [Fact]
    public void ValidateTypePolicy_Should_ReturnFailure_WhenValidationFails()
    {
        // Arrange
        var policy = CreateValidTypePolicy();
        var validationFailures = new List<ValidationFailure>
        {
            new ValidationFailure("ProcessType", "Process type is required")
            {
                ErrorCode = "PROCESS_TYPE_REQUIRED",
                AttemptedValue = ""
            },
            new ValidationFailure("Timeout", "Timeout out of range")
            {
                ErrorCode = "TIMEOUT_OUT_OF_RANGE",
                AttemptedValue = TimeSpan.Zero
            }
        };
        var validationResult = new ValidationResult(validationFailures);
        _typePolicyValidator.Validate(policy).Returns(validationResult);

        // Act
        var result = _policyValidator.ValidateTypePolicy(policy);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().HaveCount(2);
        result.Errors.Should().Contain(e => e.ErrorCode == "PROCESS_TYPE_REQUIRED");
        result.Errors.Should().Contain(e => e.ErrorCode == "TIMEOUT_OUT_OF_RANGE");
    }

    [Fact]
    public void ValidateTypePolicy_Should_LogWarning_WhenValidationFails()
    {
        // Arrange
        var policy = CreateValidTypePolicy();
        var validationFailures = new List<ValidationFailure>
        {
            new ValidationFailure("ProcessType", "Process type is required")
            {
                ErrorCode = "PROCESS_TYPE_REQUIRED"
            }
        };
        var validationResult = new ValidationResult(validationFailures);
        _typePolicyValidator.Validate(policy).Returns(validationResult);

        // Act
        _policyValidator.ValidateTypePolicy(policy);

        // Assert
        _logger.Received(1).Log(
            LogLevel.Warning,
            Arg.Any<EventId>(),
            Arg.Is<object>(o => o.ToString()!.Contains("ProcessTypePolicy validation failed")),
            Arg.Any<Exception>(),
            Arg.Any<Func<object, Exception?, string>>());
    }

    [Fact]
    public void ValidateTypePolicy_Should_MapAllErrorProperties()
    {
        // Arrange
        var policy = CreateValidTypePolicy();
        var validationFailure = new ValidationFailure("Timeout", "Timeout must be positive")
        {
            ErrorCode = "TIMEOUT_INVALID",
            AttemptedValue = TimeSpan.FromSeconds(-1)
        };
        var validationResult = new ValidationResult(new[] { validationFailure });
        _typePolicyValidator.Validate(policy).Returns(validationResult);

        // Act
        var result = _policyValidator.ValidateTypePolicy(policy);

        // Assert
        result.Errors.Should().HaveCount(1);
        var error = result.Errors[0];
        error.PropertyName.Should().Be("Timeout");
        error.ErrorCode.Should().Be("TIMEOUT_INVALID");
        error.ErrorMessage.Should().Be("Timeout must be positive");
        error.AttemptedValue.Should().Be(TimeSpan.FromSeconds(-1));
    }

    #endregion

    #region ValidateClientOverride Tests

    [Fact]
    public void ValidateClientOverride_Should_ThrowArgumentNullException_WhenOverrideIsNull()
    {
        // Act & Assert
        var act = () => _policyValidator.ValidateClientOverride(null!);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void ValidateClientOverride_Should_ReturnSuccess_WhenValidationSucceeds()
    {
        // Arrange
        var clientOverride = CreateValidClientOverride();
        var validationResult = new ValidationResult();
        _clientOverrideValidator.Validate(clientOverride).Returns(validationResult);

        // Act
        var result = _policyValidator.ValidateClientOverride(clientOverride);

        // Assert
        result.IsValid.Should().BeTrue();
        result.Errors.Should().BeEmpty();
    }

    [Fact]
    public void ValidateClientOverride_Should_ReturnFailure_WhenValidationFails()
    {
        // Arrange
        var clientOverride = CreateValidClientOverride();
        var validationFailures = new List<ValidationFailure>
        {
            new ValidationFailure("ClientId", "Client ID is required")
            {
                ErrorCode = "CLIENT_ID_REQUIRED",
                AttemptedValue = ""
            }
        };
        var validationResult = new ValidationResult(validationFailures);
        _clientOverrideValidator.Validate(clientOverride).Returns(validationResult);

        // Act
        var result = _policyValidator.ValidateClientOverride(clientOverride);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().HaveCount(1);
        result.Errors[0].ErrorCode.Should().Be("CLIENT_ID_REQUIRED");
    }

    [Fact]
    public void ValidateClientOverride_Should_LogWarning_WhenValidationFails()
    {
        // Arrange
        var clientOverride = CreateValidClientOverride();
        var validationFailures = new List<ValidationFailure>
        {
            new ValidationFailure("ClientId", "Client ID is required")
            {
                ErrorCode = "CLIENT_ID_REQUIRED"
            }
        };
        var validationResult = new ValidationResult(validationFailures);
        _clientOverrideValidator.Validate(clientOverride).Returns(validationResult);

        // Act
        _policyValidator.ValidateClientOverride(clientOverride);

        // Assert
        _logger.Received(1).Log(
            LogLevel.Warning,
            Arg.Any<EventId>(),
            Arg.Is<object>(o => o.ToString()!.Contains("ClientPolicyOverride validation failed")),
            Arg.Any<Exception>(),
            Arg.Any<Func<object, Exception?, string>>());
    }

    #endregion

    #region Helper Methods

    private static ProcessTypePolicy CreateValidTypePolicy() => new()
    {
        ProcessType = "order",
        Timeout = TimeSpan.FromMinutes(5),
        RetryPolicy = new RetryPolicy
        {
            MaxAttempts = 3,
            BackoffStrategy = BackoffStrategy.Exponential
        },
        ResultRetention = TimeSpan.FromDays(30),
        MaxConcurrentProcesses = 10,
        UpdatedAt = DateTime.UtcNow
    };

    private static ClientPolicyOverride CreateValidClientOverride() => new()
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

    #endregion
}
