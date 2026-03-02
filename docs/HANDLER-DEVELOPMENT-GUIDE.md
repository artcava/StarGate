# Process Handler Development Guide

## Overview

Process handlers implement business logic for specific process types. Each handler must implement `IProcessHandler` and be registered with the `ProcessHandlerFactory`.

## Creating a Custom Handler

### 1. Define Handler Class

```csharp
public class MyCustomHandler : IProcessHandler
{
    private readonly ILogger<MyCustomHandler> _logger;

    public MyCustomHandler(ILogger<MyCustomHandler> logger)
    {
        _logger = logger;
    }

    public string ProcessType => "my-custom-type";

    public async Task ExecuteAsync(ProcessContext context)
    {
        // Implement your business logic here
    }
}
```

### 2. Access Process Metadata

```csharp
var orderId = context.GetMetadata("orderId");
var customerId = context.GetMetadata("customerId");
```

### 3. Handle Cancellation

```csharp
public async Task ExecuteAsync(ProcessContext context)
{
    try
    {
        await SomeOperationAsync(context.CancellationToken);
    }
    catch (OperationCanceledException)
    {
        _logger.LogWarning("Process cancelled");
        throw; // Re-throw to signal cancellation
    }
}
```

### 4. Validate Input

```csharp
private void ValidateInput(string? value)
{
    if (string.IsNullOrWhiteSpace(value))
    {
        throw new InvalidOperationException("Value is required");
    }
}
```

### 5. Handle Errors

```csharp
try
{
    await ExecuteBusinessLogicAsync();
}
catch (InvalidOperationException ex)
{
    // Non-retryable errors
    _logger.LogError(ex, "Validation failed");
    throw;
}
catch (HttpRequestException ex)
{
    // Retryable errors
    _logger.LogWarning(ex, "External service error");
    throw;
}
```

### 6. Register Handler

Update `src/StarGate.Server/Extensions/ProcessHandlerServiceCollectionExtensions.cs`:

```csharp
services.AddTransient<MyCustomHandler>();

// In factory registration:
var myHandler = provider.GetRequiredService<MyCustomHandler>();
factory.RegisterHandler(myHandler.ProcessType, myHandler);
```

## Best Practices

1. **Idempotency**: Handlers should be idempotent when possible
2. **Logging**: Log at appropriate levels (Debug, Info, Warning, Error)
3. **Validation**: Validate input early, fail fast
4. **Error Types**: Use specific exception types for different error scenarios
5. **Timeouts**: Respect the cancellation token
6. **Dependencies**: Inject services via constructor
7. **Testing**: Write comprehensive unit tests

## Error Classification

### Non-Retryable Errors (InvalidOperationException)

These errors indicate validation failures or business rule violations that won't be resolved by retrying:

- Missing required metadata
- Invalid data format
- Business rule violations
- Validation failures

### Retryable Errors

These errors are transient and may succeed on retry:

- `HttpRequestException`: Network issues
- `TimeoutException`: Timeout errors
- Transient database errors
- External service unavailable

## Metadata Conventions

### Key Names

- Use camelCase: `orderId`, `customerId`, `amount`
- Be descriptive and consistent
- Document required metadata

### Example

```json
{
  "orderId": "order-123",
  "customerId": "customer-456",
  "amount": "100.00",
  "currency": "USD"
}
```

## Logging Best Practices

### Levels

- **Debug**: Internal details, intermediate steps
- **Info**: Important milestones, completion
- **Warning**: Recoverable errors, retries
- **Error**: Non-recoverable errors

### Structured Logging

```csharp
_logger.LogInformation(
    "Order processed: OrderId={OrderId}, Amount={Amount}",
    orderId,
    amount);
```

**Benefits:**
- Easy to parse
- Searchable in log aggregators
- Consistent format

## Handler Execution Flow

```
1. Validate Input → Fail fast with InvalidOperationException
2. Execute Step 1 → Call external service (with cancellation support)
3. Execute Step 2 → Call another service
4. Execute Step N → Complete business logic
5. Return → Handler completes, ProcessWorker ACKs message
```

## Testing Strategy

### Unit Tests

- Test validation logic
- Test error scenarios
- Test cancellation
- Mock external dependencies

### Integration Tests

- Test full workflow via API
- Test with real message broker
- Test retry behavior
- Test timeout enforcement

## Examples

### OrderProcessHandler

See [OrderProcessHandler.cs](../src/StarGate.Server/Handlers/OrderProcessHandler.cs) for a complete example demonstrating:

- Multi-step workflow
- External service integration (simulated)
- Comprehensive error handling
- Structured logging
- Cancellation support

### ShippingProcessHandler

Another example handler for shipping operations (to be implemented).

## Common Patterns

### Multi-Step Processing

```csharp
public async Task ExecuteAsync(ProcessContext context)
{
    _logger.LogInformation("Starting process: {ProcessId}", context.ProcessId);

    try
    {
        // Step 1
        await Step1Async(context.CancellationToken);
        _logger.LogInformation("Step 1 completed");

        // Step 2
        await Step2Async(context.CancellationToken);
        _logger.LogInformation("Step 2 completed");

        // Step N
        await StepNAsync(context.CancellationToken);
        _logger.LogInformation("Process completed successfully");
    }
    catch (OperationCanceledException)
    {
        _logger.LogWarning("Process cancelled");
        throw;
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "Process failed");
        throw;
    }
}
```

### External Service Integration

```csharp
private async Task CallExternalServiceAsync(CancellationToken cancellationToken)
{
    try
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        cts.CancelAfter(TimeSpan.FromSeconds(30)); // Service-specific timeout

        var response = await _httpClient.GetAsync(url, cts.Token);
        response.EnsureSuccessStatusCode();

        _logger.LogDebug("External service called successfully");
    }
    catch (HttpRequestException ex)
    {
        _logger.LogWarning(ex, "External service error - retryable");
        throw;
    }
    catch (OperationCanceledException)
    {
        _logger.LogWarning("External service timeout");
        throw;
    }
}
```

## Configuration

Handlers can receive configuration via constructor injection:

```csharp
public class MyHandler : IProcessHandler
{
    private readonly ILogger<MyHandler> _logger;
    private readonly IOptions<MyHandlerOptions> _options;

    public MyHandler(
        ILogger<MyHandler> logger,
        IOptions<MyHandlerOptions> options)
    {
        _logger = logger;
        _options = options;
    }
}
```

## Troubleshooting

### Handler Not Found

**Symptom**: "No handler found for process type: ProcessType=xxx"

**Solutions**:
1. Verify handler is registered in `AddProcessHandlers()`
2. Check `ProcessType` property matches expected value
3. Ensure handler is case-insensitive match

### Handler Timeout

**Symptom**: Process fails with `OperationCanceledException`

**Solutions**:
1. Check handler respects `CancellationToken`
2. Review timeout configuration in policy
3. Optimize long-running operations
4. Consider breaking into smaller steps

### Random Failures

**Symptom**: Tests or handlers fail inconsistently

**Solutions**:
1. Remove simulated failures in production code
2. Mock external dependencies in tests
3. Use deterministic test data

## References

- [IProcessHandler Interface](../src/StarGate.Core/Abstractions/IProcessHandler.cs)
- [ProcessContext](../src/StarGate.Core/Domain/ProcessContext.cs)
- [ProcessHandlerFactory](../src/StarGate.Server/Factories/ProcessHandlerFactory.cs)
- [CODING-CONVENTIONS.md](./CODING-CONVENTIONS.md)
- [TECHNICAL-ANALYSIS.md](./TECHNICAL-ANALYSIS.md)
