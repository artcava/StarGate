# Validation Rules

This document describes the validation rules applied to API requests in StarGate.

## Overview

StarGate uses [FluentValidation](https://docs.fluentvalidation.net/) to validate all incoming API requests. Validation occurs automatically before request processing through the `ValidationFilter`.

## Error Response Format

Validation errors follow the **RFC 7807 Problem Details** standard:

```json
{
  "type": "https://tools.ietf.org/html/rfc7231#section-6.5.1",
  "title": "One or more validation errors occurred.",
  "status": 400,
  "errors": {
    "PropertyName": [
      "Error message 1",
      "Error message 2"
    ]
  },
  "traceId": "00-abc123..."
}
```

## CreateProcessRequest Validation

### ClientId

**Rules:**
- **Required**: Cannot be null, empty, or whitespace
- **Max Length**: 100 characters
- **Pattern**: Alphanumeric characters, hyphens (`-`), underscores (`_`), and dots (`.`)
- **Regex**: `^[a-zA-Z0-9-_.]+$`

**Valid Examples:**
```
test-client
client_123
my.client.id
Client-123_Test
```

**Invalid Examples:**
```
""                    // Empty
"client@test"         // Invalid character @
"client test"         // Space not allowed
"client/test"         // Slash not allowed
```

**Error Messages:**
- `ClientId is required`
- `ClientId must not exceed 100 characters`
- `ClientId contains invalid characters`

---

### ProcessType

**Rules:**
- **Required**: Cannot be null, empty, or whitespace
- **Max Length**: 100 characters
- **Pattern**: Lowercase letters, numbers, and hyphens only
- **Regex**: `^[a-z0-9-]+$`

**Valid Examples:**
```
order
payment-processing
order-fulfillment-v2
shipping123
```

**Invalid Examples:**
```
""                    // Empty
"Order"               // Uppercase not allowed
"order_type"          // Underscore not allowed
"order.type"          // Dot not allowed
"order type"          // Space not allowed
```

**Error Messages:**
- `ProcessType is required`
- `ProcessType must not exceed 100 characters`
- `ProcessType must contain only lowercase letters, numbers, and hyphens`

---

### ClientProcessId

**Rules:**
- **Required**: Cannot be null, empty, or whitespace
- **Max Length**: 200 characters
- **Pattern**: No specific pattern (any characters allowed)

**Valid Examples:**
```
order-123
PAYMENT_2024_001
custom/process/id
any-string-up-to-200-chars
```

**Error Messages:**
- `ClientProcessId is required`
- `ClientProcessId must not exceed 200 characters`

---

### IdempotencyKey

**Rules:**
- **Required**: Cannot be null, empty, or whitespace
- **Max Length**: 100 characters
- **Pattern**: Alphanumeric characters, hyphens (`-`), and underscores (`_`)
- **Regex**: `^[a-zA-Z0-9-_]+$`

**Valid Examples:**
```
idempotency-key-123
key_456
KEY-789_ABC
MyKey123
```

**Invalid Examples:**
```
""                    // Empty
"key@test"            // Invalid character @
"key test"            // Space not allowed
"key.test"            // Dot not allowed
```

**Error Messages:**
- `IdempotencyKey is required`
- `IdempotencyKey must not exceed 100 characters`
- `IdempotencyKey must contain only alphanumeric characters, hyphens, and underscores`

---

### Metadata (Optional)

**Rules:**
- **Optional**: Can be null or omitted
- **Max Entries**: 50 key-value pairs
- **Key Max Length**: 200 characters per key
- **Value Max Length**: 200 characters per value
- **Key Requirement**: Keys cannot be empty or whitespace
- **Value Requirement**: Values can be null

**Valid Examples:**
```json
{
  "key1": "value1",
  "key2": "value2",
  "optional-key": null
}
```

```json
// Null or omitted is valid
null
```

**Invalid Examples:**
```json
{
  // More than 50 entries
  "key1": "value1",
  "key2": "value2",
  // ... 49 more entries
}
```

```json
{
  "": "empty-key-not-allowed"
}
```

```json
{
  "very-long-key-that-exceeds-200-characters...": "value"
}
```

**Error Messages:**
- `Metadata cannot contain more than 50 entries`
- `Metadata keys and values must not exceed 200 characters`

---

## Usage Examples

### Valid Request

```bash
curl -X POST http://localhost:5000/api/processes \
  -H "Content-Type: application/json" \
  -d '{
    "clientId": "test-client",
    "processType": "order",
    "clientProcessId": "order-123",
    "idempotencyKey": "key-123",
    "metadata": {
      "source": "api-test",
      "version": "1.0"
    }
  }'
```

**Response**: `201 Created`

### Invalid Request (Multiple Validation Errors)

```bash
curl -X POST http://localhost:5000/api/processes \
  -H "Content-Type: application/json" \
  -d '{
    "clientId": "",
    "processType": "ORDER-TYPE",
    "clientProcessId": "order-123",
    "idempotencyKey": "key@123"
  }'
```

**Response**: `400 Bad Request`

```json
{
  "type": "https://tools.ietf.org/html/rfc7231#section-6.5.1",
  "title": "One or more validation errors occurred.",
  "status": 400,
  "errors": {
    "ClientId": [
      "ClientId is required"
    ],
    "ProcessType": [
      "ProcessType must contain only lowercase letters, numbers, and hyphens"
    ],
    "IdempotencyKey": [
      "IdempotencyKey must contain only alphanumeric characters, hyphens, and underscores"
    ]
  },
  "traceId": "00-abc123..."
}
```

---

## Best Practices

### For API Consumers

1. **Validate on Client Side**: Implement the same validation rules on the client to provide immediate feedback
2. **Handle Validation Errors**: Parse the `errors` object to display field-specific error messages
3. **Use Idempotency Keys**: Generate unique idempotency keys for each request to prevent duplicates
4. **Follow Naming Conventions**: Use kebab-case for `processType` (e.g., `order-fulfillment`)

### For Developers

1. **Reuse Base Validators**: Extend `BaseValidator<T>` for common validation patterns
2. **Write Tests First**: Create comprehensive unit tests for all validation rules
3. **Keep Rules Simple**: Each rule should validate one concern
4. **Provide Clear Messages**: Error messages should be actionable and specific
5. **Document Changes**: Update this document when validation rules change

---

## Adding New Validators

### Step 1: Create Validator Class

```csharp
public class MyRequestValidator : BaseValidator<MyRequest>
{
    public MyRequestValidator()
    {
        RuleFor(x => x.MyProperty)
            .NotEmpty()
            .WithMessage("MyProperty is required");
    }
}
```

### Step 2: Register in DI Container

Validators are automatically registered via:

```csharp
builder.Services.AddValidatorsFromAssemblyContaining<CreateProcessRequestValidator>();
```

### Step 3: Apply to Endpoint

```csharp
group.MapPost("/", MyEndpointHandler)
    .AddValidation<MyRequest>();
```

### Step 4: Write Unit Tests

```csharp
public class MyRequestValidatorTests
{
    private readonly MyRequestValidator _validator;

    [Fact]
    public void Validate_Should_Succeed_WhenValid()
    {
        // Arrange
        var request = new MyRequest { MyProperty = "valid" };

        // Act
        var result = _validator.Validate(request);

        // Assert
        result.IsValid.Should().BeTrue();
    }
}
```

---

## References

- [FluentValidation Documentation](https://docs.fluentvalidation.net/)
- [RFC 7807 - Problem Details for HTTP APIs](https://tools.ietf.org/html/rfc7807)
- [ASP.NET Core Model Validation](https://learn.microsoft.com/en-us/aspnet/core/mvc/models/validation)
