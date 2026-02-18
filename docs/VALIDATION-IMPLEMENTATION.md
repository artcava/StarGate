# Policy Validation Implementation

## Overview

This document describes the policy validation system implemented in StarGate to ensure configuration integrity and prevent runtime errors from invalid policy configurations.

## Architecture

### Three-Level Validation Strategy

The validation system implements three distinct validation levels:

1. **Type Policy Validation**: Validates `ProcessTypePolicy` entities (default policies)
2. **Client Override Validation**: Validates `ClientPolicyOverride` entities (client-specific overrides)
3. **Resolved Policy Validation**: Validates final merged policies (future implementation with PolicyProvider integration)

### Components

#### Domain Models (`StarGate.Core.Domain`)

- **PolicyValidationResult**: Immutable record representing validation outcomes
  - `IsValid`: Boolean flag indicating success/failure
  - `Errors`: Collection of `PolicyValidationError` objects
  - Factory methods: `Success()` and `Failure(params PolicyValidationError[])`

- **PolicyValidationError**: Immutable record representing individual validation errors
  - `PropertyName`: Name of the validated property
  - `ErrorCode`: Machine-readable error code for categorization
  - `ErrorMessage`: Human-readable description
  - `AttemptedValue`: The invalid value that triggered the error

#### Validators (`StarGate.Infrastructure.Validation`)

- **ProcessTypePolicyValidator**: FluentValidation validator for type policies
  - Validates all required fields (ProcessType, Timeout, RetryPolicy, etc.)
  - Enforces operational constraints (timeout range, retry limits, concurrency bounds)
  - Uses declarative validation rules for maintainability

- **ClientPolicyOverrideValidator**: FluentValidation validator for client overrides
  - Validates required fields (ClientId, ProcessType)
  - Conditionally validates optional override fields only when present
  - Uses `When()` clauses for optional field validation

#### Services (`StarGate.Infrastructure.Services`)

- **IPolicyValidator**: Service interface for policy validation
  - `ValidateTypePolicy(ProcessTypePolicy)`: Validates type policies
  - `ValidateClientOverride(ClientPolicyOverride)`: Validates client overrides

- **PolicyValidator**: Implementation of policy validation service
  - Delegates to FluentValidation validators
  - Converts FluentValidation results to domain-specific `PolicyValidationResult`
  - Logs validation failures with structured logging
  - Throws `ArgumentNullException` for null inputs (fail-fast)

## Validation Constraints

### ProcessTypePolicy Constraints

| Field | Constraint | Error Code |
|-------|-----------|------------|
| ProcessType | Required, max 100 chars | `PROCESS_TYPE_REQUIRED`, `PROCESS_TYPE_TOO_LONG` |
| Timeout | 1 second - 1 hour (3600s) | `TIMEOUT_OUT_OF_RANGE` |
| RetryPolicy | Required | `RETRY_POLICY_REQUIRED` |
| RetryPolicy.MaxAttempts | 0-10 | `MAX_RETRY_OUT_OF_RANGE` |
| ResultRetention | >= 0 | `RESULT_RETENTION_NEGATIVE` |
| MaxConcurrentProcesses | 1-100 (optional) | `MAX_CONCURRENCY_OUT_OF_RANGE` |
| UpdatedAt | Required | `UPDATED_AT_REQUIRED` |

### ClientPolicyOverride Constraints

| Field | Constraint | Error Code |
|-------|-----------|------------|
| ClientId | Required, max 100 chars | `CLIENT_ID_REQUIRED`, `CLIENT_ID_TOO_LONG` |
| ProcessType | Required, max 100 chars | `PROCESS_TYPE_REQUIRED`, `PROCESS_TYPE_TOO_LONG` |
| Timeout | 1s - 1h (if provided) | `TIMEOUT_OUT_OF_RANGE` |
| RetryPolicy.MaxAttempts | 0-10 (if provided) | `MAX_RETRY_OUT_OF_RANGE` |
| ResultRetention | >= 0 (if provided) | `RESULT_RETENTION_NEGATIVE` |
| MaxConcurrentProcesses | 1-100 (if provided) | `MAX_CONCURRENCY_OUT_OF_RANGE` |
| UpdatedAt | Required | `UPDATED_AT_REQUIRED` |

## Dependency Injection Configuration

### Registration

```csharp
services.AddPolicyValidation();
```

This extension method registers:
- All validators from the assembly (`ProcessTypePolicyValidator`, `ClientPolicyOverrideValidator`)
- `IPolicyValidator` as a singleton service

### Usage Example

```csharp
public class SomeService
{
    private readonly IPolicyValidator _validator;

    public SomeService(IPolicyValidator validator)
    {
        _validator = validator;
    }

    public void ProcessPolicy(ProcessTypePolicy policy)
    {
        var result = _validator.ValidateTypePolicy(policy);
        
        if (!result.IsValid)
        {
            foreach (var error in result.Errors)
            {
                Console.WriteLine($"{error.ErrorCode}: {error.ErrorMessage}");
            }
            return;
        }

        // Process valid policy...
    }
}
```

## Testing Strategy

### Test Coverage

Comprehensive unit tests verify:

1. **Validator Tests** (`ProcessTypePolicyValidatorTests`, `ClientPolicyOverrideValidatorTests`)
   - Valid policy scenarios
   - Each constraint boundary (min, max, invalid)
   - Required field validation
   - Optional field conditional validation
   - Multiple simultaneous errors
   - Edge cases (null, zero, negative values)

2. **Service Tests** (`PolicyValidatorTests`)
   - Constructor null argument validation
   - Success scenarios with mocked validators
   - Failure scenarios with validation errors
   - Error mapping from FluentValidation to domain model
   - Structured logging verification

### Test Technologies

- **xUnit**: Test framework
- **FluentAssertions**: Readable assertions
- **NSubstitute**: Mocking framework for service tests

## Future Enhancements

### Integration with PolicyProvider

Once PolicyProvider is implemented (Issue #56, #57), the validation will be integrated:

```csharp
public async Task<ProcessPolicy?> GetPolicyAsync(string processType, string clientId)
{
    // Load type policy
    var typePolicy = await _repository.GetTypePolicyAsync(processType);
    
    // Validate type policy
    var typePolicyValidation = _validator.ValidateTypePolicy(typePolicy);
    if (!typePolicyValidation.IsValid)
    {
        _logger.LogError("Type policy validation failed: {Errors}", ...);
        return null; // Reject invalid policy
    }

    // Load and validate client override
    var clientOverride = await _repository.GetClientOverrideAsync(clientId, processType);
    if (clientOverride != null)
    {
        var overrideValidation = _validator.ValidateClientOverride(clientOverride);
        if (!overrideValidation.IsValid)
        {
            _logger.LogError("Client override validation failed: {Errors}", ...);
            clientOverride = null; // Ignore invalid override
        }
    }

    // Resolve and return policy
    return ResolvePolicy(typePolicy, clientOverride);
}
```

### Additional Validation Rules

Potential future enhancements:
- Cross-field validation (e.g., timeout should exceed retry backoff)
- Custom validation rules per process type
- Dynamic constraint configuration
- Validation result caching
- Validation metrics and alerting

## Design Decisions

### Why FluentValidation?

- **Declarative**: Rules are clear and self-documenting
- **Reusable**: Validators can be composed and extended
- **Testable**: Easy to test validation rules in isolation
- **Maintainable**: Changes to rules are localized
- **Rich**: Built-in rules for common scenarios

### Why Custom Result Model?

- **Domain Alignment**: `PolicyValidationResult` matches domain language
- **Decoupling**: Core domain doesn't depend on FluentValidation
- **Consistency**: Same result pattern across all validation scenarios
- **Extensibility**: Easy to add domain-specific fields or behavior

### Why Singleton Lifetime?

- **Stateless**: Validators and service are stateless
- **Performance**: Single instance reduces allocations
- **Thread-Safe**: FluentValidation validators are thread-safe
- **DI Best Practice**: Services without state should be singletons

## References

- [FluentValidation Documentation](https://docs.fluentvalidation.net/)
- [Issue #64: Phase 4.2 - Add Policy Validation and Constraints](https://github.com/artcava/StarGate/issues/64)
- [TECHNICAL-ANALYSIS.md - Configuration Management](https://github.com/artcava/StarGate/blob/main/docs/TECHNICAL-ANALYSIS.md#configuration-management)
- [CODING-CONVENTIONS.md](https://github.com/artcava/StarGate/blob/main/docs/CODING-CONVENTIONS.md)
