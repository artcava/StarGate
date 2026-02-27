# Rate Limiting

## Overview

StarGate API implements rate limiting to protect against abuse, ensure fair resource usage, and prevent DoS attacks. Rate limits are applied per client based on JWT claims and configured policies.

**Note**: Rate limiting functionality is built into ASP.NET Core 8.0 and doesn't require a separate NuGet package. The `Microsoft.AspNetCore.RateLimiting` namespace and `System.Threading.RateLimiting` types are part of the framework.

## Architecture

### Algorithms

The system supports two rate limiting algorithms:

#### Fixed Window
- Simple counter that resets at fixed intervals
- Example: 100 requests per minute, counter resets at :00
- Potential for burst at window boundaries
- More predictable but less smooth

#### Sliding Window (Default)
- Granular tracking with overlapping windows
- Smoother rate limiting without boundary bursts
- Recommended for production
- Divides time window into 10 segments for accurate tracking

### Client Identification

Rate limits are partitioned by client using the following priority:

1. **JWT `client_id` claim** (preferred for authenticated clients)
2. **IP address** (fallback for anonymous requests)
3. **"anonymous" literal** (if neither available)

Each authenticated client has an independent rate limit counter, while anonymous requests share a single counter per IP address.

## Configuration

### appsettings.json

```json
{
  "RateLimit": {
    "Enabled": true,
    "DefaultPolicy": {
      "PermitLimit": 1000,
      "WindowSeconds": 60,
      "QueueLimit": 0,
      "UseSlidingWindow": true
    },
    "EndpointPolicies": {
      "CreateProcess": {
        "PermitLimit": 100,
        "WindowSeconds": 60,
        "QueueLimit": 0,
        "UseSlidingWindow": true
      },
      "ReadProcess": {
        "PermitLimit": 500,
        "WindowSeconds": 60,
        "QueueLimit": 0,
        "UseSlidingWindow": true
      }
    }
  }
}
```

### Configuration Options

#### RateLimitOptions

- **Enabled**: Enable or disable rate limiting globally (default: `true`)
- **DefaultPolicy**: Rate limit policy applied to all endpoints without specific policy
- **EndpointPolicies**: Dictionary of named policies for specific endpoints

#### RateLimitPolicy

- **PermitLimit**: Maximum number of requests allowed in the window
- **WindowSeconds**: Time window duration in seconds
- **QueueLimit**: Number of requests that can queue when limit reached (0 = reject immediately)
- **UseSlidingWindow**: Use sliding window (true) or fixed window (false)

### Policy Levels

1. **Default Policy** (1000 req/min)
   - Applied to all endpoints without specific policy
   - Balanced limit for general API usage

2. **CreateProcess Policy** (100 req/min)
   - More restrictive for write operations
   - Prevents abuse of process creation
   - Protects backend resources

3. **ReadProcess Policy** (500 req/min)
   - Less restrictive for read operations
   - Allows higher query rates
   - Supports read-heavy workloads

## HTTP 429 Response

When a client exceeds the rate limit, the API returns:

**Status Code**: `429 Too Many Requests`

**Headers**:
```
Retry-After: 30
```

**Body**:
```json
{
  "type": "https://tools.ietf.org/html/rfc6585#section-4",
  "title": "Too Many Requests",
  "status": 429,
  "detail": "Rate limit exceeded. Please try again later.",
  "retryAfter": 30
}
```

## Implementation Details

### Framework Integration

Rate limiting in .NET 8.0 uses:
- **Microsoft.AspNetCore.RateLimiting** namespace (built into framework)
- **System.Threading.RateLimiting** for limiter implementations
- No additional NuGet packages required

### Extension Methods

The implementation provides:
- `AddApiRateLimiting()` - Registers rate limiting services
- `RequireRateLimiting()` - Applies policy to endpoints
- Custom `OnRejected` handler for 429 responses

## Testing

### Running Tests

```bash
# Run all rate limiting tests
dotnet test tests/StarGate.Api.Tests --filter "FullyQualifiedName~RateLimiting"

# Run specific test
dotnet test tests/StarGate.Api.Tests --filter "FullyQualifiedName~Endpoint_Should_Return429_WhenRateLimitExceeded"
```

### Manual Testing

```bash
# Start API
dotnet run --project src/StarGate.Api

# Test rate limiting with curl (bash)
for i in {1..150}; do
  curl -w "\nStatus: %{http_code}\n" \
    -H "Authorization: Bearer {token}" \
    http://localhost:5000/api/processes
done

# Expected: First 100 requests return 200/201, subsequent return 429

# Check Retry-After header
curl -i -H "Authorization: Bearer {token}" \
  http://localhost:5000/api/processes

# Look for:
# HTTP/1.1 429 Too Many Requests
# Retry-After: 30
```

### Testing with PowerShell

```powershell
# Test rate limiting
1..150 | ForEach-Object {
    $response = Invoke-WebRequest -Uri "http://localhost:5000/api/processes" `
        -Headers @{"Authorization"="Bearer {token}"} `
        -SkipHttpErrorCheck
    Write-Host "Request $_ : $($response.StatusCode)"
}
```

## Monitoring

### Logging

Rate limit violations are logged at `Warning` level:

```
Rate limit exceeded for client {ClientId} on endpoint {Endpoint}
```

### Recommended Metrics

Monitor the following metrics for capacity planning:

1. **Rate limit hits per client**
   - Identify abusive or misconfigured clients
   - Detect potential attack patterns

2. **Rate limit hits per endpoint**
   - Understand which endpoints are most constrained
   - Adjust limits based on usage patterns

3. **Memory usage**
   - Each client partition maintains counter in memory
   - Monitor for high-traffic scenarios with many unique clients

4. **429 response rate**
   - High rate may indicate limits are too restrictive
   - Spike may indicate DDoS attempt

## Performance Considerations

### Memory Usage

- Each client partition maintains a counter in memory
- Counters are automatically cleaned up when window expires
- For high-traffic scenarios with many unique clients, monitor memory usage
- Consider distributed rate limiting (Redis) for multi-instance deployments

### Queue Limits

- Default `QueueLimit: 0` rejects immediately when limit reached
- Appropriate for preventing DoS attacks
- Can be increased to handle legitimate burst traffic
- Higher queue limits increase memory usage

### Middleware Ordering

Rate limiting is positioned **before authentication** in the middleware pipeline:

```
ExceptionHandling → HTTPS → RateLimiting → Authentication → Authorization
```

This ensures:
- Protection before expensive JWT validation
- Access to authenticated user info for partitioning
- Early rejection of excess requests

## Troubleshooting

### Rate Limiting Not Working

1. **Check configuration**
   ```bash
   # Verify RateLimit:Enabled is true
   dotnet user-secrets list --project src/StarGate.Api
   ```

2. **Verify middleware registration**
   - Ensure `AddApiRateLimiting()` is called in `Program.cs`
   - Ensure `UseRateLimiter()` is called before `UseAuthentication()`

3. **Check logs**
   ```bash
   # Look for rate limiting warnings
   grep "Rate limit exceeded" logs/stargate.log
   ```

### Rate Limiting Too Aggressive

1. **Adjust limits in appsettings.json**
   ```json
   {
     "RateLimit": {
       "DefaultPolicy": {
         "PermitLimit": 2000,  // Increase limit
         "WindowSeconds": 60
       }
     }
   }
   ```

2. **Switch to per-endpoint policies**
   - Apply restrictive limits only to sensitive endpoints
   - Allow higher limits for read operations

### Clients Sharing Rate Limits

1. **Verify JWT configuration**
   - Ensure `client_id` claim is present in JWT tokens
   - Check `ClaimsExtensions.GetClientId()` logic

2. **Check authentication**
   - Anonymous requests share limit per IP
   - Ensure clients are authenticating properly

### Memory Issues

1. **Monitor partition count**
   - Log unique client IDs hitting rate limits
   - Consider TTL-based cleanup for inactive partitions

2. **Consider distributed rate limiting**
   - Use Redis for shared state across instances
   - Reduces per-instance memory usage

## Distributed Deployments

For multi-instance deployments, consider:

1. **Per-instance limits**
   - Current implementation (in-memory)
   - Each instance has independent limits
   - Effective limit = PermitLimit × Instance Count

2. **Shared limits** (recommended for production)
   - Use Redis as distributed cache
   - Consistent limits across all instances
   - Requires: `Microsoft.Extensions.Caching.StackExchangeRedis`
   - Custom implementation using Redis for limiter state

## Security Best Practices

1. **Use sliding window algorithm**
   - Prevents burst attacks at window boundaries
   - More consistent protection

2. **Apply stricter limits to write operations**
   - CreateProcess: 100 req/min
   - ReadProcess: 500 req/min

3. **Monitor rate limit violations**
   - Set up alerts for high 429 rates
   - Investigate patterns of abuse

4. **Consider dynamic rate limiting**
   - Adjust limits based on client tier/subscription
   - Implement custom policies for premium clients

5. **Combine with other security measures**
   - Authentication & authorization
   - Input validation
   - Request signing
   - API keys with rate limits

## References

- [ASP.NET Core Rate Limiting](https://learn.microsoft.com/en-us/aspnet/core/performance/rate-limit)
- [RFC 6585 - HTTP Status Code 429](https://tools.ietf.org/html/rfc6585#section-4)
- [System.Threading.RateLimiting](https://learn.microsoft.com/en-us/dotnet/api/system.threading.ratelimiting)
- [TECHNICAL-ANALYSIS.md - Phase 5.2](../docs/TECHNICAL-ANALYSIS.md)
