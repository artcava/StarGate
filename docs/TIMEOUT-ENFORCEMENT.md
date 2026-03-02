# Timeout Enforcement

## Overview

StarGate implements a comprehensive three-layer timeout enforcement strategy to ensure processes don't exceed their configured timeout duration. This document describes the architecture, implementation, and operational considerations.

## Architecture

### Three-Layer Strategy

Timeout enforcement operates at three complementary layers:

#### Layer 1: Queue Timeout Check (Pre-Execution)

**Location:** `ProcessWorker.ExecuteProcessAsync` (before handler execution)

**Purpose:** Detect processes that timed out while waiting in the message queue.

**How it works:**
```csharp
var process = await _processService.GetProcessAsync(processId);

if (process.IsTimedOut)
{
    await _processService.FailProcessAsync(
        processId,
        "PROCESS_TIMEOUT",
        $"Process timed out before handler execution (timeout: {process.TimeoutAt})",
        canRetry: true);
    return;
}
```

**Benefits:**
- Prevents unnecessary handler execution
- Saves compute resources
- Provides immediate feedback
- Fast-fails timed-out processes

#### Layer 2: Handler Execution Timeout (During Execution)

**Location:** `ProcessWorker.ExecuteProcessAsync` (during handler execution)

**Purpose:** Enforce timeout during handler execution using cancellation tokens.

**How it works:**
```csharp
// Calculate remaining time
var remainingTime = process.TimeoutAt.HasValue
    ? process.TimeoutAt.Value - DateTime.UtcNow
    : TimeSpan.FromHours(1);

if (remainingTime <= TimeSpan.Zero)
{
    remainingTime = TimeSpan.FromSeconds(5); // Minimum grace period
}

// Create linked cancellation token
using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
timeoutCts.CancelAfter(remainingTime);

try
{
    await handler.ExecuteAsync(process, timeoutCts.Token);
}
catch (OperationCanceledException) when (timeoutCts.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
{
    // Timeout occurred (not graceful shutdown)
    await _processService.FailProcessAsync(
        processId,
        "PROCESS_TIMEOUT",
        $"Handler execution exceeded timeout of {remainingTime.TotalSeconds} seconds",
        canRetry: true);
    throw;
}
```

**Benefits:**
- Precise timeout enforcement during execution
- Handler can cooperatively cancel via CancellationToken
- Distinguishes timeout from graceful shutdown
- Enables cleanup in handlers (via token cancellation)

#### Layer 3: Background Scanner (Safety Net)

**Location:** `TimeoutScannerWorker`

**Purpose:** Periodic scan for processes that escaped Layers 1 and 2.

**How it works:**
```csharp
// Runs every 1 minute
var timedOutProcesses = await _processRepository.GetTimedOutProcessesAsync();

foreach (var process in timedOutProcesses)
{
    await _processService.CheckTimeoutAsync(process.ProcessId);
}
```

**Benefits:**
- Catches edge cases (worker crash, network issues)
- Ensures no process stuck in active state indefinitely
- Runs independently of message processing
- Provides system-wide timeout guarantee

## Configuration

### Policy-Based Timeout

Timeouts are configured per process type via policies:

```json
POST /api/policies/process-types
{
  "processType": "order-processing",
  "maxRetries": 3,
  "timeoutSeconds": 300,
  "maxConcurrentProcesses": 10,
  "retentionDays": 30
}
```

### Timeout Calculation

```
TimeoutAt = CreatedAt + TimeoutSeconds (from policy)

RemainingTime = TimeoutAt - DateTime.UtcNow

If RemainingTime <= 0:
  Use minimum grace period (5 seconds)
Else:
  Use RemainingTime
```

### Default Timeout

If no timeout is configured:
- Default: **1 hour** (3600 seconds)
- Prevents infinite execution
- Configurable per deployment

### Grace Period

**Minimum grace period:** 5 seconds

**Why needed:**
- Process may have just timed out (1-2 seconds ago)
- Allows handler to start and check cancellation token
- Prevents immediate cancellation before handler initialization
- Enables proper cleanup in handlers

## Timeout vs Graceful Shutdown

### Distinguishing Timeout from Shutdown

Critical logic in ProcessWorker:

```csharp
catch (OperationCanceledException) when (
    timeoutCts.IsCancellationRequested &&
    !cancellationToken.IsCancellationRequested)
{
    // TIMEOUT occurred (not shutdown)
}
```

### Why Use Linked Tokens?

```csharp
var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
```

**Benefits:**
1. Handler cancels on **either** timeout or shutdown
2. Single token to check in handler code
3. Proper cleanup in both scenarios
4. Distinguishable via token inspection

### Decision Table

| timeoutCts | cancellationToken | Interpretation |
|------------|-------------------|----------------|
| Requested  | NOT Requested     | **TIMEOUT**    |
| Requested  | Requested         | **SHUTDOWN**   |
| NOT Req    | NOT Req           | **RUNNING**    |
| NOT Req    | Requested         | **SHUTDOWN**   |

## Handler Implementation

### Cooperative Cancellation

Handlers should check `CancellationToken` regularly:

```csharp
public class OrderProcessingHandler : IProcessHandler
{
    public async Task ExecuteAsync(Process process, CancellationToken ct)
    {
        // Check cancellation frequently
        ct.ThrowIfCancellationRequested();

        await Step1(ct);
        
        ct.ThrowIfCancellationRequested();
        
        await Step2(ct);
        
        // Long-running operation
        await LongRunningTask(ct);
    }

    private async Task LongRunningTask(CancellationToken ct)
    {
        for (int i = 0; i < 1000; i++)
        {
            // Check every iteration
            ct.ThrowIfCancellationRequested();
            
            await ProcessItem(i);
        }
    }
}
```

### Cleanup on Cancellation

```csharp
public async Task ExecuteAsync(Process process, CancellationToken ct)
{
    Resource? resource = null;
    
    try
    {
        resource = await AcquireResource(ct);
        await ProcessWithResource(resource, ct);
    }
    finally
    {
        // Cleanup even if cancelled/timed out
        if (resource != null)
        {
            await ReleaseResource(resource);
        }
    }
}
```

## Retry Behavior

### Timeout is Retryable

By default, timeout errors allow retry:

```csharp
await _processService.FailProcessAsync(
    processId,
    "PROCESS_TIMEOUT",
    message,
    canRetry: true);
```

**Rationale:**
- Timeout may be transient (temporary high load)
- Retry might succeed with more available time
- Policy `MaxRetries` limits total attempts

### Retry Considerations

**Retries will occur if:**
- `Retryable = true` on process
- `CurrentRetries < MaxRetries` (from policy)
- Process not in terminal state

**After max retries:**
- Process transitions to `Failed` (terminal)
- No further retries
- Error logged with "max retries exceeded"

## Monitoring

### Logs

**Pre-execution timeout:**
```
WARNING: Process timed out before execution: ProcessId={ProcessId}, TimeoutAt={TimeoutAt}
```

**Handler execution timeout:**
```
WARNING: Process execution timed out: ProcessId={ProcessId}, Timeout={Timeout}s
```

**Scanner detection:**
```
WARNING: Failing timed-out process: ProcessId={ProcessId}, TimeoutAt={TimeoutAt}, Status={Status}
```

### Metrics (Future Enhancement)

Recommended metrics:
- `stargate_timeouts_total{layer}` - Counter per layer
- `stargate_timeout_scan_duration_seconds` - Scanner execution time
- `stargate_timeout_processes_found` - Processes per scan
- `stargate_handler_execution_seconds{process_type}` - Handler duration

### Health Checks

TimeoutScannerWorker runs independently:
- No dedicated health check (fire-and-forget)
- Errors logged but don't affect system health
- Scanner retries on failure

## Performance Impact

### Overhead per Message

**Layer 1 (Pre-execution check):**
- 1 additional `GetProcessAsync` call: ~10ms
- Timeout calculation: <1ms
- **Total:** ~10ms per message

**Layer 2 (Handler execution):**
- CancellationToken overhead: negligible (<1ms)
- Linked token creation: <1ms
- **Total:** <1ms per message

### System-Wide Overhead

**Layer 3 (Background scanner):**
- 1 MongoDB query per minute: ~50ms
- Batch size: 100 processes
- **Total:** 50ms/minute system-wide

### Optimization Opportunities

1. **Cache process in message** (future)
   - Include process data in ProcessMessage
   - Eliminate Layer 1 GetProcessAsync call
   - Reduces latency by ~10ms per message

2. **Indexed queries**
   - Ensure indexes on `Status` and `TimeoutAt`
   - Scanner query uses composite index
   - Keeps query time <50ms even with millions of processes

3. **Configurable scan interval**
   - Currently: 1 minute (hardcoded)
   - Could be configurable via appsettings.json
   - Trade-off: accuracy vs overhead

## Troubleshooting

### Process Timing Out Unexpectedly

**Check timeout configuration:**
```bash
GET /api/policies/process-types/{processType}
```

**Verify handler execution time:**
```bash
# Check logs for handler duration
grep "Handler execution completed" logs/*.log
```

**Common causes:**
- Timeout too short for handler complexity
- Handler not checking CancellationToken
- External service slow/unavailable
- Database query taking too long

### Process Stuck in Processing

**Verify scanner is running:**
```bash
grep "TimeoutScannerWorker" logs/*.log
```

**Check if process actually timed out:**
```bash
GET /api/processes/{processId}
# Compare TimeoutAt with current time
```

**Force timeout check:**
```bash
# Scanner will detect on next cycle (max 1 minute)
# Or trigger manually via ProcessService.CheckTimeoutAsync
```

### High Timeout Rate

**Investigate root cause:**
1. Check handler performance metrics
2. Verify external dependencies healthy
3. Review database query performance
4. Check system resource utilization

**Temporary mitigation:**
1. Increase timeout in policy
2. Scale worker instances
3. Optimize handler implementation

## Testing

### Unit Tests

See:
- `tests/StarGate.Server.Tests/Workers/TimeoutScannerWorkerTests.cs`
- `tests/StarGate.Server.Tests/Workers/ProcessWorkerTimeoutTests.cs`

### Integration Tests

See:
- `tests/StarGate.Integration.Tests/Persistence/MongoProcessRepositoryTimeoutTests.cs`

### End-to-End Testing

```bash
# 1. Create policy with 10-second timeout
POST /api/policies/process-types
{
  "processType": "slow-order",
  "timeoutSeconds": 10,
  "maxRetries": 3
}

# 2. Create process
POST /api/processes
{
  "clientId": "test-client",
  "processType": "slow-order",
  "clientProcessId": "order-123"
}

# 3. Handler should exceed timeout
# 4. Verify process status
GET /api/processes/{processId}
# Expected:
# - status: Failed
# - errors[0].errorCode: PROCESS_TIMEOUT
# - errors[0].message: "Handler execution exceeded timeout of X seconds"
```

## References

- [Issue #101: Phase 7.1 Timeout Enforcement](https://github.com/artcava/StarGate/issues/101)
- [TECHNICAL-ANALYSIS.md - Phase 7](../docs/TECHNICAL-ANALYSIS.md)
- [CancellationToken Best Practices](https://learn.microsoft.com/en-us/dotnet/standard/threading/cancellation-in-managed-threads)
- [MongoDB Query Optimization](https://www.mongodb.com/docs/manual/core/query-optimization/)
