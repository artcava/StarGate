using FluentAssertions;
using StarGate.Core.Configuration;

namespace StarGate.Server.Tests.Workers;

public class RetryLogicTests
{
    [Theory]
    [InlineData(0, 5)]   // First retry: 5 seconds
    [InlineData(1, 10)]  // Second retry: 10 seconds
    [InlineData(2, 20)]  // Third retry: 20 seconds
    [InlineData(3, 40)]  // Fourth retry: 40 seconds
    [InlineData(4, 80)]  // Fifth retry: 80 seconds
    public void CalculateDelay_Should_UseExponentialBackoff(int retryCount, int expectedSeconds)
    {
        // Arrange
        var config = new RetryConfiguration
        {
            BaseDelaySeconds = 5,
            BackoffMultiplier = 2.0,
            UseJitter = false
        };

        // Act
        var delay = config.CalculateDelay(retryCount);

        // Assert
        delay.TotalSeconds.Should().Be(expectedSeconds);
    }

    [Fact]
    public void CalculateDelay_Should_RespectMaxDelay()
    {
        // Arrange
        var config = new RetryConfiguration
        {
            BaseDelaySeconds = 5,
            MaxDelaySeconds = 60,
            BackoffMultiplier = 2.0,
            UseJitter = false
        };

        // Act
        var delay = config.CalculateDelay(10); // Would be 5 * 2^10 = 5120 seconds

        // Assert
        delay.TotalSeconds.Should().Be(60); // Capped at MaxDelay
    }

    [Fact]
    public void CalculateDelay_Should_AddJitter_WhenEnabled()
    {
        // Arrange
        var config = new RetryConfiguration
        {
            BaseDelaySeconds = 10,
            UseJitter = true,
            BackoffMultiplier = 2.0
        };

        // Act
        var delays = Enumerable.Range(0, 10)
            .Select(_ => config.CalculateDelay(0).TotalSeconds)
            .ToList();

        // Assert - delays should vary due to jitter
        delays.Should().OnlyHaveUniqueItems();
        delays.Should().AllSatisfy(d => d.Should().BeInRange(7, 13)); // 10 +/- 30%
    }

    [Fact]
    public void CalculateDelay_Should_ReturnConsistentValue_WhenJitterDisabled()
    {
        // Arrange
        var config = new RetryConfiguration
        {
            BaseDelaySeconds = 10,
            UseJitter = false,
            BackoffMultiplier = 2.0
        };

        // Act
        var delays = Enumerable.Range(0, 5)
            .Select(_ => config.CalculateDelay(2).TotalSeconds)
            .ToList();

        // Assert - all delays should be identical
        delays.Should().AllSatisfy(d => d.Should().Be(40)); // 10 * 2^2 = 40
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(5)]
    public void CalculateDelay_Should_NeverExceedMaxDelay(int retryCount)
    {
        // Arrange
        var config = new RetryConfiguration
        {
            BaseDelaySeconds = 100,
            MaxDelaySeconds = 200,
            BackoffMultiplier = 3.0,
            UseJitter = true
        };

        // Act
        var delay = config.CalculateDelay(retryCount);

        // Assert
        delay.TotalSeconds.Should().BeLessOrEqualTo(config.MaxDelaySeconds);
    }

    [Fact]
    public void DefaultConfiguration_Should_HaveExpectedValues()
    {
        // Act
        var config = new RetryConfiguration();

        // Assert
        config.BaseDelaySeconds.Should().Be(5);
        config.MaxDelaySeconds.Should().Be(300);
        config.BackoffMultiplier.Should().Be(2.0);
        config.UseJitter.Should().BeTrue();
    }
}
