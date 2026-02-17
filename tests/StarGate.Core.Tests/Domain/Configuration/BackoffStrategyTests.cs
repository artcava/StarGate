using FluentAssertions;
using StarGate.Core.Domain.Configuration;
using Xunit;

namespace StarGate.Core.Tests.Domain.Configuration;

/// <summary>
/// Unit tests for BackoffStrategy enum.
/// Validates enum values, conversions, and strategy types.
/// </summary>
public class BackoffStrategyTests
{
    [Theory]
    [InlineData(BackoffStrategy.Linear, 0)]
    [InlineData(BackoffStrategy.Exponential, 1)]
    public void BackoffStrategy_Should_HaveCorrectNumericValues(BackoffStrategy strategy, int expectedValue)
    {
        // Assert
        ((int)strategy).Should().Be(expectedValue);
    }

    [Fact]
    public void BackoffStrategy_Should_BeConvertibleToString()
    {
        // Arrange
        BackoffStrategy strategy = BackoffStrategy.Exponential;

        // Act
        string strategyString = strategy.ToString();

        // Assert
        strategyString.Should().Be("Exponential");
    }

    [Theory]
    [InlineData("Linear", BackoffStrategy.Linear)]
    [InlineData("Exponential", BackoffStrategy.Exponential)]
    public void BackoffStrategy_Should_ParseFromString(string strategyString, BackoffStrategy expected)
    {
        // Act
        BackoffStrategy strategy = Enum.Parse<BackoffStrategy>(strategyString);

        // Assert
        strategy.Should().Be(expected);
    }

    [Fact]
    public void BackoffStrategy_Should_SupportAllStrategyTypes()
    {
        // Arrange
        BackoffStrategy[] allStrategies = Enum.GetValues<BackoffStrategy>();

        // Assert
        allStrategies.Should().HaveCount(2);
        allStrategies.Should().Contain(BackoffStrategy.Linear);
        allStrategies.Should().Contain(BackoffStrategy.Exponential);
    }
}
