# Technical Analysis - StarGate Software Development

**Document Version:** 1.3  
**Last Updated:** 2026-02-10  
**Status:** Draft

---

## Table of Contents

1. [Executive Summary](#executive-summary)
2. [Software Architecture](#software-architecture)
3. [Solution Structure](#solution-structure)
4. [Domain Model](#domain-model)
5. [API Design](#api-design)
6. [Data Layer](#data-layer)
7. [Message Broker](#message-broker)
8. [Business Logic](#business-logic)
9. [Process Handlers](#process-handlers)
10. [Security Implementation](#security-implementation)
11. [Resilience Patterns](#resilience-patterns)
12. [Testing Strategy](#testing-strategy)
13. [Development Roadmap](#development-roadmap)
14. [Open Questions](#open-questions)

---

## Executive Summary

This document outlines the technical analysis and development plan for the StarGate software solution. The focus is exclusively on **software development activities**, excluding infrastructure provisioning, Azure configuration, and client-side integrations.

### Scope

**IN SCOPE:**
- .NET 8 solution architecture and implementation
- Domain model and business logic
- API endpoints (Minimal APIs)
- Data access layer (MongoDB, Redis)
- Message broker integration (RabbitMQ)
- Process orchestration engine
- Authentication/Authorization logic
- Resilience patterns (retry, circuit breaker)
- Unit and integration testing
- Docker containerization
- CI/CD pipeline configuration

**OUT OF SCOPE:**
- Azure resource provisioning (VMs, VNets, etc.)
- Azure AD/Identity Provider configuration
- Client application integration (see [CLIENT-TECHNICAL-ANALYSIS.md](./CLIENT-TECHNICAL-ANALYSIS.md))
- Client SDK implementation (see [CLIENT-TECHNICAL-ANALYSIS.md](./CLIENT-TECHNICAL-ANALYSIS.md))
- Production deployment procedures
- Infrastructure as Code (Terraform/ARM)

---

## Software Architecture

### High-Level Architecture

```
┌─────────────────────────────────────────────────────────────┐
│  StarGate Solution (.NET 8)                                 │
│                                                             │
│  ┌────────────────────┐      ┌────────────────────┐        │
│  │  StarGate.Api      │      │  StarGate.Server   │        │
│  │  (Public Gateway)  │      │  (Process Engine)  │        │
│  │  - Minimal APIs    │      │  - BackgroundService│        │
│  │  - Auth/AuthZ      │      │  - Process Handlers│        │
│  │  - Rate Limiting   │      │  - Business Logic  │        │
│  └──────┬─────────────┘      └─────────┬──────────┘        │
│         │                              │                    │
│         │    ┌─────────────────────┐   │                    │
│         └────│  StarGate.Core      │───┘                    │
│              │  - Domain Entities  │                        │
│              │  - Interfaces       │                        │
│              │  - Business Services│                        │
│              └──────┬──────────────┘                        │
│                     │                                       │
│              ┌──────┴──────────────┐                        │
│              │ StarGate.Infrastructure│                     │
│              │  - MongoDB Repository│                       │
│              │  - Redis Cache       │                       │
│              │  - Message Broker    │                       │
│              │    * RabbitMQ        │                       │
│              │    * Abstraction     │                       │
│              └─────────────────────┘                        │
│                                                             │
│  ┌────────────────────┐                                     │
│  │ StarGate.Contracts │                                     │
│  │  - DTOs            │                                     │
│  │  - Public APIs     │                                     │
│  └────────────────────┘                                     │
└─────────────────────────────────────────────────────────────┘
```

### Technology Stack

| Component | Technology | Version |
|-----------|-----------|------------|
| Runtime | .NET | 8.0 |
| API Framework | ASP.NET Core Minimal APIs | 8.0 |
| Cache | StackExchange.Redis | 2.x |
| Database | MongoDB.Driver | 2.x |
| Message Broker | RabbitMQ.Client | 6.x |
| Authentication | Microsoft.AspNetCore.Authentication.JwtBearer | 8.0 |
| Resilience | Polly | 8.x |
| Logging | Serilog | 3.x |
| Testing | xUnit + FluentAssertions + Moq | Latest |
| Containerization | Docker | Latest |

### Architecture Decisions

#### Database: MongoDB
- **Decision:** Use existing MongoDB instance
- **Rationale:** MongoDB instance already available in infrastructure, reduces complexity and costs
- **Benefits:** No additional provisioning required, team familiarity, contained costs

#### Message Broker: RabbitMQ with Abstraction
- **Decision:** RabbitMQ as primary implementation with broker-agnostic interface
- **Rationale:** Reliability, maturity, support for complex patterns; abstraction enables future replacement
- **Benefits:** Scalability, message durability, easy monitoring, flexibility for future changes
- **Alternative:** Can be replaced with Azure Service Bus, Amazon SQS, or other brokers without core changes

---

## Solution Structure

### Project Organization

```
StarGate/
├── src/
│   ├── StarGate.Api/                    # Public API Gateway
│   │   ├── Endpoints/                   # Minimal API endpoints
│   │   │   ├── ProcessEndpoints.cs
│   │   │   └── HealthCheckEndpoints.cs
│   │   ├── Middleware/                  # Custom middleware
│   │   │   ├── GlobalExceptionHandler.cs
│   │   │   └── RequestLoggingMiddleware.cs
│   │   ├── Authorization/               # Authorization handlers
│   │   │   └── ProcessTypeAuthorizationHandler.cs
│   │   ├── Program.cs                   # Application entry point
│   │   ├── appsettings.json
│   │   └── Dockerfile
│   │
│   ├── StarGate.Core/                   # Business Logic & Domain
│   │   ├── Domain/                      # Domain entities
│   │   │   ├── Process.cs
│   │   │   ├── ProcessStatus.cs
│   │   │   └── ProcessError.cs
│   │   ├── Abstractions/                # Interfaces
│   │   │   ├── IProcessRepository.cs
│   │   │   ├── IStateStore.cs
│   │   │   ├── IProcessService.cs
│   │   │   ├── IProcessHandler.cs
│   │   │   ├── IMessageBroker.cs        # Broker abstraction
│   │   │   └── IMessageConsumer.cs      # Consumer abstraction
│   │   ├── Services/                    # Business services
│   │   │   └── ProcessService.cs
│   │   ├── Exceptions/                  # Custom exceptions
│   │   │   ├── ProcessNotFoundException.cs
│   │   │   └── DuplicateProcessException.cs
│   │   └── Telemetry/                   # Metrics and tracing
│   │       └── ProcessMetrics.cs
│   │
│   ├── StarGate.Infrastructure/         # Infrastructure concerns
│   │   ├── Persistence/                 # Database implementations
│   │   │   ├── MongoProcessRepository.cs
│   │   │   ├── ProcessDocument.cs
│   │   │   └── MongoDbContext.cs
│   │   ├── Caching/                     # Cache implementations
│   │   │   ├── RedisStateStore.cs
│   │   │   └── CacheKeys.cs
│   │   ├── Messaging/                   # Message broker implementations
│   │   │   ├── RabbitMQ/                # RabbitMQ specific
│   │   │   │   ├── RabbitMqBroker.cs
│   │   │   │   ├── RabbitMqConsumer.cs
│   │   │   │   ├── RabbitMqConnection.cs
│   │   │   │   └── RabbitMqOptions.cs
│   │   │   ├── ProcessMessage.cs        # Message models
│   │   │   └── MessageSerializers.cs
│   │   └── Resilience/                  # Polly policies
│   │       └── ResiliencePolicies.cs
│   │
│   ├── StarGate.Server/                 # Background Process Engine
│   │   ├── Workers/                     # Background workers
│   │   │   └── ProcessWorker.cs
│   │   ├── Handlers/                    # Process type handlers
│   │   │   ├── IProcessHandlerFactory.cs
│   │   │   ├── ProcessHandlerFactory.cs
│   │   │   ├── OrderProcessHandler.cs
│   │   │   └── ShippingProcessHandler.cs
│   │   ├── Program.cs
│   │   ├── appsettings.json
│   │   └── Dockerfile
│   │
│   └── StarGate.Contracts/              # Shared contracts
│       ├── Requests/                    # Request DTOs
│       │   └── SubmitProcessRequest.cs
│       ├── Responses/                   # Response DTOs
│       │   ├── SubmitProcessResponse.cs
│       │   ├── ProcessStatusResponse.cs
│       │   └── ErrorResponse.cs
│       └── Models/                      # Shared models
│           └── ProcessData.cs
│
├── tests/
│   ├── StarGate.Api.Tests/             # API unit tests
│   │   ├── Endpoints/
│   │   └── Middleware/
│   ├── StarGate.Core.Tests/            # Core unit tests
│   │   ├── Services/
│   │   └── Domain/
│   ├── StarGate.Infrastructure.Tests/  # Infrastructure tests
│   │   ├── Persistence/
│   │   ├── Caching/
│   │   └── Messaging/
│   ├── StarGate.Server.Tests/          # Server unit tests
│   │   ├── Workers/
│   │   └── Handlers/
│   └── StarGate.Integration.Tests/     # Integration tests
│       ├── ApiIntegrationTests.cs
│       ├── BrokerIntegrationTests.cs
│       └── EndToEndTests.cs
│
├── .editorconfig                        # Code style rules
├── .gitignore
├── StarGate.sln                         # Solution file
├── Directory.Build.props                # Shared MSBuild properties
└── docker-compose.yml                   # Local development stack
```

### Directory.Build.props

```xml
<Project>
  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
    <LangVersion>latest</LangVersion>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <TreatWarningsAsErrors>true</TreatWarningsAsErrors>
    <EnforceCodeStyleInBuild>true</EnforceCodeStyleInBuild>
    <EnableNETAnalyzers>true</EnableNETAnalyzers>
  </PropertyGroup>
</Project>
```

---

## Domain Model

### Core Entities

#### Process Entity

```csharp
namespace StarGate.Core.Domain;

/// <summary>
/// Represents a business process submitted through StarGate.
/// </summary>
public record Process
{
    /// <summary>
    /// Server-generated unique identifier (GUID).
    /// </summary>
    public required Guid ProcessId { get; init; }

    /// <summary>
    /// Client-provided unique identifier for correlation.
    /// </summary>
    public required string ClientProcessId { get; init; }

    /// <summary>
    /// Type of process (e.g., "order", "shipping").
    /// </summary>
    public required string ProcessType { get; init; }

    /// <summary>
    /// Client identifier from authentication token.
    /// </summary>
    public required string ClientId { get; init; }

    /// <summary>
    /// Current status of the process.
    /// </summary>
    public required ProcessStatus Status { get; init; }

    /// <summary>
    /// Progress percentage (0-100).
    /// </summary>
    public int Progress { get; init; }

    /// <summary>
    /// Current processing step (optional).
    /// </summary>
    public string? CurrentStep { get; init; }

    /// <summary>
    /// Input data for the process (JSON serializable).
    /// </summary>
    public object? Data { get; init; }

    /// <summary>
    /// Process result (populated when completed).
    /// </summary>
    public object? Result { get; init; }

    /// <summary>
    /// Error details (populated when failed).
    /// </summary>
    public ProcessError? Error { get; init; }

    /// <summary>
    /// Timestamp when process was created.
    /// </summary>
    public required DateTime CreatedAt { get; init; }

    /// <summary>
    /// Timestamp when process was last updated.
    /// </summary>
    public required DateTime UpdatedAt { get; init; }

    /// <summary>
    /// Timestamp when process completed or failed.
    /// </summary>
    public DateTime? CompletedAt { get; init; }

    /// <summary>
    /// Idempotency key to prevent duplicate submissions.
    /// </summary>
    public required string IdempotencyKey { get; init; }

    /// <summary>
    /// Indicates if the process can be retried after failure.
    /// </summary>
    public bool Retryable { get; init; }
}
```

#### ProcessStatus Enum

```csharp
namespace StarGate.Core.Domain;

/// <summary>
/// Represents the lifecycle status of a process.
/// </summary>
public enum ProcessStatus
{
    /// <summary>
    /// Process has been accepted and queued for processing.
    /// </summary>
    Accepted = 0,

    /// <summary>
    /// Process is currently being executed.
    /// </summary>
    Processing = 1,

    /// <summary>
    /// Process has completed successfully.
    /// </summary>
    Completed = 2,

    /// <summary>
    /// Process has failed due to an error.
    /// </summary>
    Failed = 3
}
```

#### ProcessError Record

```csharp
namespace StarGate.Core.Domain;

/// <summary>
/// Represents an error that occurred during process execution.
/// </summary>
/// <param name="Code">Error code for categorization.</param>
/// <param name="Message">Human-readable error message.</param>
/// <param name="Details">Additional error context (optional).</param>
public record ProcessError(
    string Code,
    string Message,
    object? Details);
```

### State Transitions

```
[Submitted] → Accepted → Processing → Completed
                            ↓
                          Failed ──→ (Retry?) → Processing
```

---

## API Design

### Endpoints Specification

#### 1. Submit Process

**Endpoint:** `POST /api/stargate/processes`

**Request Body:**
```json
{
  "clientProcessId": "client-uuid-123",
  "processType": "order",
  "data": {
    "orderId": "ORD-12345",
    "items": [
      { "sku": "SKU-001", "quantity": 10 }
    ]
  },
  "idempotencyKey": "unique-key-abc"
}
```

**Response (202 Accepted):**
```json
{
  "processId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "clientProcessId": "client-uuid-123",
  "processType": "order",
  "status": "accepted",
  "statusUrl": "/api/stargate/processes/3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "createdAt": "2026-02-10T16:30:00Z",
  "estimatedCompletionTime": "2026-02-10T16:35:00Z"
}
```

**Implementation:**
```csharp
namespace StarGate.Api.Endpoints;

public static class ProcessEndpoints
{
    public static void MapProcessEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/stargate/processes")
            .RequireAuthorization()
            .RequireRateLimiting("default")
            .WithOpenApi();

        group.MapPost("", SubmitProcess)
            .Produces<SubmitProcessResponse>(StatusCodes.Status202Accepted)
            .Produces<ErrorResponse>(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status429TooManyRequests);

        group.MapGet("{processId:guid}", GetProcessStatus)
            .Produces<ProcessStatusResponse>(StatusCodes.Status200OK)
            .Produces<ErrorResponse>(StatusCodes.Status404NotFound);
    }

    private static async Task<IResult> SubmitProcess(
        SubmitProcessRequest request,
        IProcessService processService,
        HttpContext context,
        CancellationToken ct)
    {
        // Extract client ID from JWT token
        var clientId = context.User.FindFirst("client_id")?.Value;
        ArgumentException.ThrowIfNullOrWhiteSpace(clientId, nameof(clientId));

        // Validate process type authorization
        var processTypeScope = $"stargate:process:{request.ProcessType}";
        var hasScope = context.User.Claims
            .Any(c => c.Type == "scope" && 
                     (c.Value == "stargate:process:*" || c.Value == processTypeScope));

        if (!hasScope)
        {
            return Results.Problem(
                statusCode: StatusCodes.Status403Forbidden,
                title: "Insufficient Permissions",
                detail: $"Process type '{request.ProcessType}' not authorized");
        }

        // Submit process
        var process = await processService.SubmitProcessAsync(
            clientId,
            request,
            ct);

        var response = new SubmitProcessResponse(
            process.ProcessId,
            process.ClientProcessId,
            process.ProcessType,
            process.Status.ToString().ToLowerInvariant(),
            $"/api/stargate/processes/{process.ProcessId}",
            process.CreatedAt,
            EstimateCompletionTime(process.ProcessType));

        return Results.Accepted(response.StatusUrl, response);
    }

    private static async Task<IResult> GetProcessStatus(
        Guid processId,
        IProcessService processService,
        HttpContext context,
        CancellationToken ct)
    {
        var process = await processService.GetProcessByIdAsync(processId, ct);

        if (process is null)
        {
            return Results.Problem(
                statusCode: StatusCodes.Status404NotFound,
                title: "Process Not Found",
                detail: $"Process with ID '{processId}' was not found");
        }

        // Verify client owns this process
        var clientId = context.User.FindFirst("client_id")?.Value;
        if (process.ClientId != clientId)
        {
            return Results.Problem(
                statusCode: StatusCodes.Status403Forbidden,
                title: "Access Denied",
                detail: "You do not have access to this process");
        }

        var response = MapToStatusResponse(process);
        return Results.Ok(response);
    }

    private static ProcessStatusResponse MapToStatusResponse(Process process)
    {
        return new ProcessStatusResponse(
            process.ProcessId,
            process.ClientProcessId,
            process.ProcessType,
            process.Status.ToString().ToLowerInvariant(),
            process.Progress,
            process.CurrentStep,
            process.Result,
            process.Error != null ? new ErrorResponse(
                process.Error.Code,
                process.Error.Message,
                process.UpdatedAt) : null,
            process.CreatedAt,
            process.UpdatedAt,
            process.CompletedAt,
            process.Retryable);
    }

    private static DateTime EstimateCompletionTime(string processType)
    {
        // Default estimation: 5 minutes
        return DateTime.UtcNow.AddMinutes(5);
    }
}
```

#### 2. Get Process Status

**Endpoint:** `GET /api/stargate/processes/{processId}`

**Response (200 OK - Processing):**
```json
{
  "processId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "clientProcessId": "client-uuid-123",
  "processType": "order",
  "status": "processing",
  "progress": 45,
  "currentStep": "inventory_check",
  "result": null,
  "error": null,
  "createdAt": "2026-02-10T16:30:00Z",
  "updatedAt": "2026-02-10T16:32:15Z",
  "completedAt": null,
  "retryable": false
}
```

**Response (200 OK - Completed):**
```json
{
  "processId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "clientProcessId": "client-uuid-123",
  "processType": "order",
  "status": "completed",
  "progress": 100,
  "currentStep": null,
  "result": {
    "orderId": "ORD-12345",
    "status": "confirmed",
    "trackingNumber": "TRACK-789456",
    "estimatedDelivery": "2026-02-13T00:00:00Z"
  },
  "error": null,
  "createdAt": "2026-02-10T16:30:00Z",
  "updatedAt": "2026-02-10T16:35:00Z",
  "completedAt": "2026-02-10T16:35:00Z",
  "retryable": false
}
```

#### 3. Health Checks

**Liveness:** `GET /health/live`  
**Readiness:** `GET /health/ready`

---

## Data Layer

### MongoDB Schema

#### ProcessDocument

```csharp
namespace StarGate.Infrastructure.Persistence;

using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

/// <summary>
/// MongoDB document representation of a Process.
/// </summary>
public class ProcessDocument
{
    [BsonId]
    [BsonGuidRepresentation(GuidRepresentation.Standard)]
    public required Guid ProcessId { get; set; }

    [BsonElement("clientProcessId")]
    [BsonRequired]
    public required string ClientProcessId { get; set; }

    [BsonElement("processType")]
    [BsonRequired]
    public required string ProcessType { get; set; }

    [BsonElement("clientId")]
    [BsonRequired]
    public required string ClientId { get; set; }

    [BsonElement("status")]
    [BsonRequired]
    public required string Status { get; set; }

    [BsonElement("progress")]
    public int Progress { get; set; }

    [BsonElement("currentStep")]
    public string? CurrentStep { get; set; }

    [BsonElement("data")]
    public BsonDocument? Data { get; set; }

    [BsonElement("result")]
    public BsonDocument? Result { get; set; }

    [BsonElement("error")]
    public ErrorDocument? Error { get; set; }

    [BsonElement("createdAt")]
    [BsonRequired]
    public required DateTime CreatedAt { get; set; }

    [BsonElement("updatedAt")]
    [BsonRequired]
    public required DateTime UpdatedAt { get; set; }

    [BsonElement("completedAt")]
    public DateTime? CompletedAt { get; set; }

    [BsonElement("idempotencyKey")]
    [BsonRequired]
    public required string IdempotencyKey { get; set; }

    [BsonElement("retryable")]
    public bool Retryable { get; set; }
}

public class ErrorDocument
{
    [BsonElement("code")]
    public required string Code { get; set; }

    [BsonElement("message")]
    public required string Message { get; set; }

    [BsonElement("details")]
    public BsonDocument? Details { get; set; }
}
```

#### Indexes

```csharp
// Create indexes for efficient queries
await collection.Indexes.CreateOneAsync(
    new CreateIndexModel<ProcessDocument>(
        Builders<ProcessDocument>.IndexKeys.Ascending(p => p.ProcessId),
        new CreateIndexOptions { Unique = true }));

await collection.Indexes.CreateOneAsync(
    new CreateIndexModel<ProcessDocument>(
        Builders<ProcessDocument>.IndexKeys
            .Ascending(p => p.ClientId)
            .Ascending(p => p.ClientProcessId),
        new CreateIndexOptions { Unique = true }));

await collection.Indexes.CreateOneAsync(
    new CreateIndexModel<ProcessDocument>(
        Builders<ProcessDocument>.IndexKeys.Ascending(p => p.Status)));

await collection.Indexes.CreateOneAsync(
    new CreateIndexModel<ProcessDocument>(
        Builders<ProcessDocument>.IndexKeys.Ascending(p => p.CreatedAt)));

await collection.Indexes.CreateOneAsync(
    new CreateIndexModel<ProcessDocument>(
        Builders<ProcessDocument>.IndexKeys.Ascending(p => p.IdempotencyKey),
        new CreateIndexOptions { Unique = true }));
```

### Repository Implementation

```csharp
namespace StarGate.Infrastructure.Persistence;

using MongoDB.Driver;
using StarGate.Core.Abstractions;
using StarGate.Core.Domain;

public class MongoProcessRepository : IProcessRepository
{
    private readonly IMongoCollection<ProcessDocument> _collection;
    private readonly ILogger<MongoProcessRepository> _logger;

    public MongoProcessRepository(
        IMongoDatabase database,
        ILogger<MongoProcessRepository> logger)
    {
        _collection = database.GetCollection<ProcessDocument>("processes");
        _logger = logger;
    }

    public async Task<Process> CreateAsync(Process process, CancellationToken ct = default)
    {
        var document = MapToDocument(process);

        try
        {
            await _collection.InsertOneAsync(document, cancellationToken: ct);
            _logger.LogInformation(
                "Process {ProcessId} created successfully",
                process.ProcessId);
            return process;
        }
        catch (MongoWriteException ex) when (ex.WriteError.Category == ServerErrorCategory.DuplicateKey)
        {
            _logger.LogWarning(
                "Duplicate process detected: {ProcessId}",
                process.ProcessId);
            throw new InvalidOperationException(
                $"Process with ID '{process.ProcessId}' already exists", ex);
        }
    }

    public async Task<Process?> GetByIdAsync(Guid processId, CancellationToken ct = default)
    {
        var document = await _collection
            .Find(p => p.ProcessId == processId)
            .FirstOrDefaultAsync(ct);

        return document != null ? MapToDomain(document) : null;
    }

    public async Task<Process?> GetByClientProcessIdAsync(
        string clientId,
        string clientProcessId,
        CancellationToken ct = default)
    {
        var document = await _collection
            .Find(p => p.ClientId == clientId && p.ClientProcessId == clientProcessId)
            .FirstOrDefaultAsync(ct);

        return document != null ? MapToDomain(document) : null;
    }

    public async Task<Process> UpdateAsync(Process process, CancellationToken ct = default)
    {
        var document = MapToDocument(process);

        var result = await _collection.ReplaceOneAsync(
            p => p.ProcessId == process.ProcessId,
            document,
            cancellationToken: ct);

        if (result.MatchedCount == 0)
        {
            throw new InvalidOperationException(
                $"Process with ID '{process.ProcessId}' not found");
        }

        _logger.LogInformation(
            "Process {ProcessId} updated to status {Status}",
            process.ProcessId,
            process.Status);

        return process;
    }

    private static ProcessDocument MapToDocument(Process process)
    {
        return new ProcessDocument
        {
            ProcessId = process.ProcessId,
            ClientProcessId = process.ClientProcessId,
            ProcessType = process.ProcessType,
            ClientId = process.ClientId,
            Status = process.Status.ToString(),
            Progress = process.Progress,
            CurrentStep = process.CurrentStep,
            Data = process.Data != null 
                ? BsonDocument.Parse(JsonSerializer.Serialize(process.Data)) 
                : null,
            Result = process.Result != null 
                ? BsonDocument.Parse(JsonSerializer.Serialize(process.Result)) 
                : null,
            Error = process.Error != null ? new ErrorDocument
            {
                Code = process.Error.Code,
                Message = process.Error.Message,
                Details = process.Error.Details != null 
                    ? BsonDocument.Parse(JsonSerializer.Serialize(process.Error.Details))
                    : null
            } : null,
            CreatedAt = process.CreatedAt,
            UpdatedAt = process.UpdatedAt,
            CompletedAt = process.CompletedAt,
            IdempotencyKey = process.IdempotencyKey,
            Retryable = process.Retryable
        };
    }

    private static Process MapToDomain(ProcessDocument document)
    {
        return new Process
        {
            ProcessId = document.ProcessId,
            ClientProcessId = document.ClientProcessId,
            ProcessType = document.ProcessType,
            ClientId = document.ClientId,
            Status = Enum.Parse<ProcessStatus>(document.Status),
            Progress = document.Progress,
            CurrentStep = document.CurrentStep,
            Data = document.Data?.ToJson(),
            Result = document.Result?.ToJson(),
            Error = document.Error != null ? new ProcessError(
                document.Error.Code,
                document.Error.Message,
                document.Error.Details?.ToJson()) : null,
            CreatedAt = document.CreatedAt,
            UpdatedAt = document.UpdatedAt,
            CompletedAt = document.CompletedAt,
            IdempotencyKey = document.IdempotencyKey,
            Retryable = document.Retryable
        };
    }
}
```

### Redis Cache Implementation

```csharp
namespace StarGate.Infrastructure.Caching;

using StackExchange.Redis;
using StarGate.Core.Abstractions;
using StarGate.Core.Domain;

public class RedisStateStore : IStateStore
{
    private readonly IConnectionMultiplexer _redis;
    private readonly ILogger<RedisStateStore> _logger;
    private const int CacheTtlMinutes = 60;
    private const string KeyPrefix = "process:";

    public RedisStateStore(
        IConnectionMultiplexer redis,
        ILogger<RedisStateStore> logger)
    {
        _redis = redis;
        _logger = logger;
    }

    public async Task<Process?> GetProcessAsync(Guid processId)
    {
        try
        {
            var db = _redis.GetDatabase();
            var key = GetKey(processId);
            var cached = await db.StringGetAsync(key);

            if (!cached.HasValue)
            {
                return null;
            }

            var process = JsonSerializer.Deserialize<Process>(cached!);
            _logger.LogDebug("Cache hit for process {ProcessId}", processId);
            return process;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving process {ProcessId} from cache", processId);
            return null;
        }
    }

    public async Task SetProcessAsync(Process process)
    {
        try
        {
            var db = _redis.GetDatabase();
            var key = GetKey(process.ProcessId);
            var serialized = JsonSerializer.Serialize(process);

            await db.StringSetAsync(
                key,
                serialized,
                TimeSpan.FromMinutes(CacheTtlMinutes));

            _logger.LogDebug("Cached process {ProcessId}", process.ProcessId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error caching process {ProcessId}", process.ProcessId);
            // Don't throw - cache failures should not break the application
        }
    }

    public async Task InvalidateAsync(Guid processId)
    {
        try
        {
            var db = _redis.GetDatabase();
            var key = GetKey(processId);
            await db.KeyDeleteAsync(key);
            _logger.LogDebug("Invalidated cache for process {ProcessId}", processId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error invalidating cache for process {ProcessId}", processId);
        }
    }

    private static string GetKey(Guid processId) => $"{KeyPrefix}{processId}";
}
```

---

## Message Broker

### Broker Abstraction

The broker-agnostic design allows replacing the RabbitMQ implementation with other brokers without modifying core code.

#### IMessageBroker Interface

```csharp
namespace StarGate.Core.Abstractions;

/// <summary>
/// Message broker abstraction.
/// Allows replacing implementation (RabbitMQ, Azure Service Bus, etc.) without core changes.
/// </summary>
public interface IMessageBroker
{
    /// <summary>
    /// Publishes a message to a queue for asynchronous processing.
    /// </summary>
    /// <typeparam name="T">Type of message payload.</typeparam>
    /// <param name="queueName">Target queue name.</param>
    /// <param name="message">Message to publish.</param>
    /// <param name="ct">Cancellation token.</param>
    Task PublishAsync<T>(string queueName, T message, CancellationToken ct = default) where T : class;

    /// <summary>
    /// Publishes a message with custom properties.
    /// </summary>
    Task PublishAsync<T>(
        string queueName, 
        T message, 
        MessageProperties properties, 
        CancellationToken ct = default) where T : class;

    /// <summary>
    /// Creates a consumer for a specific queue.
    /// </summary>
    /// <param name="queueName">Queue name to consume from.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Configured consumer.</returns>
    IMessageConsumer CreateConsumer(string queueName, CancellationToken cancellationToken = default);
}

/// <summary>
/// Additional message properties.
/// </summary>
public record MessageProperties
{
    public string? CorrelationId { get; init; }
    public string? MessageId { get; init; }
    public int Priority { get; init; }
    public TimeSpan? TimeToLive { get; init; }
    public Dictionary<string, object>? Headers { get; init; }
}
```

#### IMessageConsumer Interface

```csharp
namespace StarGate.Core.Abstractions;

/// <summary>
/// Message consumer abstraction.
/// </summary>
public interface IMessageConsumer : IAsyncDisposable
{
    /// <summary>
    /// Starts consuming messages.
    /// </summary>
    /// <typeparam name="T">Expected payload type.</typeparam>
    /// <param name="messageHandler">Handler to process messages.</param>
    /// <param name="ct">Cancellation token.</param>
    Task StartConsumingAsync<T>(
        Func<T, MessageContext, Task> messageHandler,
        CancellationToken ct = default) where T : class;

    /// <summary>
    /// Stops consuming messages.
    /// </summary>
    Task StopConsumingAsync();
}

/// <summary>
/// Context of received message.
/// </summary>
public record MessageContext
{
    public required string MessageId { get; init; }
    public string? CorrelationId { get; init; }
    public required DateTime Timestamp { get; init; }
    public required long DeliveryTag { get; init; }
    public required Func<Task> AcknowledgeAsync { get; init; }
    public required Func<bool, Task> RejectAsync { get; init; }
}
```

### RabbitMQ Implementation

#### RabbitMqBroker

```csharp
namespace StarGate.Infrastructure.Messaging.RabbitMQ;

using RabbitMQ.Client;
using StarGate.Core.Abstractions;

public class RabbitMqBroker : IMessageBroker
{
    private readonly IConnection _connection;
    private readonly ILogger<RabbitMqBroker> _logger;
    private readonly RabbitMqOptions _options;

    public RabbitMqBroker(
        IConnection connection,
        RabbitMqOptions options,
        ILogger<RabbitMqBroker> logger)
    {
        _connection = connection ?? throw new ArgumentNullException(nameof(connection));
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task PublishAsync<T>(
        string queueName, 
        T message, 
        CancellationToken ct = default) where T : class
    {
        await PublishAsync(queueName, message, new MessageProperties(), ct);
    }

    public async Task PublishAsync<T>(
        string queueName,
        T message,
        MessageProperties properties,
        CancellationToken ct = default) where T : class
    {
        using var channel = await _connection.CreateChannelAsync(ct);

        await channel.QueueDeclareAsync(
            queue: queueName,
            durable: true,
            exclusive: false,
            autoDelete: false,
            arguments: null,
            cancellationToken: ct);

        var body = MessageSerializers.Serialize(message);

        var basicProperties = new BasicProperties
        {
            Persistent = true,
            MessageId = properties.MessageId ?? Guid.NewGuid().ToString(),
            CorrelationId = properties.CorrelationId,
            Priority = (byte)properties.Priority,
            Timestamp = new AmqpTimestamp(DateTimeOffset.UtcNow.ToUnixTimeSeconds()),
            Headers = properties.Headers != null 
                ? new Dictionary<string, object?>(properties.Headers) 
                : null
        };

        if (properties.TimeToLive.HasValue)
        {
            basicProperties.Expiration = ((int)properties.TimeToLive.Value.TotalMilliseconds).ToString();
        }

        await channel.BasicPublishAsync(
            exchange: string.Empty,
            routingKey: queueName,
            mandatory: false,
            basicProperties: basicProperties,
            body: body,
            cancellationToken: ct);

        _logger.LogDebug(
            "Published message {MessageId} to queue {QueueName}",
            basicProperties.MessageId,
            queueName);
    }

    public IMessageConsumer CreateConsumer(string queueName, CancellationToken cancellationToken = default)
    {
        return new RabbitMqConsumer(_connection, queueName, _logger);
    }
}
```

#### RabbitMqConsumer

```csharp
namespace StarGate.Infrastructure.Messaging.RabbitMQ;

using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using StarGate.Core.Abstractions;

public class RabbitMqConsumer : IMessageConsumer
{
    private readonly IConnection _connection;
    private readonly string _queueName;
    private readonly ILogger _logger;
    private IChannel? _channel;
    private string? _consumerTag;

    public RabbitMqConsumer(
        IConnection connection,
        string queueName,
        ILogger logger)
    {
        _connection = connection ?? throw new ArgumentNullException(nameof(connection));
        _queueName = queueName ?? throw new ArgumentNullException(nameof(queueName));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task StartConsumingAsync<T>(
        Func<T, MessageContext, Task> messageHandler,
        CancellationToken ct = default) where T : class
    {
        _channel = await _connection.CreateChannelAsync(ct);

        await _channel.BasicQosAsync(
            prefetchSize: 0,
            prefetchCount: 1,
            global: false,
            cancellationToken: ct);

        await _channel.QueueDeclareAsync(
            queue: _queueName,
            durable: true,
            exclusive: false,
            autoDelete: false,
            arguments: null,
            cancellationToken: ct);

        var consumer = new AsyncEventingBasicConsumer(_channel);
        consumer.ReceivedAsync += async (sender, ea) =>
        {
            try
            {
                var message = MessageSerializers.Deserialize<T>(ea.Body.ToArray());

                var context = new MessageContext
                {
                    MessageId = ea.BasicProperties.MessageId ?? "unknown",
                    CorrelationId = ea.BasicProperties.CorrelationId,
                    Timestamp = DateTime.UtcNow,
                    DeliveryTag = ea.DeliveryTag,
                    AcknowledgeAsync = async () =>
                    {
                        await _channel.BasicAckAsync(ea.DeliveryTag, multiple: false);
                    },
                    RejectAsync = async (requeue) =>
                    {
                        await _channel.BasicNackAsync(ea.DeliveryTag, multiple: false, requeue: requeue);
                    }
                };

                await messageHandler(message, context);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing message from queue {QueueName}", _queueName);
                await _channel.BasicNackAsync(ea.DeliveryTag, multiple: false, requeue: true);
            }
        };

        _consumerTag = await _channel.BasicConsumeAsync(
            queue: _queueName,
            autoAck: false,
            consumer: consumer,
            cancellationToken: ct);

        _logger.LogInformation("Started consuming from queue {QueueName}", _queueName);
    }

    public async Task StopConsumingAsync()
    {
        if (_channel != null && _consumerTag != null)
        {
            await _channel.BasicCancelAsync(_consumerTag);
            _logger.LogInformation("Stopped consuming from queue {QueueName}", _queueName);
        }
    }

    public async ValueTask DisposeAsync()
    {
        await StopConsumingAsync();

        if (_channel != null)
        {
            await _channel.CloseAsync();
            await _channel.DisposeAsync();
        }
    }
}
```

#### Message Serializers

```csharp
namespace StarGate.Infrastructure.Messaging;

using System.Text;

public static class MessageSerializers
{
    public static byte[] Serialize<T>(T message) where T : class
    {
        var json = JsonSerializer.Serialize(message);
        return Encoding.UTF8.GetBytes(json);
    }

    public static T Deserialize<T>(byte[] data) where T : class
    {
        var json = Encoding.UTF8.GetString(data);
        return JsonSerializer.Deserialize<T>(json) 
            ?? throw new InvalidOperationException("Failed to deserialize message");
    }
}
```

#### RabbitMQ Configuration

```csharp
namespace StarGate.Infrastructure.Messaging.RabbitMQ;

public class RabbitMqOptions
{
    public required string HostName { get; init; }
    public int Port { get; init; } = 5672;
    public required string UserName { get; init; }
    public required string Password { get; init; }
    public string? VirtualHost { get; init; }
    public int RequestedHeartbeat { get; init; } = 60;
    public int NetworkRecoveryInterval { get; init; } = 10;
}

// In Program.cs
services.AddSingleton<IConnection>(sp =>
{
    var options = sp.GetRequiredService<RabbitMqOptions>();
    var factory = new ConnectionFactory
    {
        HostName = options.HostName,
        Port = options.Port,
        UserName = options.UserName,
        Password = options.Password,
        VirtualHost = options.VirtualHost ?? "/",
        RequestedHeartbeat = TimeSpan.FromSeconds(options.RequestedHeartbeat),
        NetworkRecoveryInterval = TimeSpan.FromSeconds(options.NetworkRecoveryInterval),
        AutomaticRecoveryEnabled = true
    };

    return factory.CreateConnectionAsync().GetAwaiter().GetResult();
});

services.AddSingleton<IMessageBroker, RabbitMqBroker>();
```

---

## Business Logic

### Process Service

```csharp
namespace StarGate.Core.Services;

using StarGate.Core.Abstractions;
using StarGate.Core.Domain;

public class ProcessService : IProcessService
{
    private readonly IProcessRepository _repository;
    private readonly IStateStore _cache;
    private readonly IMessageBroker _messageBroker;
    private readonly ILogger<ProcessService> _logger;
    private const string ProcessQueueName = "stargate.processes";

    public ProcessService(
        IProcessRepository repository,
        IStateStore cache,
        IMessageBroker messageBroker,
        ILogger<ProcessService> logger)
    {
        _repository = repository;
        _cache = cache;
        _messageBroker = messageBroker;
        _logger = logger;
    }

    public async Task<Process> SubmitProcessAsync(
        string clientId,
        SubmitProcessRequest request,
        CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(clientId);
        ArgumentNullException.ThrowIfNull(request);

        _logger.LogInformation(
            "Submitting process for client {ClientId}, type {ProcessType}",
            clientId,
            request.ProcessType);

        var existing = await _repository.GetByClientProcessIdAsync(
            clientId,
            request.ClientProcessId,
            ct);

        if (existing is not null)
        {
            _logger.LogInformation(
                "Idempotent request detected for client process {ClientProcessId}",
                request.ClientProcessId);
            return existing;
        }

        var processId = Guid.NewGuid();

        var process = new Process
        {
            ProcessId = processId,
            ClientProcessId = request.ClientProcessId,
            ProcessType = request.ProcessType,
            ClientId = clientId,
            Status = ProcessStatus.Accepted,
            Progress = 0,
            CurrentStep = null,
            Data = request.Data,
            Result = null,
            Error = null,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            CompletedAt = null,
            IdempotencyKey = request.IdempotencyKey,
            Retryable = true
        };

        await _repository.CreateAsync(process, ct);
        await _cache.SetProcessAsync(process);

        await _messageBroker.PublishAsync(
            ProcessQueueName,
            process,
            new MessageProperties
            {
                MessageId = processId.ToString(),
                CorrelationId = request.ClientProcessId
            },
            ct);

        _logger.LogInformation(
            "Process {ProcessId} submitted successfully",
            processId);

        return process;
    }

    public async Task<Process?> GetProcessByIdAsync(
        Guid processId,
        CancellationToken ct = default)
    {
        var cached = await _cache.GetProcessAsync(processId);
        if (cached is not null)
        {
            _logger.LogDebug("Process {ProcessId} retrieved from cache", processId);
            return cached;
        }

        var process = await _repository.GetByIdAsync(processId, ct);

        if (process is not null)
        {
            await _cache.SetProcessAsync(process);
            _logger.LogDebug("Process {ProcessId} retrieved from database", processId);
        }
        else
        {
            _logger.LogWarning("Process {ProcessId} not found", processId);
        }

        return process;
    }

    public async Task<Process> UpdateProcessStatusAsync(
        Guid processId,
        ProcessStatus status,
        int progress = 0,
        string? currentStep = null,
        object? result = null,
        ProcessError? error = null,
        CancellationToken ct = default)
    {
        var process = await _repository.GetByIdAsync(processId, ct);

        if (process is null)
        {
            throw new InvalidOperationException(
                $"Process with ID '{processId}' not found");
        }

        var updated = process with
        {
            Status = status,
            Progress = progress,
            CurrentStep = currentStep,
            Result = result,
            Error = error,
            UpdatedAt = DateTime.UtcNow,
            CompletedAt = status is ProcessStatus.Completed or ProcessStatus.Failed
                ? DateTime.UtcNow
                : process.CompletedAt
        };

        await _repository.UpdateAsync(updated, ct);
        await _cache.SetProcessAsync(updated);

        _logger.LogInformation(
            "Process {ProcessId} updated to status {Status}",
            processId,
            status);

        return updated;
    }
}
```

---

## Process Handlers

### Handler Factory Pattern

```csharp
namespace StarGate.Server.Handlers;

using StarGate.Core.Domain;

public interface IProcessHandler
{
    string ProcessType { get; }
    Task<object> ExecuteAsync(Process process, CancellationToken ct);
}

public interface IProcessHandlerFactory
{
    IProcessHandler GetHandler(string processType);
}

public class ProcessHandlerFactory : IProcessHandlerFactory
{
    private readonly IEnumerable<IProcessHandler> _handlers;
    private readonly ILogger<ProcessHandlerFactory> _logger;

    public ProcessHandlerFactory(
        IEnumerable<IProcessHandler> handlers,
        ILogger<ProcessHandlerFactory> logger)
    {
        _handlers = handlers;
        _logger = logger;
    }

    public IProcessHandler GetHandler(string processType)
    {
        var handler = _handlers.FirstOrDefault(h => 
            h.ProcessType.Equals(processType, StringComparison.OrdinalIgnoreCase));

        if (handler is null)
        {
            _logger.LogError("No handler found for process type {ProcessType}", processType);
            throw new NotSupportedException(
                $"Process type '{processType}' is not supported");
        }

        return handler;
    }
}
```

### Process Worker

```csharp
namespace StarGate.Server.Workers;

using StarGate.Core.Abstractions;
using StarGate.Core.Domain;

public class ProcessWorker : BackgroundService
{
    private readonly IMessageBroker _messageBroker;
    private readonly IProcessHandlerFactory _handlerFactory;
    private readonly IProcessService _processService;
    private readonly ILogger<ProcessWorker> _logger;
    private const string ProcessQueueName = "stargate.processes";

    public ProcessWorker(
        IMessageBroker messageBroker,
        IProcessHandlerFactory handlerFactory,
        IProcessService processService,
        ILogger<ProcessWorker> logger)
    {
        _messageBroker = messageBroker;
        _handlerFactory = handlerFactory;
        _processService = processService;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("ProcessWorker starting");

        var consumer = _messageBroker.CreateConsumer(ProcessQueueName, stoppingToken);

        await consumer.StartConsumingAsync<Process>(ProcessMessageAsync, stoppingToken);

        await Task.Delay(Timeout.Infinite, stoppingToken);
    }

    private async Task ProcessMessageAsync(Process process, MessageContext context)
    {
        _logger.LogInformation(
            "Processing message {MessageId} for process {ProcessId}",
            context.MessageId,
            process.ProcessId);

        try
        {
            await _processService.UpdateProcessStatusAsync(
                process.ProcessId,
                ProcessStatus.Processing,
                progress: 0,
                currentStep: "initializing");

            var handler = _handlerFactory.GetHandler(process.ProcessType);
            var result = await handler.ExecuteAsync(process, CancellationToken.None);

            await _processService.UpdateProcessStatusAsync(
                process.ProcessId,
                ProcessStatus.Completed,
                progress: 100,
                currentStep: null,
                result: result);

            await context.AcknowledgeAsync();

            _logger.LogInformation(
                "Process {ProcessId} completed successfully",
                process.ProcessId);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Process {ProcessId} failed with error",
                process.ProcessId);

            await _processService.UpdateProcessStatusAsync(
                process.ProcessId,
                ProcessStatus.Failed,
                error: new ProcessError(
                    "EXECUTION_ERROR",
                    ex.Message,
                    ex.StackTrace));

            await context.AcknowledgeAsync();
        }
    }
}
```

### Example Handler: Order Process

```csharp
namespace StarGate.Server.Handlers;

using StarGate.Core.Domain;

public class OrderProcessHandler : IProcessHandler
{
    private readonly ILogger<OrderProcessHandler> _logger;

    public string ProcessType => "order";

    public OrderProcessHandler(ILogger<OrderProcessHandler> logger)
    {
        _logger = logger;
    }

    public async Task<object> ExecuteAsync(Process process, CancellationToken ct)
    {
        _logger.LogInformation(
            "Starting order processing for {ProcessId}",
            process.ProcessId);

        var orderData = JsonSerializer.Deserialize<OrderData>(
            process.Data!.ToString()!);

        if (orderData is null)
        {
            throw new InvalidOperationException("Invalid order data");
        }

        await ValidateOrderAsync(orderData, ct);
        await CheckInventoryAsync(orderData, ct);
        await ReserveItemsAsync(orderData, ct);
        await Task.Delay(TimeSpan.FromSeconds(2), ct);

        var trackingNumber = GenerateTrackingNumber();

        var result = new OrderResult
        {
            OrderId = orderData.OrderId,
            Status = "confirmed",
            TrackingNumber = trackingNumber,
            EstimatedDelivery = DateTime.UtcNow.AddDays(3)
        };

        _logger.LogInformation(
            "Order processing completed for {ProcessId}",
            process.ProcessId);

        return result;
    }

    private async Task ValidateOrderAsync(OrderData order, CancellationToken ct)
    {
        await Task.Delay(TimeSpan.FromMilliseconds(500), ct);

        if (order.Items.Count == 0)
        {
            throw new InvalidOperationException("Order must contain at least one item");
        }
    }

    private async Task CheckInventoryAsync(OrderData order, CancellationToken ct)
    {
        await Task.Delay(TimeSpan.FromSeconds(1), ct);

        foreach (var item in order.Items)
        {
            if (item.Quantity > 1000)
            {
                throw new InvalidOperationException(
                    $"Insufficient inventory for SKU {item.Sku}");
            }
        }
    }

    private async Task ReserveItemsAsync(OrderData order, CancellationToken ct)
    {
        await Task.Delay(TimeSpan.FromMilliseconds(800), ct);
    }

    private static string GenerateTrackingNumber()
    {
        return $"TRACK-{Guid.NewGuid().ToString("N")[..10].ToUpperInvariant()}";
    }
}

public class OrderData
{
    public required string OrderId { get; init; }
    public required List<OrderItem> Items { get; init; }
}

public class OrderItem
{
    public required string Sku { get; init; }
    public required int Quantity { get; init; }
}

public class OrderResult
{
    public required string OrderId { get; init; }
    public required string Status { get; init; }
    public required string TrackingNumber { get; init; }
    public required DateTime EstimatedDelivery { get; init; }
}
```

---

## Security Implementation

### JWT Token Validation

```csharp
namespace StarGate.Api;

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.Authority = builder.Configuration["Auth:Authority"];
        options.Audience = builder.Configuration["Auth:Audience"];
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ClockSkew = TimeSpan.FromMinutes(5)
        };
    });

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("ProcessAccess", policy =>
        policy.RequireClaim("scope", "stargate:process:*"));
});
```

### Rate Limiting

```csharp
namespace StarGate.Api;

builder.Services.AddRateLimiter(options =>
{
    options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
    {
        var clientId = context.User.FindFirst("client_id")?.Value ?? "anonymous";

        return RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: clientId,
            factory: _ => new FixedWindowRateLimiterOptions
            {
                Window = TimeSpan.FromMinutes(1),
                PermitLimit = 10,
                QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                QueueLimit = 5
            });
    });

    options.OnRejected = async (context, ct) =>
    {
        context.HttpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;

        var response = new ErrorResponse(
            "RATE_LIMIT_EXCEEDED",
            "Too many requests. Please try again later.",
            DateTime.UtcNow);

        await context.HttpContext.Response.WriteAsJsonAsync(response, ct);
    };
});
```

---

## Resilience Patterns

### Polly Policies

```csharp
namespace StarGate.Infrastructure.Resilience;

using Polly;
using Polly.CircuitBreaker;
using Polly.Retry;

public static class ResiliencePolicies
{
    public static IAsyncPolicy<HttpResponseMessage> GetHttpRetryPolicy(ILogger logger)
    {
        return Policy
            .HandleResult<HttpResponseMessage>(r =>
                !r.IsSuccessStatusCode &&
                r.StatusCode != HttpStatusCode.BadRequest &&
                r.StatusCode != HttpStatusCode.Unauthorized &&
                r.StatusCode != HttpStatusCode.Forbidden)
            .Or<HttpRequestException>()
            .Or<TaskCanceledException>()
            .WaitAndRetryAsync(
                retryCount: 5,
                sleepDurationProvider: attempt => TimeSpan.FromSeconds(Math.Pow(2, attempt)),
                onRetry: (outcome, timespan, retryAttempt, context) =>
                {
                    logger.LogWarning(
                        "Retry {Attempt} after {Delay}ms due to {Reason}",
                        retryAttempt,
                        timespan.TotalMilliseconds,
                        outcome.Exception?.Message ?? outcome.Result.StatusCode.ToString());
                });
    }

    public static IAsyncPolicy<HttpResponseMessage> GetCircuitBreakerPolicy(ILogger logger)
    {
        return Policy
            .HandleResult<HttpResponseMessage>(r => (int)r.StatusCode >= 500)
            .Or<HttpRequestException>()
            .CircuitBreakerAsync(
                handledEventsAllowedBeforeBreaking: 10,
                durationOfBreak: TimeSpan.FromSeconds(30),
                onBreak: (outcome, timespan) =>
                {
                    logger.LogWarning(
                        "Circuit breaker opened for {Duration}s",
                        timespan.TotalSeconds);
                },
                onReset: () =>
                {
                    logger.LogInformation("Circuit breaker reset");
                },
                onHalfOpen: () =>
                {
                    logger.LogInformation("Circuit breaker half-open, testing...");
                });
    }

    public static IAsyncPolicy<HttpResponseMessage> GetCombinedPolicy(ILogger logger)
    {
        return Policy.WrapAsync(
            GetCircuitBreakerPolicy(logger),
            GetHttpRetryPolicy(logger));
    }
}
```

---

## Testing Strategy

### Unit Tests Structure

```csharp
namespace StarGate.Core.Tests.Services;

public class ProcessServiceTests
{
    private readonly Mock<IProcessRepository> _repositoryMock;
    private readonly Mock<IStateStore> _cacheMock;
    private readonly Mock<IMessageBroker> _brokerMock;
    private readonly ProcessService _sut;

    public ProcessServiceTests()
    {
        _repositoryMock = new Mock<IProcessRepository>();
        _cacheMock = new Mock<IStateStore>();
        _brokerMock = new Mock<IMessageBroker>();

        _sut = new ProcessService(
            _repositoryMock.Object,
            _cacheMock.Object,
            _brokerMock.Object,
            Mock.Of<ILogger<ProcessService>>());
    }

    [Fact]
    public async Task SubmitProcessAsync_ShouldCreateNewProcess_WhenNotDuplicate()
    {
        var clientId = "test-client";
        var request = new SubmitProcessRequest(
            "client-process-123",
            "order",
            new { orderId = "ORD-001" },
            "idempotency-key-123");

        _repositoryMock
            .Setup(r => r.GetByClientProcessIdAsync(
                clientId,
                request.ClientProcessId,
                default))
            .ReturnsAsync((Process?)null);

        var result = await _sut.SubmitProcessAsync(clientId, request);

        result.Should().NotBeNull();
        result.ProcessId.Should().NotBe(Guid.Empty);
        result.ClientProcessId.Should().Be(request.ClientProcessId);
        result.Status.Should().Be(ProcessStatus.Accepted);
        result.ClientId.Should().Be(clientId);

        _repositoryMock.Verify(
            r => r.CreateAsync(It.IsAny<Process>(), default),
            Times.Once);

        _cacheMock.Verify(
            c => c.SetProcessAsync(It.IsAny<Process>()),
            Times.Once);

        _brokerMock.Verify(
            b => b.PublishAsync(
                It.IsAny<string>(), 
                It.IsAny<Process>(), 
                It.IsAny<MessageProperties>(),
                default),
            Times.Once);
    }

    [Fact]
    public async Task SubmitProcessAsync_ShouldReturnExisting_WhenIdempotentRequest()
    {
        var clientId = "test-client";
        var existing = new Process
        {
            ProcessId = Guid.NewGuid(),
            ClientProcessId = "client-process-123",
            ProcessType = "order",
            ClientId = clientId,
            Status = ProcessStatus.Completed,
            Progress = 100,
            CreatedAt = DateTime.UtcNow.AddMinutes(-5),
            UpdatedAt = DateTime.UtcNow.AddMinutes(-2),
            IdempotencyKey = "existing-key",
            Retryable = false
        };

        var request = new SubmitProcessRequest(
            existing.ClientProcessId,
            "order",
            new { orderId = "ORD-001" },
            "new-idempotency-key");

        _repositoryMock
            .Setup(r => r.GetByClientProcessIdAsync(
                clientId,
                request.ClientProcessId,
                default))
            .ReturnsAsync(existing);

        var result = await _sut.SubmitProcessAsync(clientId, request);

        result.Should().Be(existing);

        _repositoryMock.Verify(
            r => r.CreateAsync(It.IsAny<Process>(), default),
            Times.Never);
    }

    [Fact]
    public async Task GetProcessByIdAsync_ShouldReturnFromCache_WhenCached()
    {
        var processId = Guid.NewGuid();
        var cachedProcess = new Process
        {
            ProcessId = processId,
            ClientProcessId = "client-123",
            ProcessType = "order",
            ClientId = "test-client",
            Status = ProcessStatus.Processing,
            Progress = 50,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            IdempotencyKey = "key",
            Retryable = true
        };

        _cacheMock
            .Setup(c => c.GetProcessAsync(processId))
            .ReturnsAsync(cachedProcess);

        var result = await _sut.GetProcessByIdAsync(processId);

        result.Should().Be(cachedProcess);

        _repositoryMock.Verify(
            r => r.GetByIdAsync(It.IsAny<Guid>(), default),
            Times.Never);
    }
}
```

---

## Development Roadmap

### Phase 1: Foundation (Week 1-2)

#### Sprint 1.1: Project Setup
- [ ] Create solution structure with all projects
- [ ] Configure `.editorconfig` and code analysis
- [ ] Setup CI/CD pipeline (GitHub Actions)
- [ ] Configure Docker Compose for local development
- [ ] Document setup instructions in README

#### Sprint 1.2: Domain Model
- [ ] Implement core domain entities (Process, ProcessStatus, ProcessError)
- [ ] Define repository interfaces (IProcessRepository, IStateStore)
- [ ] Define broker interfaces (IMessageBroker, IMessageConsumer)
- [ ] Define service interfaces (IProcessService, IProcessHandler)
- [ ] Write unit tests for domain model

### Phase 2: Data Layer (Week 3)

#### Sprint 2.1: MongoDB Implementation
- [ ] Implement MongoProcessRepository
- [ ] Create ProcessDocument and mapping logic
- [ ] Configure MongoDB indexes
- [ ] Write unit tests for repository
- [ ] Integration tests with MongoDB container

#### Sprint 2.2: Redis Cache
- [ ] Implement RedisStateStore
- [ ] Add cache invalidation logic
- [ ] Configure connection pooling
- [ ] Write unit tests for cache
- [ ] Integration tests with Redis container

### Phase 3: Message Broker (Week 4)

#### Sprint 3.1: RabbitMQ Implementation
- [ ] Implement RabbitMqBroker
- [ ] Implement RabbitMqConsumer
- [ ] Configure connection management and recovery
- [ ] Implement message serialization
- [ ] Write unit tests for broker

#### Sprint 3.2: Broker Integration
- [ ] Integration tests with RabbitMQ container
- [ ] Test message publishing and consumption
- [ ] Test error handling and requeue logic
- [ ] Document broker configuration

### Phase 4: API Gateway (Week 5-6)

#### Sprint 4.1: API Endpoints
- [ ] Implement ProcessEndpoints (POST, GET)
- [ ] Add request validation
- [ ] Implement global exception handler
- [ ] Add health check endpoints
- [ ] Write unit tests for endpoints

#### Sprint 4.2: Security
- [ ] Configure JWT authentication
- [ ] Implement authorization policies
- [ ] Add rate limiting
- [ ] Configure CORS
- [ ] Security testing

### Phase 5: Business Logic (Week 7)

#### Sprint 5.1: Process Service
- [ ] Implement ProcessService with GUID generation
- [ ] Add idempotency handling
- [ ] Integrate message broker publishing
- [ ] Implement process state transitions
- [ ] Write comprehensive unit tests

### Phase 6: Process Engine (Week 8-9)

#### Sprint 6.1: Background Worker
- [ ] Implement ProcessWorker with message consumption
- [ ] Add graceful shutdown handling
- [ ] Implement error handling and acknowledgment
- [ ] Add telemetry and logging
- [ ] Write unit tests

#### Sprint 6.2: Process Handlers
- [ ] Implement ProcessHandlerFactory
- [ ] Create OrderProcessHandler (example)
- [ ] Create ShippingProcessHandler (example)
- [ ] Add handler registration mechanism
- [ ] Write unit tests for each handler

### Phase 7: Resilience (Week 10)

#### Sprint 7.1: Polly Integration
- [ ] Implement retry policies
- [ ] Implement circuit breaker
- [ ] Add timeout policies
- [ ] Configure policies in DI container
- [ ] Write resilience tests

### Phase 8: Testing & Quality (Week 11-12)

#### Sprint 8.1: Integration Tests
- [ ] Write API integration tests
- [ ] Write end-to-end workflow tests
- [ ] Add test coverage reporting
- [ ] Achieve >80% coverage target

#### Sprint 8.2: Load Testing
- [ ] Create k6 load test scripts
- [ ] Run performance baseline tests
- [ ] Identify and fix bottlenecks
- [ ] Document performance characteristics

### Phase 9: Containerization (Week 13)

#### Sprint 9.1: Docker
- [ ] Create Dockerfile for API
- [ ] Create Dockerfile for Server
- [ ] Optimize image sizes
- [ ] Configure health checks in containers

#### Sprint 9.2: Orchestration
- [ ] Complete docker-compose.yml
- [ ] Add environment configuration
- [ ] Test local deployment
- [ ] Document deployment procedures

### Phase 10: Documentation & Handoff (Week 14)

#### Sprint 10.1: Documentation
- [ ] Complete API documentation (OpenAPI/Swagger)
- [ ] Write developer guide
- [ ] Create troubleshooting guide
- [ ] Document architecture decisions

#### Sprint 10.2: Final Review
- [ ] Code review and refactoring
- [ ] Security audit
- [ ] Performance review
- [ ] Prepare for production deployment

---

## Open Questions

### Technical Decisions Pending

1. **Process Handler Registration**
   - **Question:** Should handlers be auto-discovered or explicitly registered?
   - **Current:** Manual registration via DI
   - **Alternative:** Assembly scanning for IProcessHandler implementations
   - **Impact:** Affects handler development workflow and testability

2. **Telemetry Implementation**
   - **Question:** Use OpenTelemetry, Application Insights, or custom metrics?
   - **Current:** Planning for OpenTelemetry (vendor-neutral)
   - **Alternative:** Application Insights (Azure-specific, rich features)
   - **Impact:** Affects observability, debugging capabilities, and operational costs

### Business Logic Questions

1. **Process Timeout Policy**
   - **Question:** What happens when a process exceeds 10-minute timeout?
   - **Options:** Auto-fail with timeout error, continue with warning, configurable per process type
   - **Impact:** Affects resource management and user expectations

2. **Retry Strategy for Failed Processes**
   - **Question:** Should failed processes auto-retry? How many attempts?
   - **Current:** Manual retry via API
   - **Alternative:** Automatic retry with exponential backoff (3-5 attempts)
   - **Impact:** Affects failure handling, message broker configuration, and user experience

3. **Process Result Retention**
   - **Question:** How long should completed process results be retained?
   - **Options:** 24 hours (minimal), 7 days (recommended), 30 days (extended), indefinitely (with archival)
   - **Impact:** Database sizing, cleanup jobs, and audit capabilities

4. **Concurrent Process Limit**
   - **Question:** Should there be a limit on concurrent processes per client?
   - **Options:** No limit (trust rate limiting), per-client queue limit (e.g., 100 pending), per-client active processing limit (e.g., 10 concurrent)
   - **Impact:** Fair resource distribution and system stability

---

## Related Documents

- [Client SDK Technical Analysis](./CLIENT-TECHNICAL-ANALYSIS.md)
- [Coding Conventions](./CODING-CONVENTIONS.md)
- [Git Flow](./GIT-FLOW.md)
- [Pull Request Process](./PULL-REQUEST-PROCESS.md)
- [Release Process](./RELEASE-PROCESS.md)

---

**Document Owner:** Development Team
