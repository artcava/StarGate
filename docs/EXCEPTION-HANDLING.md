# Exception Handling - StarGate API

## Overview

StarGate API implements a global exception handler that catches and formats all unhandled exceptions, providing consistent error responses across the entire API and preventing sensitive information leakage.

## Architecture

### Components

1. **GlobalExceptionHandlerMiddleware**: ASP.NET Core `IExceptionHandler` implementation
2. **ProblemDetailsFactory**: Maps exceptions to RFC 7807 Problem Details responses
3. **ExceptionHandlingExtensions**: Dependency injection configuration helpers

### Pipeline Position

The exception handler is registered **early in the middleware pipeline** to catch all exceptions:

```csharp
app.UseGlobalExceptionHandling(); // First
app.UseHttpsRedirection();
app.UseAuthentication();
app.UseAuthorization();
app.MapEndpoints();
```

## Exception to HTTP Status Code Mapping

| Exception Type | HTTP Status | Title | Description |
|----------------|-------------|-------|-------------|
| `ProcessNotFoundException` | 404 Not Found | Process Not Found | Process with specified ID not found |
| `DuplicateProcessException` | 409 Conflict | Duplicate Process | Process with same idempotency key exists |
| `PolicyViolationException` | 429 Too Many Requests | Policy Violation | Rate limit or policy constraint violated |
| `ValidationException` | 400 Bad Request | Validation Error | FluentValidation errors |
| `DomainException` | 400 Bad Request | Domain Error | Business rule violation |
| `ArgumentException` | 400 Bad Request | Invalid Argument | Invalid method argument |
| `OperationCanceledException` | 499 Client Closed Request | Request Cancelled | Client closed connection |
| `TimeoutException` | 408 Request Timeout | Request Timeout | Operation exceeded timeout |
| Other exceptions | 500 Internal Server Error | Internal Server Error | Unexpected system error |

## RFC 7807 Problem Details Format

All error responses follow the [RFC 7807](https://tools.ietf.org/html/rfc7807) Problem Details specification:

```json
{
  "type": "https://tools.ietf.org/html/rfc7231#section-6.5.4",
  "title": "Process Not Found",
  "status": 404,
  "detail": "Process with ID 'abc123' was not found",
  "instance": "/api/processes/abc123",
  "traceId": "00-xyz789...",
  "timestamp": "2026-02-19T14:15:00Z"
}
```

### Response Fields

- **type**: URI reference identifying the problem type
- **title**: Short, human-readable summary
- **status**: HTTP status code
- **detail**: Human-readable explanation specific to this occurrence
- **instance**: URI reference identifying the specific occurrence
- **traceId**: Correlation ID for tracking the request
- **timestamp**: UTC timestamp when the error occurred

## Environment-Specific Behavior

### Production Environment

**Security-focused**: Minimal information disclosure

- Generic error messages for internal errors
- No stack traces
- No sensitive system information
- Safe business rule messages (domain exceptions)

**Example**:
```json
{
  "type": "https://tools.ietf.org/html/rfc7231#section-6.6.1",
  "title": "Internal Server Error",
  "status": 500,
  "detail": "An unexpected error occurred. Please try again later.",
  "instance": "/api/processes",
  "traceId": "00-abc123...",
  "timestamp": "2026-02-19T14:15:00Z"
}
```

### Development Environment

**Developer-friendly**: Detailed debugging information

- Full error messages
- Detailed exception information
- Pretty-printed JSON
- Complete error context

**Example**:
```json
{
  "type": "https://tools.ietf.org/html/rfc7231#section-6.6.1",
  "title": "Internal Server Error",
  "status": 500,
  "detail": "Object reference not set to an instance of an object at ProcessService.GetById(Guid id)",
  "instance": "/api/processes/123",
  "traceId": "00-abc123...",
  "timestamp": "2026-02-19T14:15:00Z"
}
```

## Logging Strategy

### Log Levels

Exceptions are logged with appropriate severity:

| Log Level | Exception Types | Rationale |
|-----------|----------------|------------|
| **Information** | `OperationCanceledException` | Client-initiated, normal operation |
| **Warning** | Domain exceptions, validation errors | Expected business scenarios |
| **Error** | Unhandled exceptions, system errors | Requires investigation |

### Log Context

Every log entry includes:

- **TraceId**: Correlation identifier
- **Method**: HTTP method (GET, POST, etc.)
- **Path**: Request path
- **ExceptionType**: Exception class name
- **Message**: Exception message
- **StackTrace**: Full stack trace (for errors)

**Example log entry**:
```
warn: StarGate.Api.Middleware.GlobalExceptionHandlerMiddleware[0]
      Unhandled exception occurred. TraceId: 00-abc123, Method: GET, Path: /api/processes/invalid-id, ExceptionType: ProcessNotFoundException
      StarGate.Core.Exceptions.ProcessNotFoundException: Process with ID 'invalid-id' was not found
         at ProcessService.GetByIdAsync(Guid id)
         at ProcessEndpoints.<>c.<<MapProcessEndpoints>b__0_1>d.MoveNext()
```

## Usage Examples

### Throwing Domain Exceptions

```csharp
public async Task<Process> GetByIdAsync(Guid id, CancellationToken cancellationToken)
{
    var process = await _repository.GetByIdAsync(id, cancellationToken);
    
    if (process == null)
    {
        throw new ProcessNotFoundException(id);
    }
    
    return process;
}
```

**Client receives**:
```json
{
  "type": "https://tools.ietf.org/html/rfc7231#section-6.5.4",
  "title": "Process Not Found",
  "status": 404,
  "detail": "Process with ID '123e4567-e89b-12d3-a456-426614174000' was not found",
  "instance": "/api/processes/123e4567-e89b-12d3-a456-426614174000",
  "traceId": "00-xyz...",
  "timestamp": "2026-02-19T14:15:00Z"
}
```

### Policy Violations

```csharp
public async Task CreateAsync(Process process, CancellationToken cancellationToken)
{
    var policy = await _policyProvider.GetPolicyAsync(
        process.ProcessType,
        process.ClientId,
        cancellationToken);
    
    if (!policy.CanExecute())
    {
        throw new PolicyViolationException("Maximum concurrent executions exceeded");
    }
    
    await _repository.AddAsync(process, cancellationToken);
}
```

**Client receives**:
```json
{
  "type": "https://tools.ietf.org/html/rfc6585#section-4",
  "title": "Policy Violation",
  "status": 429,
  "detail": "Maximum concurrent executions exceeded",
  "instance": "/api/processes",
  "traceId": "00-xyz...",
  "timestamp": "2026-02-19T14:15:00Z"
}
```

### Client Cancellation

When a client closes the connection:

```csharp
public async Task<Process> LongRunningOperationAsync(CancellationToken cancellationToken)
{
    // Operation takes time
    await Task.Delay(TimeSpan.FromMinutes(5), cancellationToken);
    // Client closes connection -> OperationCanceledException
}
```

**Response**: 499 Client Closed Request

**Log level**: Information (normal client behavior)

## Testing Exception Handling

### Unit Tests

Test exception mapping:

```csharp
[Fact]
public async Task Should_Return404_ForProcessNotFoundException()
{
    // Arrange
    var exception = new ProcessNotFoundException(Guid.NewGuid());
    
    // Act
    var handled = await _middleware.TryHandleAsync(
        _httpContext,
        exception,
        CancellationToken.None);
    
    // Assert
    handled.Should().BeTrue();
    _httpContext.Response.StatusCode.Should().Be(404);
}
```

### Integration Tests

Test end-to-end behavior:

```csharp
[Fact]
public async Task GetProcess_Should_Return404_WhenNotFound()
{
    // Arrange
    var nonExistentId = Guid.NewGuid();
    
    // Act
    var response = await _client.GetAsync($"/api/processes/{nonExistentId}");
    var content = await response.Content.ReadAsStringAsync();
    var problemDetails = JsonSerializer.Deserialize<ProblemDetails>(content);
    
    // Assert
    response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    problemDetails.Status.Should().Be(404);
    problemDetails.Title.Should().Be("Process Not Found");
    problemDetails.Extensions.Should().ContainKey("traceId");
}
```

## Best Practices

### Do's ✅

1. **Use domain exceptions** for business rule violations
2. **Include correlation IDs** (traceId) in all responses
3. **Log with appropriate severity** based on exception type
4. **Preserve exception messages** for domain exceptions (safe)
5. **Test exception handling** in unit and integration tests

### Don'ts ❌

1. **Don't expose stack traces** in production
2. **Don't include sensitive data** in exception messages
3. **Don't catch exceptions** just to rethrow them
4. **Don't use exceptions** for flow control
5. **Don't log at Error level** for expected business scenarios

## Troubleshooting

### Finding Errors by TraceId

Use the `traceId` from error responses to search logs:

```bash
# Search logs for specific trace ID
grep "00-abc123" application.log

# Or use structured logging query
az monitor log-analytics query \
  --workspace <workspace-id> \
  --analytics-query "traces | where customDimensions.TraceId == '00-abc123'"
```

### Common Issues

**Problem**: Sensitive information in production errors

**Solution**: Verify `ASPNETCORE_ENVIRONMENT` is set to `Production`

---

**Problem**: Missing trace IDs in responses

**Solution**: Ensure middleware is registered before other middleware

---

**Problem**: 500 errors for domain exceptions

**Solution**: Ensure domain exceptions inherit from `DomainException`

## Related Documentation

- [RFC 7807 - Problem Details for HTTP APIs](https://tools.ietf.org/html/rfc7807)
- [ASP.NET Core Error Handling](https://learn.microsoft.com/en-us/aspnet/core/fundamentals/error-handling)
- [Domain Exceptions](./DOMAIN-EXCEPTIONS.md)
- [Logging Guidelines](./LOGGING.md)
