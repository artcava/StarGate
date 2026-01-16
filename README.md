# StarGate - Integration Architecture & Solution Overview

---

## TABLE OF CONTENTS

1. [The Problem](#section-1%3A-the-problem)
2. [The Solution (StarGate)](#section-2%3A-the-solution-(stargate))
3. [Architecture - Synchronous Pull Model](#section-3%3A-architecture---synchronous-pull-model)
4. [Architecture - System Components](#section-4%3A-architecture---system-components)
5. [Extensibility](#section-5%3A-extensibility)
6. [Security & Compliance](#section-6%3A-security-%26-compliance)
7. [Implementation Approach](#section-7%3A-implementation-approach)
8. [Key Benefits](#section-8%3A-key-benefits)
9. [Assumptions & Constraints](#section-9%3A-assumptions-%26-constraints)
10. [Success Metrics](#section-10%3A-success-metrics)
11. [API Specifications](#section-11%3A-api-specifications)
12. [Error Handling & Resilience](#section-12%3A-error-handling-%26-resilience)
13. [Monitoring & Observability](#section-13%3A-monitoring-%26-observability)

---

## SECTION 1: THE PROBLEM

### Current Situation

The organization manages two **completely isolated** systems:

```
┌──────────────────────────────┐       ┌─────────────────────────┐
│   SERVER                     │       │  CLIENT                 │
│   - Azure Private            │       │  - Supplier or Customer │
│   - Not publicly exposed     │   X   │  - Digital Twin         │
│   - Critical Business Logic  │       │  - Controls             │
└──────────────────────────────┘       └─────────────────────────┘

                           NO DIRECT COMMUNICATION
                        ❌ Cannot talk to each other
                        ❌ No data flow
                        ❌ Manual processes required
```

### Business Impact

| Aspect | Current State |
|--------|---------------|
| **Process Automation** | 100% manual |
| **Data Integration** | No synchronization - errors, duplicates |
| **Scalability** | Limited by manual processes (not scalable beyond 20-30 customers) |
| **Time-to-Enable** | 3-6 months to enable each existing customer for new centralized processes |
| **Process Visibility** | No real-time status or tracking |

### What "Time-to-Enable" Means

Every time you want to expose a **new process or workflow** from Server to an existing Client instance:

- Analyze customer/supplier-specific process flows and data formats
- Develop custom integrations (connectors, data mappers, orchestration logic)
- Configure security, networking, authentication per customer
- Test end-to-end on their environment
- Deploy, stabilize, and provide support

This 3-6 month cycle repeats **per customer/supplier** for **each new process type** you want to enable.

### Current Constraints

- ❌ Customers/Supplier **CANNOT** access any central service
- ❌ Central system **MUST REMAIN** private (compliance, security, data protection)
- ❌ No "public gateway" exists today
- ❌ Each integration requires manual development

---

## SECTION 2: THE SOLUTION (STARGATE)

### Key Concept

**StarGate is a secure hybrid bridge** built directly into Customer/Supplier **Client** as a standard component. It enables the local instance to initiate and monitor processes with the central system while maintaining complete isolation of **Server**.

``` mermaid
graph TD 
SERVER["🔒 SERVER<br/>- Remains completely<br/>isolated and secure"] 
STARGATE["⚡ STARGATE<br/>AZURE PUBLIC<br/>━━━━━━━━━━━━━━<br/>✓ Authentication<br/>✓ Authorization<br/>✓ Request/Response<br/>✓ State Management"] 
CLIENT1["👥 CLIENT Instance 1<br/>Customer/Supplier Local<br/>━━━━━━━━━━━━━━<br/>📊 Process Control<br/>⚙️ Local Process Engine<br/>🔌 StarGate Client Built-in"] 
CLIENT2["👥 CLIENT Instance 2<br/>Customer/Supplier Local<br/>━━━━━━━━━━━━━━<br/>📊 Process Control<br/>⚙️ Local Process Engine<br/>🔌 StarGate Client Built-in"] 
CLIENT3["👥 CLIENT Instance N<br/>Customer/Supplier Local<br/>━━━━━━━━━━━━━━<br/>📊 Process Control<br/>⚙️ Local Process Engine<br/>🔌 StarGate Client Built-in"] 

SERVER -->|"Isolated & Secure"| STARGATE 
STARGATE -->|"Auth + Control"| CLIENT1 
STARGATE -->|"Auth + Control"| CLIENT2 
STARGATE -->|"Auth + Control"| CLIENT3 

style SERVER fill:#ff6b6b,stroke:#c92a2a,color:#fff 
style STARGATE fill:#4dabf7,stroke:#1971c2,color:#fff 
style CLIENT1 fill:#51cf66,stroke:#2b8a3e,color:#fff 
style CLIENT2 fill:#51cf66,stroke:#2b8a3e,color:#fff 
style CLIENT3 fill:#51cf66,stroke:#2b8a3e,color:#fff 
```

### The 4 Pillars of StarGate

#### **1. Controlled Exposure**
- Public service on Azure (`StarGate.API`)
- Authenticated and authorized access only
- Rate limiting for robustness
- Complete audit trail

#### **2. Built-In Customer Integration**
- StarGate client shipped as standard component for Customer/Supplier
- No custom integration required
- Automatic polling for state updates
- Offline queue capability (if connectivity lost)
- Automatic retry logic

#### **3. Process Orchestration**
- Seamless process submission from customer/supplier to Server
- Process status tracking
- State synchronization between systems
- Extensible for additional business processes

#### **4. Resilience**
- Connectivity failure management
- Automatic retry with exponential backoff
- Local persistent queue
- Transaction consistency guarantees

---

## SECTION 3: ARCHITECTURE - SYNCHRONOUS PULL MODEL

### Interaction Pattern

All communication is **initiated by Horizon-Customer (via built-in StarGate client)**:

``` mermaid
sequenceDiagram
    participant Customer as CLIENT<br/>(Customer/Supplier)
    participant StarGate as STARGATE<br/>GATEWAY
    participant Horizon as SERVER<br/>(Central)

    Customer->>StarGate: 1. POST /process<br/>(HTTPS + JWT)
    activate StarGate
    
    Note over StarGate: 2. Validate Auth<br/>Check Rate Limit
    
    StarGate->>Horizon: 3. Queue Request
    activate Horizon
    
    Note over Horizon: 4. Process<br/>Asynchronously
    
    StarGate-->>Customer: 4. 202 Accepted<br/>(processId)
    deactivate StarGate
    
    loop Auto Polling (StarGate manages)
        Customer->>StarGate: 5. GET /status/{id}
        activate StarGate
        StarGate->>Horizon: 6. Query state
        Horizon-->>StarGate: State update
        StarGate-->>Customer: 7. 200 OK<br/>(Process state)
        deactivate StarGate
        Note over Customer: Until complete
    end
    
    deactivate Horizon
```

###Polling Strategy (Intelligent Adaptive)
The **StarGate Client** implements an intelligent polling strategy designed to balance responsiveness with resource efficiency:
```
POLLING STRATEGY TIMELINE:

Minute 0:   [Process submitted - 202 Accepted]
            ↓
Minute 0:   Poll immediately (check initial status)
            ↓
Minute 0-2: Poll every 30 seconds (aggressive)
            - First 2 minutes: high frequency for quick processes
            - Expect completion for 80% of operations here
            ↓
Minute 2+:  Poll every 60 seconds (conservative)
            - After 2 minutes: switch to longer intervals
            - Reduce load on gateway for long-running processes
            ↓
Max Timeout: 10 minutes (default, configurable per process type)
            - If process not completed after 10 min → log warning
            - Continue polling with 60s intervals
            - Allow manual intervention if needed
```

### Key Characteristics

- **Automatic via StarGate**: Built-in client handles all API calls
- **Stateless Gateway**: StarGate routes and validates, Central processes
- **Pull-Based State**: Local instance queries status via built-in polling mechanism
- **Asynchronous Processing**: Request accepted immediately (HTTP 202 Accepted), processed in background
- **Zero Integration Needed**: Works out-of-the-box with StarGate Client

### Communication Standards

**Endpoint**: `https://azure.tenant.com/api/stargate`

**Authentication**: OAuth 2.0 Bearer Token (JWT)

**Request Pattern** (Internally Generated by StarGate Client):
```json
POST /api/stargate/processes
Content-Type: application/json
Authorization: Bearer {token}

{
  "clientProcessId": "8f3c2a4e-7b91-4d8f-9e6a-2c4f8b1d7e53",
  "processType": "process-A",
  "data": { 
    /* business-specific payload */
  }
}

Response (202 Accepted):
{
  "serverProcessId": "PROC-20260114-00001",
  "clientProcessId": "8f3c2a4e-7b91-4d8f-9e6a-2c4f8b1d7e53",
  "status": "accepted",
  "statusUrl": "/api/stargate/processes/PROC-20260114-00001",
  "createdAt": "2026-01-14T12:48:00Z"
}
```

**Status Query Pattern** (Automatically Polled by StarGate Client):
```json
GET /api/stargate/processes/{processId}
Authorization: Bearer {token}

Response (200 OK):
{
  "processId": "PROC-20260114-00001",
  "clientProcessId": "8f3c2a4e-7b91-4d8f-9e6a-2c4f8b1d7e53",
  "status": "processing",
  "progress": 45,
  "result": null,
  "updatedAt": "2026-01-14T12:30:00Z"
}
```

### Process States

```
Request Submitted (by local process)
       │
       ▼
    Accepted (202 returned to Client)
       │
       ├─→ Processing (StarGate auto-polls)
       │
       ├─→ Completed (result available to local system)
       │   └─→ Local system consumes result automatically
       │
       └─→ Failed (error details available)
           └─→ Auto-retry or manual intervention
```

---

## SECTION 4: ARCHITECTURE - SYSTEM COMPONENTS

### High-Level Component View

```
┌───────────────────────────────────────────┐
│ AZURE PUBLIC (Managed, Scalable)          │
│                                           │
│  ┌────────────────────────────────────┐   │
│  │ API Gateway (StarGate Public)      │   │
│  │ • Request validation               │   │
│  │ • Authentication (OAuth 2.0)       │   │
│  │ • Rate limiting                    │   │
│  │ • Request/Response routing         │   │
│  └────────────────────────────────────┘   │
│                   │                       │
│  ┌────────────────▼───────────────────┐   │
│  │ State Store & Cache                │   │
│  │ • Track process state              │   │
│  │ • Results storage (temporary)      │   │
│  │ • Fast state queries (Redis)       │   │
│  └────────────────────────────────────┘   │
└───────────────────────────────────────────┘
                   │
         Secure Private Link
                   │
┌──────────────────▼────────────────────────┐
│ AZURE PRIVATE (Locked Down)               │
│                                           │
│  ┌────────────────────────────────────┐   │
│  │ SERVER (Process Engine)            │   │
│  │ • Consumes incoming requests       │   │
│  │ • Executes business logic          │   │
│  │ • Persists data                    │   │
│  │ • Updates process state            │   │
│  └────────────────────────────────────┘   │
│                   │                       │
│  ┌────────────────▼───────────────────┐   │
│  │ Data Layer                         │   │
│  │ • MongoDB (requests, audit logs)   │   │
│  │ • Persistent storage & backup      │   │
│  └────────────────────────────────────┘   │
└───────────────────────────────────────────┘

┌───────────────────────────────────────────┐
│ CUSTOMER ON-PREMISES (Multiple Instances) │
│                                           │
│  ┌────────────────────────────────────┐   │
│  │ Customer Instance #1               │   │
│  │ ┌────────────────────────────────┐ │   │
│  │ │ Process Controls               │ │   │
│  │ │ Local Process Engine           │ │   │
│  │ │ ┌────────────────────────────┐ │ │   │
│  │ │ │ StarGate Client (Built-in) │ │ │   │
│  │ │ │ • Credential management    │ │ │   │
│  │ │ │ • Auto request submission  │ │ │   │
│  │ │ │ • Auto polling             │ │ │   │
│  │ │ │ • Offline queue            │ │ │   │
│  │ │ └────────────────────────────┘ │ │   │
│  │ └────────────────────────────────┘ │   │
│  └────────────────────────────────────┘   │
│                                           │
│  ┌────────────────────────────────────┐   │
│  │ Supplier Instance #2               │   │
│  │ (Same structure...)                │   │
│  └────────────────────────────────────┘   │
│                                           │
│  [Additional instances as needed]         │
└───────────────────────────────────────────┘
```

### Component Responsibilities

**API Gateway (Public)**:
- Validate JWT tokens with `processType` scope
- Check request rate limits per tenant
- Route to process handler
- Return immediate response
- Audit all requests with correlation IDs

**State Store & Cache**:
- Track process execution state in Redis
- Store results temporarily
- Enable fast status queries
- Expire cached data after TTL

**Server (Private)**:
- Consume incoming requests (via private link)
- Execute business logic with received data context
- Handle errors and automatic retries
- Update state in cache and persistent storage
- Persist business results and audit logs

**Data Layer**:
- **MongoDB**: Audit logs, request history, state snapshots
- Immutable event logs for compliance
- Backup and disaster recovery

**StarGate Client (Built-in to Client)**:
- Manage OAuth credentials (x processType)
- Submit process requests on behalf of local instance
- Implement intelligent polling strategy
- Handle polling intervals and adaptive backoff
- Manage local offline queue
- Retry failed requests
- Deliver results to local process engine

---

## SECTION 5: EXTENSIBILITY

### Process Types

StarGate is **process-agnostic**. The framework supports any business process that follows the request→processing→result pattern:

```
Example Processes:
├─ [Order Management]
│  ├─ Submit order
│  ├─ Track fulfillment
│  └─ Retrieve order status
│
├─ [Shipping Process]
│  ├─ Send items
│  ├─ Track shipping
│  └─ Retrieve delivery status
│
└─ Future Processes (Extensible Architecture)
   ├─ Inventory management
   ├─ Quality control
   ├─ Resource allocation
   └─ Custom business workflows
```

### Adding New Process Types

The architecture supports adding new processes:

1. **Define process contract** (request schema, result schema)
2. **Implement handler** in SERVER
4. **Enable for customer tenant** (RBAC configuration)
5. **Validate with test data** (per-process testing)
5. **Implement logic** for Customer/Supplier (data preparing)
6. **Built-in client automatically picks up** new process types

---

## SECTION 6: SECURITY & COMPLIANCE

### Authentication & Authorization

**OAuth 2.0**:
- Client credentials flow
- Customer identity isolation
- Token expiry (1 hour)
- Refresh tokens (30 days)
- Scope includes `processType` for granular access control
- Credentials securely stored in local Client system

**RBAC (Role-Based Access Control)**:
- Per-client isolation (data separation)
- Per-process access control
- Granular operation permissions
- Audit trail of all access with request context

### Data Protection

**In Transit**: TLS 1.2+ (HTTPS only)
**At Rest**: AES-256 encryption (MongoDB)
**Audit**: Complete request/response logging with immutable event logs

### Compliance Considerations

- **GDPR**: Data ownership, deletion rights, portability (**TBD**: MongoDB audit trail retention policy)
- **SOC 2**: Security, availability, processing integrity
- **Data Residency**: All data stored within compliance zones

---

## SECTION 7: IMPLEMENTATION APPROACH

### Phased Rollout

**Phase 1: Foundation**
- API Gateway infrastructure
- OAuth 2.0 integration with `processType` scoping
- State store (Redis cache + MongoDB)
- First process type (e.g., orders)
- StarGate client with intelligent polling strategy (30s → 60s adaptive)
- Single pilot customer

**Phase 2: Stabilization**
- Production hardening
- Performance optimization with caching and polling interval tuning
- Resilience & error handling
- Expanded customer base (5-10)
- Polling metrics collection and analysis

**Phase 3: Growth**
- Additional process types
- Advanced features (analytics, reporting)
- Scaling to 50+ customers
- Operational excellence
- Optimized polling configurations per process type

### Technology Stack

| Layer | Technology | Rationale |
|-------|-----------|-----------|
| **API Implementation** | .NET 8 Minimal APIs (C#) | Type-safe, high-performance, enterprise-grade |
| **Caching** | Redis | Fast state queries, distributed session management |
| **Document Database** | MongoDB | Flexible schema for audit logs, request history, audit compliance |
| **Containerization** | Docker | Consistent deployment environment |
| **Infrastructure** | Ubuntu VM (Azure) | Cost-effective, flexible scaling, proven stability |

---

## SECTION 8: KEY BENEFITS

### Operational

✅ **Process Automation**: Manual workflows become automated and auditable

✅ **Visibility**: Real-time status of all processes (automatically tracked)

✅ **Reliability**: Persistent state ensures no request loss even on failures

✅ **Scalability**: Stateless design allows horizontal scaling at all layers

✅ **Integration**: Zero integration effort - StarGate is built-in for Server and Client

### Technical

✅ **Isolation**: Central system remains private, never exposed to internet

✅ **Flexibility**: Extensible to support additional process types

✅ **Compliance**: Audit trail, data encryption, access control, data residency built-in

✅ **Resilience**: Automatic retry with exponential backoff, offline queue, error recovery

✅ **Performance**: Redis caching for sub-millisecond state queries, optimized payload handling

### Business

✅ **Time-to-Enable**: Reduce 3-6 month integration projects to weeks

✅ **Cost Reduction**: No custom integration development per customer

✅ **Scalability**: Enable 50+ customers without proportional resource increase

✅ **Predictability**: Standardized communication model across all customers

---

## SECTION 9: ASSUMPTIONS & CONSTRAINTS

### Assumptions

- Customers have stable internet connectivity (or local queue for offline scenarios)
- StarGate client automatically polls API at reasonable intervals
- Process completion times acceptable (synchronous: <5 seconds; asynchronous: <5 minutes)
- Credentials securely managed within Client instance provisioned at customer setup time

### Constraints

- No real-time push notifications (polling-based only)
- No bidirectional communication initiated by server
- Request/response size limits (standard HTTP limits apply)
- Rate limiting enforced (per-family burst capacity defined)
- API backward compatibility required (versioning strategy needed)

---

## SECTION 10: SUCCESS METRICS

### Operational Metrics

- **Availability**: 99.9% uptime target
- **Latency**: API response <500ms (p95), process completion within SLA
- **Throughput**: Support 10,000+ requests/day sustained
- **Error Rate**: <0.1% failed requests
- **Data Consistency**: Zero data loss during failures

### Adoption Metrics

- Number of customers with Horizon-Customer deployed
- Number of process types supported
- Volume of requests processed per family/project
- Time-to-enable new processes (target: <1 month)
- Customer satisfaction

---

## SECTION 11: API SPECIFICATIONS

### Detailed Endpoint Documentation

#### Submit Process Request

``` json
POST /api/stargate/processes
Content-Type: application/json
Authorization: Bearer {token}

REQUEST BODY (Generated by StarGate Client):
{
  "clientProcessId": "8f3c2a4e-7b91-4d8f-9e6a-2c4f8b1d7e53",  // Process item identifier
  "processType": "order",                                     // Type of process
  "data": {                                                   // Process-specific payload
    "orderId": "ORD-12345",
    "items": [
      {
        "sku": "SKU-001",
        "quantity": 10,
      },
      {
        "sku": "SKU-002",
        "quantity": 5,
      }
    ]
  },
  "idempotencyKey": "UUID-4-string"                           // Prevent duplicate submissions
}

RESPONSE (202 Accepted):
{
  "processId": "PROC-20260114-00001",
  "clientProcessId": "8f3c2a4e-7b91-4d8f-9e6a-2c4f8b1d7e53",
  "processType": "order",
  "status": "accepted",
  "statusUrl": "/api/stargate/processes/PROC-20260114-00001",
  "createdAt": "2026-01-14T12:48:00Z",
  "estimatedCompletionTime": "2026-01-14T13:00:00Z"
}

ERROR RESPONSES:
400 Bad Request:
{
  "error": "INVALID_REQUEST",
  "message": "Missing required field: clientProcessId",
  "timestamp": "2026-01-14T12:48:00Z"
}

401 Unauthorized:
{
  "error": "INVALID_TOKEN",
  "message": "Token expired or invalid",
  "timestamp": "2026-01-14T12:48:00Z"
}

403 Forbidden:
{
  "error": "INSUFFICIENT_PERMISSIONS",
  "message": "ProcessType not authorized",
  "timestamp": "2026-01-14T12:48:00Z"
}

429 Too Many Requests:
{
  "error": "RATE_LIMIT_EXCEEDED",
  "message": "Request rate limit exceeded (10 req/min)",
  "retryAfter": 30,
  "timestamp": "2026-01-14T12:48:00Z"
}
```

#### Query Process Status

``` json
GET /api/stargate/processes/{processId}
Authorization: Bearer {token}

RESPONSE (200 OK):
{
  "processId": "PROC-20260114-00001",
  "clientProcessId": "8f3c2a4e-7b91-4d8f-9e6a-2c4f8b1d7e53",
  "processType": "order",
  "status": "processing",
  "progress": 45,
  "currentStep": "inventory_check",
  "result": null,
  "createdAt": "2026-01-14T12:48:00Z",
  "updatedAt": "2026-01-14T12:50:15Z",
  "estimatedCompletionTime": "2026-01-14T13:00:00Z"
}

RESPONSE (200 OK - Completed):
{
  "processId": "PROC-20260114-00001",
  "clientProcessId": "8f3c2a4e-7b91-4d8f-9e6a-2c4f8b1d7e53",
  "processType": "order",
  "status": "completed",
  "progress": 100,
  "result": {
    "orderId": "ORD-12345",
    "status": "confirmed",
    "trackingNumber": "TRACK-123456",
    "estimatedDelivery": "2026-01-17T00:00:00Z"
  },
  "createdAt": "2026-01-14T12:48:00Z",
  "completedAt": "2026-01-14T13:05:00Z"
}

RESPONSE (200 OK - Failed):
{
  "processId": "PROC-20260114-00001",
  "clientProcessId": "8f3c2a4e-7b91-4d8f-9e6a-2c4f8b1d7e53",
  "processType": "order",
  "status": "failed",
  "progress": 30,
  "error": {
    "code": "INVENTORY_INSUFFICIENT",
    "message": "Insufficient stock for SKU-001",
    "details": {
      "sku": "SKU-001",
      "requested": 10,
      "available": 5
    }
  },
  "createdAt": "2026-01-14T12:48:00Z",
  "failedAt": "2026-01-14T12:52:00Z",
  "retryable": true
}
```

---

## SECTION 12: ERROR HANDLING & RESILIENCE

### Error Categories

**Client Errors (4xx)**:
- `400 Bad Request`: Malformed request, missing fields, invalid data
- `401 Unauthorized`: Missing or invalid JWT token
- `403 Forbidden`: Insufficient permissions for processType
- `404 Not Found`: Process ID not found
- `429 Too Many Requests`: Rate limit exceeded

**Server Errors (5xx)**:
- `500 Internal Server Error`: Unrecoverable processing error
- `502 Bad Gateway`: Downstream service unavailable
- `503 Service Unavailable`: Temporary overload or maintenance

### Retry Strategy

**For StarGate Client (Automatic)**:
- **Polling Strategy**:
  * **Initial**: Immediate poll upon 202 acceptance
  * **Phase 1 (0-2 minutes)**: Every 30 seconds (aggressive)
  * **Phase 2 (2+ minutes)**: Every 60 seconds (conservative)
  * **Maximum Timeout**: 10 minutes (default, configurable per process type)
- **Polling Backoff on Failures**:
  * **Transient Poll Failures** (network timeout, 503): Exponential backoff
  * Attempt 1: Retry after 5 seconds
  * Attempt 2: Retry after 10 seconds
  * Attempt 3: Retry after 20 seconds
  * Attempt 4: Retry after 40 seconds
  * Attempt 5: Retry after 80 seconds
  * Max retries: 5 before alerting
- **Idempotent Requests**: Use `idempotencyKey` to prevent duplicate processing on retry
- **Offline Queue**: Local queue persists requests during connectivity loss
  * Queue stored in local database (SQLite or SQL Server Compact)
  * Automatic flush when connectivity restored
  * Maintains request order

**For Internal Processing (Horizon Core):**
- **Automatic Retries**: Transient failures (network, timeout) retried internally
- **Circuit Breaker**: Protect against cascading failures
- **Fallback**: Use cached state if database temporarily unavailable

### Resilience Patterns

**State Consistency:**
- All state changes persisted immediately to MongoDB
- Redis cache used for fast queries with MongoDB as source of truth
- No data loss on partial failures
- Polling reads always from authoritative state store

**Graceful Degradation:**
- API Gateway remains responsive even if Horizon Core temporarily unavailable
- Accept requests with 202 response, queue them for later processing
- StarGate client can queue requests locally if gateway unavailable

**Failure Recovery:**
- Automatic reprocessing of failed requests
- Manual retry capability via API (if process marked `retryable: true`)
- Complete audit trail for debugging failures
- Polling logs captured for root cause analysis of timeouts or failures

---

## SECTION 13: MONITORING & OBSERVABILITY

### Key Observability Requirements

**Distributed Tracing:**
- Correlation IDs throughout request lifecycle
- Trace request from Horizon-Customer → API Gateway → Horizon → databases
- Performance bottleneck identification
- Polling event tracing (each poll logged with correlation ID)

**Logging:**
- Structured logs (JSON format) from all components
- Request/response payloads (sanitized for sensitive data)
- Error logs with stack traces
- Audit logs with user context (familyId, projectId, processData)
- StarGate client activity logs
    * Polling initiation and completion
    * Polling interval transitions (30s → 60s)
    * Poll failures and backoff attempts
    * Process result retrieval and consumption

**Alerting:**
- High error rate (>1% requests failing)
- P95 latency exceeding 2 seconds
- Queue backlog exceeding 1000 items
- Database connection exhaustion
- Cache eviction rate spikes
- Repeated polling failures per customer (>3 consecutive failures per process)
- Polling timeout rate exceeding 0.5% (processes not completing within 10 minutes)
- Polling interval anomalies (stuck in Phase 1 beyond expected completion times)

### Health Checks

**Liveness Probe**: API responds to `GET /health/live`
**Readiness Probe**: `GET /health/ready` returns success only if all dependencies healthy
**Dependencies**: Database connectivity, Redis connectivity, downstream service availability
