# Process State Machine Documentation

## Overview

This document describes the state machine implementation for process lifecycle management in StarGate. The state machine ensures that processes transition through well-defined states with validation, retry logic, and comprehensive error handling.

## State Diagram

```
                    ┌─────────┐
                    │ Pending │
                    └────┬────┘
                         │
           ┌─────────────┼─────────────┐
           │                           │
           ▼                           ▼
      ┌─────────┐              ┌──────────┐
      │ Rejected│              │ Accepted │
      └─────────┘              └────┬─────┘
      (Terminal)                    │
                                    │
                              ┌─────▼──────┐
                              │ Processing │
                              └─────┬──────┘
                                    │
           ┌────────────────────────┼────────────────────┐
           │                        │                    │
           ▼                        ▼                    ▼
      ┌──────────┐            ┌──────────┐         ┌────────┐
      │ Completed│            │ Retrying │         │ Failed │
      └──────────┘            └────┬─────┘         └────────┘
      (Terminal)                   │               (Terminal)
                                   │
                                   └──────────────┐
                                                  │
                                            (loops back
                                          to Processing)
```

## States

### Pending
- **Description**: Initial state after process submission, awaiting validation
- **Valid Transitions**:
  - `Accepted`: Validation passed, process queued for execution
  - `Rejected`: Validation failed, business rules not met
- **Duration**: Typically milliseconds to seconds
- **Purpose**: Allows pre-processing validation before resource commitment

### Accepted
- **Description**: Process validated and queued for execution
- **Valid Transitions**:
  - `Processing`: Worker picked up the process
  - `Failed`: Catastrophic failure before processing started
  - `Rejected`: Late-stage validation failure
- **Duration**: Depends on queue depth and worker availability
- **Purpose**: Separates admission control from execution

### Processing
- **Description**: Process is actively being executed by a worker
- **Valid Transitions**:
  - `Completed`: Execution finished successfully
  - `Failed`: Permanent failure (non-retryable or retry limit exceeded)
  - `Retrying`: Recoverable failure, retry scheduled
- **Duration**: Varies based on process type and workload
- **Purpose**: Main execution phase with progress tracking

### Retrying
- **Description**: Process encountered a recoverable error and is waiting for retry
- **Valid Transitions**:
  - `Processing`: Retry attempt started
  - `Failed`: Retry limit exceeded or non-retryable error occurred
- **Duration**: Configured retry delay (exponential backoff)
- **Purpose**: Handles transient failures without permanent failure

### Completed
- **Description**: Process finished successfully
- **Valid Transitions**: None (terminal state)
- **Properties Set**:
  - `CompletedAt`: Timestamp of completion
  - `Progress`: Set to 100
  - `Result`: Contains execution output
- **Purpose**: Indicates successful completion, result available

### Failed
- **Description**: Process failed permanently
- **Valid Transitions**: None (terminal state)
- **Properties Set**:
  - `FailedAt`: Timestamp of permanent failure
  - `Errors`: List of all errors encountered
- **Reasons for Failure**:
  - Non-retryable error occurred
  - Retry limit exceeded
  - Process marked as non-retryable
- **Purpose**: Indicates permanent failure, requires manual intervention

### Rejected
- **Description**: Process rejected due to validation or policy violation
- **Valid Transitions**: None (terminal state)
- **Properties Set**:
  - `Errors`: Contains rejection reason with code `PROCESS_REJECTED`
- **Common Reasons**:
  - Business rule violation
  - Invalid input data
  - Policy constraint not met
- **Purpose**: Differentiates validation failures from execution failures

## Terminal States

Three states are terminal and allow no further transitions:
1. **Completed**: Successful completion
2. **Failed**: Permanent failure
3. **Rejected**: Validation/policy rejection

Once a process reaches a terminal state, it cannot be modified. To retry, a new process must be created.

## Transition Logic

### Valid Transitions Matrix

| From State  | To States                              |
|-------------|---------------------------------------|
| Pending     | Accepted, Rejected                    |
| Accepted    | Processing, Failed, Rejected          |
| Processing  | Completed, Failed, Retrying           |
| Retrying    | Processing, Failed                    |
| Completed   | (none - terminal)                     |
| Failed      | (none - terminal)                     |
| Rejected    | (none - terminal)                     |

### Validation Rules

1. **All transitions are validated** before execution
2. **Invalid transitions throw** `InvalidStateTransitionException`
3. **Terminal states** cannot transition to any other state
4. **Timestamps** are automatically updated on state changes
5. **Progress** is set to 100 only on Completed state

## Retry Logic

The retry decision follows this logic:

```csharp
shouldRetry = canRetry &&              // Error is retryable
              process.Retryable &&      // Process allows retries
              !process.IsRetryLimitExceeded;  // Limit not reached

if (shouldRetry)
    Status = Retrying + Increment RetryCount
else
    Status = Failed + Set FailedAt
```

### Retry Parameters

- **MaxRetries**: Maximum retry attempts (from policy at creation)
- **RetryCount**: Current number of retry attempts
- **Retryable**: Whether the process supports retries
- **canRetry**: Whether the specific error is retryable

### Retry Scenarios

| Scenario                        | Result  | Reason                      |
|---------------------------------|---------|-----------------------------|
| Error retryable, retries < max  | Retrying| Normal retry                |
| Error retryable, retries >= max | Failed  | Retry limit exceeded        |
| Error retryable, process not    | Failed  | Process doesn't allow retry |
| Error not retryable             | Failed  | Fatal error                 |

## Error Handling

### Error Recording

Processes maintain a list of errors encountered during execution:

```csharp
public class ProcessErrorEntry
{
    public string ErrorCode { get; init; }
    public string Message { get; init; }
    public bool Retryable { get; init; }
    public DateTime Timestamp { get; init; }
}
```

### Error Methods

1. **RecordProcessErrorAsync**: Adds error without changing status
2. **FailProcessAsync**: Records error and transitions to Failed or Retrying

### Error Accumulation

- Errors are **never removed**, providing complete history
- Each retry attempt can add new errors
- Errors include timestamp for chronological analysis

## Timeout Handling

### Timeout Check

```csharp
public bool IsTimedOut => TimeoutAt.HasValue && DateTime.UtcNow > TimeoutAt.Value;
```

### CheckTimeoutAsync Behavior

1. Compares `TimeoutAt` with current UTC time
2. If timed out, calls `FailProcessAsync` with:
   - ErrorCode: `"PROCESS_TIMEOUT"`
   - Message: Includes timeout duration
   - canRetry: `true` (allows retry if policy permits)
3. If not timed out, does nothing

### Timeout Includes

- Queuing time (while in Accepted state)
- Processing time (while in Processing state)
- Retry delays (while in Retrying state)

## Progress Tracking

### Progress Updates

- **Range**: 0-100 (enforced with validation)
- **Method**: `UpdateProcessProgressAsync`
- **Status Independence**: Progress updates don't change status
- **Best Practices**:
  - Update regularly (e.g., every 5-10%)
  - Always initialize to 0
  - Set to 100 only on Completed

### Progress Validation

```csharp
if (progress < 0 || progress > 100)
    throw new ArgumentOutOfRangeException();
```

## Helper Properties

### Process Domain Model

The Process entity includes computed properties for state checking:

```csharp
public bool IsRetryLimitExceeded => RetryCount >= MaxRetries;
public bool IsTimedOut => TimeoutAt.HasValue && DateTime.UtcNow > TimeoutAt.Value;
public bool IsTerminal => Status is Completed or Failed or Rejected;
public bool IsActive => Status is Accepted or Processing or Retrying;
```

### Usage Examples

```csharp
// Check if process can be retried
if (!process.IsRetryLimitExceeded && process.Retryable)
{
    // Retry is possible
}

// Check if process needs timeout handling
if (process.IsTimedOut && !process.IsTerminal)
{
    await _processService.CheckTimeoutAsync(process.ProcessId);
}
```

## API Methods

### State Transition Methods

| Method                         | Purpose                           | Validates Transition |
|--------------------------------|-----------------------------------|---------------------|
| UpdateProcessStatusAsync       | Generic status update             | Yes                 |
| TransitionToProcessingAsync    | Transition to Processing          | Yes                 |
| CompleteProcessAsync           | Mark as completed                 | Yes                 |
| FailProcessAsync               | Fail with retry logic             | Yes                 |
| RejectProcessAsync             | Reject process                    | Yes                 |

### Supporting Methods

| Method                      | Purpose                              |
|-----------------------------|--------------------------------------|
| UpdateProcessProgressAsync  | Update progress (0-100)              |
| RecordProcessErrorAsync     | Add error without status change      |
| IncrementRetryCountAsync    | Increment retry counter              |
| CheckTimeoutAsync           | Check and handle timeout             |

## State-Specific Timestamps

| State      | Timestamp Property | When Set                    |
|------------|-------------------|-----------------------------||
| Created    | CreatedAt         | Process creation            |
| Updated    | UpdatedAt         | Every state change          |
| Completed  | CompletedAt       | Transition to Completed     |
| Failed     | FailedAt          | Transition to Failed        |

## Best Practices

### For Process Workers

1. **Always validate state** before attempting transitions
2. **Use specific methods** (e.g., `CompleteProcessAsync`) instead of generic `UpdateProcessStatusAsync`
3. **Record errors** using `RecordProcessErrorAsync` for non-fatal issues
4. **Update progress regularly** to provide visibility
5. **Check timeouts periodically** for long-running processes

### For Error Handling

1. **Classify errors correctly** (retryable vs. non-retryable)
2. **Use structured error codes** (e.g., `"TIMEOUT"`, `"VALIDATION_ERROR"`)
3. **Provide descriptive messages** for debugging
4. **Never remove errors** from the history

### For Testing

1. **Test all valid transitions** with [Theory] tests
2. **Test all invalid transitions** to ensure exceptions
3. **Test retry logic** with various retry counts
4. **Test timeout scenarios** with past and future timestamps
5. **Test edge cases** (e.g., progress boundaries, terminal states)

## Logging

All state transitions are logged with structured logging:

```csharp
_logger.LogInformation(
    "Process status updated: ProcessId={ProcessId}, From={FromStatus}, To={ToStatus}",
    processId,
    previousStatus,
    newStatus);
```

### Log Levels

- **Information**: Successful transitions, creation, completion
- **Warning**: Failures, rejections, timeouts, retries
- **Error**: Unexpected exceptions, validation failures
- **Debug**: Progress updates, detailed flow

## Performance Considerations

### Database Updates

- Each state transition requires a database write
- Use batch updates where possible
- Consider caching for frequently accessed processes

### Concurrency

- State transitions should be atomic
- Use optimistic concurrency where supported
- Handle race conditions gracefully

### Scalability

- State machine logic is stateless and horizontally scalable
- Retry scheduling can be distributed across workers
- Progress updates can be throttled to reduce write load

## Migration Notes

### From Previous Version

The new state machine adds three states:
- **Pending**: New initial state
- **Retrying**: Explicit retry state
- **Rejected**: Validation failure state

### Breaking Changes

1. Initial status changed from `Accepted` to `Pending` (if creating new processes)
2. `Accepted` is no longer initial state for newly created processes
3. Terminal state transitions now throw exceptions instead of silently failing

### Migration Path

1. Update database schema to support new states
2. Update existing processes in `Accepted` state (they remain valid)
3. Update worker code to handle new `Retrying` state
4. Update validation logic to use `Rejected` state
5. Test all state transitions thoroughly

## Examples

### Successful Process Flow

```
Pending → Accepted → Processing → Completed
```

### Failed Process with Retry

```
Pending → Accepted → Processing → Retrying → Processing → Completed
```

### Failed Process (Retry Limit)

```
Pending → Accepted → Processing → Retrying → Processing → Retrying → Processing → Failed
```

### Rejected Process

```
Pending → Rejected
```

### Timeout Scenario

```
Pending → Accepted → Processing → (timeout) → Retrying → Processing → Completed
```

## Related Documentation

- [Technical Analysis](./TECHNICAL-ANALYSIS.md#process-state-machine)
- [Coding Conventions](./CODING-CONVENTIONS.md)
- [API Documentation](./API.md#process-lifecycle)
