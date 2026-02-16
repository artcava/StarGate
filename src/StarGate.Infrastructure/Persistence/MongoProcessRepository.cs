using Microsoft.Extensions.Logging;
using MongoDB.Bson;
using MongoDB.Driver;
using StarGate.Core.Abstractions;
using StarGate.Core.Domain;

namespace StarGate.Infrastructure.Persistence;

/// <summary>
/// MongoDB implementation of IProcessRepository.
/// Provides persistent storage for Process entities using MongoDB.Driver.
/// Follows clean architecture principles: Core defines interface, Infrastructure implements.
/// </summary>
public class MongoProcessRepository : IProcessRepository
{
    private readonly IMongoCollection<ProcessDocument> _collection;
    private readonly ILogger<MongoProcessRepository> _logger;

    static MongoProcessRepository()
    {
        // Register BsonClassMap for ProcessDocument with explicit Guid serialization
        // This ensures ProcessId (_id) uses Standard GuidRepresentation
        ProcessDocumentClassMap.Register();
    }

    public MongoProcessRepository(
        IMongoDatabase database,
        ILogger<MongoProcessRepository> logger)
    {
        ArgumentNullException.ThrowIfNull(database);
        ArgumentNullException.ThrowIfNull(logger);

        _collection = database.GetCollection<ProcessDocument>("processes");
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<Process> CreateAsync(Process process, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(process);

        _logger.LogDebug(
            "Creating process {ProcessId} (ClientProcessId: {ClientProcessId}, Type: {ProcessType})",
            process.ProcessId,
            process.ClientProcessId,
            process.ProcessType);

        try
        {
            var document = ProcessMapper.MapToDocument(process);
            await _collection.InsertOneAsync(document, cancellationToken: ct);

            _logger.LogInformation(
                "Process {ProcessId} created successfully for client {ClientId}",
                process.ProcessId,
                process.ClientId);

            return process;
        }
        catch (MongoWriteException ex) when (ex.WriteError.Category == ServerErrorCategory.DuplicateKey)
        {
            _logger.LogWarning(
                ex,
                "Duplicate key error creating process {ProcessId}: {ErrorMessage}",
                process.ProcessId,
                ex.WriteError.Message);

            // Determine which unique constraint was violated
            var errorMessage = ex.WriteError.Message;
            
            // Check for _id constraint (ProcessId is mapped to _id)
            if (errorMessage.Contains("_id_") || errorMessage.Contains("ProcessId"))
            {
                throw new InvalidOperationException(
                    $"Process with ID '{process.ProcessId}' already exists",
                    ex);
            }
            else if (errorMessage.Contains("IdempotencyKey"))
            {
                throw new InvalidOperationException(
                    $"Process with idempotency key '{process.IdempotencyKey}' already exists",
                    ex);
            }
            else if (errorMessage.Contains("ClientId") || errorMessage.Contains("ClientProcessId"))
            {
                throw new InvalidOperationException(
                    $"Process with ClientId '{process.ClientId}' and ClientProcessId '{process.ClientProcessId}' already exists",
                    ex);
            }
            else
            {
                throw new InvalidOperationException(
                    $"Duplicate key error: {errorMessage}",
                    ex);
            }
        }
    }

    /// <inheritdoc />
    public async Task<Process?> GetByIdAsync(Guid processId, CancellationToken ct = default)
    {
        _logger.LogDebug("Retrieving process {ProcessId}", processId);

        // CRITICAL: MongoDB stores Guid with subType 04 (UUID Standard RFC 4122)
        // We must create BsonBinaryData with the exact same subType
        // BsonBinaryData constructor with byte array creates subType 04 by default
        var guidBytes = processId.ToByteArray();
        var bsonGuid = new BsonBinaryData(guidBytes, BsonBinarySubType.UuidStandard);
        var filter = Builders<ProcessDocument>.Filter.Eq("_id", bsonGuid);
        
        var document = await _collection.Find(filter).FirstOrDefaultAsync(ct);

        if (document == null)
        {
            _logger.LogDebug("Process {ProcessId} not found", processId);
            return null;
        }

        var process = ProcessMapper.MapToDomain(document);
        _logger.LogDebug(
            "Process {ProcessId} retrieved (Status: {Status})",
            processId,
            process.Status);

        return process;
    }

    /// <inheritdoc />
    public async Task<Process?> GetByClientProcessIdAsync(
        string clientId,
        string clientProcessId,
        CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(clientId);
        ArgumentException.ThrowIfNullOrWhiteSpace(clientProcessId);

        _logger.LogDebug(
            "Retrieving process by ClientId {ClientId} and ClientProcessId {ClientProcessId}",
            clientId,
            clientProcessId);

        var filter = Builders<ProcessDocument>.Filter.And(
            Builders<ProcessDocument>.Filter.Eq(p => p.ClientId, clientId),
            Builders<ProcessDocument>.Filter.Eq(p => p.ClientProcessId, clientProcessId));

        var document = await _collection.Find(filter).FirstOrDefaultAsync(ct);

        if (document == null)
        {
            _logger.LogDebug(
                "Process not found for ClientId {ClientId} and ClientProcessId {ClientProcessId}",
                clientId,
                clientProcessId);
            return null;
        }

        var process = ProcessMapper.MapToDomain(document);
        _logger.LogDebug(
            "Process {ProcessId} found for client {ClientId}",
            process.ProcessId,
            clientId);

        return process;
    }

    /// <inheritdoc />
    public async Task<Process> UpdateAsync(Process process, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(process);

        _logger.LogDebug(
            "Updating process {ProcessId} (Status: {Status}, Progress: {Progress})",
            process.ProcessId,
            process.Status,
            process.Progress);

        try
        {
            var document = ProcessMapper.MapToDocument(process);
            
            // CRITICAL: Use same BsonBinaryData construction as GetByIdAsync
            var guidBytes = process.ProcessId.ToByteArray();
            var bsonGuid = new BsonBinaryData(guidBytes, BsonBinarySubType.UuidStandard);
            var filter = Builders<ProcessDocument>.Filter.Eq("_id", bsonGuid);

            var result = await _collection.ReplaceOneAsync(
                filter,
                document,
                new ReplaceOptions { IsUpsert = false },
                ct);

            if (result.MatchedCount == 0)
            {
                _logger.LogWarning(
                    "Process {ProcessId} not found for update",
                    process.ProcessId);

                throw new InvalidOperationException(
                    $"Process with ID '{process.ProcessId}' not found");
            }

            _logger.LogInformation(
                "Process {ProcessId} updated successfully (Status: {Status})",
                process.ProcessId,
                process.Status);

            return process;
        }
        catch (MongoWriteException ex)
        {
            _logger.LogError(
                ex,
                "Error updating process {ProcessId}: {ErrorMessage}",
                process.ProcessId,
                ex.WriteError.Message);
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<Process>> GetByStatusAsync(
        ProcessStatus status,
        int limit = 100,
        CancellationToken ct = default)
    {
        if (limit < 1 || limit > 1000)
        {
            throw new ArgumentOutOfRangeException(
                nameof(limit),
                limit,
                "Limit must be between 1 and 1000");
        }

        _logger.LogDebug(
            "Retrieving processes with status {Status} (limit: {Limit})",
            status,
            limit);

        var filter = Builders<ProcessDocument>.Filter.Eq(
            p => p.Status,
            status.ToString());

        var sort = Builders<ProcessDocument>.Sort.Ascending(p => p.CreatedAt);

        var documents = await _collection
            .Find(filter)
            .Sort(sort)
            .Limit(limit)
            .ToListAsync(ct);

        var processes = documents
            .Select(ProcessMapper.MapToDomain)
            .ToList();

        _logger.LogDebug(
            "Retrieved {Count} processes with status {Status}",
            processes.Count,
            status);

        return processes;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<Process>> GetByClientIdAsync(
        string clientId,
        int skip = 0,
        int limit = 100,
        CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(clientId);

        if (skip < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(skip),
                skip,
                "Skip must be non-negative");

        }

        if (limit < 1 || limit > 1000)
        {
            throw new ArgumentOutOfRangeException(
                nameof(limit),
                limit,
                "Limit must be between 1 and 1000");
        }

        _logger.LogDebug(
            "Retrieving processes for client {ClientId} (skip: {Skip}, limit: {Limit})",
            clientId,
            skip,
            limit);

        var filter = Builders<ProcessDocument>.Filter.Eq(p => p.ClientId, clientId);
        var sort = Builders<ProcessDocument>.Sort.Descending(p => p.CreatedAt);

        var documents = await _collection
            .Find(filter)
            .Sort(sort)
            .Skip(skip)
            .Limit(limit)
            .ToListAsync(ct);

        var processes = documents
            .Select(ProcessMapper.MapToDomain)
            .ToList();

        _logger.LogDebug(
            "Retrieved {Count} processes for client {ClientId}",
            processes.Count,
            clientId);

        return processes;
    }

    /// <inheritdoc />
    public async Task<int> CountActiveProcessesAsync(
        string clientId,
        string processType,
        CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(clientId);
        ArgumentException.ThrowIfNullOrWhiteSpace(processType);

        _logger.LogDebug(
            "Counting active processes for client {ClientId} and type {ProcessType}",
            clientId,
            processType);

        var filter = Builders<ProcessDocument>.Filter.And(
            Builders<ProcessDocument>.Filter.Eq(p => p.ClientId, clientId),
            Builders<ProcessDocument>.Filter.Eq(p => p.ProcessType, processType),
            Builders<ProcessDocument>.Filter.In(
                p => p.Status,
                new[] { "Accepted", "Processing" }));

        var count = await _collection.CountDocumentsAsync(filter, cancellationToken: ct);

        _logger.LogDebug(
            "Found {Count} active processes for client {ClientId} and type {ProcessType}",
            count,
            clientId,
            processType);

        return (int)count;
    }
}
