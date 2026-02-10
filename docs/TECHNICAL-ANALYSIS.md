# Technical Analysis - StarGate Software Development

**Document Version:** 1.0  
**Last Updated:** 2026-02-10  
**Status:** Draft - In Progress

---

## Table of Contents

1. [Executive Summary](#executive-summary)
2. [Software Architecture](#software-architecture)
3. [Solution Structure](#solution-structure)
4. [Domain Model](#domain-model)
5. [API Design](#api-design)
6. [Data Layer](#data-layer)
7. [Business Logic](#business-logic)
8. [Process Handlers](#process-handlers)
9. [Client SDK](#client-sdk)
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
- Process orchestration engine
- Client SDK library
- Authentication/Authorization logic
- Resilience patterns (retry, circuit breaker)
- Unit and integration testing
- Docker containerization
- CI/CD pipeline configuration

**OUT OF SCOPE:**
- Azure resource provisioning (VMs, VNets, etc.)
- Azure AD/Identity Provider configuration
- Client application integration
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
│  │  (Public Gateway)  │──────│  (Process Engine)  │        │
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
│              │  - Queue (In-Memory) │                       │
│              └─────────────────────┘                        │
│                                                             │
│  ┌────────────────────┐      ┌────────────────────┐        │
│  │ StarGate.Contracts │      │  StarGate.Client   │        │
│  │  - DTOs            │      │  - SDK Library     │        │
│  │  - Public APIs     │      │  - Polling Logic   │        │
│  └────────────────────┘      │  - Offline Queue   │        │
│                              └────────────────────┘        │
└─────────────────────────────────────────────────────────────┘
```

### Technology Stack

| Component | Technology | Version |
|-----------|-----------|---------|
| Runtime | .NET | 8.0 |
| API Framework | ASP.NET Core Minimal APIs | 8.0 |
| Cache | StackExchange.Redis | 2.x |
| Database | MongoDB.Driver | 2.x |
| Authentication | Microsoft.AspNetCore.Authentication.JwtBearer | 8.0 |
| Resilience | Polly | 8.x |
| Logging | Serilog | 3.x |
| Testing | xUnit + FluentAssertions + Moq | Latest |
| Containerization | Docker | Latest |

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
│   │   │   └── IProcessQueue.cs
│   │   ├── Services/                    # Business services
│   │   │   ├── ProcessService.cs
│   │   │   └── ProcessIdGenerator.cs
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
│   │   ├── Queue/                       # Queue implementations
│   │   │   ├── InMemoryProcessQueue.cs
│   │   │   └── ProcessQueueItem.cs
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
│   ├── StarGate.Contracts/              # Shared contracts
│   │   ├── Requests/                    # Request DTOs
│   │   │   └── SubmitProcessRequest.cs
│   │   ├── Responses/                   # Response DTOs
│   │   │   ├── SubmitProcessResponse.cs
│   │   │   ├── ProcessStatusResponse.cs
│   │   │   └── ErrorResponse.cs
│   │   └── Models/                      # Shared models
│   │       └── ProcessData.cs
│   │
│   └── StarGate.Client/                 # Client SDK
│       ├── StarGateClient.cs            # Main client class
│       ├── StarGateClientOptions.cs     # Configuration
│       ├── Auth/                        # Authentication
│       │   ├── ITokenProvider.cs
│       │   └── OAuth2TokenProvider.cs
│       ├── Polling/                     # Polling strategy
│       │   └── ProcessPoller.cs
│       └── Queue/                       # Offline queue
│           ├── IOfflineQueue.cs
│           └── FileBasedOfflineQueue.cs
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
│   │   └── Caching/
│   ├── StarGate.Server.Tests/          # Server unit tests
│   │   ├── Workers/
│   │   └── Handlers/
│   ├── StarGate.Client.Tests/          # Client SDK tests
│   │   ├── Polling/
│   │   └── Queue/
│   └── StarGate.Integration.Tests/     # Integration tests
│       ├── ApiIntegrationTests.cs
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
    /// Server-generated unique identifier (e.g., PROC-20260210-00001).
    /// </summary>
    public required string ProcessId { get; init; }

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
  "processId": "PROC-20260210-00001",
  "clientProcessId": "client-uuid-123",
  "processType": "order",
  "status": "accepted",
  "statusUrl": "/api/stargate/processes/PROC-20260210-00001",
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

        group.MapGet("{processId}", GetProcessStatus)
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
        string processId,
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
  "processId": "PROC-20260210-00001",
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
  "processId": "PROC-20260210-00001",
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
    [BsonRepresentation(BsonType.ObjectId)]
    public string? Id { get; set; }

    [BsonElement("processId")]
    [BsonRequired]
    public required string ProcessId { get; set; }

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

    public async Task<Process?> GetByIdAsync(string processId, CancellationToken ct = default)
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

    public async Task<Process?> GetProcessAsync(string processId)
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

    public async Task InvalidateAsync(string processId)
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

    private static string GetKey(string processId) => $"{KeyPrefix}{processId}";
}
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
    private readonly IProcessQueue _queue;
    private readonly IProcessIdGenerator _idGenerator;
    private readonly ILogger<ProcessService> _logger;

    public ProcessService(
        IProcessRepository repository,
        IStateStore cache,
        IProcessQueue queue,
        IProcessIdGenerator idGenerator,
        ILogger<ProcessService> logger)
    {
        _repository = repository;
        _cache = cache;
        _queue = queue;
        _idGenerator = idGenerator;
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

        // Check for duplicate using client process ID
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

        // Generate server process ID
        var processId = await _idGenerator.GenerateAsync(ct);

        // Create process entity
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

        // Persist to database
        await _repository.CreateAsync(process, ct);

        // Cache for fast queries
        await _cache.SetProcessAsync(process);

        // Enqueue for background processing
        await _queue.EnqueueAsync(process, ct);

        _logger.LogInformation(
            "Process {ProcessId} submitted successfully",
            processId);

        return process;
    }

    public async Task<Process?> GetProcessByIdAsync(
        string processId,
        CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(processId);

        // Try cache first (sub-millisecond)
        var cached = await _cache.GetProcessAsync(processId);
        if (cached is not null)
        {
            _logger.LogDebug("Process {ProcessId} retrieved from cache", processId);
            return cached;
        }

        // Fallback to database
        var process = await _repository.GetByIdAsync(processId, ct);

        if (process is not null)
        {
            // Populate cache for next time
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
        string processId,
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

### Process ID Generator

```csharp
namespace StarGate.Core.Services;

public interface IProcessIdGenerator
{
    Task<string> GenerateAsync(CancellationToken ct = default);
}

public class ProcessIdGenerator : IProcessIdGenerator
{
    private long _counter;
    private readonly object _lock = new();

    public Task<string> GenerateAsync(CancellationToken ct = default)
    {
        long currentCounter;

        lock (_lock)
        {
            _counter++;
            currentCounter = _counter;
        }

        var timestamp = DateTime.UtcNow.ToString("yyyyMMdd");
        var paddedCounter = currentCounter.ToString("D5");
        var processId = $"PROC-{timestamp}-{paddedCounter}";

        return Task.FromResult(processId);
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

        // Deserialize order data
        var orderData = JsonSerializer.Deserialize<OrderData>(
            process.Data!.ToString()!);

        if (orderData is null)
        {
            throw new InvalidOperationException("Invalid order data");
        }

        // Step 1: Validate order
        await ValidateOrderAsync(orderData, ct);

        // Step 2: Check inventory
        await CheckInventoryAsync(orderData, ct);

        // Step 3: Reserve items
        await ReserveItemsAsync(orderData, ct);

        // Step 4: Process payment (simulated)
        await Task.Delay(TimeSpan.FromSeconds(2), ct);

        // Step 5: Create shipping label
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
        // Business logic for validation
        await Task.Delay(TimeSpan.FromMilliseconds(500), ct);

        if (order.Items.Count == 0)
        {
            throw new InvalidOperationException("Order must contain at least one item");
        }
    }

    private async Task CheckInventoryAsync(OrderData order, CancellationToken ct)
    {
        // Business logic for inventory check
        await Task.Delay(TimeSpan.FromSeconds(1), ct);

        foreach (var item in order.Items)
        {
            // Simulate inventory check
            if (item.Quantity > 1000)
            {
                throw new InvalidOperationException(
                    $"Insufficient inventory for SKU {item.Sku}");
            }
        }
    }

    private async Task ReserveItemsAsync(OrderData order, CancellationToken ct)
    {
        // Business logic for item reservation
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

## Client SDK

### StarGate Client

```csharp
namespace StarGate.Client;

public interface IStarGateClient
{
    Task<ProcessSubmissionResult> SubmitProcessAsync<TData>(
        string clientProcessId,
        string processType,
        TData data,
        CancellationToken ct = default);

    Task<Process?> GetProcessStatusAsync(
        string processId,
        CancellationToken ct = default);

    Task<Process> WaitForCompletionAsync(
        string processId,
        CancellationToken ct = default);
}

public class StarGateClient : IStarGateClient
{
    private readonly HttpClient _httpClient;
    private readonly ITokenProvider _tokenProvider;
    private readonly IOfflineQueue _offlineQueue;
    private readonly ProcessPoller _poller;
    private readonly ILogger<StarGateClient> _logger;

    public StarGateClient(
        HttpClient httpClient,
        ITokenProvider tokenProvider,
        IOfflineQueue offlineQueue,
        ILogger<StarGateClient> logger)
    {
        _httpClient = httpClient;
        _tokenProvider = tokenProvider;
        _offlineQueue = offlineQueue;
        _poller = new ProcessPoller(this, logger);
        _logger = logger;
    }

    public async Task<ProcessSubmissionResult> SubmitProcessAsync<TData>(
        string clientProcessId,
        string processType,
        TData data,
        CancellationToken ct = default)
    {
        var request = new SubmitProcessRequest(
            clientProcessId,
            processType,
            data,
            Guid.NewGuid().ToString()); // Generate idempotency key

        try
        {
            // Get OAuth token for this process type
            var token = await _tokenProvider.GetTokenAsync(processType, ct);

            _httpClient.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", token);

            // Submit request
            var response = await _httpClient.PostAsJsonAsync(
                "/api/stargate/processes",
                request,
                ct);

            if (response.StatusCode == HttpStatusCode.TooManyRequests)
            {
                _logger.LogWarning(
                    "Rate limit exceeded for process submission");
                throw new InvalidOperationException("Rate limit exceeded");
            }

            response.EnsureSuccessStatusCode();

            var result = await response.Content
                .ReadFromJsonAsync<SubmitProcessResponse>(ct);

            _logger.LogInformation(
                "Process {ProcessId} submitted successfully",
                result!.ProcessId);

            return new ProcessSubmissionResult(
                result.ProcessId,
                result.ClientProcessId,
                result.Status);
        }
        catch (HttpRequestException ex)
        {
            _logger.LogWarning(
                ex,
                "Failed to submit process, enqueueing offline");

            // Enqueue for later retry
            await _offlineQueue.EnqueueAsync(request, ct);

            return new ProcessSubmissionResult(
                null,
                clientProcessId,
                "queued_offline");
        }
    }

    public async Task<Process?> GetProcessStatusAsync(
        string processId,
        CancellationToken ct = default)
    {
        try
        {
            var token = await _tokenProvider.GetTokenAsync(ct: ct);
            _httpClient.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", token);

            var response = await _httpClient.GetAsync(
                $"/api/stargate/processes/{processId}",
                ct);

            if (response.StatusCode == HttpStatusCode.NotFound)
            {
                return null;
            }

            response.EnsureSuccessStatusCode();

            return await response.Content.ReadFromJsonAsync<Process>(ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Error retrieving process status for {ProcessId}",
                processId);
            return null;
        }
    }

    public async Task<Process> WaitForCompletionAsync(
        string processId,
        CancellationToken ct = default)
    {
        return await _poller.WaitForCompletionAsync(processId, ct);
    }
}
```

### Adaptive Polling Strategy

```csharp
namespace StarGate.Client.Polling;

public class ProcessPoller
{
    private readonly IStarGateClient _client;
    private readonly ILogger _logger;
    private const int Phase1DurationMinutes = 2;
    private const int Phase1IntervalSeconds = 30;
    private const int Phase2IntervalSeconds = 60;
    private const int TimeoutMinutes = 10;

    public ProcessPoller(IStarGateClient client, ILogger logger)
    {
        _client = client;
        _logger = logger;
    }

    public async Task<Process> WaitForCompletionAsync(
        string processId,
        CancellationToken ct = default)
    {
        var startTime = DateTime.UtcNow;

        _logger.LogInformation(
            "Starting adaptive polling for process {ProcessId}",
            processId);

        while (!ct.IsCancellationRequested)
        {
            // Poll for current status
            var process = await _client.GetProcessStatusAsync(processId, ct);

            if (process is null)
            {
                throw new InvalidOperationException(
                    $"Process {processId} not found");
            }

            // Check if completed
            if (process.Status is ProcessStatus.Completed or ProcessStatus.Failed)
            {
                var duration = DateTime.UtcNow - startTime;
                _logger.LogInformation(
                    "Process {ProcessId} completed with status {Status} after {Duration}",
                    processId,
                    process.Status,
                    duration);

                return process;
            }

            // Calculate elapsed time
            var elapsed = DateTime.UtcNow - startTime;
            var elapsedMinutes = elapsed.TotalMinutes;

            // Adaptive delay
            TimeSpan delay;
            if (elapsedMinutes < Phase1DurationMinutes)
            {
                // Phase 1: Aggressive polling (30s)
                delay = TimeSpan.FromSeconds(Phase1IntervalSeconds);
            }
            else
            {
                // Phase 2: Conservative polling (60s)
                delay = TimeSpan.FromSeconds(Phase2IntervalSeconds);
            }

            _logger.LogDebug(
                "Process {ProcessId} at {Progress}% ({Status}), waiting {Delay}s",
                processId,
                process.Progress,
                process.Status,
                delay.TotalSeconds);

            await Task.Delay(delay, ct);

            // Timeout warning
            if (elapsedMinutes > TimeoutMinutes)
            {
                _logger.LogWarning(
                    "Process {ProcessId} exceeded timeout ({Timeout} minutes)",
                    processId,
                    TimeoutMinutes);
            }
        }

        throw new OperationCanceledException();
    }
}
```

---

## Security Implementation

### JWT Token Validation

```csharp
namespace StarGate.Api;

// In Program.cs
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

// In Program.cs
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
    private readonly Mock<IProcessQueue> _queueMock;
    private readonly Mock<IProcessIdGenerator> _idGeneratorMock;
    private readonly ProcessService _sut;

    public ProcessServiceTests()
    {
        _repositoryMock = new Mock<IProcessRepository>();
        _cacheMock = new Mock<IStateStore>();
        _queueMock = new Mock<IProcessQueue>();
        _idGeneratorMock = new Mock<IProcessIdGenerator>();

        _sut = new ProcessService(
            _repositoryMock.Object,
            _cacheMock.Object,
            _queueMock.Object,
            _idGeneratorMock.Object,
            Mock.Of<ILogger<ProcessService>>());
    }

    [Fact]
    public async Task SubmitProcessAsync_ShouldCreateNewProcess_WhenNotDuplicate()
    {
        // Arrange
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

        _idGeneratorMock
            .Setup(g => g.GenerateAsync(default))
            .ReturnsAsync("PROC-20260210-00001");

        // Act
        var result = await _sut.SubmitProcessAsync(clientId, request);

        // Assert
        result.Should().NotBeNull();
        result.ProcessId.Should().Be("PROC-20260210-00001");
        result.ClientProcessId.Should().Be(request.ClientProcessId);
        result.Status.Should().Be(ProcessStatus.Accepted);
        result.ClientId.Should().Be(clientId);

        _repositoryMock.Verify(
            r => r.CreateAsync(It.IsAny<Process>(), default),
            Times.Once);

        _cacheMock.Verify(
            c => c.SetProcessAsync(It.IsAny<Process>()),
            Times.Once);

        _queueMock.Verify(
            q => q.EnqueueAsync(It.IsAny<Process>(), default),
            Times.Once);
    }

    [Fact]
    public async Task SubmitProcessAsync_ShouldReturnExisting_WhenIdempotentRequest()
    {
        // Arrange
        var clientId = "test-client";
        var existing = new Process
        {
            ProcessId = "PROC-EXISTING",
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

        // Act
        var result = await _sut.SubmitProcessAsync(clientId, request);

        // Assert
        result.Should().Be(existing);

        _repositoryMock.Verify(
            r => r.CreateAsync(It.IsAny<Process>(), default),
            Times.Never);

        _idGeneratorMock.Verify(
            g => g.GenerateAsync(default),
            Times.Never);
    }

    [Fact]
    public async Task GetProcessByIdAsync_ShouldReturnFromCache_WhenCached()
    {
        // Arrange
        var processId = "PROC-20260210-00001";
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

        // Act
        var result = await _sut.GetProcessByIdAsync(processId);

        // Assert
        result.Should().Be(cachedProcess);

        _repositoryMock.Verify(
            r => r.GetByIdAsync(It.IsAny<string>(), default),
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
- [ ] Define repository interfaces (IProcessRepository, IStateStore, IProcessQueue)
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

### Phase 3: API Gateway (Week 4-5)

#### Sprint 3.1: API Endpoints
- [ ] Implement ProcessEndpoints (POST, GET)
- [ ] Add request validation
- [ ] Implement global exception handler
- [ ] Add health check endpoints
- [ ] Write unit tests for endpoints

#### Sprint 3.2: Security
- [ ] Configure JWT authentication
- [ ] Implement authorization policies
- [ ] Add rate limiting
- [ ] Configure CORS
- [ ] Security testing

### Phase 4: Business Logic (Week 6)

#### Sprint 4.1: Process Service
- [ ] Implement ProcessService
- [ ] Implement ProcessIdGenerator
- [ ] Add idempotency handling
- [ ] Implement process state transitions
- [ ] Write comprehensive unit tests

#### Sprint 4.2: Queue Implementation
- [ ] Implement in-memory queue
- [ ] Add queue monitoring
- [ ] Implement backlog handling
- [ ] Write unit tests

### Phase 5: Process Engine (Week 7-8)

#### Sprint 5.1: Background Worker
- [ ] Implement ProcessWorker (BackgroundService)
- [ ] Add graceful shutdown handling
- [ ] Implement error handling and retry logic
- [ ] Add telemetry and logging
- [ ] Write unit tests

#### Sprint 5.2: Process Handlers
- [ ] Implement ProcessHandlerFactory
- [ ] Create OrderProcessHandler (example)
- [ ] Create ShippingProcessHandler (example)
- [ ] Add handler registration mechanism
- [ ] Write unit tests for each handler

### Phase 6: Client SDK (Week 9)

#### Sprint 6.1: Core Client
- [ ] Implement StarGateClient
- [ ] Add token provider interface
- [ ] Implement OAuth2TokenProvider
- [ ] Write unit tests

#### Sprint 6.2: Polling & Offline Queue
- [ ] Implement ProcessPoller with adaptive strategy
- [ ] Implement FileBasedOfflineQueue
- [ ] Add offline queue flush mechanism
- [ ] Write unit tests

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

### Technical Decisions Needed

1. **Process Queue Implementation**
   - **Question:** Should we use in-memory queue or external message broker (RabbitMQ, Azure Service Bus)?
   - **Current:** In-memory queue (simple, no external dependencies)
   - **Alternative:** External broker (better scalability, durability)
   - **Decision:** TBD based on scale requirements

2. **MongoDB vs CosmosDB**
   - **Question:** Use MongoDB self-hosted or Azure CosmosDB (MongoDB API)?
   - **Current:** MongoDB (more control, lower cost)
   - **Alternative:** CosmosDB (managed, auto-scaling)
   - **Decision:** TBD based on operational requirements

3. **Token Storage in Client SDK**
   - **Question:** Where should OAuth tokens be stored in client?
   - **Options:** In-memory, encrypted file, secure credential store
   - **Decision:** TBD - depends on client deployment environment

4. **Process Handler Registration**
   - **Question:** Should handlers be auto-discovered or explicitly registered?
   - **Current:** Manual registration via DI
   - **Alternative:** Assembly scanning for IProcessHandler implementations
   - **Decision:** TBD

5. **Telemetry Implementation**
   - **Question:** Use OpenTelemetry, Application Insights, or custom metrics?
   - **Current:** Planning for OpenTelemetry (vendor-neutral)
   - **Decision:** TBD

### Business Logic Questions

1. **Process Timeout Policy**
   - **Question:** What happens when a process exceeds 10-minute timeout?
   - **Options:** Auto-fail, continue with warning, configurable per process type
   - **Decision:** TBD

2. **Retry Strategy for Failed Processes**
   - **Question:** Should failed processes auto-retry? How many attempts?
   - **Current:** Manual retry via API
   - **Alternative:** Automatic retry with exponential backoff
   - **Decision:** TBD

3. **Process Result Retention**
   - **Question:** How long should completed process results be retained?
   - **Options:** 24 hours, 7 days, 30 days, indefinitely
   - **Decision:** TBD - impacts storage costs

4. **Concurrent Process Limit**
   - **Question:** Should there be a limit on concurrent processes per client?
   - **Decision:** TBD - impacts queue design

---

## Next Steps

1. **Review this document** with team and stakeholders
2. **Resolve open questions** and document decisions
3. **Refine estimates** for each sprint
4. **Create detailed task breakdown** in project management tool
5. **Begin Phase 1: Foundation** development
6. **Schedule weekly review** meetings to track progress

---

**Document Status:** Draft - Awaiting Review  
**Next Review:** TBD  
**Owner:** Development Team
