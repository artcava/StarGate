# Coding Conventions - StarGate

## Overview

This document describes the coding conventions and style guidelines for the StarGate project. These conventions are based on [Microsoft's C# Coding Conventions](https://learn.microsoft.com/en-us/dotnet/csharp/fundamentals/coding-style/coding-conventions) and are enforced through the `.editorconfig` file.

**Goals:**
- **Consistency**: Maintain a uniform code style across the entire codebase
- **Readability**: Make code easy to understand and maintain
- **Correctness**: Write resilient code that works correctly even after multiple edits
- **Collaboration**: Enable team members to work together effectively

---

## Table of Contents

1. [Tools and Enforcement](#tools-and-enforcement)
2. [Naming Conventions](#naming-conventions)
3. [Language Guidelines](#language-guidelines)
4. [Code Organization](#code-organization)
5. [Style Guidelines](#style-guidelines)
6. [Comments](#comments)
7. [Security](#security)

---

## Tools and Enforcement

### EditorConfig

The project uses an `.editorconfig` file to automatically enforce coding standards. Visual Studio and other IDEs will automatically apply these rules when you format your code.

**To format code in Visual Studio:**
- **Windows**: `Ctrl + K, Ctrl + D`
- **Mac**: `Cmd + K, Cmd + D`

**To format code via CLI:**
```bash
dotnet format
```

### Code Analysis

The project enables Roslyn analyzers to detect code quality issues. Build warnings will appear when conventions are violated.

**Run code analysis:**
```bash
dotnet build /p:TreatWarningsAsErrors=true
```

---

## Naming Conventions

### General Rules

Following [Microsoft's naming guidelines](https://learn.microsoft.com/en-us/dotnet/csharp/fundamentals/coding-style/coding-conventions#naming-conventions), use descriptive names that clearly convey the purpose of each element.

### Specific Conventions

| Element | Convention | Example |
|---------|-----------|----------|
| **Namespace** | PascalCase | `StarGate.Api.Services` |
| **Class** | PascalCase | `ProcessService` |
| **Interface** | IPascalCase (prefix with `I`) | `IProcessRepository` |
| **Method** | PascalCase | `GetProcessById` |
| **Property** | PascalCase | `ProcessId` |
| **Public Field** | PascalCase | `MaxRetryCount` |
| **Private Field** | _camelCase (prefix with `_`) | `_connectionString` |
| **Constant** | PascalCase | `MaxTimeout` |
| **Local Variable** | camelCase | `processId` |
| **Parameter** | camelCase | `clientId` |
| **Async Method** | PascalCaseAsync (suffix with `Async`) | `GetProcessByIdAsync` |
| **Record Type** | PascalCase (parameters also PascalCase) | `Person(string FirstName, string LastName)` |

### Examples

**Classes and Interfaces:**
```csharp
public interface IProcessService
{
    Task<Process> GetProcessByIdAsync(string processId);
}

public class ProcessService : IProcessService
{
    private readonly IProcessRepository _repository;
    private const int MaxRetryCount = 3;
    
    public ProcessService(IProcessRepository repository)
    {
        _repository = repository;
    }
    
    public async Task<Process> GetProcessByIdAsync(string processId)
    {
        // Implementation
    }
}
```

**Record Types:**
```csharp
// Primary constructor parameters use PascalCase for records
public record Person(string FirstName, string LastName);

// For classes and structs, use camelCase
public class Container(string label)
{
    public string Label { get; } = label;
}
```

---

## Language Guidelines

### Use Modern C# Features

Utilize modern language features to write cleaner, more expressive code.

### Type Keywords vs. Runtime Types

**DO** use language keywords instead of runtime types:

```csharp
// ✅ Correct
string name = "John";
int count = 10;

// ❌ Avoid
String name = "John";
Int32 count = 10;
```

### String Handling

**String Interpolation:**
```csharp
// ✅ Correct - Use string interpolation
string displayName = $"{user.LastName}, {user.FirstName}";

// ❌ Avoid - String concatenation
string displayName = user.LastName + ", " + user.FirstName;
```

**StringBuilder for Loops:**
```csharp
// ✅ Correct - Use StringBuilder in loops
var result = new StringBuilder();
for (var i = 0; i < 10000; i++)
{
    result.Append("text");
}
```

**Raw String Literals:**
```csharp
// ✅ Correct - Use raw string literals for multi-line strings
var message = """
    This is a long message that spans across multiple lines.
    It uses raw string literals. This means we can
    also include characters like \n and \t without escaping them.
    """;
```

### Collection Initialization

**Collection Expressions (C# 12+):**
```csharp
// ✅ Correct - Use collection expressions
string[] vowels = ["a", "e", "i", "o", "u"];
List<int> numbers = [1, 2, 3, 4, 5];
```

### Object Initialization

**Object Initializers:**
```csharp
// ✅ Correct - Use object initializers
var process = new Process 
{ 
    Id = "PROC-001", 
    Status = ProcessStatus.Running,
    CreatedAt = DateTime.UtcNow 
};

// ✅ Correct - Target-typed new
Process process = new() 
{ 
    Id = "PROC-001",
    Status = ProcessStatus.Running 
};
```

### Implicitly Typed Variables (`var`)

Following [Microsoft's guidance on implicit typing](https://learn.microsoft.com/en-us/dotnet/csharp/fundamentals/coding-style/coding-conventions#implicitly-typed-local-variables):

**DO use `var` when:**
- The type is obvious from the right side of the assignment
- Using `new` operator or explicit cast
- The variable is assigned a literal value

```csharp
// ✅ Correct - Type is obvious
var message = "This is clearly a string";
var count = 42;
var process = new Process();
var customer = (Customer)entity;
```

**DO NOT use `var` when:**
- The type is not apparent from the right side
- Using method calls where return type is unclear

```csharp
// ✅ Correct - Type is not obvious
int result = CalculateValue();
string data = GetDataFromApi();
```

**Special cases:**
```csharp
// ✅ Use var in for loops
for (var i = 0; i < 100; i++)
{
    // ...
}

// ✅ Use explicit types in foreach loops
foreach (Customer customer in customers)
{
    // ...
}

// ✅ Use var for LINQ queries (often results in anonymous types)
var query = from c in customers
            where c.City == "Turin"
            select c;
```

### Delegates

**Use `Func<>` and `Action<>` instead of custom delegates:**

```csharp
// ✅ Correct
Action<string> logger = message => Console.WriteLine(message);
Func<int, int, int> add = (x, y) => x + y;

// ❌ Avoid (unless you need a specific delegate type)
public delegate void LogHandler(string message);
```

### Exception Handling

**Try-Catch:**
```csharp
// ✅ Correct - Catch specific exceptions
try
{
    var result = await ProcessDataAsync();
}
catch (InvalidOperationException ex)
{
    _logger.LogError(ex, "Invalid operation");
    throw;
}
catch (TimeoutException ex)
{
    _logger.LogError(ex, "Operation timed out");
    throw;
}
```

**Using Statements:**
```csharp
// ✅ Correct - Use declaration (C# 8+)
using var connection = new SqlConnection(connectionString);
// Connection disposed at end of scope

// ✅ Also correct - Traditional using statement
using (var connection = new SqlConnection(connectionString))
{
    // Use connection
}
```

### Operators

**Conditional Logical Operators:**
```csharp
// ✅ Correct - Use && and || for short-circuit evaluation
if (divisor != 0 && (dividend / divisor) > 10)
{
    // Safe from division by zero
}

// ❌ Avoid - & and | always evaluate both operands
if (divisor != 0 & (dividend / divisor) > 10)
{
    // Not safe!
}
```

### LINQ Queries

Following [Microsoft's LINQ guidelines](https://learn.microsoft.com/en-us/dotnet/csharp/fundamentals/coding-style/coding-conventions#linq-queries):

```csharp
// ✅ Correct - Use meaningful names and proper formatting
var activeProcesses = from process in processes
                      where process.Status == ProcessStatus.Running
                      orderby process.CreatedAt descending
                      select process;

// ✅ Correct - Use aliases for clarity
var customerOrders = from customer in customers
                     join order in orders on customer.Id equals order.CustomerId
                     select new { CustomerName = customer.Name, OrderId = order.Id };

// ✅ Correct - Use implicit typing for query variables
var scoreQuery = from student in students
                 from score in student.Scores
                 where score > 90
                 select new { Last = student.LastName, score };
```

### Async/Await

```csharp
// ✅ Correct - Use async/await for I/O operations
public async Task<Process> GetProcessAsync(string processId)
{
    var process = await _repository.GetByIdAsync(processId);
    return process;
}

// ✅ Correct - Use ConfigureAwait(false) in library code
public async Task<Process> GetProcessAsync(string processId)
{
    var process = await _repository.GetByIdAsync(processId).ConfigureAwait(false);
    return process;
}
```

---

## Code Organization

### Namespace Declarations

**File-Scoped Namespaces (C# 10+):**

Following [Microsoft's recommendation](https://learn.microsoft.com/en-us/dotnet/csharp/fundamentals/coding-style/coding-conventions#file-scoped-namespace-declarations), use file-scoped namespace declarations:

```csharp
// ✅ Correct - File-scoped namespace
namespace StarGate.Api.Services;

public class ProcessService
{
    // Implementation
}
```

```csharp
// ❌ Avoid - Traditional namespace (adds unnecessary nesting)
namespace StarGate.Api.Services
{
    public class ProcessService
    {
        // Implementation
    }
}
```

### Using Directives

**Place `using` directives outside the namespace:**

Following [Microsoft's guidance](https://learn.microsoft.com/en-us/dotnet/csharp/fundamentals/coding-style/coding-conventions#place-the-using-directives-outside-the-namespace-declaration):

```csharp
// ✅ Correct - Using directives outside namespace
using System;
using System.Collections.Generic;
using Microsoft.Extensions.Logging;

namespace StarGate.Api.Services;

public class ProcessService
{
    // Implementation
}
```

**Reason:** When `using` directives are outside the namespace, they use fully qualified names, avoiding ambiguity and potential namespace conflicts.

### Member Organization

**Order members within a class:**

1. Constants
2. Static fields
3. Private fields
4. Constructors
5. Properties
6. Public methods
7. Private methods

```csharp
public class ProcessService
{
    // 1. Constants
    private const int MaxRetryCount = 3;
    
    // 2. Static fields
    private static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(30);
    
    // 3. Private fields
    private readonly IProcessRepository _repository;
    private readonly ILogger<ProcessService> _logger;
    
    // 4. Constructors
    public ProcessService(IProcessRepository repository, ILogger<ProcessService> logger)
    {
        _repository = repository;
        _logger = logger;
    }
    
    // 5. Properties
    public int RetryCount { get; set; } = MaxRetryCount;
    
    // 6. Public methods
    public async Task<Process> GetProcessAsync(string id)
    {
        return await GetProcessInternalAsync(id);
    }
    
    // 7. Private methods
    private async Task<Process> GetProcessInternalAsync(string id)
    {
        // Implementation
    }
}
```

---

## Style Guidelines

### Formatting

Following [Microsoft's formatting conventions](https://learn.microsoft.com/en-us/dotnet/csharp/fundamentals/coding-style/coding-conventions#style-guidelines):

**Indentation:**
- Use **4 spaces** for indentation (not tabs)
- Indent continuation lines one tab stop (4 spaces)

**Braces (Allman Style):**
```csharp
// ✅ Correct - Allman style (opening brace on new line)
if (condition)
{
    DoSomething();
}
else
{
    DoSomethingElse();
}

public void Method()
{
    // Implementation
}
```

**Line Length:**
- Prefer lines under 120 characters for better readability
- Break long statements into multiple lines

**Statements:**
- Write only **one statement per line**
- Write only **one declaration per line**

```csharp
// ✅ Correct
int x = 10;
int y = 20;

// ❌ Avoid
int x = 10, y = 20;
```

**Blank Lines:**
- Add at least one blank line between method definitions
- Add at least one blank line between property definitions
- Use blank lines to separate logical groups of code

**Parentheses:**
```csharp
// ✅ Correct - Use parentheses for clarity
if ((startX > endX) && (startX > previousX))
{
    // Take appropriate action
}
```

### Static Members

**Call static members using the class name:**

```csharp
// ✅ Correct
Console.WriteLine("Message");
Math.Sqrt(16);

// ❌ Avoid using instance to call static
var console = Console.Out;
console.WriteLine("Message"); // Misleading
```

---

## Comments

Following [Microsoft's comment guidelines](https://learn.microsoft.com/en-us/dotnet/csharp/fundamentals/coding-style/coding-conventions#comment-style):

### General Rules

- Place comments on a **separate line**, not at the end of a line of code
- Begin comment text with an **uppercase letter**
- End comment text with a **period**
- Insert **one space** between the comment delimiter (`//`) and the comment text

### Single-Line Comments

```csharp
// ✅ Correct - Brief explanation on separate line
// Calculate the total price including tax.
var totalPrice = basePrice * (1 + taxRate);

// ❌ Avoid - Comment at end of line
var totalPrice = basePrice * (1 + taxRate); // calculate total
```

### XML Documentation Comments

**Use XML comments for all public members:**

```csharp
/// <summary>
/// Retrieves a process by its unique identifier.
/// </summary>
/// <param name="processId">The unique identifier of the process.</param>
/// <returns>The process if found; otherwise, null.</returns>
/// <exception cref="ArgumentNullException">Thrown when processId is null.</exception>
public async Task<Process?> GetProcessByIdAsync(string processId)
{
    ArgumentNullException.ThrowIfNull(processId);
    return await _repository.GetByIdAsync(processId);
}
```

**Document complex logic:**

```csharp
// The polling strategy uses adaptive intervals to balance responsiveness
// with resource efficiency. It starts with aggressive 30-second intervals
// for the first 2 minutes, then switches to conservative 60-second intervals.
if (elapsedTime < TimeSpan.FromMinutes(2))
{
    await Task.Delay(TimeSpan.FromSeconds(30));
}
else
{
    await Task.Delay(TimeSpan.FromSeconds(60));
}
```

### TODO Comments

```csharp
// TODO: Implement retry logic with exponential backoff
// TODO: Add telemetry for monitoring polling performance
```

---

## Security

Follow [Secure Coding Guidelines](https://learn.microsoft.com/en-us/dotnet/standard/security/secure-coding-guidelines):

### Input Validation

```csharp
public async Task<Process> GetProcessAsync(string processId)
{
    // ✅ Validate input
    ArgumentException.ThrowIfNullOrWhiteSpace(processId);
    
    return await _repository.GetByIdAsync(processId);
}
```

### Sensitive Data

```csharp
// ✅ Correct - Never log sensitive data
_logger.LogInformation("Processing request for client {ClientId}", clientId);

// ❌ Avoid - Don't log passwords, tokens, etc.
_logger.LogInformation("Auth token: {Token}", authToken); // NEVER DO THIS
```

### SQL Injection Prevention

```csharp
// ✅ Correct - Use parameterized queries
var query = "SELECT * FROM Processes WHERE Id = @Id";
var process = await connection.QueryFirstOrDefaultAsync<Process>(query, new { Id = processId });

// ❌ Avoid - String concatenation
var query = $"SELECT * FROM Processes WHERE Id = '{processId}'"; // VULNERABLE!
```

---

## Code Quality Checklist

Before committing code, ensure:

- [ ] Code follows naming conventions
- [ ] File-scoped namespaces are used
- [ ] `using` directives are outside namespace
- [ ] Code is properly formatted (run `dotnet format`)
- [ ] XML documentation for public APIs
- [ ] No compiler warnings
- [ ] Code analysis passes
- [ ] Unit tests are included
- [ ] No sensitive data in logs or comments

---

## References

- [Microsoft C# Coding Conventions](https://learn.microsoft.com/en-us/dotnet/csharp/fundamentals/coding-style/coding-conventions)
- [.NET Runtime Coding Style](https://github.com/dotnet/runtime/blob/main/docs/coding-guidelines/coding-style.md)
- [Framework Design Guidelines](https://learn.microsoft.com/en-us/dotnet/standard/design-guidelines/)
- [Secure Coding Guidelines](https://learn.microsoft.com/en-us/dotnet/standard/security/secure-coding-guidelines)

---

## Enforcement

These conventions are enforced through:

1. **EditorConfig** (`.editorconfig`) - Automatic formatting in IDEs
2. **Roslyn Analyzers** - Compile-time warnings and errors
3. **CI/CD Pipeline** - Build failures on violations (see `ci.yml`)
4. **Code Reviews** - Human review of pull requests

For questions about these conventions, refer to [GIT-FLOW.md](./GIT-FLOW.md) for the development process.
