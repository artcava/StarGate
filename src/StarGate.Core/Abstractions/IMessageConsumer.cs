namespace StarGate.Core.Abstractions;

/// <summary>
/// Message consumer abstraction for receiving messages from queues.
/// Supports asynchronous message processing with acknowledgment.
/// Implements IAsyncDisposable for proper resource cleanup.
/// </summary>
public interface IMessageConsumer : IAsyncDisposable
{
    /// <summary>
    /// Starts consuming messages from the queue.
    /// Message handler is invoked for each received message.
    /// Handler must acknowledge or reject message via MessageContext.
    /// Consumer continues until StopConsumingAsync is called or cancellation token is triggered.
    /// </summary>
    /// <typeparam name="T">Expected payload type (must be reference type).</typeparam>
    /// <param name="messageHandler">Handler to process messages. Receives message and context.</param>
    /// <param name="ct">Cancellation token to stop consuming.</param>
    /// <returns>Task representing the asynchronous operation.</returns>
    /// <exception cref="ArgumentNullException">If messageHandler is null.</exception>
    /// <exception cref="InvalidOperationException">If consumer is already started or disposed.</exception>
    public Task StartConsumingAsync<T>(
        Func<T, MessageContext, Task> messageHandler,
        CancellationToken ct = default) where T : class;

    /// <summary>
    /// Stops consuming messages.
    /// Waits for current message handler to complete before stopping.
    /// Does not dispose consumer - call DisposeAsync to release resources.
    /// </summary>
    /// <returns>Task representing the asynchronous operation.</returns>
    /// <exception cref="InvalidOperationException">If consumer is not started or already disposed.</exception>
    public Task StopConsumingAsync();
}
