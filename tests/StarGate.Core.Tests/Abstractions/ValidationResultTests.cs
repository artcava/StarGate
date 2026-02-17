using FluentAssertions;
using StarGate.Core.Abstractions;
using Xunit;

namespace StarGate.Core.Tests.Abstractions;

/// <summary>
/// Unit tests for ValidationResult record type and factory methods.
/// Verifies validation pattern behavior.
/// </summary>
public class ValidationResultTests
{
    [Fact]
    public void ValidationResult_Success_Should_BeValid()
    {
        // Act
        ValidationResult result = ValidationResult.Success();

        // Assert
        result.IsValid.Should().BeTrue();
        result.Errors.Should().BeNull();
    }

    [Fact]
    public void ValidationResult_Failure_Should_ContainErrors()
    {
        // Arrange
        ValidationError error1 = new("Field1", "Error 1", "ERR001");
        ValidationError error2 = new("Field2", "Error 2", "ERR002");

        // Act
        ValidationResult result = ValidationResult.Failure(error1, error2);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().HaveCount(2);
        result.Errors![0].Field.Should().Be("Field1");
    }
}
