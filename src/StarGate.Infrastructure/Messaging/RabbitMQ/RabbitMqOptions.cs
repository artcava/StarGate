namespace StarGate.Infrastructure.Messaging.RabbitMQ;

/// <summary>
/// Configuration options for RabbitMQ connection and behavior.
/// </summary>
public class RabbitMqOptions
{
    public const string SectionName = "RabbitMQ";

    /// <summary>
    /// RabbitMQ connection hostname.
    /// </summary>
    public required string HostName { get; init; }

    /// <summary>
    /// RabbitMQ connection port.
    /// </summary>
    public int Port { get; init; } = 5672;

    /// <summary>
    /// RabbitMQ username.
    /// </summary>
    public required string UserName { get; init; }

    /// <summary>
    /// RabbitMQ password.
    /// </summary>
    public required string Password { get; init; }

    /// <summary>
    /// Virtual host.
    /// </summary>
    public string VirtualHost { get; init; } = "/";

    /// <summary>
    /// Whether to enable publisher confirms.
    /// </summary>
    public bool PublisherConfirms { get; init; } = true;

    /// <summary>
    /// Timeout for publisher confirms in milliseconds.
    /// </summary>
    public int PublisherConfirmTimeoutMs { get; init; } = 5000;

    /// <summary>
    /// Whether RabbitMQ is enabled.
    /// </summary>
    public bool Enabled { get; init; } = true;

    /// <summary>
    /// Exchange name for process messages.
    /// </summary>
    public string ProcessExchange { get; init; } = "stargate.processes";

    /// <summary>
    /// Dead letter exchange name.
    /// </summary>
    public string DeadLetterExchange { get; init; } = "stargate.dlx";

    /// <summary>
    /// Dead letter queue name.
    /// </summary>
    public string DeadLetterQueue { get; init; } = "stargate.dlq";

    /// <summary>
    /// Consumer prefetch count (QoS).
    /// Maximum number of unacknowledged messages per consumer.
    /// Controls throughput and backpressure.
    /// </summary>
    public ushort PrefetchCount { get; init; } = 10;

    /// <summary>
    /// Grace period in milliseconds for graceful consumer shutdown.
    /// Time to wait for pending messages to complete before closing channels.
    /// </summary>
    public int ShutdownGracePeriodMs { get; init; } = 1000;
}
