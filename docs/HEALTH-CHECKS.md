# Health Checks Documentation

## Overview

StarGate API implements comprehensive health check endpoints to monitor application availability, dependencies status, and system health. These endpoints are essential for:

- **Orchestrators** (Kubernetes, Docker Swarm): Detecting unhealthy instances for automatic restart
- **Load Balancers**: Routing traffic only to ready instances
- **Monitoring Systems**: Tracking service health and triggering alerts
- **Operations Teams**: Debugging and troubleshooting issues

## Endpoints

### Liveness Probe - `/health/live`

**Purpose**: Indicates if the API process is alive and running.

**Behavior**:
- Returns `200 OK` if the application process is running
- Does not check dependencies
- Always responds quickly (< 100ms)
- Used by orchestrators to determine if the container needs to be restarted

**Response Example**:
```json
{
  "status": "Healthy",
  "timestamp": "2026-02-19T14:30:00Z"
}
```

**Usage**:
```bash
curl http://localhost:5000/health/live
```

**Kubernetes Configuration**:
```yaml
livenessProbe:
  httpGet:
    path: /health/live
    port: 80
  initialDelaySeconds: 10
  periodSeconds: 10
  timeoutSeconds: 1
  failureThreshold: 3
```

---

### Readiness Probe - `/health/ready`

**Purpose**: Indicates if the API is ready to accept traffic.

**Behavior**:
- Returns `200 OK` only if all critical dependencies are healthy
- Returns `503 Service Unavailable` if any critical dependency is down
- Checks: MongoDB, RabbitMQ, ProcessService, PolicyProvider
- Response time: 3-10 seconds (depends on dependency checks)
- Used by load balancers to route traffic

**Response Example (Healthy)**:
```json
{
  "status": "Healthy",
  "totalDuration": "00:00:00.0234567",
  "entries": {
    "mongodb": {
      "status": "Healthy",
      "duration": "00:00:00.0123456",
      "tags": ["db", "mongodb", "ready"]
    },
    "rabbitmq": {
      "status": "Healthy",
      "duration": "00:00:00.0089012",
      "tags": ["messagebroker", "rabbitmq", "ready"]
    },
    "process-service": {
      "status": "Healthy",
      "description": "ProcessService is operational",
      "duration": "00:00:00.0156789",
      "tags": ["service", "ready"]
    },
    "policy-provider": {
      "status": "Healthy",
      "description": "PolicyProvider is operational",
      "duration": "00:00:00.0098765",
      "tags": ["service", "ready"]
    }
  }
}
```

**Usage**:
```bash
curl http://localhost:5000/health/ready
```

**Kubernetes Configuration**:
```yaml
readinessProbe:
  httpGet:
    path: /health/ready
    port: 80
  initialDelaySeconds: 15
  periodSeconds: 10
  timeoutSeconds: 5
  failureThreshold: 3
```

---

### Detailed Health - `/health`

**Purpose**: Provides detailed status of all health checks for monitoring and debugging.

**Behavior**:
- Returns comprehensive status of all health checks
- Includes timing information, tags, and error details
- Returns `200 OK`, `503 Service Unavailable`, or `200 OK with Degraded status`
- Checks all dependencies including optional ones (Redis cache)

**Response Example**:
```json
{
  "status": "Healthy",
  "totalDuration": "00:00:00.0456789",
  "entries": {
    "mongodb": {
      "status": "Healthy",
      "duration": "00:00:00.0123456",
      "tags": ["db", "mongodb", "ready"]
    },
    "redis": {
      "status": "Degraded",
      "description": "Redis connection timeout",
      "duration": "00:00:03.0000000",
      "tags": ["cache", "redis", "ready"]
    },
    "rabbitmq": {
      "status": "Healthy",
      "duration": "00:00:00.0089012",
      "tags": ["messagebroker", "rabbitmq", "ready"]
    },
    "process-service": {
      "status": "Healthy",
      "description": "ProcessService is operational",
      "duration": "00:00:00.0156789",
      "tags": ["service", "ready"]
    },
    "policy-provider": {
      "status": "Healthy",
      "description": "PolicyProvider is operational",
      "duration": "00:00:00.0098765",
      "tags": ["service", "ready"]
    }
  }
}
```

**Usage**:
```bash
curl http://localhost:5000/health | jq
```

---

## Health Status Levels

### Healthy
- All checks passed successfully
- Service is fully operational
- HTTP Status: `200 OK`

### Degraded
- Non-critical checks failed (e.g., Redis cache)
- Service is operational but with reduced performance
- HTTP Status: `200 OK` (liveness/health) or `503` (readiness)

### Unhealthy
- Critical checks failed (e.g., MongoDB, RabbitMQ)
- Service cannot operate properly
- HTTP Status: `503 Service Unavailable`

---

## Dependency Classification

### Critical Dependencies (Unhealthy on failure)

**MongoDB**
- Primary data store
- Stores process state, results, and configuration
- Failure status: `Unhealthy`
- Timeout: 3 seconds

**RabbitMQ**
- Message broker for process orchestration
- Handles async process execution
- Failure status: `Unhealthy`
- Timeout: 3 seconds

**ProcessService**
- Core business service
- Manages process lifecycle
- Failure status: `Unhealthy`
- Timeout: 5 seconds

### Non-Critical Dependencies (Degraded on failure)

**Redis**
- Cache layer for performance optimization
- Application can function without it (slower)
- Failure status: `Degraded`
- Timeout: 3 seconds

**PolicyProvider**
- Policy configuration provider
- Has fallback to default policies
- Failure status: `Degraded`
- Timeout: 5 seconds

---

## Configuration

### Connection Strings

Configure in `appsettings.json`:

```json
{
  "ConnectionStrings": {
    "MongoDB": "mongodb://localhost:27017/stargate",
    "Redis": "localhost:6379",
    "RabbitMQ": "amqp://guest:guest@localhost:5672"
  }
}
```

### Health Check Logging

Configure logging level in `appsettings.json`:

```json
{
  "Logging": {
    "LogLevel": {
      "Microsoft.Extensions.Diagnostics.HealthChecks": "Information"
    }
  }
}
```

### Timeouts

| Check Type | Timeout | Rationale |
|------------|---------|----------|
| Database (MongoDB, Redis) | 3 seconds | Quick response expected for DB queries |
| Message Broker (RabbitMQ) | 3 seconds | Connection check should be fast |
| Services (ProcessService, PolicyProvider) | 5 seconds | May involve repository calls |
| Total Health Check | 10 seconds max | Prevent health check hanging |

---

## Testing

### Local Testing

```bash
# Start dependencies
docker-compose up -d mongodb redis rabbitmq

# Run API
dotnet run --project src/StarGate.Api

# Test liveness
curl http://localhost:5000/health/live

# Test readiness
curl http://localhost:5000/health/ready

# Test detailed health
curl http://localhost:5000/health | jq
```

### Simulate Unhealthy Dependencies

```bash
# Stop MongoDB
docker-compose stop mongodb

# Check readiness (should return 503)
curl -v http://localhost:5000/health/ready

# Check liveness (should still return 200)
curl http://localhost:5000/health/live

# Restart MongoDB
docker-compose start mongodb
```

### Unit Tests

```bash
# Run health check unit tests
dotnet test tests/StarGate.Api.Tests --filter "FullyQualifiedName~HealthCheck"
```

### Integration Tests

```bash
# Run health check integration tests
dotnet test tests/StarGate.Integration.Tests --filter "FullyQualifiedName~HealthCheck"
```

---

## Monitoring Integration

### Prometheus Metrics

Health checks can be scraped by Prometheus using the `/health` endpoint with appropriate formatters.

### Application Insights

Health check results are automatically logged and can be tracked in Application Insights.

### Custom Monitoring

Use the `/health` endpoint to build custom monitoring dashboards:

```bash
# Example: Check if service is healthy
if curl -f http://localhost:5000/health/ready > /dev/null 2>&1; then
  echo "Service is healthy"
else
  echo "Service is unhealthy"
  # Trigger alert
fi
```

---

## Troubleshooting

### Health Check Always Returns Unhealthy

1. Check connection strings in configuration
2. Verify dependencies are running: `docker-compose ps`
3. Check logs: `docker-compose logs api`
4. Test dependency connectivity manually

### Slow Health Check Response

1. Check network latency to dependencies
2. Verify database indexes are created
3. Review timeout configurations
4. Check for resource contention (CPU, memory)

### Health Check Intermittently Fails

1. Check for network issues
2. Review connection pool settings
3. Monitor resource usage (connections, memory)
4. Adjust timeout values if needed

---

## Security Considerations

### Anonymous Access

Health endpoints are configured with `.AllowAnonymous()` to enable:
- Orchestrator access without authentication
- Load balancer health checks
- External monitoring systems

### Information Disclosure

Health endpoints expose minimal information:
- Service names and status
- No sensitive data (passwords, keys, etc.)
- No internal implementation details

### Rate Limiting

Consider implementing rate limiting for health endpoints if exposed publicly to prevent abuse.

---

## References

- [ASP.NET Core Health Checks Documentation](https://learn.microsoft.com/en-us/aspnet/core/host-and-deploy/health-checks)
- [Kubernetes Probes](https://kubernetes.io/docs/tasks/configure-pod-container/configure-liveness-readiness-startup-probes/)
- [Azure Application Insights Health Monitoring](https://learn.microsoft.com/en-us/azure/azure-monitor/app/asp-net-core)
- [Issue #69: Add Health Check Endpoints](https://github.com/artcava/StarGate/issues/69)
