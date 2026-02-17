// Initialize MongoDB database and collections for StarGate

db = db.getSiblingDB('stargate');

print('Initializing StarGate database...');

// Create collections
db.createCollection('processes');
db.createCollection('process_type_policies');
db.createCollection('client_policy_overrides');

print('Collections created.');

// Create indexes for processes collection
print('Creating indexes for processes collection...');

// Primary identifier - automatically created by MongoDB for _id
// ProcessId is mapped to _id in the application

// Unique index on ClientId + ClientProcessId (idempotency per client)
db.processes.createIndex(
    { "clientId": 1, "clientProcessId": 1 },
    { unique: true, name: "idx_clientId_clientProcessId" }
);

// Index on Status for filtering by process state
db.processes.createIndex(
    { "status": 1 },
    { name: "idx_status" }
);

// Index on CreatedAt for time-based queries
db.processes.createIndex(
    { "createdAt": 1 },
    { name: "idx_createdAt" }
);

// Unique index on IdempotencyKey
db.processes.createIndex(
    { "idempotencyKey": 1 },
    { unique: true, name: "idx_idempotencyKey" }
);

// Composite index for active process count queries
db.processes.createIndex(
    { "clientId": 1, "processType": 1, "status": 1 },
    { name: "idx_clientId_processType_status" }
);

print('Indexes created for processes collection.');

// Create indexes for policies
print('Creating indexes for policy collections...');

// ProcessType is the primary key (_id) for process_type_policies
// No additional index needed

// Composite unique index for client policy overrides
db.client_policy_overrides.createIndex(
    { "clientId": 1, "processType": 1 },
    { unique: true, name: "idx_clientId_processType" }
);

// Index on ClientId for listing overrides by client
db.client_policy_overrides.createIndex(
    { "clientId": 1 },
    { name: "idx_clientId" }
);

print('Indexes created for policy collections.');

print('StarGate database initialized successfully!');
print('');
print('Collections created:');
print('  - processes');
print('  - process_type_policies');
print('  - client_policy_overrides');
print('');
print('Indexes created:');
print('  - processes: 5 indexes');
print('  - client_policy_overrides: 2 indexes');
