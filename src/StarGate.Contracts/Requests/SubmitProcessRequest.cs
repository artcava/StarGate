namespace StarGate.Contracts.Requests;

/// <summary>
/// Request to submit a new process for asynchronous execution.
/// Contains process identification, type, payload, and idempotency key.
/// </summary>
/// <param name="ClientProcessId">Client-provided unique identifier for this process. Used for idempotency checks.</param>
/// <param name="ProcessType">Type of process to execute (e.g., "order", "payment", "shipment").</param>
/// <param name="Payload">Process-specific data. Will be serialized and passed to handler.</param>
/// <param name="IdempotencyKey">Unique key to prevent duplicate submissions. If same key is submitted twice, returns existing process.</param>
public record SubmitProcessRequest(
    string ClientProcessId,
    string ProcessType,
    object Payload,
    string IdempotencyKey);
