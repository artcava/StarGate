# Circuit Breaker Pattern - StarGate Implementation

## Overview

The Circuit Breaker pattern prevents cascading failures when external services (databases, message brokers, HTTP APIs) are unavailable or degraded. It acts as a protective barrier that "trips" when failures exceed a threshold, allowing the system to fail fast and recover gracefully.

## How It Works

### Circuit States

```
Closed (Normal Operation)
  ↓ (failures > threshold)
Open (Blocking All Requests)
  ↓ (after break duration)
Half-Open (Testing Recovery)
  ↓ (success)     ↓ (failure)
Closed          Open
```

#### Closed State
- **Behavior**: Normal operation, requests pass through
- **Tracking**: Failures are monitored and counted
- **Transition**: Opens when failure rate exceeds threshold

#### Open State
- **Behavior**: All requests fail immediately with `BrokenCircuitException`
- **Purpose**: Prevents overwhelming a failing service
- **Duration**: Remains open for configured `BreakDuration`
- **Benefits**: Fast failure (< 0.1ms), no downstream calls

#### Half-Open State
- **Behavior**: Allows one test request to check recovery
- **Success**: Transitions back to Closed
- **Failure**: Returns to Open state
- **Purpose**: Automatic recovery detection

## Implementation Details

### Advanced Circuit Breaker

StarGate uses **Advanced Circuit Breaker** instead of Simple Circuit Breaker:

```csharp
.AdvancedCircuitBreakerAsync(
    failureThreshold: 0.5,        // 50% failure rate
    samplingDuration: 60s,         // In last 60 seconds
    minimumThroughput: 10,         // At least 10 requests
    durationOfBreak: 30s)          // Circuit open duration
```

**Advantages**:
- Calculates **failure rate** instead of counting consecutive failures
- Requires minimum throughput before opening (avoids premature opening)
- Better handles variable traffic patterns
- More production-ready than simple circuit breaker

**vs Simple Circuit Breaker**:
```csharp
// Simple: Opens after N consecutive failures
.CircuitBreakerAsync(
    handledEventsAllowedBeforeBreaking: 5,
    durationOfBreak: TimeSpan.FromSeconds(30))
```

### Components

#### 1. CircuitBreakerConfiguration

Configures circuit breaker behavior:

```csharp
public class CircuitBreakerConfiguration
{
    public int FailureThreshold { get; set; } = 5;
    public double FailureRateThreshold { get; set; } = 0.5;  // 50%
    public int MinimumThroughput { get; set; } = 10;
    public double BreakDurationSeconds { get; set; } = 30.0;
    public double SamplingDurationSeconds { get; set; } = 60.0;
}
```

#### 2. CircuitBreakerFactory

Creates circuit breaker policies for different service types:

- **HTTP**: `CreateHttpCircuitBreaker()` - handles HTTP status codes and exceptions
- **Database**: `CreateDatabaseCircuitBreaker()` - handles MongoDB timeouts and connection errors
- **Broker**: `CreateBrokerCircuitBreaker()` - handles RabbitMQ connection failures

Each factory includes callbacks for state changes:
- `onBreak`: Logs when circuit opens
- `onReset`: Logs when circuit closes
- `onHalfOpen`: Logs during recovery testing

#### 3. ResiliencePolicyWrapper

Combines retry and circuit breaker policies:

```
Circuit Breaker (outer)
  ↓
Retry (inner)
  ↓
Actual Operation
```

**Why this order?**
1. Circuit breaker checks first
2. If open → fail immediately (no retry)
3. If closed → allow retry attempts
4. If retries exhausted → circuit breaker counts failure

#### 4. CircuitBreakerStateService

Tracks circuit states across the application:

```csharp
public class CircuitBreakerStateService
{
    void RecordStateChange(string circuitName, CircuitState state);
    CircuitState? GetState(string circuitName);
    Dictionary<string, CircuitState> GetAllStates();
    bool HasOpenCircuit();
}
```

#### 5. CircuitBreakerHealthCheck

Integrates with ASP.NET Core Health Checks:

- **Healthy**: All circuits closed
- **Degraded**: Some circuits half-open (testing recovery)
- **Unhealthy**: Any circuit open

## Configuration

### appsettings.json

```json
{
  "Resilience": {
    "Retry": {
      "MaxRetryAttempts": 3,
      "InitialDelaySeconds": 1.0,
      "MaxDelaySeconds": 30.0,
      "BackoffMultiplier": 2.0,
      "UseJitter": true
    },
    "CircuitBreaker": {
      "FailureThreshold": 5,
      "FailureRateThreshold": 0.5,
      "MinimumThroughput": 10,
      "BreakDurationSeconds": 30.0,
      "SamplingDurationSeconds": 60.0
    }
  }
}
```

### Configuration Recommendations

#### Production (Conservative)
```json
{
  "FailureThreshold": 5,
  "FailureRateThreshold": 0.5,
  "MinimumThroughput": 10,
  "BreakDurationSeconds": 60.0,
  "SamplingDurationSeconds": 60.0
}
```
- Higher thresholds
- Longer break duration
- Less sensitive to transient issues

#### Testing (Aggressive)
```json
{
  "FailureThreshold": 3,
  "FailureRateThreshold": 0.3,
  "MinimumThroughput": 5,
  "BreakDurationSeconds": 10.0,
  "SamplingDurationSeconds": 30.0
}
```
- Lower thresholds
- Shorter break duration
- Faster to trigger for testing

## Usage

### Database Operations

```csharp
public class MongoProcessRepository
{
    private readonly AsyncPolicyWrap _resiliencePolicy;

    public MongoProcessRepository(
        IMongoDatabase database,
        AsyncPolicyWrap resiliencePolicy)
    {
        _resiliencePolicy = resiliencePolicy;
    }

    public async Task CreateAsync(Process process)
    {
        await _resiliencePolicy.ExecuteAsync(async () =>
        {
            await _collection.InsertOneAsync(process);
        });
    }
}
```

### Message Broker Operations

```csharp
public class RabbitMqBroker
{
    private readonly AsyncPolicyWrap _resiliencePolicy;

    public async Task PublishAsync<T>(T message)
    {
        await _resiliencePolicy.ExecuteAsync(async () =>
        {
            using var channel = _connection.CreateModel();
            var body = SerializeMessage(message);
            channel.BasicPublish("exchange", "routing.key", null, body);
            await Task.CompletedTask;
        });
    }
}
```

### HTTP Operations

```csharp
public class ExternalApiClient
{
    private readonly HttpClient _httpClient;
    private readonly AsyncPolicyWrap<HttpResponseMessage> _resiliencePolicy;

    public async Task<ApiResponse> GetDataAsync()
    {
        var response = await _resiliencePolicy.ExecuteAsync(async () =>
        {
            return await _httpClient.GetAsync("/api/data");
        });

        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<ApiResponse>();
    }
}
```

## Monitoring

### Health Check Endpoint

```bash
GET /health
```

**Healthy Response**:
```json
{
  "status": "Healthy",
  "results": {
    "circuit-breakers": {
      "status": "Healthy",
      "description": "All circuit breakers closed",
      "data": {
        "database": "Closed",
        "broker": "Closed"
      }
    }
  }
}
```

**Unhealthy Response**:
```json
{
  "status": "Unhealthy",
  "results": {
    "circuit-breakers": {
      "status": "Unhealthy",
      "description": "Circuit breakers open: database",
      "data": {
        "database": "Open",
        "broker": "Closed"
      }
    }
  }
}
```

### Logging

Circuit breaker state changes are automatically logged:

```
[Error] Database circuit breaker opened: Exception=TimeoutException, BreakDuration=30s
[Warning] Database circuit breaker half-open: Testing recovery
[Information] Database circuit breaker reset: Circuit closed
```

### Key Metrics to Monitor

1. **Circuit State** (Closed/Open/Half-Open)
2. **Number of Open Circuits**
3. **Circuit Open Duration**
4. **Circuit Open Frequency**
5. **Failure Rate Before Opening**

### Alerting Strategy

- Circuit opened → Notify on-call engineer
- Circuit open > 5 minutes → Escalate to senior team
- Multiple circuits open → Declare major incident
- Circuit frequently opening → Investigate root cause

## Benefits

### 1. Prevents Cascading Failures

**Without Circuit Breaker**:
```
Service A → Service B (failing)
  ↓
Threads blocked waiting
  ↓
Service A becomes unresponsive
  ↓
Clients timeout
  ↓
Cascading failure
```

**With Circuit Breaker**:
```
Service A → Service B (failing)
  ↓
Circuit opens
  ↓
Service A fails fast
  ↓
Other features continue working
  ↓
System remains partially operational
```

### 2. Fast Failure

- **Circuit Open**: Fails in < 0.1ms (no downstream call)
- **Circuit Closed**: Normal latency + retry overhead
- Protects resources (connections, threads, memory)

### 3. Automatic Recovery

- Half-open state tests recovery automatically
- No manual intervention required
- Gradual return to normal operation

### 4. System Stability

- Isolates failures to specific subsystems
- Prevents thread pool exhaustion
- Maintains responsiveness for other operations

## Testing

### Unit Tests

See `tests/StarGate.Infrastructure.Tests/Resilience/CircuitBreakerTests.cs`:

- Circuit opening after threshold
- Circuit reset after break duration
- State transitions
- Fail-fast behavior
- State service tracking

### Integration Tests

```bash
# 1. Start infrastructure
docker-compose up -d

# 2. Monitor health
watch -n 1 curl -s http://localhost:5000/health | jq

# 3. Stop MongoDB to simulate failure
docker-compose stop mongodb

# 4. Trigger failures (create 20 processes)
for i in {1..20}; do
  curl -X POST http://localhost:5000/api/processes \
    -H "Content-Type: application/json" \
    -d '{"clientId": "test", "processType": "order"}'
  sleep 0.1
done

# 5. Verify circuit opens in logs
# Expected: "Database circuit breaker opened: BreakDuration=30s"

# 6. Verify health check shows unhealthy
curl http://localhost:5000/health
# Expected: Status=Unhealthy, "Circuit breakers open: database"

# 7. Verify subsequent requests fail immediately
# No retry delays observed

# 8. Wait for half-open state (30 seconds)
sleep 30

# 9. Restart MongoDB
docker-compose start mongodb

# 10. Verify circuit closes automatically
# Expected: "Database circuit breaker reset: Circuit closed"

# 11. Verify health check is healthy
curl http://localhost:5000/health
# Expected: Status=Healthy, "All circuit breakers closed"
```

## Performance Impact

### Circuit Closed (Normal)
- Overhead: < 1ms
- Memory: Minimal (state tracking)
- CPU: Negligible

### Circuit Open (Failing)
- Overhead: < 0.1ms (immediate failure)
- Memory: Constant (no queue buildup)
- CPU: Minimal (no downstream calls)
- **Benefit**: Prevents resource exhaustion

### Circuit Half-Open (Recovery)
- Overhead: Slightly higher (one test request)
- Worth the cost for automatic recovery

## Troubleshooting

### Circuit Frequently Opening

**Possible Causes**:
1. Infrastructure issues (MongoDB/RabbitMQ unstable)
2. Configuration too aggressive
3. Network problems
4. Insufficient resources

**Actions**:
1. Check infrastructure logs
2. Monitor resource utilization
3. Review recent deployments
4. Consider increasing thresholds

### Circuit Stuck Open

**Possible Causes**:
1. Service still failing in half-open tests
2. Break duration too short
3. Underlying issue not resolved

**Actions**:
1. Verify service health manually
2. Check service logs for errors
3. Increase break duration temporarily
4. Restart affected service

### Circuit Never Opens

**Possible Causes**:
1. Thresholds too high
2. Insufficient throughput
3. Failures not reaching threshold

**Actions**:
1. Review configuration values
2. Check failure logs
3. Verify policy is being used
4. Add telemetry for policy execution

## References

- [Circuit Breaker Pattern - Microsoft](https://docs.microsoft.com/en-us/azure/architecture/patterns/circuit-breaker)
- [Polly Circuit Breaker Documentation](https://github.com/App-vNext/Polly/wiki/Circuit-Breaker)
- [Advanced Circuit Breaker](https://github.com/App-vNext/Polly/wiki/Advanced-Circuit-Breaker)
- [Issue #108](https://github.com/artcava/StarGate/issues/108)
- [Issue #107 - Retry Policies](https://github.com/artcava/StarGate/issues/107)
