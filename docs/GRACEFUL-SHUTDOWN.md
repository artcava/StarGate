# Graceful Shutdown Guide

This document explains the graceful shutdown implementation in the `ProcessWorker` and provides testing instructions.

## Overview

The ProcessWorker implements comprehensive graceful shutdown handling to ensure:
- No message loss during shutdown
- Clean termination of in-progress operations
- Proper resource cleanup
- Coordinated shutdown with host application

## Architecture

### Shutdown Timeline

```
t=0s    SIGTERM received
        └─> CancellationToken signaled
        └─> IsShuttingDown = true
        └─> Reject new messages
        └─> Continue processing active messages

t=30s   Worker shutdown timeout reached
        └─> Log warning if messages still active
        └─> Force stop worker

t=45s   Host shutdown timeout
        └─> Process forcefully terminated
```

### Two-Timeout Strategy

#### Worker Shutdown Timeout (30s)
- **Purpose**: Internal timeout for active message completion
- **Behavior**: Allows worker to log warnings and handle stragglers gracefully
- **Configured in**: `ProcessWorker._shutdownTimeout`

#### Host Shutdown Timeout (45s)
- **Purpose**: External timeout for entire application
- **Behavior**: Includes worker shutdown + cleanup + 15s buffer
- **Configured in**: `Program.cs` → `HostOptions.ShutdownTimeout`
- **Why 45s**: Prevents indefinite hangs while allowing graceful disposal

### Active Message Tracking

The worker uses a `ConcurrentDictionary<string, Task>` to track messages currently being processed:

```csharp
private readonly ConcurrentDictionary<string, Task> _activeMessages;
```

- **Key**: `{ProcessId}_{UniqueGuid}` to handle multiple deliveries of same message
- **Value**: The `Task` representing the message processing operation
- **Purpose**: Enables `Task.WhenAll()` to wait for completion during shutdown

## Message Requeue Strategy

### Cancelled Messages

Messages cancelled during shutdown are:
1. **NACK'd with requeue=true** → Will be processed after restart
2. **Marked with error** → `PROCESS_CANCELLED` with `retryable: true`
3. **Recorded in audit trail** → Client can query process status

### Benefits
- **Zero message loss**: Every message is either completed or requeued
- **Eventual consistency**: Cancelled messages will be retried
- **Clear audit trail**: Process status reflects cancellation

## Fresh CancellationToken Pattern

### Problem
During shutdown, the main `CancellationToken` is cancelled. If we need to record errors in the database, the operation would be cancelled too.

### Solution
```csharp
using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
await _processService.FailProcessAsync(processId, errorCode, message, canRetry, cts.Token);
```

### Benefits
- Error recording completes even during shutdown
- Short timeout (5s) prevents indefinite hangs
- Best-effort approach for critical operations

## Health Check Integration

The `ProcessWorkerHealthCheck` reports:
- **Healthy**: Normal operation, low message count
- **Degraded**: Shutting down OR high message count (>100)

Health check data includes:
```json
{
  "status": "Healthy",
  "data": {
    "activeMessages": 5
  }
}
```

### Kubernetes Integration

```yaml
apiVersion: v1
kind: Pod
metadata:
  name: stargate-server
spec:
  containers:
  - name: stargate
    image: stargate:latest
    readinessProbe:
      httpGet:
        path: /health
        port: 8080
      initialDelaySeconds: 10
      periodSeconds: 5
    livenessProbe:
      httpGet:
        path: /health
        port: 8080
      initialDelaySeconds: 30
      periodSeconds: 10
      failureThreshold: 3
```

**During Shutdown**:
1. Health check returns `Degraded`
2. Kubernetes stops routing new traffic
3. In-flight messages complete within timeout
4. Pod terminates cleanly

## Testing Instructions

### Unit Tests

```bash
# Run shutdown-specific tests
dotnet test --filter "FullyQualifiedName~ProcessWorkerShutdownTests"

# Run health check tests
dotnet test --filter "FullyQualifiedName~ProcessWorkerHealthCheckTests"
```

### Local Testing

#### 1. Test Normal Shutdown

```bash
# Start dependencies
docker-compose up -d rabbitmq mongodb redis

# Start server
dotnet run --project src/StarGate.Server

# In another terminal, create test processes
for i in {1..5}; do
  curl -X POST http://localhost:5000/api/processes \
    -H "Content-Type: application/json" \
    -d '{"clientId":"test","processType":"order","clientProcessId":"order-'$i'"}'
done

# Send SIGTERM (Ctrl+C in server terminal)
# Verify logs show:
# - "Shutdown requested. Active messages: X"
# - "Waiting for X active message(s) to complete"
# - "All active messages completed successfully"
# - "ProcessWorker stopped"
```

#### 2. Test Shutdown Timeout

```bash
# Create a handler that sleeps for 60 seconds
# (This simulates a long-running process)

# Start server and create process
curl -X POST http://localhost:5000/api/processes \
  -H "Content-Type: application/json" \
  -d '{"clientId":"test","processType":"long-running","clientProcessId":"test-1"}'

# Immediately send SIGTERM
# Verify logs show:
# - "Shutdown timeout exceeded. 1 message(s) still processing"
```

#### 3. Test Health Check

```bash
# Check health during normal operation
curl http://localhost:5000/health
# Expected: {"status":"Healthy","data":{"activeMessages":0}}

# Create multiple processes
for i in {1..10}; do
  curl -X POST http://localhost:5000/api/processes \
    -H "Content-Type: application/json" \
    -d '{"clientId":"test","processType":"order","clientProcessId":"order-'$i'"}'
done

# Check health during processing
curl http://localhost:5000/health
# Expected: {"status":"Healthy","data":{"activeMessages":10}}

# Trigger shutdown and check immediately
# Expected: {"status":"Degraded","data":{"activeMessages":X}}
```

### Docker Container Testing

```bash
# Build and start container
docker-compose up -d stargate-server

# Check logs
docker logs -f stargate-server

# Graceful stop
docker-compose stop stargate-server

# Verify graceful shutdown in logs
docker logs stargate-server | grep "Shutdown"
```

### Kubernetes Testing

```bash
# Deploy to cluster
kubectl apply -f k8s/deployment.yaml

# Watch pod during shutdown
kubectl get pod -w

# Delete pod (triggers graceful shutdown)
kubectl delete pod <pod-name>

# Check logs
kubectl logs <pod-name> | grep "Shutdown"
```

## Monitoring and Observability

### Key Metrics to Track

1. **Shutdown Duration**: Time from SIGTERM to process exit
2. **Active Messages at Shutdown**: Count when shutdown begins
3. **Timeout Exceeded Count**: How often 30s timeout is hit
4. **Message Requeue Rate**: Frequency of cancelled message requeues

### Log Queries

```bash
# Find shutdown events
grep "Shutdown requested" logs/*.log

# Find timeout events
grep "timeout exceeded" logs/*.log

# Find cancelled processes
grep "PROCESS_CANCELLED" logs/*.log
```

## Production Considerations

### Tuning Timeouts

**Factors to Consider**:
- Average message processing duration
- 95th percentile message duration
- Message complexity and dependencies
- Database operation latency

**Recommendations**:
- Worker timeout should be 2x the 95th percentile
- Host timeout should be worker timeout + 15s buffer
- Monitor and adjust based on actual metrics

### Alerting

**Critical Alerts**:
- Shutdown timeout exceeded (indicates slow messages)
- High requeue rate (indicates frequent restarts)
- Health check degraded for >5 minutes

**Warning Alerts**:
- Active message count >100 (high load)
- Shutdown duration >20s (approaching timeout)

## Troubleshooting

### Issue: Shutdown takes too long

**Symptoms**: Logs show timeout warnings

**Diagnosis**:
1. Check message processing duration in logs
2. Identify slow handlers
3. Look for database/network latency

**Solutions**:
- Increase worker timeout
- Optimize slow handlers
- Add timeout to handler operations

### Issue: Messages lost during shutdown

**Symptoms**: Processes in "Processing" state after restart

**Diagnosis**:
1. Check if NACK is being called
2. Verify RabbitMQ requeue behavior
3. Check for exceptions in shutdown logic

**Solutions**:
- Ensure NACK with requeue=true
- Verify message consumer configuration
- Add exception handling in shutdown path

### Issue: Health check always degraded

**Symptoms**: Kubernetes constantly restarting pods

**Diagnosis**:
1. Check active message count
2. Verify if worker is stuck
3. Look for deadlocks or infinite loops

**Solutions**:
- Investigate high message count cause
- Add handler timeouts
- Review handler implementation

## References

- [.NET Generic Host Shutdown](https://learn.microsoft.com/en-us/dotnet/core/extensions/generic-host)
- [Graceful Shutdown Best Practices](https://andrewlock.net/extending-the-shutdown-timeout-setting-to-ensure-graceful-ihostedservice-shutdown/)
- [Health Checks in .NET](https://learn.microsoft.com/en-us/aspnet/core/host-and-deploy/health-checks)
- [Kubernetes Pod Lifecycle](https://kubernetes.io/docs/concepts/workloads/pods/pod-lifecycle/)
