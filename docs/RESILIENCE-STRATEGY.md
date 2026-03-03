# Resilience Strategy

## Overview

StarGate implements comprehensive resilience patterns using Polly to handle failures gracefully and prevent cascading failures in distributed systems. The resilience framework combines three complementary patterns: **Timeout**, **Circuit Breaker**, and **Retry**.

## Policies Implemented

### 1. Timeout Policy

**Purpose:** Prevent indefinite waiting on slow operations.

**Strategy:**
- **Pessimistic (Default):** Actively cancels operations via CancellationToken
- **Optimistic:** Monitors duration without canceling (use only when cancellation not possible)

**Timeout Values:**
- **HTTP:** 30 seconds - External API calls with network latency
- **Database:** 10 seconds - Local network, queries should be fast
- **Broker:** 5 seconds - Local network, should be very fast

**Configuration:**
```json
{
  "Resilience": {
    "Timeout": {
      "HttpTimeoutSeconds": 30.0,
      "DatabaseTimeoutSeconds": 10.0,
      "BrokerTimeoutSeconds": 5.0,
      "UsePessimisticTimeout": true
    }
  }
}
```

### 2. Retry Policy

**Purpose:** Handle transient failures through automatic retry with exponential backoff.

**Strategy:** Exponential backoff with jitter to prevent thundering herd.

**Configuration:**
- **Max Attempts:** 3
- **Initial Delay:** 1 second
- **Backoff Multiplier:** 2.0
- **Delays:** 1s → 2s → 4s (+/- 10% jitter)

**Retryable Failures:**
- TimeoutException
- HttpRequestException
- IOException
- Connection errors

**Non-Retryable Failures:**
- Validation errors (InvalidOperationException, ArgumentException)
- Authorization errors (UnauthorizedException)
- HTTP 4xx errors (except 408, 429)

```json
{
  "Resilience": {
    "Retry": {
      "MaxRetryAttempts": 3,
      "InitialDelaySeconds": 1.0,
      "MaxDelaySeconds": 30.0,
      "BackoffMultiplier": 2.0,
      "UseJitter": true
    }
  }
}
```

### 3. Circuit Breaker

**Purpose:** Prevent cascading failures by failing fast when services are unhealthy.

**Strategy:** Advanced circuit breaker with failure rate threshold.

**Configuration:**
- **Failure Rate Threshold:** 50% - Opens when failure rate exceeds this
- **Minimum Throughput:** 10 requests - Minimum requests before considering failure rate
- **Break Duration:** 30 seconds - Time circuit stays open before testing recovery
- **Sampling Duration:** 60 seconds - Window for failure rate calculation

**Circuit States:**
- **Closed:** Normal operation, requests pass through
- **Open:** All requests fail immediately, no downstream calls
- **Half-Open:** Testing recovery with one request

```json
{
  "Resilience": {
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

## Policy Combination

Policies are wrapped in a specific order to ensure optimal behavior:

```
Timeout (outer) → Ensures total operation time is bounded
  ↓
Circuit Breaker → Prevents retries when service is down
  ↓
Retry (inner) → Handles transient failures with backoff
  ↓
Operation → Actual work
```

### Why This Order?

1. **Timeout Outermost:** Guarantees total operation time including all retries is bounded
2. **Circuit Breaker Middle:** Prevents wasted retry attempts when service is known to be down
3. **Retry Innermost:** Each retry attempt respects circuit state and overall timeout

### Example Flow

**Scenario 1: Transient Failure**
```
1. Request enters timeout policy (starts 30s timer)
2. Passes through circuit breaker (closed)
3. Enters retry policy
4. Operation fails (TimeoutException)
5. Retry waits 1s and tries again
6. Operation succeeds
7. Returns success within timeout
```

**Scenario 2: Service Down**
```
1. Multiple requests fail
2. Circuit breaker tracks 50% failure rate
3. Circuit opens after minimum throughput reached
4. New requests fail immediately at circuit breaker
5. No retries attempted (saves resources)
6. After 30s, circuit enters half-open
7. One test request allowed
8. If succeeds, circuit closes
```

**Scenario 3: Slow Operation**
```
1. Request enters timeout policy (starts 10s timer for database)
2. Passes through circuit breaker (closed)
3. Enters retry policy
4. Operation takes 5s (slow but within timeout)
5. Retry attempts another operation
6. Second operation also slow (5s)
7. Timeout policy triggers at 10s total
8. Operation canceled, TimeoutRejectedException thrown
```

## Configuration

All resilience policies are configured in `appsettings.json` under the `Resilience` section:

```json
{
  "Resilience": {
    "Timeout": {
      "HttpTimeoutSeconds": 30.0,
      "DatabaseTimeoutSeconds": 10.0,
      "BrokerTimeoutSeconds": 5.0,
      "UsePessimisticTimeout": true
    },
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

### Environment-Specific Configuration

**Development:** Faster feedback, shorter timeouts
```json
{
  "Resilience": {
    "Timeout": {
      "DatabaseTimeoutSeconds": 5.0
    },
    "Retry": {
      "MaxRetryAttempts": 2,
      "InitialDelaySeconds": 0.5
    },
    "CircuitBreaker": {
      "BreakDurationSeconds": 10.0
    }
  }
}
```

**Production:** More resilient, longer timeouts
```json
{
  "Resilience": {
    "Timeout": {
      "DatabaseTimeoutSeconds": 10.0
    },
    "Retry": {
      "MaxRetryAttempts": 3,
      "InitialDelaySeconds": 1.0
    },
    "CircuitBreaker": {
      "BreakDurationSeconds": 30.0
    }
  }
}
```

## Usage

### Database Operations

```csharp
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
```

### Message Broker Operations

```csharp
private readonly AsyncPolicyWrap _resiliencePolicy;

public async Task PublishAsync<T>(T message)
{
    await _resiliencePolicy.ExecuteAsync(async () =>
    {
        // Publish message
    });
}
```

### HTTP Client Operations

```csharp
private readonly AsyncPolicyWrap<HttpResponseMessage> _httpPolicy;

public async Task<HttpResponseMessage> GetAsync(string url)
{
    return await _httpPolicy.ExecuteAsync(async () =>
    {
        return await _httpClient.GetAsync(url);
    });
}
```

## Monitoring

Resilience policies emit structured logs for monitoring:

### Timeout Events
```
HTTP operation timed out: Timeout=30s, Strategy=Pessimistic
Database operation timed out: Timeout=10s, Strategy=Pessimistic
Broker operation timed out: Timeout=5s, Strategy=Pessimistic
```

### Retry Events
```
Database retry attempt 1/3: Exception=TimeoutException, Delay=1000ms
Database retry attempt 2/3: Exception=TimeoutException, Delay=2000ms
Database retry attempt 3/3: Exception=TimeoutException, Delay=4000ms
```

### Circuit Breaker Events
```
Database circuit breaker opened: BreakDuration=30s
Database circuit breaker half-open: Testing recovery
Database circuit breaker reset: Circuit closed
```

### Health Endpoint

Check resilience status via health endpoint:

```bash
curl http://localhost:5000/health | jq
```

**Response:**
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

**Unhealthy State:**
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

## Performance Impact

### Success Case Overhead

- **Retry Policy:** ~0.5ms (state tracking)
- **Circuit Breaker:** ~0.3ms (state check)
- **Timeout Policy:** ~0.2ms (timer setup)
- **Total Overhead:** ~1ms (acceptable)

### Failure Case Impact

- **Retry:** +7s total (1s + 2s + 4s delays)
- **Circuit Breaker:** Fail immediately when open (~0.1ms)
- **Timeout:** Fail at timeout threshold

**Trade-off:** Small overhead in success case for significant resilience in failure cases.

## Testing

The resilience framework is validated through comprehensive testing:

### Unit Tests
- Policy configuration validation
- Timeout calculation correctness
- Retry backoff logic
- Circuit breaker state transitions

### Integration Tests
- Retry on transient failures
- Circuit breaker opening after threshold
- Timeout on slow operations
- Combined policy interaction

### Chaos Tests
- Database intermittent failures (30% failure rate)
- Database prolonged outages
- Broker slow responses
- Network partitions
- High load scenarios

### Performance Tests
- Measure overhead of each policy
- Benchmark complete policy stack
- Compare with/without policies

**Run Tests:**
```bash
# Unit tests
dotnet test tests/StarGate.Infrastructure.Tests --filter "FullyQualifiedName~Resilience"

# Integration tests
dotnet test tests/StarGate.IntegrationTests --filter "FullyQualifiedName~Resilience"

# Chaos tests
dotnet test tests/StarGate.IntegrationTests --filter "FullyQualifiedName~Chaos"

# Performance tests
cd tests/StarGate.PerformanceTests
dotnet run -c Release
```

## Best Practices

### 1. Always Use Complete Policy Stack

Use all three policies together for maximum resilience:

```csharp
var policy = ResiliencePolicyWrapper.CreateCompleteDatabaseResiliencePolicy(
    timeoutConfig, retryConfig, circuitConfig, logger);
```

### 2. Respect CancellationTokens

Ensure operations support cancellation for pessimistic timeouts:

```csharp
await policy.ExecuteAsync(async (ct) =>
{
    await operation(ct); // Pass cancellation token
});
```

### 3. Configure Per Environment

Adjust thresholds based on environment characteristics:
- Development: Fast feedback
- Staging: Production-like
- Production: Conservative, resilient

### 4. Monitor Circuit States

Set up alerts for circuit breaker state changes:
- Circuit opened → Investigate service health
- Circuit frequently opening → Adjust thresholds or fix service

### 5. Log Structured Data

Use structured logging for easy querying:

```csharp
logger.LogWarning(
    "Retry attempt {Attempt}/{Max}: {Exception}",
    attemptNumber, maxAttempts, exception.GetType().Name);
```

## Troubleshooting

### Timeouts Occurring Too Frequently

**Symptoms:** Many timeout logs, operations failing

**Solutions:**
- Increase timeout values in configuration
- Optimize slow operations (queries, external calls)
- Check network latency
- Review operation performance

### Circuit Breaker Opening Often

**Symptoms:** BrokenCircuitException, circuit open logs

**Solutions:**
- Investigate downstream service health
- Check if failure rate threshold too aggressive
- Increase minimum throughput requirement
- Review retry configuration (may be masking issues)

### High Retry Rates

**Symptoms:** Many retry attempt logs

**Solutions:**
- Investigate root cause of transient failures
- Check infrastructure health (database, broker, network)
- May indicate systemic issues, not transient failures
- Consider if retries are appropriate for the failure type

## References

- [Polly Documentation](https://github.com/App-vNext/Polly)
- [Circuit Breaker Pattern](https://docs.microsoft.com/en-us/azure/architecture/patterns/circuit-breaker)
- [Retry Pattern](https://docs.microsoft.com/en-us/azure/architecture/patterns/retry)
- [Timeout Pattern](https://github.com/App-vNext/Polly/wiki/Timeout)
- [Resilience Testing](https://docs.microsoft.com/en-us/azure/architecture/framework/resiliency/testing)
