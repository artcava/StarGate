namespace StarGate.Core.Abstractions;

/// <summary>
/// Message broker abstraction for publishing messages to queues.
/// Allows replacing implementation (RabbitMQ, Azure Service Bus, etc.) without core changes.
/// Supports asynchronous message publishing and consumer creation.
/// </summary>
public interface IMessageBroker
{
    /// <summary>
    /// Publishes a message to a queue for asynchronous processing.
    /// Uses default message properties (no custom metadata).
    /// Message is serialized and sent to the specified queue.
    /// </summary>
    /// <typeparam name="T">Type of message payload (must be reference type).</typeparam>
    /// <param name="queueName">Target queue name.</param>
    /// <param name="message">Message to publish.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Task representing the asynchronous operation.</returns>
    /// <exception cref="ArgumentNullException">If queueName or message is null.</exception>
    /// <exception cref="InvalidOperationException">If broker connection is not available.</exception>
    public Task PublishAsync<T>(string queueName, T message, CancellationToken ct = default) where T : class;

    /// <summary>
    /// Publishes a message with custom properties.
    /// Allows setting correlation ID, priority, TTL, and custom headers.
    /// Useful for advanced messaging patterns (request/response, priority queues).
    /// </summary>
    /// <typeparam name="T">Type of message payload (must be reference type).</typeparam>
    /// <param name="queueName">Target queue name.</param>
    /// <param name="message">Message to publish.</param>
    /// <param name="properties">Additional message properties (correlation, priority, TTL, headers).</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Task representing the asynchronous operation.</returns>
    /// <exception cref="ArgumentNullException">If queueName, message, or properties is null.</exception>
    /// <exception cref="InvalidOperationException">If broker connection is not available.</exception>
    public Task PublishAsync<T>(
        string queueName,
        T message,
        MessageProperties properties,
        CancellationToken ct = default) where T : class;

    /// <summary>
    /// Publishes a message to the broker using a routing key.
    /// </summary>
    /// <typeparam name="T">Type of message payload (must be reference type).</typeparam>
    /// <param name="message">Message to publish.</param>
    /// <param name="routingKey">Routing key for topic-based routing.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Task representing the asynchronous operation.</returns>
    public Task PublishAsync<T>(
        T message,
        string routingKey,
        CancellationToken cancellationToken = default) where T : class;

    /// <summary>
    /// Publishes a message with delay.
    /// </summary>
    /// <typeparam name="T">Type of message payload (must be reference type).</typeparam>
    /// <param name="message">Message to publish.</param>
    /// <param name="routingKey">Routing key for topic-based routing.</param>
    /// <param name="delay">Delay before message delivery.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Task representing the asynchronous operation.</returns>
    public Task PublishWithDelayAsync<T>(
        T message,
        string routingKey,
        TimeSpan delay,
        CancellationToken cancellationToken = default) where T : class;

    /// <summary>
    /// Creates a consumer for a specific queue.
    /// Consumer must be started with StartConsumingAsync to begin receiving messages.
    /// Dispose consumer when done to release resources.
    /// </summary>
    /// <param name="queueName">Queue name to consume from.</param>
    /// <param name="cancellationToken">Cancellation token for consumer lifecycle.</param>
    /// <returns>Configured consumer ready to start consuming messages.</returns>
    /// <exception cref="ArgumentNullException">If queueName is null.</exception>
    /// <exception cref="InvalidOperationException">If broker connection is not available.</exception>
    public IMessageConsumer CreateConsumer(string queueName, CancellationToken cancellationToken = default);
}
