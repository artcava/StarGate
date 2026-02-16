namespace StarGate.Infrastructure.Tests.Messaging;

using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using StarGate.Core.Abstractions;
using StarGate.Core.Domain;
using StarGate.Infrastructure.Messaging;
using Xunit;

public class NullMessageConsumerTests
{
    [Fact]
    public async Task StartConsumingAsync_Should_NotThrow()
    {
        // Arrange
        var consumer = new NullMessageConsumer(NullLogger<NullMessageConsumer>.Instance);
        Func<Process, MessageContext, Task> handler =
            (process, ctx) => Task.CompletedTask;

        // Act
        Func<Task> act = async () => await consumer.StartConsumingAsync(handler);

        // Assert
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task StopConsumingAsync_Should_NotThrow()
    {
        // Arrange
        var consumer = new NullMessageConsumer(NullLogger<NullMessageConsumer>.Instance);

        // Act
        Func<Task> act = async () => await consumer.StopConsumingAsync();

        // Assert
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task DisposeAsync_Should_NotThrow()
    {
        // Arrange
        var consumer = new NullMessageConsumer(NullLogger<NullMessageConsumer>.Instance);

        // Act
        Func<Task> act = async () => await consumer.DisposeAsync();

        // Assert
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public void Constructor_Should_ThrowArgumentNull_WhenLoggerNull()
    {
        // Act
        Action act = () => new NullMessageConsumer(null!);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public async Task DisposeAsync_Should_BeIdempotent()
    {
        // Arrange
        var consumer = new NullMessageConsumer(NullLogger<NullMessageConsumer>.Instance);

        // Act
        await consumer.DisposeAsync();
        await consumer.DisposeAsync();
        await consumer.DisposeAsync();

        // Assert - Should not throw
    }
}
