# Retry Logic Implementation

## Overview

This document describes the retry logic implementation in StarGate ProcessWorker, which handles transient failures with exponential backoff and coordinated message redelivery through RabbitMQ.

## Architecture

### Components

1. **RetryConfiguration** (`src/StarGate.Core/Configuration/RetryConfiguration.cs`)
   - Configures retry behavior parameters
   - Implements exponential backoff calculation
   - Supports jitter to prevent thundering herd

2. **ProcessWorker** (`src/StarGate.Server/Workers/ProcessWorker.cs`)
   - Consumes process messages
   - Executes handlers with timeout enforcement
   - Implements comprehensive retry logic
   - Coordinates with RabbitMQ for message redelivery

3. **RabbitMqBroker** (`src/StarGate.Infrastructure/Messaging/RabbitMQ/RabbitMqBroker.cs`)
   - Publishes delayed messages for retry
   - Uses message TTL and dead-letter exchange pattern

## Exponential Backoff Formula

The retry delay is calculated using exponential backoff:

```
Delay = BaseDelay × (Multiplier ^ RetryCount)
```

### Example with Default Configuration

- BaseDelay = 5 seconds
- Multiplier = 2.0
- MaxDelay = 300 seconds (5 minutes)

| Retry Attempt | Calculated Delay | Actual Delay (with cap) |
|---------------|------------------|------------------------|
| 0 (1st retry) | 5s               | 5s                     |
| 1 (2nd retry) | 10s              | 10s                    |
| 2 (3rd retry) | 20s              | 20s                    |
| 3 (4th retry) | 40s              | 40s                    |
| 4 (5th retry) | 80s              | 80s                    |
| 5 (6th retry) | 160s             | 160s                   |
| 6 (7th retry) | 320s             | 300s (capped)          |

## Jitter Implementation

Jitter adds randomization to retry delays to prevent thundering herd problem:

```
JitterRange = Delay × 30%
FinalDelay = Delay × (1 + Random(-0.15, +0.15))
```

### Benefits of Jitter

**Without Jitter:**
- All failed processes retry at the same time
- Causes load spikes on downstream systems
- Can trigger cascading failures

**With Jitter:**
- Retries distributed over time
- Smoother load distribution
- Better system stability

## Error Classification

### Retryable Errors

Errors that indicate transient failures and should trigger retry:

- `TimeoutException` - Process execution timeout
- `OperationCanceledException` - Graceful shutdown (will retry after restart)
- `HttpRequestException` - Network/HTTP errors
- `UNKNOWN_ERROR` - Unclassified errors (default to retry)

### Non-Retryable Errors

Errors that indicate permanent failures and should not retry:

- `InvalidOperationException` - Business logic violations
- `NO_HANDLER_FOUND` - Missing handler for process type
- Validation failures
- Authorization errors

## Retry Flow

```
┌─────────────────────────────────────┐
│ Handler Execution Fails             │
└──────────────┬──────────────────────┘
               │
               ▼
┌─────────────────────────────────────┐
│ Classify Error Type                 │
│ (Retryable vs Non-Retryable)        │
└──────────────┬──────────────────────┘
               │
               ▼
┌─────────────────────────────────────┐
│ Call ProcessService.FailProcessAsync│
│ (pass canRetry flag)                │
└──────────────┬──────────────────────┘
               │
               ▼
┌─────────────────────────────────────┐
│ ProcessService Decides:             │
│ - Check RetryCount vs MaxRetries    │
│ - Set Status: Retrying or Failed    │
└──────────────┬──────────────────────┘
               │
      ┌────────┴────────┐
      ▼                 ▼
┌───────────┐    ┌──────────────┐
│ Retrying  │    │ Failed       │
│ Status    │    │ (Permanent)  │
└─────┬─────┘    └──────────────┘
      │
      ▼
┌─────────────────────────────────────┐
│ Calculate Delay (Exponential +      │
│ Jitter)                             │
└──────────────┬──────────────────────┘
               │
               ▼
┌─────────────────────────────────────┐
│ Publish Delayed Message to RabbitMQ │
│ (using PublishWithDelayAsync)       │
└──────────────┬──────────────────────┘
               │
               ▼
┌─────────────────────────────────────┐
│ Message Redelivered After Delay     │
└──────────────┬──────────────────────┘
               │
               ▼
┌─────────────────────────────────────┐
│ ProcessWorker Receives Message      │
│ Retry Attempt Begins                │
└─────────────────────────────────────┘
```

## Configuration

### appsettings.json

```json
{
  "Retry": {
    "BaseDelaySeconds": 5,
    "MaxDelaySeconds": 300,
    "BackoffMultiplier": 2.0,
    "UseJitter": true
  }
}
```

### appsettings.Development.json

```json
{
  "Retry": {
    "BaseDelaySeconds": 3,
    "MaxDelaySeconds": 60,
    "BackoffMultiplier": 2.0,
    "UseJitter": true
  }
}
```

### Configuration Properties

| Property            | Type   | Default | Description                                    |
|---------------------|--------|---------|------------------------------------------------|
| BaseDelaySeconds    | int    | 5       | Initial delay for first retry                  |
| MaxDelaySeconds     | int    | 300     | Maximum delay cap (prevents excessive waits)   |
| BackoffMultiplier   | double | 2.0     | Exponential growth factor                      |
| UseJitter           | bool   | true    | Enable/disable jitter randomization            |

## RabbitMQ Delayed Messages

### Implementation Approach: Message TTL + Dead Letter Exchange

The implementation uses RabbitMQ's native TTL (Time-To-Live) and Dead Letter Exchange mechanism:

```
┌──────────────┐    TTL Expires    ┌──────────────┐    Route    ┌──────────────┐
│ Delay Queue  │ ─────────────────▶ │ Dead Letter  │ ──────────▶ │ Main Queue   │
│ (with TTL)   │                    │ Exchange     │             │              │
└──────────────┘                    └──────────────┘             └──────────────┘
```

### Message Flow

1. **Initial Publish**: Message published with `Expiration` property
2. **TTL Wait**: Message sits in queue until TTL expires
3. **DLX Route**: Expired message routed through Dead Letter Exchange
4. **Redelivery**: Message arrives in main queue for processing

### Advantages

- No plugins required (native RabbitMQ feature)
- Reliable and well-tested
- Scales efficiently
- Supports arbitrary delay durations

## Testing Retry Behavior

### Manual Testing Steps

1. **Start Infrastructure**
   ```bash
   docker-compose up -d rabbitmq mongodb redis
   dotnet run --project src/StarGate.Server
   ```

2. **Create Policy with Retries**
   ```bash
   curl -X POST http://localhost:5000/api/policies/process-types \
     -H "Content-Type: application/json" \
     -d '{
       "processType": "test-retry",
       "maxRetries": 3,
       "timeoutSeconds": 30
     }'
   ```

3. **Create Failing Process**
   ```bash
   curl -X POST http://localhost:5000/api/processes \
     -H "Content-Type: application/json" \
     -d '{
       "clientId": "test-client",
       "processType": "test-retry",
       "clientProcessId": "retry-test-001"
     }'
   ```

4. **Verify Retry Timing**
   - Attempt 1: Immediate (t=0s)
   - Attempt 2: ~5 seconds after failure (t≈5s)
   - Attempt 3: ~10 seconds after 2nd failure (t≈15s)
   - Attempt 4: ~20 seconds after 3rd failure (t≈35s)
   - Final Status: `Failed` (MaxRetries exceeded)

5. **Check Process Status**
   ```bash
   curl http://localhost:5000/api/processes/{processId}
   ```

   Expected response:
   ```json
   {
     "processId": "...",
     "status": "Failed",
     "retryCount": 3,
     "maxRetries": 3,
     "errors": [
       { "errorCode": "...", "timestamp": "..." },
       { "errorCode": "...", "timestamp": "..." },
       { "errorCode": "...", "timestamp": "..." },
       { "errorCode": "...", "timestamp": "..." }
     ]
   }
   ```

### Unit Tests

Run retry logic unit tests:

```bash
dotnet test tests/StarGate.Server.Tests --filter "FullyQualifiedName~Retry"
```

Test coverage includes:
- Exponential backoff calculation
- Max delay enforcement
- Jitter randomization
- Configuration defaults

## Monitoring and Observability

### Log Events

The retry logic produces structured logs for monitoring:

```csharp
// Retry decision
_logger.LogWarning(
    "Handling process failure: ProcessId={ProcessId}, ErrorCode={ErrorCode}, CanRetry={CanRetry}",
    processId, errorCode, canRetry);

// Retry scheduled
_logger.LogInformation(
    "Process will retry: ProcessId={ProcessId}, RetryCount={RetryCount}/{MaxRetries}, Delay={Delay}s",
    processId, process.RetryCount, process.MaxRetries, retryDelay.TotalSeconds);

// Permanent failure
_logger.LogWarning(
    "Process failed permanently: ProcessId={ProcessId}, Status={Status}, RetryCount={RetryCount}",
    processId, process.Status, process.RetryCount);
```

### Metrics to Monitor

- **Retry Rate**: Percentage of processes requiring retry
- **Retry Count Distribution**: How many retries before success/failure
- **Retry Delay Accuracy**: Actual vs expected retry timing
- **Permanent Failure Rate**: Processes that exhaust all retries

## Performance Considerations

### Memory Impact

Delayed messages are stored in RabbitMQ queues:
- Memory usage scales with number of delayed messages
- Use appropriate queue limits if necessary

### Network Impact

- Each retry publishes a new message to RabbitMQ
- Minimal network overhead (single publish operation)

### Throughput Impact

- Retry logic executes asynchronously
- No blocking on ProcessWorker threads
- Failed processes don't block new message consumption

## Troubleshooting

### Problem: Messages Not Retrying

**Possible Causes:**
1. Process marked as non-retryable (`canRetry = false`)
2. MaxRetries already reached
3. RabbitMQ delayed message configuration issue

**Solution:**
- Check process status and `canRetry` flag in logs
- Verify `maxRetries` in process policy
- Verify RabbitMQ Dead Letter Exchange configuration

### Problem: Retry Delays Too Short/Long

**Possible Causes:**
1. Incorrect configuration in appsettings.json
2. Jitter causing unexpected variance

**Solution:**
- Review `RetryConfiguration` settings
- Disable jitter temporarily for testing: `"UseJitter": false`
- Monitor actual delay times in logs

### Problem: Thundering Herd

**Symptoms:**
- Multiple processes retrying simultaneously
- Load spikes at regular intervals

**Solution:**
- Ensure `UseJitter` is enabled
- Increase jitter range if needed (modify `RetryConfiguration.CalculateDelay`)
- Stagger initial process creation times

## Future Enhancements

### Planned Improvements

1. **Adaptive Backoff**: Adjust multiplier based on system load
2. **Per-Error-Type Configuration**: Different retry strategies per error
3. **Circuit Breaker Integration**: Stop retries during outages
4. **Metrics Dashboard**: Real-time retry statistics
5. **Retry Budget**: Limit total retry attempts across all processes

## References

- [Exponential Backoff Pattern](https://en.wikipedia.org/wiki/Exponential_backoff)
- [RabbitMQ TTL and DLX](https://www.rabbitmq.com/ttl.html)
- [RabbitMQ Delayed Messages](https://www.rabbitmq.com/blog/2015/04/16/scheduling-messages-with-rabbitmq)
- [TECHNICAL-ANALYSIS.md - Phase 7.1](../TECHNICAL-ANALYSIS.md)
- [Issue #102](https://github.com/artcava/StarGate/issues/102)
