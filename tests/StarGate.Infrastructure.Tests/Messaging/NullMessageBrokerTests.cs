using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using StarGate.Core.Abstractions;
using StarGate.Core.Domain;
using StarGate.Infrastructure.Messaging;
using Xunit;

namespace StarGate.Infrastructure.Tests.Messaging;

public class NullMessageBrokerTests
{
    [Fact]
    public async Task PublishAsync_Should_NotThrow()
    {
        // Arrange
        var broker = new NullMessageBroker(NullLogger<NullMessageBroker>.Instance);
        var process = new Process
        {
            ProcessId = Guid.NewGuid(),
            ClientProcessId = "test",
            ProcessType = "order",
            ClientId = "client",
            Status = ProcessStatus.Accepted,
            Progress = 0,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            IdempotencyKey = Guid.NewGuid().ToString(),
            Retryable = true
        };

        // Act
        Func<Task> act = async () => await broker.PublishAsync("test.queue", process);

        // Assert
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task PublishAsync_WithProperties_Should_NotThrow()
    {
        // Arrange
        var broker = new NullMessageBroker(NullLogger<NullMessageBroker>.Instance);
        var process = new Process
        {
            ProcessId = Guid.NewGuid(),
            ClientProcessId = "test",
            ProcessType = "order",
            ClientId = "client",
            Status = ProcessStatus.Accepted,
            Progress = 0,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            IdempotencyKey = Guid.NewGuid().ToString(),
            Retryable = true
        };

        var properties = new MessageProperties
        {
            MessageId = "msg-123",
            CorrelationId = "corr-456"
        };

        // Act
        Func<Task> act = async () => await broker.PublishAsync("test.queue", process, properties);

        // Assert
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public void Constructor_Should_ThrowArgumentNull_WhenLoggerNull()
    {
        // Act
        Action act = () => new NullMessageBroker(null!);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void CreateConsumer_Should_ThrowNotSupported()
    {
        // Arrange
        var broker = new NullMessageBroker(NullLogger<NullMessageBroker>.Instance);

        // Act
        Action act = () => broker.CreateConsumer("test.queue");

        // Assert
        act.Should().Throw<NotSupportedException>()
            .WithMessage("*does not support message consumers*");
    }
}
