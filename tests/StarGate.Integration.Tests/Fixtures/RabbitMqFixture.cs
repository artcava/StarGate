using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using RabbitMQ.Client;
using StarGate.Infrastructure.Messaging;
using StarGate.Infrastructure.Messaging.RabbitMQ;
using Testcontainers.RabbitMq;
using Xunit;

namespace StarGate.Integration.Tests.Fixtures;

/// <summary>
/// Provides a RabbitMQ test container for integration tests.
/// </summary>
public class RabbitMqFixture : IAsyncLifetime
{
    private readonly RabbitMqContainer _rabbitMqContainer;
    private IConnection? _connection;
    private RabbitMqBroker? _broker;
    private RabbitMqConsumer? _consumer;
    private JsonMessageSerializer? _serializer;

    public RabbitMqFixture()
    {
        _rabbitMqContainer = new RabbitMqBuilder()
            .WithImage("rabbitmq:3.12-management-alpine")
            .WithPortBinding(5672, true)
            .WithPortBinding(15672, true)
            .WithUsername("guest")
            .WithPassword("guest")
            .Build();
    }

    public IConnection Connection => _connection
        ?? throw new InvalidOperationException("Connection not initialized");

    public RabbitMqBroker Broker => _broker
        ?? throw new InvalidOperationException("Broker not initialized");

    public RabbitMqConsumer Consumer => _consumer
        ?? throw new InvalidOperationException("Consumer not initialized");

    public JsonMessageSerializer Serializer => _serializer
        ?? throw new InvalidOperationException("Serializer not initialized");

    public string ConnectionString => _rabbitMqContainer.GetConnectionString();

    public RabbitMqOptions Options { get; private set; } = null!;

    public async Task InitializeAsync()
    {
        await _rabbitMqContainer.StartAsync();

        Options = new RabbitMqOptions
        {
            HostName = _rabbitMqContainer.Hostname,
            Port = _rabbitMqContainer.GetMappedPublicPort(5672),
            UserName = "guest",
            Password = "guest",
            VirtualHost = "/",
            PublisherConfirms = true,
            PublisherConfirmTimeoutMs = 5000,
            Enabled = true,
            ProcessExchange = "test.stargate.processes",
            DeadLetterExchange = "test.stargate.dlx",
            DeadLetterQueue = "test.stargate.dlq"
        };

        _serializer = new JsonMessageSerializer(NullLogger<JsonMessageSerializer>.Instance);

        // RabbitMqConnectionFactory is static and requires ILogger (not ILogger<T>)
        _connection = RabbitMqConnectionFactory.CreateConnection(
            Options,
            NullLogger.Instance);

        _broker = new RabbitMqBroker(
            _connection,
            _serializer,
            Options,
            NullLogger<RabbitMqBroker>.Instance);

        _consumer = new RabbitMqConsumer(
            _connection,
            _serializer,
            Options,
            NullLogger<RabbitMqConsumer>.Instance);
    }

    public async Task DisposeAsync()
    {
        _broker?.Dispose();
        
        // RabbitMqConsumer implements IAsyncDisposable, not IDisposable
        if (_consumer != null)
        {
            await _consumer.DisposeAsync();
        }
        
        _connection?.Dispose();
        await _rabbitMqContainer.DisposeAsync();
    }

    /// <summary>
    /// Ensures DLQ is bound to DLX with the correct routing key for a specific queue.
    /// Required because DLX is Direct exchange and doesn't support wildcard routing.
    /// When a message is rejected from a queue, it's sent to DLX with routing key = queue name.
    /// </summary>
    public void EnsureDlqBinding(string queueName)
    {
        using var channel = _connection!.CreateModel();
        try
        {
            // Bind DLQ to DLX with routing key matching the source queue name
            // This ensures rejected messages from 'queueName' are routed to DLQ
            channel.QueueBind(
                queue: Options.DeadLetterQueue,
                exchange: Options.DeadLetterExchange,
                routingKey: queueName);
        }
        catch
        {
            // Binding might already exist, ignore
        }
    }

    /// <summary>
    /// Purges all messages from a queue.
    /// </summary>
    public void PurgeQueue(string queueName)
    {
        using var channel = _connection!.CreateModel();
        try
        {
            channel.QueuePurge(queueName);
        }
        catch
        {
            // Queue might not exist, ignore
        }
    }

    /// <summary>
    /// Gets the message count in a queue.
    /// </summary>
    public uint GetMessageCount(string queueName)
    {
        using var channel = _connection!.CreateModel();
        var result = channel.QueueDeclarePassive(queueName);
        return result.MessageCount;
    }

    /// <summary>
    /// Deletes a queue if it exists.
    /// </summary>
    public void DeleteQueue(string queueName)
    {
        using var channel = _connection!.CreateModel();
        try
        {
            channel.QueueDelete(queueName);
        }
        catch
        {
            // Queue might not exist, ignore
        }
    }
}
