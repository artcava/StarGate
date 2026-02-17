# MongoDB Persistence Layer

## Overview

This directory contains the MongoDB implementation of the StarGate persistence layer, following Clean Architecture principles where the Core layer defines interfaces and the Infrastructure layer provides concrete implementations.

## Components

### Repository Implementation

#### **MongoProcessRepository**
MongoDB implementation of `IProcessRepository` interface.

**Key Features:**
- Full CRUD operations for Process entities
- Comprehensive error handling for duplicate keys
- Optimized queries with proper indexing
- Detailed logging for debugging and monitoring
- Idempotency support via unique constraints

**Methods:**
- `CreateAsync`: Insert new process with duplicate detection
- `GetByIdAsync`: Retrieve by ProcessId (primary key)
- `GetByClientProcessIdAsync`: Retrieve by composite key (ClientId + ClientProcessId)
- `UpdateAsync`: Replace existing document
- `GetByStatusAsync`: Query by status with FIFO ordering
- `GetByClientIdAsync`: Query by client with pagination
- `CountActiveProcessesAsync`: Count active processes for concurrency limits

### Data Models

#### **ProcessDocument**
BSON document representation of `Process` domain entity.

**Attributes:**
- `[BsonId]`: ProcessId as primary key
- `[BsonElement]`: Custom field names for MongoDB
- `[BsonRequired]`: Mandatory fields validation
- `[BsonGuidRepresentation]`: GUID serialization format

#### **ProcessMapper**
Bidirectional mapping between `Process` ↔ `ProcessDocument`.

**Responsibilities:**
- Convert domain entities to MongoDB documents
- Parse MongoDB documents to domain entities
- Handle JSON/BSON serialization for nested objects
- Enum parsing with validation

### Configuration

#### **MongoDbOptions**
Configuration binding for `appsettings.json`.

```json
{
  "MongoDB": {
    "ConnectionString": "mongodb://localhost:27017",
    "DatabaseName": "stargate",
    "CreateIndexesOnStartup": true,
    "ConnectionTimeoutMs": 30000,
    "ServerSelectionTimeoutMs": 30000
  }
}
```

#### **DependencyInjection**
Service registration following ASP.NET Core patterns.

**Registrations:**
- `IMongoClient`: Singleton (connection pooling)
- `IMongoDatabase`: Singleton (database instance)
- `IProcessRepository`: Scoped (per-request)
- `MongoDbHealthCheck`: Health check registration
- `MongoDbIndexCreationService`: Hosted service for startup tasks

### Indexes

#### **MongoDbIndexes**
Automatic index creation for optimal performance.

**Created Indexes:**

1. **idx_processId** (Unique)
   - Fields: `ProcessId`
   - Purpose: Primary key lookup
   - Used by: `GetByIdAsync`

2. **idx_clientId_clientProcessId** (Unique)
   - Fields: `ClientId` + `ClientProcessId`
   - Purpose: Idempotency checks
   - Used by: `GetByClientProcessIdAsync`

3. **idx_status**
   - Fields: `Status`
   - Purpose: Status-based queries
   - Used by: `GetByStatusAsync`

4. **idx_createdAt**
   - Fields: `CreatedAt`
   - Purpose: Temporal queries and ordering
   - Used by: All list queries

5. **idx_idempotencyKey** (Unique)
   - Fields: `IdempotencyKey`
   - Purpose: Prevent duplicate submissions
   - Used by: `CreateAsync`

6. **idx_clientId_processType_status**
   - Fields: `ClientId` + `ProcessType` + `Status`
   - Purpose: Concurrency limit enforcement
   - Used by: `CountActiveProcessesAsync`

**Index Creation:**
- Automatic on startup via `MongoDbIndexCreationService`
- Configurable with `CreateIndexesOnStartup` option
- Idempotent (safe to run multiple times)
- Handles existing indexes gracefully

### Health Checks

#### **MongoDbHealthCheck**
Implements `IHealthCheck` for monitoring.

**Functionality:**
- Executes MongoDB ping command
- Returns connection status
- Provides detailed diagnostics
- Supports Kubernetes readiness probes

**Health Check Endpoint:**
```bash
curl http://localhost:5000/health
```

**Response:**
```json
{
  "status": "Healthy",
  "results": {
    "mongodb": {
      "status": "Healthy",
      "description": "MongoDB is responsive",
      "data": {
        "database": "stargate",
        "connected": true,
        "timestamp": "2026-02-12T10:56:00Z"
      }
    }
  }
}
```

## Usage

### 1. Configuration

Add MongoDB configuration to `appsettings.json`:

```json
{
  "MongoDB": {
    "ConnectionString": "mongodb://localhost:27017",
    "DatabaseName": "stargate"
  }
}
```

### 2. Service Registration

In `Program.cs` or `Startup.cs`:

```csharp
builder.Services.AddInfrastructure(builder.Configuration);
```

### 3. Dependency Injection

Inject `IProcessRepository` in your services:

```csharp
public class ProcessService
{
    private readonly IProcessRepository _repository;

    public ProcessService(IProcessRepository repository)
    {
        _repository = repository;
    }

    public async Task<Process> CreateProcessAsync(Process process)
    {
        return await _repository.CreateAsync(process);
    }
}
```

## Error Handling

### Duplicate Key Violations

The repository detects which unique constraint was violated:

```csharp
try
{
    await repository.CreateAsync(process);
}
catch (InvalidOperationException ex) when (ex.Message.Contains("ProcessId"))
{
    // ProcessId already exists
}
catch (InvalidOperationException ex) when (ex.Message.Contains("IdempotencyKey"))
{
    // Idempotency key already used
}
catch (InvalidOperationException ex) when (ex.Message.Contains("ClientId"))
{
    // ClientId + ClientProcessId combination already exists
}
```

### Connection Failures

```csharp
try
{
    var process = await repository.GetByIdAsync(processId);
}
catch (MongoConnectionException ex)
{
    // MongoDB connection failed - handle gracefully
}
catch (TimeoutException ex)
{
    // Operation timed out - retry or fallback
}
```

## Performance Optimization

### Connection Pooling

- `IMongoClient` registered as **Singleton**
- MongoDB driver handles connection pooling automatically
- Reuses connections across requests
- Configurable timeouts for resilience

### Query Optimization

- All queries use indexed fields
- Results limited to prevent memory issues (max 1000)
- Projection can be added for large documents
- Pagination supported via `skip` and `limit`

### Index Strategy

- Unique indexes enforce data integrity at database level
- Composite indexes support multi-field queries
- Index creation is idempotent and safe
- Covered queries when possible (index-only)

## Testing

### Manual Testing with MongoDB

```bash
# Start MongoDB container
docker run -d -p 27017:27017 --name mongodb mongo:7.0

# Run application
dotnet run --project src/StarGate.Api

# Verify indexes created
mongosh stargate --eval "db.processes.getIndexes()"

# Check health endpoint
curl http://localhost:5000/health
```

### Unit Tests

Unit tests with mocks are in `tests/StarGate.Infrastructure.Tests/`.

See issue #22 for unit test implementation.

### Integration Tests

Integration tests with real MongoDB container are in `tests/StarGate.Integration.Tests/`.

See issue #23 for integration test implementation.

## Architecture Decisions

### Why MongoDB?

- **Flexible schema**: Process.Data can store arbitrary JSON
- **Horizontal scalability**: Sharding support for future growth
- **Rich querying**: Complex filters on nested documents
- **Performance**: Fast reads with proper indexing
- **Operational maturity**: Battle-tested in production

### Repository Pattern

- **Abstraction**: Core defines `IProcessRepository` interface
- **Implementation**: Infrastructure provides MongoDB implementation
- **Testability**: Easy to mock for unit tests
- **Flexibility**: Can swap MongoDB with other databases
- **SOLID**: Follows Dependency Inversion Principle

### Document Mapping

- **Separation of Concerns**: Domain model vs. Persistence model
- **Type Safety**: Strong typing with records
- **Validation**: Enum parsing with error handling
- **Flexibility**: Easy to add fields without breaking changes

## Troubleshooting

### Indexes not created

**Symptom:** Slow queries, no indexes visible in MongoDB

**Solution:**
```bash
# Check logs for errors
dotnet run --project src/StarGate.Api | grep "MongoDB index"

# Manually create indexes
mongosh stargate --eval "load('scripts/create-indexes.js')"
```

### Connection timeout

**Symptom:** `MongoConnectionException: Timeout`

**Solutions:**
- Verify MongoDB is running: `docker ps`
- Check connection string in appsettings.json
- Increase `ConnectionTimeoutMs` in configuration
- Check network/firewall settings

### Duplicate key error

**Symptom:** `MongoWriteException: E11000 duplicate key error`

**Solutions:**
- Check if process already exists before creating
- Use `GetByClientProcessIdAsync` for idempotency checks
- Generate unique `IdempotencyKey` for each submission
- Review application logic for race conditions

## References

- [MongoDB .NET Driver Documentation](https://www.mongodb.com/docs/drivers/csharp/current/)
- [TECHNICAL-ANALYSIS.md](../../../docs/TECHNICAL-ANALYSIS.md)
- [CODING-CONVENTIONS.md](../../../docs/CODING-CONVENTIONS.md)
- Issue #19: MongoProcessRepository Implementation
- Issue #21: MongoDB Documents and Mapping Logic
