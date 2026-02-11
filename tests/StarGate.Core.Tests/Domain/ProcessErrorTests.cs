using FluentAssertions;
using StarGate.Core.Domain;
using System.Text.Json;
using Xunit;

namespace StarGate.Core.Tests.Domain;

/// <summary>
/// Unit tests for ProcessError record.
/// Validates property storage, immutability, and complex details handling.
/// </summary>
public class ProcessErrorTests
{
    [Fact]
    public void ProcessError_Should_StoreAllProperties()
    {
        // Arrange
        string code = "VALIDATION_ERROR";
        string message = "Invalid input data";
        object detailsObject = new { Field = "Amount", Value = -100 };
        JsonDocument details = JsonDocument.Parse(JsonSerializer.Serialize(detailsObject));

        // Act
        ProcessError error = new(code, message, details);

        // Assert
        error.Code.Should().Be(code);
        error.Message.Should().Be(message);
        error.Details.Should().NotBeNull();
        error.Details!.RootElement.GetProperty("Field").GetString().Should().Be("Amount");
    }

    [Fact]
    public void ProcessError_Should_AllowNullDetails()
    {
        // Arrange & Act
        ProcessError error = new("ERROR_CODE", "Error message", null);

        // Assert
        error.Details.Should().BeNull();
    }

    [Fact]
    public void ProcessError_Should_BeImmutable()
    {
        // Arrange
        ProcessError error = new("CODE1", "Message1", null);

        // Act
        ProcessError modified = error with { Code = "CODE2" };

        // Assert
        error.Code.Should().Be("CODE1");
        modified.Code.Should().Be("CODE2");
        modified.Message.Should().Be("Message1");
    }

    [Fact]
    public void ProcessError_Should_SupportComplexDetails()
    {
        // Arrange
        object complexDetailsObject = new
        {
            Timestamp = DateTime.UtcNow,
            Stack = new[] { "Method1", "Method2", "Method3" },
            Context = new { UserId = "user-123", RequestId = "req-456" }
        };
        JsonDocument complexDetails = JsonDocument.Parse(JsonSerializer.Serialize(complexDetailsObject));

        // Act
        ProcessError error = new("COMPLEX_ERROR", "Complex error occurred", complexDetails);

        // Assert
        error.Details.Should().NotBeNull();
        error.Details!.RootElement.GetProperty("Context").GetProperty("UserId").GetString().Should().Be("user-123");
    }

    [Theory]
    [InlineData("", "Message")]
    [InlineData("CODE", "")]
    public void ProcessError_Should_AcceptEmptyStrings(string code, string message)
    {
        // Arrange & Act
        ProcessError error = new(code, message, null);

        // Assert
        error.Code.Should().Be(code);
        error.Message.Should().Be(message);
    }
}
