# Redis Connection Pooling

## Overview

This document describes the Redis connection pooling implementation in StarGate, including configuration, monitoring, and troubleshooting.

## Architecture

### Connection Multiplexer Singleton

StackExchange.Redis uses a `ConnectionMultiplexer` that internally manages connection pooling through socket multiplexing:

- **Single TCP connection** handles multiple concurrent operations
- **Thread-safe** design allows safe concurrent access
- **Lazy initialization** with double-checked locking prevents race conditions
- **Automatic reconnection** with exponential backoff (1s to 30s)

### Components

#### RedisConnectionFactory

Factory class responsible for:

- Creating and configuring `IConnectionMultiplexer` instances
- Implementing singleton pattern with `GetOrCreateConnection`
- Registering connection event handlers for monitoring
- Configuring timeouts, retry policies, and resilience settings

**Key Configuration:**

```csharp
options.AbortOnConnectFail = false;      // Don't fail on startup
options.ConnectRetry = 5;                 // Retry 5 times
options.ConnectTimeout = 10000;           // 10 seconds
options.SyncTimeout = 5000;               // 5 seconds
options.AsyncTimeout = 10000;             // 10 seconds
options.KeepAlive = 60;                   // 60 seconds
options.ReconnectRetryPolicy = new ExponentialRetry(1000, 30000);
```

#### RedisHealthCheck

Implements `IHealthCheck` for Kubernetes readiness probes:

- Tests connection establishment
- Performs write operation with temporary key
- Validates read operation returns expected value
- Returns `Healthy`, `Degraded`, or `Unhealthy` status

**Health States:**

- **Healthy**: Connection active, read/write operations successful
- **Degraded**: Timeout or read/write mismatch (Redis slow but reachable)
- **Unhealthy**: Connection not established or connection errors

#### RedisConnectionDiagnostics

Provides connection introspection:

- `GetStatus()`: Returns current connection status
- `LogDiagnostics()`: Logs detailed endpoint information
- Useful for troubleshooting and monitoring

## Configuration

### appsettings.json

```json
{
  "Redis": {
    "ConnectionString": "localhost:6379,abortConnect=false,connectRetry=5,connectTimeout=10000",
    "DefaultTtlSeconds": 3600,
    "Enabled": true
  },
  "HealthChecks": {
    "Redis": {
      "Enabled": true,
      "FailureStatus": "Degraded",
      "Tags": ["cache", "redis"]
    }
  }
}
```

### Connection String Parameters

- `abortConnect=false`: Application starts even if Redis is down
- `connectRetry=5`: Number of connection attempts before failing
- `connectTimeout=10000`: Milliseconds to wait for initial connection

## Dependency Injection

Registration in `DependencyInjection.cs`:

```csharp
// Singleton Redis connection (connection pooling)
services.AddSingleton<IConnectionMultiplexer>(sp =>
{
    ILogger logger = sp.GetRequiredService<ILoggerFactory>()
        .CreateLogger("RedisConnectionFactory");
    return RedisConnectionFactory.GetOrCreateConnection(
        redisOptions.ConnectionString,
        logger);
});

// Health check registration
services.AddHealthChecks()
    .AddCheck<RedisHealthCheck>(
        name: "redis",
        failureStatus: HealthStatus.Degraded,
        tags: new[] { "cache", "redis", "ready" });

// Diagnostics registration
services.AddSingleton<RedisConnectionDiagnostics>();
```

## Health Check Endpoints

### Liveness Probe

```bash
curl http://localhost:5000/health
```

### Readiness Probe

```bash
curl http://localhost:5000/health/ready
```

**Example Response (Healthy):**

```json
{
  "status": "Healthy",
  "results": {
    "redis": {
      "status": "Healthy",
      "description": "Redis is responsive",
      "data": {
        "endpoints": "localhost:6379",
        "connected": true,
        "timestamp": "2026-02-13T10:30:00Z"
      }
    }
  }
}
```

**Example Response (Degraded):**

```json
{
  "status": "Degraded",
  "results": {
    "redis": {
      "status": "Degraded",
      "description": "Redis timeout",
      "exception": "RedisTimeoutException: Timeout performing GET"
    }
  }
}
```

## Testing

### Test Scenario 1: Normal Operation

```bash
# Start Redis
docker run -d -p 6379:6379 --name redis-test redis:7.0

# Run application
dotnet run --project src/StarGate.Api

# Check health
curl http://localhost:5000/health/ready

# Expected: Status=Healthy
```

### Test Scenario 2: Redis Down at Startup

```bash
# Ensure Redis is not running
docker stop redis-test

# Start application (should start successfully)
dotnet run --project src/StarGate.Api

# Check health
curl http://localhost:5000/health/ready

# Expected: Status=Unhealthy, application running

# Start Redis
docker start redis-test

# Wait 5-10 seconds for reconnection
sleep 10

# Check health again
curl http://localhost:5000/health/ready

# Expected: Status=Healthy (automatic reconnection)
```

### Test Scenario 3: Connection Lost During Operation

```bash
# Start with Redis running
docker run -d -p 6379:6379 --name redis-test redis:7.0
dotnet run --project src/StarGate.Api

# Stop Redis while application is running
docker stop redis-test

# Monitor logs for ConnectionFailed events
# Expected: "Redis connection failed: EndPoint=..."

# Check health
curl http://localhost:5000/health/ready

# Expected: Status=Unhealthy

# Restart Redis
docker start redis-test

# Monitor logs for ConnectionRestored events
# Expected: "Redis connection restored: EndPoint=..."
```

### Test Scenario 4: Connection Diagnostics

```csharp
// Inject RedisConnectionDiagnostics in a controller or service
public class DiagnosticsController : ControllerBase
{
    private readonly RedisConnectionDiagnostics _diagnostics;

    public DiagnosticsController(RedisConnectionDiagnostics diagnostics)
    {
        _diagnostics = diagnostics;
    }

    [HttpGet("/diagnostics/redis")]
    public IActionResult GetStatus()
    {
        var status = _diagnostics.GetStatus();
        return Ok(status);
    }
}
```

## Monitoring

### Connection Events

The factory registers handlers for these events:

1. **ConnectionFailed**: Logged as `Error` when connection drops
2. **ConnectionRestored**: Logged as `Information` when reconnected
3. **ErrorMessage**: Logged as `Error` for Redis protocol errors
4. **InternalError**: Logged as `Error` for library internal errors
5. **ConfigurationChanged**: Logged as `Information` when config updates

### Log Examples

```
info: RedisConnectionFactory[0]
      Creating Redis connection...
info: RedisConnectionFactory[0]
      Redis configuration: ConnectTimeout=10000ms, SyncTimeout=5000ms, KeepAlive=60s
info: RedisConnectionFactory[0]
      Redis connection established: localhost:6379, Status=Connected
```

```
error: RedisConnectionFactory[0]
       Redis connection failed: EndPoint=localhost:6379, FailureType=UnableToConnect, Exception=Connection refused
```

```
info: RedisConnectionFactory[0]
      Redis connection restored: EndPoint=localhost:6379, FailureType=UnableToConnect
```

## Troubleshooting

### Issue: Application fails to start with Redis unavailable

**Cause**: `AbortOnConnectFail` is set to `true`

**Solution**: Ensure connection string includes `abortConnect=false`

```json
"ConnectionString": "localhost:6379,abortConnect=false"
```

### Issue: Frequent reconnections

**Cause**: Network instability or Redis server restarting

**Solution**:

1. Check network connectivity between application and Redis
2. Verify Redis server logs for crashes or restarts
3. Consider increasing `KeepAlive` interval if network has high latency

### Issue: Timeout errors under load

**Cause**: `SyncTimeout` or `AsyncTimeout` too low for workload

**Solution**: Increase timeout values based on P95/P99 latencies:

```csharp
options.SyncTimeout = 10000;   // Increase from 5s to 10s
options.AsyncTimeout = 15000;  // Increase from 10s to 15s
```

### Issue: Health check always returns Unhealthy

**Possible Causes**:

1. Redis not running: Check `docker ps` or Redis service status
2. Firewall blocking port 6379: Verify with `telnet localhost 6379`
3. Incorrect connection string: Check `appsettings.json` configuration

**Debug Steps**:

```bash
# Test Redis connectivity
redis-cli -h localhost -p 6379 ping

# Check application logs
dotnet run --project src/StarGate.Api | grep Redis

# Test health endpoint with verbose output
curl -v http://localhost:5000/health/ready
```

## Best Practices

1. **Always use singleton registration** for `IConnectionMultiplexer`
2. **Don't create multiple multiplexers** - reuse the single instance
3. **Monitor connection events** in production for early issue detection
4. **Set appropriate timeouts** based on your workload characteristics
5. **Use health checks** for Kubernetes readiness/liveness probes
6. **Enable connection diagnostics** for troubleshooting in dev/staging
7. **Test failover scenarios** before production deployment

## Performance Characteristics

### Connection Pooling Benefits

- **Reduced latency**: No connection establishment overhead per operation
- **Resource efficiency**: Single TCP connection vs. connection per request
- **Automatic pipelining**: Multiple commands can be sent without waiting for responses
- **Thread safety**: No need for connection pooling libraries

### Expected Metrics

- **Connection time**: < 100ms on initial startup
- **Operation latency**: 1-5ms for local Redis, 10-50ms for remote
- **Reconnection time**: 1-30s depending on exponential backoff iteration
- **Memory overhead**: ~100KB per ConnectionMultiplexer instance

## References

- [StackExchange.Redis Documentation](https://stackexchange.github.io/StackExchange.Redis/)
- [ASP.NET Core Health Checks](https://learn.microsoft.com/en-us/aspnet/core/host-and-deploy/health-checks)
- [Redis Best Practices](https://redis.io/docs/manual/client-side-caching/)
- [Issue #26](https://github.com/artcava/StarGate/issues/26)
