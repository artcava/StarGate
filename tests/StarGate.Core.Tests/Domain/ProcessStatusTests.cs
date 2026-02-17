using FluentAssertions;
using StarGate.Core.Domain;
using Xunit;

namespace StarGate.Core.Tests.Domain;

/// <summary>
/// Unit tests for ProcessStatus enum.
/// Validates enum values, conversions, and lifecycle states.
/// </summary>
public class ProcessStatusTests
{
    [Theory]
    [InlineData(ProcessStatus.Accepted, 0)]
    [InlineData(ProcessStatus.Processing, 1)]
    [InlineData(ProcessStatus.Completed, 2)]
    [InlineData(ProcessStatus.Failed, 3)]
    public void ProcessStatus_Should_HaveCorrectNumericValues(ProcessStatus status, int expectedValue)
    {
        // Assert
        ((int)status).Should().Be(expectedValue);
    }

    [Fact]
    public void ProcessStatus_Should_BeConvertibleToString()
    {
        // Arrange
        ProcessStatus status = ProcessStatus.Processing;

        // Act
        string statusString = status.ToString();

        // Assert
        statusString.Should().Be("Processing");
    }

    [Theory]
    [InlineData("Accepted", ProcessStatus.Accepted)]
    [InlineData("Processing", ProcessStatus.Processing)]
    [InlineData("Completed", ProcessStatus.Completed)]
    [InlineData("Failed", ProcessStatus.Failed)]
    public void ProcessStatus_Should_ParseFromString(string statusString, ProcessStatus expected)
    {
        // Act
        ProcessStatus status = Enum.Parse<ProcessStatus>(statusString);

        // Assert
        status.Should().Be(expected);
    }

    [Fact]
    public void ProcessStatus_Should_SupportAllLifecycleStates()
    {
        // Arrange
        ProcessStatus[] allStatuses = Enum.GetValues<ProcessStatus>();

        // Assert
        allStatuses.Should().HaveCount(4);
        allStatuses.Should().Contain(ProcessStatus.Accepted);
        allStatuses.Should().Contain(ProcessStatus.Processing);
        allStatuses.Should().Contain(ProcessStatus.Completed);
        allStatuses.Should().Contain(ProcessStatus.Failed);
    }

    [Fact]
    public void ProcessStatus_Should_SupportComparison()
    {
        // Arrange
        ProcessStatus accepted = ProcessStatus.Accepted;
        ProcessStatus processing = ProcessStatus.Processing;
        ProcessStatus completed = ProcessStatus.Completed;

        // Assert
        (accepted < processing).Should().BeTrue();
        (processing < completed).Should().BeTrue();
        (accepted < completed).Should().BeTrue();
    }
}
