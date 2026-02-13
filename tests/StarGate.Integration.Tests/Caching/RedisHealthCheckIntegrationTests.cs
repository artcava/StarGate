using FluentAssertions;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging.Abstractions;
using StarGate.Infrastructure.Caching;
using StarGate.Integration.Tests.Fixtures;
using Xunit;

namespace StarGate.Integration.Tests.Caching;

public class RedisHealthCheckIntegrationTests : IClassFixture<RedisFixture>
{
    private readonly RedisFixture _fixture;
    private readonly RedisHealthCheck _healthCheck;

    public RedisHealthCheckIntegrationTests(RedisFixture fixture)
    {
        _fixture = fixture;
        _healthCheck = new RedisHealthCheck(
            _fixture.Redis,
            NullLogger<RedisHealthCheck>.Instance);
    }

    [Fact]
    public async Task CheckHealthAsync_Should_ReturnHealthy_WhenRedisConnected()
    {
        // Act
        var result = await _healthCheck.CheckHealthAsync(new HealthCheckContext());

        // Assert
        result.Status.Should().Be(HealthStatus.Healthy);
        result.Description.Should().Be("Redis is responsive");
        result.Data.Should().ContainKey("endpoints");
        result.Data.Should().ContainKey("connected");
    }
}
