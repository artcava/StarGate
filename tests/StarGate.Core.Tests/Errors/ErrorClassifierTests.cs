namespace StarGate.Core.Tests.Errors;

using FluentAssertions;
using StarGate.Core.Errors;
using System.Text.Json;
using Xunit;

public class ErrorClassifierTests
{
    [Fact]
    public void Classify_Should_ReturnMalformedMessage_ForJsonException()
    {
        // Arrange
        var exception = new JsonException("Invalid JSON");

        // Act
        var classification = ErrorClassifier.Classify(exception);

        // Assert
        classification.ErrorCode.Should().Be("MALFORMED_MESSAGE");
        classification.IsRetryable.Should().BeFalse();
        classification.ShouldRequeue.Should().BeFalse();
        classification.Severity.Should().Be(ErrorSeverity.Error);
    }

    [Fact]
    public void Classify_Should_ReturnRetryable_ForTimeoutException()
    {
        // Arrange
        var exception = new TimeoutException("Operation timed out");

        // Act
        var classification = ErrorClassifier.Classify(exception);

        // Assert
        classification.ErrorCode.Should().Be("PROCESS_TIMEOUT");
        classification.IsRetryable.Should().BeTrue();
        classification.ShouldRequeue.Should().BeTrue();
        classification.Severity.Should().Be(ErrorSeverity.Warning);
    }

    [Fact]
    public void Classify_Should_ReturnNonRetryable_ForInvalidOperationException()
    {
        // Arrange
        var exception = new InvalidOperationException("Invalid operation");

        // Act
        var classification = ErrorClassifier.Classify(exception);

        // Assert
        classification.ErrorCode.Should().Be("INVALID_OPERATION");
        classification.IsRetryable.Should().BeFalse();
        classification.ShouldRequeue.Should().BeFalse();
        classification.Severity.Should().Be(ErrorSeverity.Error);
    }

    [Fact]
    public void Classify_Should_ReturnRetryable_ForHttpRequestException()
    {
        // Arrange
        var exception = new HttpRequestException("Network error");

        // Act
        var classification = ErrorClassifier.Classify(exception);

        // Assert
        classification.ErrorCode.Should().Be("HTTP_ERROR");
        classification.IsRetryable.Should().BeTrue();
        classification.ShouldRequeue.Should().BeTrue();
        classification.Severity.Should().Be(ErrorSeverity.Warning);
    }

    [Fact]
    public void Classify_Should_ReturnNonRetryable_ForArgumentException()
    {
        // Arrange
        var exception = new ArgumentException("Invalid argument");

        // Act
        var classification = ErrorClassifier.Classify(exception);

        // Assert
        classification.ErrorCode.Should().Be("INVALID_ARGUMENT");
        classification.IsRetryable.Should().BeFalse();
        classification.ShouldRequeue.Should().BeFalse();
        classification.Severity.Should().Be(ErrorSeverity.Error);
    }

    [Fact]
    public void Classify_Should_ReturnUnknownError_ForUnknownException()
    {
        // Arrange
        var exception = new Exception("Unknown error");

        // Act
        var classification = ErrorClassifier.Classify(exception);

        // Assert
        classification.ErrorCode.Should().Be("UNKNOWN_ERROR");
        classification.IsRetryable.Should().BeTrue();
        classification.ShouldRequeue.Should().BeTrue();
        classification.Severity.Should().Be(ErrorSeverity.Error);
    }
}
