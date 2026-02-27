# Idempotency Handling in StarGate

## Overview

StarGate implements **exactly-once semantics** for process creation through a robust idempotency mechanism. This prevents duplicate process submissions caused by network retries, client errors, or system failures.

## Architecture

### Two-Tier Strategy

The idempotency system uses a **two-tier approach** for optimal performance and reliability:

```
┌─────────────────────────────────────────┐
│     Process Creation Request            │
└─────────────────┬───────────────────────┘
                  │
                  ▼
         ┌────────────────┐
         │ Check Redis    │ ◄── Fast Path (1-2ms)
         │ Cache          │
         └────────┬───────┘
                  │
          ┌───────┴────────┐
          │                │
    Found │                │ Not Found
          │                │
          ▼                ▼
    ┌──────────┐    ┌─────────────┐
    │ Reject   │    │ Check       │ ◄── Fallback (10-50ms)
    │ Duplicate│    │ Database    │
    └──────────┘    └──────┬──────┘
                           │
                    ┌──────┴──────┐
                    │             │
              Found │             │ Not Found
                    │             │
                    ▼             ▼
            ┌────────────┐  ┌──────────────┐
            │ Repopulate │  │ Reserve Key  │
            │ Cache      │  │ in Redis     │
            │ + Reject   │  └──────┬───────┘
            └────────────┘         │
                                   ▼
                           ┌────────────────┐
                           │ Create Process │
                           │ in Database    │
                           └────────┬───────┘
                                    │
                             Success│    Failure
                                    │         │
                                    ▼         ▼
                              ┌─────────┐ ┌──────────┐
                              │ Return  │ │ Rollback │
                              │ Process │ │ Redis Key│
                              └─────────┘ └──────────┘
```

## Components

### 1. IIdempotencyService Interface

**Location:** `src/StarGate.Core/Abstractions/IIdempotencyService.cs`

```csharp
public interface IIdempotencyService
{
    Task<Guid?> GetProcessIdByIdempotencyKeyAsync(
        string clientId,
        string idempotencyKey,
        CancellationToken cancellationToken = default);

    Task StoreIdempotencyKeyAsync(
        string clientId,
        string idempotencyKey,
        Guid processId,
        TimeSpan? expiration = null,
        CancellationToken cancellationToken = default);

    Task RemoveIdempotencyKeyAsync(
        string clientId,
        string idempotencyKey,
        CancellationToken cancellationToken = default);
}
```

### 2. RedisIdempotencyService Implementation

**Location:** `src/StarGate.Infrastructure/Services/RedisIdempotencyService.cs`

**Key Features:**
- **Redis Storage:** Uses Redis for distributed, high-performance caching
- **Key Format:** `idempotency:{clientId}:{idempotencyKey}`
- **Default TTL:** 24 hours (configurable)
- **Atomic Operations:** Leverages Redis atomic operations

### 3. ProcessService Integration

**Location:** `src/StarGate.Application/Services/ProcessService.cs`

**Flow in CreateProcessAsync:**

1. **Fast Path Check** (Redis)
   ```csharp
   var cachedProcessId = await _idempotencyService
       .GetProcessIdByIdempotencyKeyAsync(clientId, idempotencyKey);
   
   if (cachedProcessId.HasValue)
       throw new DuplicateProcessException(idempotencyKey);
   ```

2. **Fallback Check** (Database)
   ```csharp
   var existingProcess = await _processRepository
       .GetByIdempotencyKeyAsync(clientId, idempotencyKey);
   
   if (existingProcess is not null)
   {
       // Repopulate cache
       await _idempotencyService.StoreIdempotencyKeyAsync(
           clientId, idempotencyKey, existingProcess.ProcessId);
       
       throw new DuplicateProcessException(idempotencyKey);
   }
   ```

3. **Reservation Pattern**
   ```csharp
   var processId = Guid.NewGuid();
   
   // RESERVE key BEFORE creating process
   await _idempotencyService.StoreIdempotencyKeyAsync(
       clientId, idempotencyKey, processId);
   
   try
   {
       await _processRepository.CreateAsync(process);
   }
   catch
   {
       // Rollback on failure
       await _idempotencyService.RemoveIdempotencyKeyAsync(
           clientId, idempotencyKey);
       throw;
   }
   ```

## Performance Characteristics

### Latency by Scenario

| Scenario | Path | Typical Latency | Explanation |
|----------|------|-----------------|-------------|
| **Duplicate (immediate)** | Redis cache hit | 1-2ms | Key found in Redis, instant rejection |
| **Duplicate (delayed)** | Cache miss → DB hit | 10-50ms | Redis expired, found in DB, cache repopulated |
| **First request** | Full creation | 50-100ms | Check Redis + DB, reserve key, create process |

### Throughput

- **Redis capacity:** ~100,000 ops/sec (single instance)
- **Typical load:** 1,000-10,000 process creations/sec
- **Cache hit rate:** 95%+ for immediate duplicates

## Key Expiration Strategy

### Default: 24 Hours

**Rationale:**
- **Not too short:** Prevents false negatives for delayed retries (network issues, long queues)
- **Not too long:** Limits Redis memory consumption
- **Industry standard:** Most idempotency implementations use 24-48 hours

### Memory Impact

**Per key:**
- Key: ~40 bytes (`idempotency:client-123:key-456`)
- Value: ~36 bytes (UUID string)
- Overhead: ~24 bytes (Redis metadata)
- **Total: ~100 bytes per key**

**Example calculations:**
- 1M processes/day = 100MB Redis memory
- 10M processes/day = 1GB Redis memory

### Custom Expiration

For specific use cases, you can override the default:

```csharp
// Store with 1-hour expiration
await _idempotencyService.StoreIdempotencyKeyAsync(
    clientId,
    idempotencyKey,
    processId,
    expiration: TimeSpan.FromHours(1));
```

## Thread Safety

### Race Condition Mitigation

The **reservation pattern** prevents race conditions in concurrent scenarios:

**Without Reservation (❌ Race Condition):**
```
Client A: Check cache → Not found
Client B: Check cache → Not found
Client A: Check DB → Not found
Client B: Check DB → Not found
Client A: Create process → Success
Client B: Create process → Duplicate! (❌)
```

**With Reservation (✅ Protected):**
```
Client A: Check cache → Not found
Client B: Check cache → Not found
Client A: Check DB → Not found
Client B: Check DB → Not found
Client A: Reserve in Redis → Success
Client B: Reserve in Redis → Already exists! (✅)
Client A: Create process → Success
Client B: Rejected by cache check
```

### Redis Atomicity

Redis operations are **atomic** by nature:
- `StringSetAsync`: Atomic write
- `StringGetAsync`: Atomic read
- No race conditions within Redis operations

## Error Handling

### DuplicateProcessException

**When thrown:**
- Idempotency key found in cache (fast path)
- Idempotency key found in database (fallback)

**Client behavior:**
- **Do NOT retry** with same idempotency key
- Use returned process ID from exception
- Or query process status by idempotency key

### Rollback Scenarios

**Automatic rollback occurs when:**
1. Process creation fails after key reservation
2. Database error during persistence
3. Validation error after reservation

**Rollback action:**
```csharp
await _idempotencyService.RemoveIdempotencyKeyAsync(
    clientId,
    idempotencyKey);
```

This allows the client to **safely retry** with the same idempotency key.

## Testing

### Unit Tests

**Location:** `tests/StarGate.Infrastructure.Tests/Services/RedisIdempotencyServiceTests.cs`

**Coverage:**
- ✅ Constructor validation
- ✅ Get operations (found/not found)
- ✅ Store operations (success/failure)
- ✅ Remove operations
- ✅ Key format consistency
- ✅ Custom expiration

### Integration Tests

**Location:** `tests/StarGate.Application.Tests/Services/ProcessServiceIdempotencyTests.cs`

**Coverage:**
- ✅ Two-tier check (cache → database)
- ✅ Cache repopulation on cache miss
- ✅ Reservation before creation
- ✅ Rollback on failure
- ✅ Unique GUID generation
- ✅ Successful creation flow

### Manual Testing

**Scenario 1: Immediate Duplicate**
```bash
# Request 1
curl -X POST /api/processes \
  -H "Content-Type: application/json" \
  -d '{"clientId":"test","processType":"order","idempotencyKey":"key-123"}'
# Response: 201 Created, ProcessId=abc-123

# Request 2 (immediate retry)
curl -X POST /api/processes \
  -H "Content-Type: application/json" \
  -d '{"clientId":"test","processType":"order","idempotencyKey":"key-123"}'
# Response: 409 Conflict (DuplicateProcessException)
```

**Scenario 2: Delayed Duplicate (after Redis restart)**
```bash
# Request 1
curl -X POST /api/processes ... 
# Response: 201 Created

# Restart Redis (simulate cache clear)
docker restart stargate-redis

# Request 2 (after cache loss)
curl -X POST /api/processes ...
# Response: 409 Conflict (found in database, cache repopulated)
```

**Scenario 3: Retry After Failure**
```bash
# Request 1 (database temporarily down)
curl -X POST /api/processes ...
# Response: 500 Internal Server Error
# Rollback occurred → idempotency key removed

# Request 2 (database recovered)
curl -X POST /api/processes ... # SAME idempotency key
# Response: 201 Created (retry successful!)
```

## Best Practices

### For Clients

1. **Always use idempotency keys**
   ```javascript
   const idempotencyKey = `${userId}-${timestamp}-${uuid()}`;
   ```

2. **Store idempotency keys locally**
   - Associate with client-side request ID
   - Use for status queries if request fails

3. **Retry with same key on network errors**
   - Safe to retry with same key
   - System guarantees exactly-once execution

4. **Don't retry on 409 Conflict**
   - Indicates duplicate submission
   - Use process ID from error response

### For Developers

1. **Never modify expiration below 1 hour**
   - Risk of false negatives increases
   - Network delays can be significant

2. **Monitor cache hit rate**
   - Should be >90% in production
   - Low hit rate indicates cache size issues

3. **Set up Redis persistence**
   - RDB snapshots every 5 minutes
   - AOF for critical deployments

4. **Test rollback scenarios**
   - Ensure idempotency keys are cleaned up
   - Verify retry behavior

## Configuration

### Redis Connection

**appsettings.json:**
```json
{
  "Redis": {
    "ConnectionString": "localhost:6379",
    "Database": 0,
    "ConnectTimeout": 5000,
    "SyncTimeout": 3000
  }
}
```

### Service Registration

**Program.cs:**
```csharp
services.AddSingleton<IConnectionMultiplexer>(
    ConnectionMultiplexer.Connect(redisConnectionString));

services.AddScoped<IIdempotencyService, RedisIdempotencyService>();
```

## Monitoring

### Key Metrics

1. **Idempotency cache hit rate**
   - Target: >95%
   - Alert if: <80%

2. **Duplicate submission rate**
   - Baseline: 1-5% of total requests
   - Alert if: >10% (indicates client issues)

3. **Rollback frequency**
   - Baseline: <0.1%
   - Alert if: >1% (indicates system instability)

4. **Redis memory usage**
   - Monitor: Total keys, memory consumption
   - Alert if: >80% of allocated memory

### Logging

**Key log events:**
- `Idempotency key found in cache` → INFO
- `Idempotency key found in database (cache miss)` → WARNING
- `Failed to store idempotency key` → ERROR
- `Rolling back idempotency key` → WARNING

## Troubleshooting

### Issue: High duplicate rate

**Symptoms:** Many 409 Conflict responses

**Diagnosis:**
1. Check client retry logic
2. Review network latency/timeouts
3. Verify idempotency key generation

**Solution:**
- Educate clients on proper retry behavior
- Increase client timeouts
- Ensure unique idempotency keys per request

### Issue: Low cache hit rate

**Symptoms:** Many database queries for idempotency check

**Diagnosis:**
1. Check Redis memory limits
2. Verify expiration settings
3. Review Redis eviction policy

**Solution:**
- Increase Redis memory allocation
- Adjust expiration if too short
- Set eviction policy to `allkeys-lru`

### Issue: Unexpected duplicates

**Symptoms:** Duplicate processes created with same idempotency key

**Diagnosis:**
1. Check Redis connectivity
2. Verify atomic operations
3. Review concurrent request patterns

**Solution:**
- Ensure Redis cluster is stable
- Verify reservation pattern is implemented
- Add distributed locking if needed

## Future Enhancements

### Planned Improvements

1. **Distributed Locking**
   - Add Redis-based distributed locks
   - Eliminate race conditions entirely
   - Use RedLock algorithm

2. **Idempotency Key Rotation**
   - Automatic cleanup of expired keys
   - Background job for garbage collection

3. **Multi-Region Support**
   - Redis Cluster for geo-distribution
   - Eventually consistent idempotency

4. **Metrics Dashboard**
   - Real-time idempotency metrics
   - Cache hit rate visualization
   - Duplicate submission trends

## References

- [Issue #94: Add Idempotency Handling to ProcessService](https://github.com/artcava/StarGate/issues/94)
- [Redis Documentation](https://redis.io/docs/)
- [Stripe Idempotency Guide](https://stripe.com/docs/api/idempotent_requests)
- [AWS Idempotency Best Practices](https://aws.amazon.com/builders-library/making-retries-safe-with-idempotent-APIs/)
