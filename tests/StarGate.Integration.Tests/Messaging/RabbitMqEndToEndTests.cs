using System.Text.Json;
using FluentAssertions;
using StarGate.Core.Abstractions;
using StarGate.Core.Domain;
using StarGate.Integration.Tests.Fixtures;
using Xunit;

namespace StarGate.Integration.Tests.Messaging;

public class RabbitMqEndToEndTests : IClassFixture<RabbitMqFixture>, IAsyncLifetime
{
    private readonly RabbitMqFixture _fixture;
    private readonly string _testQueue = "stargate.process"; // Convention-based queue name for Process type

    public RabbitMqEndToEndTests(RabbitMqFixture fixture)
    {
        _fixture = fixture;
    }

    public Task InitializeAsync() => Task.CompletedTask;

    public async Task DisposeAsync()
    {
        await _fixture.Consumer.StopConsumingAsync();
        _fixture.DeleteQueue(_testQueue);
    }

    [Fact]
    public async Task FullWorkflow_Should_PublishConsumeAndAcknowledge()
    {
        // Arrange
        var originalProcess = CreateTestProcess();
        Process? receivedProcess = null;
        var tcs = new TaskCompletionSource();

        async Task Handler(Process message, MessageContext context)
        {
            receivedProcess = message;
            await context.AcknowledgeAsync();
            tcs.SetResult();
        }

        // Act
        await _fixture.Consumer.StartConsumingAsync<Process>(Handler, CancellationToken.None);
        await _fixture.Broker.PublishAsync(_testQueue, originalProcess);

        var consumed = await Task.WhenAny(tcs.Task, Task.Delay(5000)) == tcs.Task;

        // Assert
        consumed.Should().BeTrue();
        receivedProcess.Should().NotBeNull();
        receivedProcess!.ProcessId.Should().Be(originalProcess.ProcessId);
        receivedProcess.ClientProcessId.Should().Be(originalProcess.ClientProcessId);
        receivedProcess.ProcessType.Should().Be(originalProcess.ProcessType);
        receivedProcess.Status.Should().Be(originalProcess.Status);
    }

    [Fact]
    public async Task FullWorkflow_Should_PreserveComplexData()
    {
        // Arrange
        var complexData = new
        {
            orderId = "ORD-12345",
            customer = new { id = "CUST-001", name = "John Doe" },
            items = new[] { new { sku = "SKU-001", quantity = 10 } }
        };

        // Convert anonymous type to JsonDocument
        var jsonString = JsonSerializer.Serialize(complexData);
        var jsonDocument = JsonDocument.Parse(jsonString);

        var originalProcess = CreateTestProcess() with { Data = jsonDocument };
        Process? receivedProcess = null;
        var tcs = new TaskCompletionSource();

        async Task Handler(Process message, MessageContext context)
        {
            receivedProcess = message;
            await context.AcknowledgeAsync();
            tcs.SetResult();
        }

        // Act
        await _fixture.Consumer.StartConsumingAsync<Process>(Handler, CancellationToken.None);
        await _fixture.Broker.PublishAsync(_testQueue, originalProcess);

        var consumed = await Task.WhenAny(tcs.Task, Task.Delay(5000)) == tcs.Task;

        // Assert
        consumed.Should().BeTrue();
        receivedProcess.Should().NotBeNull();
        receivedProcess!.Data.Should().NotBeNull();
    }

    private static Process CreateTestProcess() => new()
    {
        ProcessId = Guid.NewGuid(),
        ClientProcessId = $"client-{Guid.NewGuid()}",
        ProcessType = "order",
        ClientId = "test-client",
        Status = ProcessStatus.Accepted,
        Progress = 0,
        CreatedAt = DateTime.UtcNow,
        UpdatedAt = DateTime.UtcNow,
        IdempotencyKey = Guid.NewGuid().ToString(),
        Retryable = true
    };
}
