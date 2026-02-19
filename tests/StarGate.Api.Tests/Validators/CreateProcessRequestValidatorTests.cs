using FluentAssertions;
using StarGate.Api.Validators;
using StarGate.Contracts.Requests;
using Xunit;

namespace StarGate.Api.Tests.Validators;

public class CreateProcessRequestValidatorTests
{
    private readonly CreateProcessRequestValidator _validator;

    public CreateProcessRequestValidatorTests()
    {
        _validator = new CreateProcessRequestValidator();
    }

    [Fact]
    public async Task Validate_Should_Pass_WhenRequestIsValid()
    {
        // Arrange
        var request = new CreateProcessRequest
        {
            ClientId = "test-client",
            ProcessType = "order",
            ClientProcessId = "order-123",
            IdempotencyKey = "idempotency-123",
            Metadata = new Dictionary<string, string>()
        };

        // Act
        var result = await _validator.ValidateAsync(request);

        // Assert
        result.IsValid.Should().BeTrue();
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    public async Task Validate_Should_Fail_WhenClientIdIsEmpty(string? clientId)
    {
        // Arrange
        var request = new CreateProcessRequest
        {
            ClientId = clientId!,
            ProcessType = "order",
            ClientProcessId = "order-123",
            IdempotencyKey = "idempotency-123"
        };

        // Act
        var result = await _validator.ValidateAsync(request);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "ClientId");
    }

    [Fact]
    public async Task Validate_Should_Fail_WhenClientIdIsTooLong()
    {
        // Arrange
        var request = new CreateProcessRequest
        {
            ClientId = new string('a', 101),
            ProcessType = "order",
            ClientProcessId = "order-123",
            IdempotencyKey = "idempotency-123"
        };

        // Act
        var result = await _validator.ValidateAsync(request);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "ClientId" && e.ErrorMessage.Contains("100 characters"));
    }

    [Theory]
    [InlineData("Order")] // Uppercase
    [InlineData("order_type")] // Underscore
    [InlineData("order type")] // Space
    [InlineData("order@type")] // Special char
    public async Task Validate_Should_Fail_WhenProcessTypeHasInvalidFormat(string processType)
    {
        // Arrange
        var request = new CreateProcessRequest
        {
            ClientId = "test-client",
            ProcessType = processType,
            ClientProcessId = "order-123",
            IdempotencyKey = "idempotency-123"
        };

        // Act
        var result = await _validator.ValidateAsync(request);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "ProcessType");
    }

    [Fact]
    public async Task Validate_Should_Pass_WhenProcessTypeIsValid()
    {
        // Arrange
        var request = new CreateProcessRequest
        {
            ClientId = "test-client",
            ProcessType = "order-processing-123",
            ClientProcessId = "order-123",
            IdempotencyKey = "idempotency-123"
        };

        // Act
        var result = await _validator.ValidateAsync(request);

        // Assert
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public async Task Validate_Should_Fail_WhenClientProcessIdIsTooLong()
    {
        // Arrange
        var request = new CreateProcessRequest
        {
            ClientId = "test-client",
            ProcessType = "order",
            ClientProcessId = new string('a', 201),
            IdempotencyKey = "idempotency-123"
        };

        // Act
        var result = await _validator.ValidateAsync(request);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "ClientProcessId" && e.ErrorMessage.Contains("200 characters"));
    }

    [Fact]
    public async Task Validate_Should_Fail_WhenIdempotencyKeyIsEmpty()
    {
        // Arrange
        var request = new CreateProcessRequest
        {
            ClientId = "test-client",
            ProcessType = "order",
            ClientProcessId = "order-123",
            IdempotencyKey = ""
        };

        // Act
        var result = await _validator.ValidateAsync(request);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "IdempotencyKey");
    }

    [Fact]
    public async Task Validate_Should_Fail_WhenMetadataHasTooManyEntries()
    {
        // Arrange
        var metadata = new Dictionary<string, string>();
        for (var i = 0; i < 51; i++)
        {
            metadata.Add($"key{i}", $"value{i}");
        }

        var request = new CreateProcessRequest
        {
            ClientId = "test-client",
            ProcessType = "order",
            ClientProcessId = "order-123",
            IdempotencyKey = "idempotency-123",
            Metadata = metadata
        };

        // Act
        var result = await _validator.ValidateAsync(request);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "Metadata" && e.ErrorMessage.Contains("50 entries"));
    }

    [Fact]
    public async Task Validate_Should_Pass_WhenMetadataIsNull()
    {
        // Arrange
        var request = new CreateProcessRequest
        {
            ClientId = "test-client",
            ProcessType = "order",
            ClientProcessId = "order-123",
            IdempotencyKey = "idempotency-123",
            Metadata = null
        };

        // Act
        var result = await _validator.ValidateAsync(request);

        // Assert
        result.IsValid.Should().BeTrue();
    }
}
