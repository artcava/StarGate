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
    [InlineData(ProcessStatus.Pending, 0)]
    [InlineData(ProcessStatus.Accepted, 1)]
    [InlineData(ProcessStatus.Processing, 2)]
    [InlineData(ProcessStatus.Completed, 3)]
    [InlineData(ProcessStatus.Failed, 4)]
    [InlineData(ProcessStatus.Retrying, 5)]
    [InlineData(ProcessStatus.Rejected, 6)]
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
    [InlineData("Pending", ProcessStatus.Pending)]
    [InlineData("Accepted", ProcessStatus.Accepted)]
    [InlineData("Processing", ProcessStatus.Processing)]
    [InlineData("Completed", ProcessStatus.Completed)]
    [InlineData("Failed", ProcessStatus.Failed)]
    [InlineData("Retrying", ProcessStatus.Retrying)]
    [InlineData("Rejected", ProcessStatus.Rejected)]
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
        allStatuses.Should().HaveCount(7);
        allStatuses.Should().Contain(ProcessStatus.Pending);
        allStatuses.Should().Contain(ProcessStatus.Accepted);
        allStatuses.Should().Contain(ProcessStatus.Processing);
        allStatuses.Should().Contain(ProcessStatus.Completed);
        allStatuses.Should().Contain(ProcessStatus.Failed);
        allStatuses.Should().Contain(ProcessStatus.Retrying);
        allStatuses.Should().Contain(ProcessStatus.Rejected);
    }

    [Fact]
    public void ProcessStatus_Should_SupportComparison()
    {
        // Arrange
        ProcessStatus pending = ProcessStatus.Pending;
        ProcessStatus accepted = ProcessStatus.Accepted;
        ProcessStatus processing = ProcessStatus.Processing;
        ProcessStatus completed = ProcessStatus.Completed;

        // Assert
        (pending < accepted).Should().BeTrue();
        (accepted < processing).Should().BeTrue();
        (processing < completed).Should().BeTrue();
        (pending < completed).Should().BeTrue();
    }

    [Theory]
    [InlineData(ProcessStatus.Completed)]
    [InlineData(ProcessStatus.Failed)]
    [InlineData(ProcessStatus.Rejected)]
    public void ProcessStatus_Should_IdentifyTerminalStates(ProcessStatus status)
    {
        // Arrange
        var terminalStates = new[] { ProcessStatus.Completed, ProcessStatus.Failed, ProcessStatus.Rejected };

        // Assert
        terminalStates.Should().Contain(status);
    }

    [Theory]
    [InlineData(ProcessStatus.Pending)]
    [InlineData(ProcessStatus.Accepted)]
    [InlineData(ProcessStatus.Processing)]
    [InlineData(ProcessStatus.Retrying)]
    public void ProcessStatus_Should_IdentifyNonTerminalStates(ProcessStatus status)
    {
        // Arrange
        var terminalStates = new[] { ProcessStatus.Completed, ProcessStatus.Failed, ProcessStatus.Rejected };

        // Assert
        terminalStates.Should().NotContain(status);
    }
}
