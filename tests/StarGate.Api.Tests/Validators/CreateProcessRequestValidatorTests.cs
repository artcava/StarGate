using FluentAssertions;
using StarGate.Api.Validators;
using StarGate.Contracts.Requests;
using Xunit;

namespace StarGate.Api.Tests.Validators;

/// <summary>
/// Unit tests for CreateProcessRequestValidator.
/// </summary>
public class CreateProcessRequestValidatorTests
{
    private readonly CreateProcessRequestValidator _validator;

    public CreateProcessRequestValidatorTests()
    {
        _validator = new CreateProcessRequestValidator();
    }

    [Fact]
    public void Validate_Should_Succeed_WhenRequestIsValid()
    {
        // Arrange
        var request = new CreateProcessRequest
        {
            ClientId = "test-client",
            ProcessType = "order",
            ClientProcessId = "order-123",
            IdempotencyKey = "idempotency-123",
            Metadata = new Dictionary<string, string>
            {
                { "key1", "value1" }
            }
        };

        // Act
        var result = _validator.Validate(request);

        // Assert
        result.IsValid.Should().BeTrue();
        result.Errors.Should().BeEmpty();
    }

#pragma warning disable xUnit1012 // Null should not be used for non-nullable type parameter
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Validate_Should_Fail_WhenClientIdIsEmpty(string? clientId)
    {
        // Arrange
        var request = CreateValidRequest();
        request = request with { ClientId = clientId! };

        // Act
        var result = _validator.Validate(request);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(CreateProcessRequest.ClientId));
    }
#pragma warning restore xUnit1012

    [Fact]
    public void Validate_Should_Fail_WhenClientIdExceedsMaxLength()
    {
        // Arrange
        var request = CreateValidRequest();
        request = request with { ClientId = new string('a', 101) };

        // Act
        var result = _validator.Validate(request);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e =>
            e.PropertyName == nameof(CreateProcessRequest.ClientId) &&
            e.ErrorMessage.Contains("must not exceed 100 characters"));
    }

    [Theory]
    [InlineData("client@invalid")]
    [InlineData("client#invalid")]
    [InlineData("client invalid")]
    [InlineData("client/invalid")]
    public void Validate_Should_Fail_WhenClientIdHasInvalidCharacters(string clientId)
    {
        // Arrange
        var request = CreateValidRequest();
        request = request with { ClientId = clientId };

        // Act
        var result = _validator.Validate(request);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e =>
            e.PropertyName == nameof(CreateProcessRequest.ClientId) &&
            e.ErrorMessage.Contains("invalid characters"));
    }

    [Theory]
    [InlineData("valid-client-123")]
    [InlineData("valid_client_123")]
    [InlineData("valid.client.123")]
    [InlineData("ValidClient123")]
    public void Validate_Should_Succeed_WhenClientIdIsValid(string clientId)
    {
        // Arrange
        var request = CreateValidRequest();
        request = request with { ClientId = clientId };

        // Act
        var result = _validator.Validate(request);

        // Assert
        result.IsValid.Should().BeTrue();
    }

#pragma warning disable xUnit1012 // Null should not be used for non-nullable type parameter
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Validate_Should_Fail_WhenProcessTypeIsEmpty(string? processType)
    {
        // Arrange
        var request = CreateValidRequest();
        request = request with { ProcessType = processType! };

        // Act
        var result = _validator.Validate(request);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(CreateProcessRequest.ProcessType));
    }
#pragma warning restore xUnit1012

    [Fact]
    public void Validate_Should_Fail_WhenProcessTypeExceedsMaxLength()
    {
        // Arrange
        var request = CreateValidRequest();
        request = request with { ProcessType = new string('a', 101) };

        // Act
        var result = _validator.Validate(request);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e =>
            e.PropertyName == nameof(CreateProcessRequest.ProcessType) &&
            e.ErrorMessage.Contains("must not exceed 100 characters"));
    }

    [Theory]
    [InlineData("Order-Type")]  // Uppercase not allowed
    [InlineData("order_type")]  // Underscore not allowed
    [InlineData("order.type")]  // Dot not allowed
    [InlineData("order type")]  // Space not allowed
    [InlineData("order@type")]  // Special char not allowed
    public void Validate_Should_Fail_WhenProcessTypeHasInvalidCharacters(string processType)
    {
        // Arrange
        var request = CreateValidRequest();
        request = request with { ProcessType = processType };

        // Act
        var result = _validator.Validate(request);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e =>
            e.PropertyName == nameof(CreateProcessRequest.ProcessType) &&
            e.ErrorMessage.Contains("lowercase letters, numbers, and hyphens"));
    }

    [Theory]
    [InlineData("order")]
    [InlineData("order-type")]
    [InlineData("order123")]
    [InlineData("order-type-123")]
    public void Validate_Should_Succeed_WhenProcessTypeIsValid(string processType)
    {
        // Arrange
        var request = CreateValidRequest();
        request = request with { ProcessType = processType };

        // Act
        var result = _validator.Validate(request);

        // Assert
        result.IsValid.Should().BeTrue();
    }

#pragma warning disable xUnit1012 // Null should not be used for non-nullable type parameter
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Validate_Should_Fail_WhenClientProcessIdIsEmpty(string? clientProcessId)
    {
        // Arrange
        var request = CreateValidRequest();
        request = request with { ClientProcessId = clientProcessId! };

        // Act
        var result = _validator.Validate(request);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(CreateProcessRequest.ClientProcessId));
    }
#pragma warning restore xUnit1012

    [Fact]
    public void Validate_Should_Fail_WhenClientProcessIdExceedsMaxLength()
    {
        // Arrange
        var request = CreateValidRequest();
        request = request with { ClientProcessId = new string('a', 201) };

        // Act
        var result = _validator.Validate(request);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e =>
            e.PropertyName == nameof(CreateProcessRequest.ClientProcessId) &&
            e.ErrorMessage.Contains("must not exceed 200 characters"));
    }

#pragma warning disable xUnit1012 // Null should not be used for non-nullable type parameter
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Validate_Should_Fail_WhenIdempotencyKeyIsEmpty(string? idempotencyKey)
    {
        // Arrange
        var request = CreateValidRequest();
        request = request with { IdempotencyKey = idempotencyKey! };

        // Act
        var result = _validator.Validate(request);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(CreateProcessRequest.IdempotencyKey));
    }
#pragma warning restore xUnit1012

    [Fact]
    public void Validate_Should_Fail_WhenIdempotencyKeyExceedsMaxLength()
    {
        // Arrange
        var request = CreateValidRequest();
        request = request with { IdempotencyKey = new string('a', 101) };

        // Act
        var result = _validator.Validate(request);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e =>
            e.PropertyName == nameof(CreateProcessRequest.IdempotencyKey) &&
            e.ErrorMessage.Contains("must not exceed 100 characters"));
    }

    [Theory]
    [InlineData("key@invalid")]
    [InlineData("key#invalid")]
    [InlineData("key invalid")]
    [InlineData("key.invalid")]
    public void Validate_Should_Fail_WhenIdempotencyKeyHasInvalidCharacters(string idempotencyKey)
    {
        // Arrange
        var request = CreateValidRequest();
        request = request with { IdempotencyKey = idempotencyKey };

        // Act
        var result = _validator.Validate(request);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e =>
            e.PropertyName == nameof(CreateProcessRequest.IdempotencyKey) &&
            e.ErrorMessage.Contains("alphanumeric characters, hyphens, and underscores"));
    }

    [Theory]
    [InlineData("valid-key-123")]
    [InlineData("valid_key_123")]
    [InlineData("ValidKey123")]
    [InlineData("valid-KEY_123")]
    public void Validate_Should_Succeed_WhenIdempotencyKeyIsValid(string idempotencyKey)
    {
        // Arrange
        var request = CreateValidRequest();
        request = request with { IdempotencyKey = idempotencyKey };

        // Act
        var result = _validator.Validate(request);

        // Assert
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_Should_Succeed_WhenMetadataIsNull()
    {
        // Arrange
        var request = CreateValidRequest();
#pragma warning disable CS8625 // Cannot convert null literal to non-nullable reference type - intentional for testing
        request = request with { Metadata = null };
#pragma warning restore CS8625

        // Act
        var result = _validator.Validate(request);

        // Assert
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_Should_Fail_WhenMetadataExceedsMaxEntries()
    {
        // Arrange
        var metadata = Enumerable.Range(0, 51)
            .ToDictionary(i => $"key{i}", i => $"value{i}");

        var request = CreateValidRequest();
        request = request with { Metadata = metadata };

        // Act
        var result = _validator.Validate(request);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e =>
            e.PropertyName == nameof(CreateProcessRequest.Metadata) &&
            e.ErrorMessage.Contains("cannot contain more than 50 entries"));
    }

    [Fact]
    public void Validate_Should_Fail_WhenMetadataKeyExceedsMaxLength()
    {
        // Arrange
        var metadata = new Dictionary<string, string>
        {
            { new string('a', 201), "value" }
        };

        var request = CreateValidRequest();
        request = request with { Metadata = metadata };

        // Act
        var result = _validator.Validate(request);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e =>
            e.PropertyName == nameof(CreateProcessRequest.Metadata) &&
            e.ErrorMessage.Contains("must not exceed 200 characters"));
    }

    [Fact]
    public void Validate_Should_Fail_WhenMetadataValueExceedsMaxLength()
    {
        // Arrange
        var metadata = new Dictionary<string, string>
        {
            { "key", new string('a', 201) }
        };

        var request = CreateValidRequest();
        request = request with { Metadata = metadata };

        // Act
        var result = _validator.Validate(request);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e =>
            e.PropertyName == nameof(CreateProcessRequest.Metadata) &&
            e.ErrorMessage.Contains("must not exceed 200 characters"));
    }

    [Fact]
    public void Validate_Should_Fail_WhenMetadataHasEmptyKey()
    {
        // Arrange
        var metadata = new Dictionary<string, string>
        {
            { "", "value" }
        };

        var request = CreateValidRequest();
        request = request with { Metadata = metadata };

        // Act
        var result = _validator.Validate(request);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(CreateProcessRequest.Metadata));
    }

    [Fact]
    public void Validate_Should_Succeed_WhenMetadataIsValid()
    {
        // Arrange
        var metadata = new Dictionary<string, string>
        {
            { "key1", "value1" },
            { "key2", "value2" },
            { "key3", null! }  // Null values allowed
        };

        var request = CreateValidRequest();
        request = request with { Metadata = metadata };

        // Act
        var result = _validator.Validate(request);

        // Assert
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_Should_Succeed_WithMaximumValidLengths()
    {
        // Arrange
        // Create 50 unique keys with max length (200 chars)
        // Keys: "k{0}" padded to 200, "k{1}" padded to 200, etc.
        var metadata = Enumerable.Range(0, 50)
            .ToDictionary(
                i => $"k{i}".PadRight(200, 'x'),  // Unique keys with max length
                i => new string('v', 200));        // Values with max length

        var request = new CreateProcessRequest
        {
            ClientId = new string('a', 100),
            ProcessType = new string('a', 100),
            ClientProcessId = new string('a', 200),
            IdempotencyKey = new string('a', 100),
            Metadata = metadata
        };

        // Act
        var result = _validator.Validate(request);

        // Assert
        result.IsValid.Should().BeTrue();
    }

    private static CreateProcessRequest CreateValidRequest() => new()
    {
        ClientId = "test-client",
        ProcessType = "order",
        ClientProcessId = "order-123",
        IdempotencyKey = "idempotency-123",
        Metadata = null
    };
}
