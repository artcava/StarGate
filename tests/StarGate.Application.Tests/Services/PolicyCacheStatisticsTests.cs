using FluentAssertions;
using StarGate.Application.Services;

namespace StarGate.Application.Tests.Services;

/// <summary>
/// Unit tests for PolicyCacheStatistics.
/// Tests cache metrics tracking, hit ratio calculation, and statistics management.
/// </summary>
public class PolicyCacheStatisticsTests
{
    [Fact]
    public void RecordHit_Should_IncrementHitCount()
    {
        // Arrange
        var stats = new PolicyCacheStatistics();

        // Act
        stats.RecordHit("key1");
        stats.RecordHit("key2");
        stats.RecordHit("key1");

        // Assert
        stats.Hits.Should().Be(3);
    }

    [Fact]
    public void RecordMiss_Should_IncrementMissCount()
    {
        // Arrange
        var stats = new PolicyCacheStatistics();

        // Act
        stats.RecordMiss("key1");
        stats.RecordMiss("key2");

        // Assert
        stats.Misses.Should().Be(2);
    }

    [Fact]
    public void RecordEviction_Should_IncrementEvictionCount()
    {
        // Arrange
        var stats = new PolicyCacheStatistics();

        // Act
        stats.RecordEviction();
        stats.RecordEviction();
        stats.RecordEviction();

        // Assert
        stats.Evictions.Should().Be(3);
    }

    [Fact]
    public void TotalRequests_Should_ReturnSumOfHitsAndMisses()
    {
        // Arrange
        var stats = new PolicyCacheStatistics();

        // Act
        stats.RecordHit("key1");
        stats.RecordHit("key1");
        stats.RecordHit("key2");
        stats.RecordMiss("key3");
        stats.RecordMiss("key4");

        // Assert
        stats.TotalRequests.Should().Be(5);
        stats.Hits.Should().Be(3);
        stats.Misses.Should().Be(2);
    }

    [Fact]
    public void HitRatio_Should_CalculateCorrectly()
    {
        // Arrange
        var stats = new PolicyCacheStatistics();

        // Act
        stats.RecordHit("key1");
        stats.RecordHit("key1");
        stats.RecordHit("key1");
        stats.RecordMiss("key2");

        // Assert
        stats.HitRatio.Should().BeApproximately(0.75, 0.01); // 3 hits out of 4 total = 0.75
    }

    [Fact]
    public void HitRatio_Should_ReturnZero_WhenNoRequests()
    {
        // Arrange
        var stats = new PolicyCacheStatistics();

        // Act
        var hitRatio = stats.HitRatio;

        // Assert
        hitRatio.Should().Be(0.0);
    }

    [Fact]
    public void HitRatio_Should_ReturnOne_WhenAllHits()
    {
        // Arrange
        var stats = new PolicyCacheStatistics();

        // Act
        stats.RecordHit("key1");
        stats.RecordHit("key2");
        stats.RecordHit("key3");

        // Assert
        stats.HitRatio.Should().Be(1.0);
    }

    [Fact]
    public void HitRatio_Should_ReturnZero_WhenAllMisses()
    {
        // Arrange
        var stats = new PolicyCacheStatistics();

        // Act
        stats.RecordMiss("key1");
        stats.RecordMiss("key2");

        // Assert
        stats.HitRatio.Should().Be(0.0);
    }

    [Fact]
    public void GetKeyStatistics_Should_ReturnPerKeyMetrics()
    {
        // Arrange
        var stats = new PolicyCacheStatistics();
        stats.RecordHit("key1");
        stats.RecordHit("key1");
        stats.RecordMiss("key1");
        stats.RecordHit("key2");

        // Act
        var keyStats = stats.GetKeyStatistics();

        // Assert
        keyStats.Should().ContainKey("key1");
        keyStats["key1"].Hits.Should().Be(2);
        keyStats["key1"].Misses.Should().Be(1);
        keyStats["key1"].TotalRequests.Should().Be(3);
        keyStats["key1"].HitRatio.Should().BeApproximately(0.67, 0.01);
    }

    [Fact]
    public void GetKeyStatistics_Should_ReturnMultipleKeys()
    {
        // Arrange
        var stats = new PolicyCacheStatistics();
        stats.RecordHit("key1");
        stats.RecordHit("key1");
        stats.RecordMiss("key2");
        stats.RecordMiss("key2");
        stats.RecordMiss("key2");
        stats.RecordHit("key3");

        // Act
        var keyStats = stats.GetKeyStatistics();

        // Assert
        keyStats.Should().HaveCount(3);
        keyStats.Should().ContainKeys("key1", "key2", "key3");
        
        keyStats["key1"].Hits.Should().Be(2);
        keyStats["key1"].Misses.Should().Be(0);
        keyStats["key1"].HitRatio.Should().Be(1.0);
        
        keyStats["key2"].Hits.Should().Be(0);
        keyStats["key2"].Misses.Should().Be(3);
        keyStats["key2"].HitRatio.Should().Be(0.0);
        
        keyStats["key3"].Hits.Should().Be(1);
        keyStats["key3"].Misses.Should().Be(0);
        keyStats["key3"].HitRatio.Should().Be(1.0);
    }

    [Fact]
    public void GetKeyStatistics_Should_ReturnEmptyDictionary_WhenNoStatistics()
    {
        // Arrange
        var stats = new PolicyCacheStatistics();

        // Act
        var keyStats = stats.GetKeyStatistics();

        // Assert
        keyStats.Should().BeEmpty();
    }

    [Fact]
    public void Reset_Should_ClearAllStatistics()
    {
        // Arrange
        var stats = new PolicyCacheStatistics();
        stats.RecordHit("key1");
        stats.RecordHit("key2");
        stats.RecordMiss("key3");
        stats.RecordEviction();

        // Act
        stats.Reset();

        // Assert
        stats.Hits.Should().Be(0);
        stats.Misses.Should().Be(0);
        stats.Evictions.Should().Be(0);
        stats.TotalRequests.Should().Be(0);
        stats.HitRatio.Should().Be(0.0);
        stats.GetKeyStatistics().Should().BeEmpty();
    }

    [Fact]
    public void Reset_Should_AllowNewStatisticsAfterReset()
    {
        // Arrange
        var stats = new PolicyCacheStatistics();
        stats.RecordHit("key1");
        stats.RecordMiss("key2");
        stats.Reset();

        // Act
        stats.RecordHit("key3");
        stats.RecordMiss("key4");

        // Assert
        stats.Hits.Should().Be(1);
        stats.Misses.Should().Be(1);
        stats.TotalRequests.Should().Be(2);
        stats.GetKeyStatistics().Should().ContainKeys("key3", "key4");
    }

    [Fact]
    public async Task Statistics_Should_BeThreadSafe_WhenAccessedConcurrently()
    {
        // Arrange
        var stats = new PolicyCacheStatistics();
        var tasks = new List<Task>();
        const int iterationsPerTask = 100;
        const int numberOfTasks = 10;

        // Act
        for (int i = 0; i < numberOfTasks; i++)
        {
            var taskId = i;
            tasks.Add(Task.Run(() =>
            {
                for (int j = 0; j < iterationsPerTask; j++)
                {
                    if (j % 2 == 0)
                    {
                        stats.RecordHit($"key-{taskId}");
                    }
                    else
                    {
                        stats.RecordMiss($"key-{taskId}");
                    }
                    
                    if (j % 10 == 0)
                    {
                        stats.RecordEviction();
                    }
                }
            }));
        }

        await Task.WhenAll(tasks);

        // Assert
        stats.Hits.Should().Be(numberOfTasks * iterationsPerTask / 2);
        stats.Misses.Should().Be(numberOfTasks * iterationsPerTask / 2);
        stats.TotalRequests.Should().Be(numberOfTasks * iterationsPerTask);
        stats.Evictions.Should().Be(numberOfTasks * (iterationsPerTask / 10));
    }

    [Fact]
    public void CacheKeyStatistics_Should_HaveCorrectProperties()
    {
        // Arrange
        var stats = new PolicyCacheStatistics();
        stats.RecordHit("test-key");
        stats.RecordHit("test-key");
        stats.RecordMiss("test-key");

        // Act
        var keyStats = stats.GetKeyStatistics();
        var testKeyStats = keyStats["test-key"];

        // Assert
        testKeyStats.Key.Should().Be("test-key");
        testKeyStats.Hits.Should().Be(2);
        testKeyStats.Misses.Should().Be(1);
        testKeyStats.TotalRequests.Should().Be(3);
        testKeyStats.HitRatio.Should().BeApproximately(0.67, 0.01);
    }
}
