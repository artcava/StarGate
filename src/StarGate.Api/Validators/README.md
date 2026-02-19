# Validators

This directory contains FluentValidation validators for API request validation.

## Overview

Validators in this directory are automatically registered with the DI container and applied to API endpoints through the `ValidationFilter`.

## Structure

```
Validators/
├── BaseValidator.cs                      # Abstract base with reusable rules
└── CreateProcessRequestValidator.cs      # Validator for CreateProcessRequest
```

## Base Validator

`BaseValidator<T>` provides reusable validation rules:

- **MustBeAlphanumericWithHyphens**: Validates lowercase alphanumeric with hyphens
- **MustNotBeEmptyGuid**: Ensures GUID is not empty
- **MustHaveMaxEntries**: Limits dictionary entries

## Creating New Validators

1. **Inherit from BaseValidator**:
   ```csharp
   public class MyRequestValidator : BaseValidator<MyRequest>
   {
       public MyRequestValidator()
       {
           RuleFor(x => x.Property)
               .NotEmpty()
               .WithMessage("Property is required");
       }
   }
   ```

2. **Use Fluent API** for complex rules:
   ```csharp
   RuleFor(x => x.Email)
       .NotEmpty()
       .EmailAddress()
       .MaximumLength(100);
   ```

3. **Custom validation methods**:
   ```csharp
   private static bool BeValidFormat(string value)
   {
       return Regex.IsMatch(value, "^[a-z0-9-]+$");
   }
   ```

## Testing

All validators must have corresponding unit tests in `tests/StarGate.Api.Tests/Validators/`.

See `CreateProcessRequestValidatorTests.cs` for examples.

## Documentation

See [VALIDATION-RULES.md](../../../docs/VALIDATION-RULES.md) for complete validation rules documentation.
