# Polly Retry Policies Implementation

## Overview

This document describes the Polly-based retry policy implementation for handling transient failures in infrastructure components (HTTP clients, database operations, message broker). This is **different** from the ProcessWorker retry logic documented in [RETRY-LOGIC.md](./RETRY-LOGIC.md).

## Two-Level Retry Strategy

StarGate implements a two-level retry strategy:

### Level 1: Infrastructure Retry (Polly) - **This Document**
- **Purpose**: Handle transient failures in external services (MongoDB, RabbitMQ, HTTP)
- **Scope**: Single operation (e.g., `InsertOneAsync`, `BasicPublish`)
- **Speed**: Fast (1s → 2s → 4s = 7s total)
- **Transparency**: Automatic and transparent to business logic
- **Location**: `StarGate.Infrastructure.Resilience`

### Level 2: Application Retry (ProcessWorker)
- **Purpose**: Retry entire failed process execution
- **Scope**: Complete process workflow
- **Speed**: Slower (5s → 10s → 20s = 35s+ total)
- **Visibility**: Changes process status to "Retrying"
- **Location**: `StarGate.Server.Workers`
- **Documentation**: [RETRY-LOGIC.md](./RETRY-LOGIC.md)

## Architecture

```
┌─────────────────────────────────────────────────────────┐
│ Application Layer (ProcessWorker)                       │
│ - Executes business logic                               │
│ - Catches unhandled exceptions                          │
│ - Implements process-level retry (5s → 10s → 20s)     │
└────────────────────┬────────────────────────────────────┘
                     │
                     ▼
┌─────────────────────────────────────────────────────────┐
│ Infrastructure Layer (Repositories, Brokers)            │
│ - MongoDB operations (MongoProcessRepository)           │
│ - RabbitMQ operations (RabbitMqBroker)                  │
│ - HTTP calls (External APIs)                            │
│ ┌─────────────────────────────────────────────────┐     │
│ │ Polly Retry Policies (Infrastructure Retry)    │     │
│ │ - Intercepts TimeoutException, IOException     │     │
│ │ - Retries automatically (1s → 2s → 4s)        │     │
│ │ - Logs retry attempts                          │     │
│ └─────────────────────────────────────────────────┘     │
└────────────────────┬────────────────────────────────────┘
                     │
                     ▼
┌─────────────────────────────────────────────────────────┐
│ External Services                                        │
│ - MongoDB                                                │
│ - RabbitMQ                                               │
│ - External HTTP APIs                                     │
└─────────────────────────────────────────────────────────┘
```

## Components

### 1. RetryPolicyConfiguration

**Location**: `src/StarGate.Infrastructure/Resilience/RetryPolicyConfiguration.cs`

```csharp
public class RetryPolicyConfiguration
{
    public int MaxRetryAttempts { get; set; } = 3;
    public double InitialDelaySeconds { get; set; } = 1.0;
    public double MaxDelaySeconds { get; set; } = 30.0;
    public double BackoffMultiplier { get; set; } = 2.0;
    public bool UseJitter { get; set; } = true;

    public TimeSpan CalculateDelay(int retryAttempt)
    {
        var exponentialDelay = InitialDelaySeconds * Math.Pow(BackoffMultiplier, retryAttempt - 1);
        var delay = Math.Min(exponentialDelay, MaxDelaySeconds);

        if (UseJitter)
        {
            var random = new Random();
            var jitter = delay * 0.2 * (random.NextDouble() - 0.5); // ±10%
            delay += jitter;
        }

        return TimeSpan.FromSeconds(Math.Max(delay, 0));
    }
}
```

### 2. RetryPolicyFactory

**Location**: `src/StarGate.Infrastructure/Resilience/RetryPolicyFactory.cs`

Provides static factory methods for creating specialized retry policies:

#### HTTP Retry Policy

```csharp
var policy = RetryPolicyFactory.CreateHttpRetryPolicy(config, logger);
```

**Handles**:
- `HttpRequestException`
- `TimeoutException`
- HTTP responses with non-success status codes

**Use Cases**:
- External API calls
- Webhook deliveries
- Service-to-service communication

#### Database Retry Policy

```csharp
var policy = RetryPolicyFactory.CreateDatabaseRetryPolicy(config, logger);
```

**Handles**:
- `TimeoutException`
- `IOException`
- `InvalidOperationException` containing "connection"

**Use Cases**:
- MongoDB operations
- Connection pool exhaustion
- Network interruptions

#### Broker Retry Policy

```csharp
var policy = RetryPolicyFactory.CreateBrokerRetryPolicy(config, logger);
```

**Handles**:
- `TimeoutException`
- `IOException`
- `InvalidOperationException` containing "connection"

**Use Cases**:
- RabbitMQ publishing
- Message consumption
- Channel creation

#### Generic Retry Policy

```csharp
var policy = RetryPolicyFactory.CreateGenericRetryPolicy(config, logger);
```

**Handles**: Any transient exception

**Use Cases**:
- General-purpose retry logic
- New integrations

### 3. ResilienceServiceCollectionExtensions

**Location**: `src/StarGate.Infrastructure/Extensions/ResilienceServiceCollectionExtensions.cs`

Provides extension methods for registering policies in dependency injection:

```csharp
// In Program.cs
builder.Services.AddResiliencePolicies(builder.Configuration);

// For HTTP clients
builder.Services.AddHttpClientWithRetry<IExternalApiClient>("external-api");
```

## Exponential Backoff Formula

The retry delay is calculated using exponential backoff with optional jitter:

```
Delay = InitialDelay × (Multiplier ^ (RetryAttempt - 1))
Delay = min(Delay, MaxDelay)

With Jitter:
Jitter = Delay × 0.2 × (Random - 0.5)  // ±10%
FinalDelay = Delay + Jitter
```

### Example Calculations

With default configuration (InitialDelay=1s, Multiplier=2.0, MaxDelay=30s):

| Retry | Formula | Base Delay | Jitter Range | Final Range |
|-------|---------|------------|--------------|-------------|
| 1st   | 1 × 2⁰  | 1.0s       | ±0.1s        | 0.9s - 1.1s |
| 2nd   | 1 × 2¹  | 2.0s       | ±0.2s        | 1.8s - 2.2s |
| 3rd   | 1 × 2²  | 4.0s       | ±0.4s        | 3.6s - 4.4s |
| 4th   | 1 × 2³  | 8.0s       | ±0.8s        | 7.2s - 8.8s |

**Total time for 3 retries**: ~7 seconds (1s + 2s + 4s)

### Comparison with ProcessWorker Retry

| Aspect | Polly Retry | ProcessWorker Retry |
|--------|-------------|---------------------|
| Initial Delay | 1s | 5s |
| Delay Range | 1s - 30s | 5s - 300s |
| Jitter | ±10% | ±30% |
| Total Time (3 retries) | ~7s | ~35s |
| Purpose | Transient failures | Process execution failures |

## Configuration

### appsettings.json (Production)

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

### appsettings.Development.json

```json
{
  "Resilience": {
    "Retry": {
      "MaxRetryAttempts": 2,
      "InitialDelaySeconds": 0.5,
      "MaxDelaySeconds": 10.0,
      "BackoffMultiplier": 2.0,
      "UseJitter": true
    }
  }
}
```

**Development Configuration Rationale**:
- Fewer retries (2 vs 3) for faster feedback
- Shorter delays (0.5s vs 1s) for quicker development cycles
- Lower max delay (10s vs 30s) to avoid long waits during debugging

## Usage Examples

### Applying Retry Policy to MongoDB Repository

```csharp
public class MongoProcessRepository : IProcessRepository
{
    private readonly IMongoCollection<ProcessDocument> _collection;
    private readonly AsyncRetryPolicy _retryPolicy;
    private readonly ILogger<MongoProcessRepository> _logger;

    public MongoProcessRepository(
        IMongoDatabase database,
        AsyncRetryPolicy retryPolicy,
        ILogger<MongoProcessRepository> logger)
    {
        _collection = database.GetCollection<ProcessDocument>("processes");
        _retryPolicy = retryPolicy ?? throw new ArgumentNullException(nameof(retryPolicy));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<Process> CreateAsync(Process process, CancellationToken ct = default)
    {
        return await _retryPolicy.ExecuteAsync(async () =>
        {
            var document = ProcessMapper.MapToDocument(process);
            await _collection.InsertOneAsync(document, cancellationToken: ct);
            
            _logger.LogDebug("Process created: ProcessId={ProcessId}", process.ProcessId);
            return process;
        });
    }

    public async Task<Process?> GetByIdAsync(Guid processId, CancellationToken ct = default)
    {
        return await _retryPolicy.ExecuteAsync(async () =>
        {
            var bsonGuid = new BsonBinaryData(processId, GuidRepresentation.Standard);
            var filter = Builders<ProcessDocument>.Filter.Eq("_id", bsonGuid);
            var document = await _collection.Find(filter).FirstOrDefaultAsync(ct);
            
            return document != null ? ProcessMapper.MapToDomain(document) : null;
        });
    }
}
```

### Applying Retry Policy to RabbitMQ Broker

```csharp
public class RabbitMqBroker
{
    private readonly IConnection _connection;
    private readonly AsyncRetryPolicy _retryPolicy;
    private readonly ILogger<RabbitMqBroker> _logger;

    public RabbitMqBroker(
        IConnection connection,
        AsyncRetryPolicy retryPolicy,
        ILogger<RabbitMqBroker> logger)
    {
        _connection = connection ?? throw new ArgumentNullException(nameof(connection));
        _retryPolicy = retryPolicy ?? throw new ArgumentNullException(nameof(retryPolicy));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task PublishAsync<T>(
        T message,
        string routingKey,
        CancellationToken ct = default) where T : class
    {
        await _retryPolicy.ExecuteAsync(async () =>
        {
            using var channel = _connection.CreateModel();
            
            var messageBody = SerializeMessage(message);
            var properties = channel.CreateBasicProperties();
            properties.Persistent = true;
            properties.ContentType = "application/json";
            properties.MessageId = Guid.NewGuid().ToString();

            channel.BasicPublish(
                exchange: "stargate.processes",
                routingKey: routingKey,
                basicProperties: properties,
                body: messageBody);

            _logger.LogDebug(
                "Message published: RoutingKey={RoutingKey}, MessageId={MessageId}",
                routingKey,
                properties.MessageId);

            await Task.CompletedTask;
        });
    }
}
```

### Registering Policies in DI Container

```csharp
// Program.cs
builder.Services.AddResiliencePolicies(builder.Configuration);
```

This automatically registers:
- `AsyncRetryPolicy` for database operations
- `AsyncRetryPolicy` for broker operations
- `RetryPolicyConfiguration` from appsettings.json

## Error Classification

### Transient Errors (Retryable)

Errors that indicate temporary issues that may resolve on retry:

- **Network Errors**: `HttpRequestException`, `IOException`
- **Timeout Errors**: `TimeoutException`
- **Connection Errors**: `InvalidOperationException` with "connection" in message
- **HTTP Status Codes**: 408, 429, 500, 502, 503, 504

### Permanent Errors (Non-Retryable)

Errors that indicate persistent issues that won't be fixed by retrying:

- **Validation Errors**: `ArgumentException`, `ArgumentNullException`
- **Authorization Errors**: `UnauthorizedException`, 401, 403
- **Not Found Errors**: 404
- **Bad Request Errors**: 400
- **Business Logic Errors**: `InvalidOperationException` (without "connection")

## Jitter Strategy

### Why Jitter?

**Without Jitter**:
```
100 failed requests at t=0
→ All retry at t=1s (thundering herd)
→ All retry at t=3s (1s+2s)
→ All retry at t=7s (1s+2s+4s)
→ Load spikes every time
```

**With Jitter (±10%)**:
```
100 failed requests at t=0
→ Retries distributed between 0.9s - 1.1s
→ Retries distributed between 2.7s - 3.3s
→ Retries distributed between 6.3s - 7.7s
→ Smooth load distribution
```

### Jitter Implementation

```csharp
if (UseJitter)
{
    var random = new Random();
    // Generate jitter between -10% and +10%
    var jitter = delay * 0.2 * (random.NextDouble() - 0.5);
    delay += jitter;
}
```

**Range**: ±10% (smaller than ProcessWorker's ±30%)

**Rationale**: Infrastructure retries happen more frequently and need tighter coordination.

## Testing

### Unit Tests

Run retry policy unit tests:

```bash
dotnet test tests/StarGate.Infrastructure.Tests \
  --filter "FullyQualifiedName~Resilience"
```

Test coverage includes:
- Exponential backoff calculation
- Max delay enforcement
- Jitter randomization
- Retry count accuracy
- Eventual success scenarios
- Non-retryable exceptions

### Integration Tests

#### Test MongoDB Retry

```bash
# 1. Start MongoDB
docker-compose up -d mongodb

# 2. Create a process (should succeed)
curl -X POST http://localhost:5000/api/processes \
  -H "Content-Type: application/json" \
  -d '{
    "clientId": "test-client",
    "processType": "test-type",
    "clientProcessId": "test-001"
  }'

# 3. Stop MongoDB to simulate failure
docker-compose stop mongodb

# 4. Try to create another process (should retry then fail)
curl -X POST http://localhost:5000/api/processes \
  -H "Content-Type: application/json" \
  -d '{
    "clientId": "test-client",
    "processType": "test-type",
    "clientProcessId": "test-002"
  }'

# 5. Check logs for retry attempts
docker logs stargate-server | grep "Database retry attempt"

# Expected output:
# Database retry attempt 1/3: Exception=TimeoutException, Delay=1000ms
# Database retry attempt 2/3: Exception=TimeoutException, Delay=2000ms
# Database retry attempt 3/3: Exception=TimeoutException, Delay=4000ms

# 6. Restart MongoDB
docker-compose start mongodb

# 7. Verify requests succeed again
```

#### Test RabbitMQ Retry

```bash
# 1. Stop RabbitMQ during process creation
docker-compose stop rabbitmq

# 2. Create process (should retry broker operations)
curl -X POST http://localhost:5000/api/processes \
  -H "Content-Type: application/json" \
  -d '{
    "clientId": "test-client",
    "processType": "test-type",
    "clientProcessId": "test-003"
  }'

# 3. Check logs for broker retry attempts
docker logs stargate-server | grep "Broker retry attempt"
```

#### Test Jitter Randomization

```bash
# Create 10 processes simultaneously
for i in {1..10}; do
  curl -X POST http://localhost:5000/api/processes \
    -H "Content-Type: application/json" \
    -d "{\"clientId\":\"test-client\",\"processType\":\"test-type\",\"clientProcessId\":\"test-$i\"}" &
done

# Verify retry delays vary (not all exactly 1s, 2s, 4s)
```

## Monitoring and Observability

### Log Events

Polly retry policies produce structured logs:

```csharp
// HTTP retry
logger.LogWarning(
    "HTTP retry attempt {RetryAttempt}/{MaxRetries}: StatusCode={StatusCode}, Exception={Exception}, Delay={Delay}ms",
    retryAttempt, maxRetries, statusCode, exception, delay);

// Database retry
logger.LogWarning(
    exception,
    "Database retry attempt {RetryAttempt}/{MaxRetries}: Exception={Exception}, Delay={Delay}ms",
    retryAttempt, maxRetries, exceptionType, delay);

// Broker retry
logger.LogWarning(
    exception,
    "Broker retry attempt {RetryAttempt}/{MaxRetries}: Exception={Exception}, Delay={Delay}ms",
    retryAttempt, maxRetries, exceptionType, delay);
```

### Metrics to Monitor

#### Infrastructure Retry Metrics

- **Retry Rate**: Percentage of operations requiring retry
- **Retry Count Distribution**: How many retries before success
- **Retry Success Rate**: Operations that succeed after retry
- **Retry Failure Rate**: Operations that fail after all retries

#### Performance Metrics

- **P50 Latency**: Median operation time (should be ~base time)
- **P95 Latency**: 95th percentile (may include 1-2 retries)
- **P99 Latency**: 99th percentile (may include all 3 retries)

#### Health Indicators

- **High Retry Rate** (>10%): Infrastructure issues
- **Increasing Retry Failures**: Persistent outages
- **Jitter Distribution**: Should be evenly distributed

### Example Log Queries

```bash
# Find all retry attempts in last hour
grep "retry attempt" /var/log/stargate/*.log | tail -100

# Count retries by exception type
grep "retry attempt" /var/log/stargate/*.log | \
  grep -oP "Exception=\K[^,]+" | sort | uniq -c

# Calculate average retry count
grep "retry attempt" /var/log/stargate/*.log | \
  grep -oP "RetryAttempt=\K\d+" | \
  awk '{sum+=$1; count++} END {print "Average:", sum/count}'
```

## Performance Considerations

### Success Case

- **Overhead**: <1ms (policy check is fast)
- **Memory**: Negligible (policy is singleton)
- **Throughput**: No impact on successful operations

### Failure Case

- **Additional Latency**: Up to 7 seconds (1s + 2s + 4s)
- **Memory**: Minimal (no state stored between retries)
- **Throughput**: Reduces during outages (expected behavior)

### Comparison

| Scenario | Without Polly | With Polly |
|----------|---------------|------------|
| Success | ~50ms | ~51ms |
| 1 Transient Failure | Immediate failure | +1s → Success |
| 2 Transient Failures | Immediate failure | +3s → Success |
| 3 Transient Failures | Immediate failure | +7s → Success |
| Permanent Failure | Immediate failure | +7s → Failure |

**Trade-off**: Slight increase in failure latency vs. significantly higher success rate.

## Troubleshooting

### Problem: Operations Still Failing After Retries

**Possible Causes**:
1. Persistent infrastructure outage
2. MaxRetryAttempts too low
3. Network issues

**Solutions**:
- Check infrastructure status (MongoDB, RabbitMQ)
- Increase `MaxRetryAttempts` temporarily
- Verify network connectivity
- Review exception logs for non-transient errors

### Problem: Retry Delays Too Short/Long

**Possible Causes**:
1. Incorrect configuration in appsettings.json
2. Jitter causing unexpected variance

**Solutions**:
- Review `Resilience:Retry` settings
- Disable jitter temporarily: `"UseJitter": false`
- Monitor actual delay times in logs
- Adjust `InitialDelaySeconds` or `BackoffMultiplier`

### Problem: High Retry Rate

**Symptoms**:
- >10% of operations require retry
- Logs flooded with retry warnings

**Solutions**:
- Investigate infrastructure stability
- Check network latency
- Review timeout configurations
- Consider infrastructure scaling

### Problem: Thundering Herd Despite Jitter

**Symptoms**:
- Load spikes at regular intervals
- Multiple operations retrying simultaneously

**Solutions**:
- Verify `UseJitter` is enabled
- Increase jitter range in code (modify CalculateDelay)
- Stagger initial operation times
- Implement circuit breaker (future enhancement)

## Future Enhancements

### Planned Improvements

1. **Circuit Breaker Integration** (Issue #108)
   - Stop retries during known outages
   - Fail fast when service is down
   - Automatic recovery detection

2. **Adaptive Backoff**
   - Adjust multiplier based on system load
   - Faster retries during low load
   - Slower retries during high load

3. **Per-Operation Configuration**
   - Different retry strategies per operation
   - Critical operations: more retries
   - Non-critical operations: fewer retries

4. **Metrics Dashboard**
   - Real-time retry statistics
   - Success/failure rates
   - Latency distributions

5. **Retry Budget**
   - Limit total retry attempts across all operations
   - Prevent retry storms
   - Preserve system resources

## References

- [Polly Documentation](https://github.com/App-vNext/Polly)
- [Exponential Backoff Pattern](https://en.wikipedia.org/wiki/Exponential_backoff)
- [Transient Fault Handling (Microsoft)](https://docs.microsoft.com/en-us/azure/architecture/best-practices/transient-faults)
- [Retry Pattern (Cloud Design Patterns)](https://docs.microsoft.com/en-us/azure/architecture/patterns/retry)
- [Issue #107](https://github.com/artcava/StarGate/issues/107)
- [RETRY-LOGIC.md](./RETRY-LOGIC.md) (ProcessWorker Retry)
- [CODING-CONVENTIONS.md](./CODING-CONVENTIONS.md)
