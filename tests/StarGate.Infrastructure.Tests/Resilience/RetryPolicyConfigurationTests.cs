using FluentAssertions;
using StarGate.Infrastructure.Resilience;

namespace StarGate.Infrastructure.Tests.Resilience;

public class RetryPolicyConfigurationTests
{
    [Theory]
    [InlineData(1, 1.0)]   // First retry: 1 second
    [InlineData(2, 2.0)]   // Second retry: 2 seconds
    [InlineData(3, 4.0)]   // Third retry: 4 seconds
    [InlineData(4, 8.0)]   // Fourth retry: 8 seconds
    public void CalculateDelay_Should_UseExponentialBackoff(int retryAttempt, double expectedSeconds)
    {
        // Arrange
        var config = new RetryPolicyConfiguration
        {
            InitialDelaySeconds = 1.0,
            BackoffMultiplier = 2.0,
            MaxDelaySeconds = 30.0,
            UseJitter = false
        };

        // Act
        var delay = config.CalculateDelay(retryAttempt);

        // Assert
        delay.TotalSeconds.Should().Be(expectedSeconds);
    }

    [Fact]
    public void CalculateDelay_Should_RespectMaxDelay()
    {
        // Arrange
        var config = new RetryPolicyConfiguration
        {
            InitialDelaySeconds = 1.0,
            BackoffMultiplier = 2.0,
            MaxDelaySeconds = 5.0,
            UseJitter = false
        };

        // Act
        var delay = config.CalculateDelay(10); // Would be 512 seconds without cap

        // Assert
        delay.TotalSeconds.Should().Be(5.0);
    }

    [Fact]
    public void CalculateDelay_Should_AddJitter_WhenEnabled()
    {
        // Arrange
        var config = new RetryPolicyConfiguration
        {
            InitialDelaySeconds = 10.0,
            BackoffMultiplier = 2.0,
            UseJitter = true
        };

        // Act
        var delays = Enumerable.Range(0, 20)
            .Select(_ => config.CalculateDelay(1).TotalSeconds)
            .ToList();

        // Assert - delays should vary due to jitter
        delays.Should().OnlyHaveUniqueItems();
        delays.Should().AllSatisfy(d => d.Should().BeInRange(9.0, 11.0)); // 10 +/- 10%
    }

    [Fact]
    public void CalculateDelay_Should_NotReturnNegativeDelay()
    {
        // Arrange
        var config = new RetryPolicyConfiguration
        {
            InitialDelaySeconds = 0.1,
            UseJitter = true
        };

        // Act
        var delays = Enumerable.Range(0, 100)
            .Select(_ => config.CalculateDelay(1))
            .ToList();

        // Assert
        delays.Should().AllSatisfy(d => d.Should().BeGreaterOrEqualTo(TimeSpan.Zero));
    }

    [Fact]
    public void CalculateDelay_Should_UseDefaultValues()
    {
        // Arrange
        var config = new RetryPolicyConfiguration();

        // Assert
        config.MaxRetryAttempts.Should().Be(3);
        config.InitialDelaySeconds.Should().Be(1.0);
        config.MaxDelaySeconds.Should().Be(30.0);
        config.BackoffMultiplier.Should().Be(2.0);
        config.UseJitter.Should().BeTrue();
    }

    [Fact]
    public void CalculateDelay_Should_HandleZeroRetryAttempt()
    {
        // Arrange
        var config = new RetryPolicyConfiguration
        {
            InitialDelaySeconds = 1.0,
            BackoffMultiplier = 2.0,
            UseJitter = false
        };

        // Act
        var delay = config.CalculateDelay(0);

        // Assert
        // 0th retry: 1.0 * 2^(-1) = 0.5 seconds
        delay.TotalSeconds.Should().Be(0.5);
    }

    [Theory]
    [InlineData(1, 2.0, 5.0)]   // 5 * 2^0 = 5
    [InlineData(2, 2.0, 10.0)]  // 5 * 2^1 = 10
    [InlineData(3, 2.0, 20.0)]  // 5 * 2^2 = 20
    [InlineData(4, 2.0, 30.0)]  // 5 * 2^3 = 40, capped at 30
    public void CalculateDelay_Should_CalculateCorrectly_WithCustomInitialDelay(
        int retryAttempt,
        double multiplier,
        double expectedSeconds)
    {
        // Arrange
        var config = new RetryPolicyConfiguration
        {
            InitialDelaySeconds = 5.0,
            BackoffMultiplier = multiplier,
            MaxDelaySeconds = 30.0,
            UseJitter = false
        };

        // Act
        var delay = config.CalculateDelay(retryAttempt);

        // Assert
        delay.TotalSeconds.Should().Be(expectedSeconds);
    }
}
