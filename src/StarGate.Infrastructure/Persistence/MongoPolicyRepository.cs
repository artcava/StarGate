using Microsoft.Extensions.Logging;
using MongoDB.Driver;
using StarGate.Core.Abstractions;
using StarGate.Core.Domain.Configuration;

namespace StarGate.Infrastructure.Persistence;

/// <summary>
/// MongoDB implementation of IPolicyRepository.
/// Manages policy configuration persistence for both defaults and client-specific overrides.
/// </summary>
public class MongoPolicyRepository : IPolicyRepository
{
    private readonly IMongoCollection<ProcessTypePolicyDocument> _processTypePolicies;
    private readonly IMongoCollection<ClientPolicyOverrideDocument> _clientOverrides;
    private readonly ILogger<MongoPolicyRepository> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="MongoPolicyRepository"/> class.
    /// </summary>
    /// <param name="database">MongoDB database instance.</param>
    /// <param name="logger">Logger for tracking operations.</param>
    /// <exception cref="ArgumentNullException">Thrown when database or logger is null.</exception>
    public MongoPolicyRepository(
        IMongoDatabase database,
        ILogger<MongoPolicyRepository> logger)
    {
        ArgumentNullException.ThrowIfNull(database);
        ArgumentNullException.ThrowIfNull(logger);

        _processTypePolicies = database.GetCollection<ProcessTypePolicyDocument>("processTypePolicies");
        _clientOverrides = database.GetCollection<ClientPolicyOverrideDocument>("clientPolicyOverrides");
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task<ProcessTypePolicy> GetProcessTypePolicyAsync(
        string processType,
        CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(processType);

        ProcessTypePolicyDocument? document = await _processTypePolicies
            .Find(p => p.ProcessType == processType)
            .FirstOrDefaultAsync(ct);

        if (document == null)
        {
            _logger.LogError(
                "Process type policy not found: {ProcessType}",
                processType);
            throw new InvalidOperationException(
                $"Process type policy '{processType}' not found");
        }

        _logger.LogDebug(
            "Retrieved process type policy: {ProcessType}",
            processType);

        return PolicyMapper.MapToDomain(document);
    }

    /// <inheritdoc/>
    public async Task<ClientPolicyOverride?> GetClientOverrideAsync(
        string clientId,
        string processType,
        CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(clientId);
        ArgumentException.ThrowIfNullOrWhiteSpace(processType);

        ClientPolicyOverrideDocument? document = await _clientOverrides
            .Find(o => o.ClientId == clientId && o.ProcessType == processType)
            .FirstOrDefaultAsync(ct);

        if (document == null)
        {
            _logger.LogDebug(
                "No client override found for ClientId={ClientId}, ProcessType={ProcessType}",
                clientId,
                processType);
            return null;
        }

        _logger.LogDebug(
            "Retrieved client override for ClientId={ClientId}, ProcessType={ProcessType}",
            clientId,
            processType);

        return PolicyMapper.MapToDomain(document);
    }

    /// <inheritdoc/>
    public async Task<ProcessTypePolicy> SaveProcessTypePolicyAsync(
        ProcessTypePolicy policy,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(policy);

        ProcessTypePolicyDocument document = PolicyMapper.MapToDocument(policy);

        ReplaceOptions options = new ReplaceOptions { IsUpsert = true };
        await _processTypePolicies.ReplaceOneAsync(
            p => p.ProcessType == policy.ProcessType,
            document,
            options,
            ct);

        _logger.LogInformation(
            "Process type policy saved: {ProcessType}, Timeout={Timeout}, MaxAttempts={MaxAttempts}",
            policy.ProcessType,
            policy.Timeout,
            policy.RetryPolicy.MaxAttempts);

        return policy;
    }

    /// <inheritdoc/>
    public async Task<ClientPolicyOverride> SaveClientOverrideAsync(
        ClientPolicyOverride @override,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(@override);

        // Fetch existing document to preserve ObjectId
        ClientPolicyOverrideDocument? existingDocument = await _clientOverrides
            .Find(o => o.ClientId == @override.ClientId && o.ProcessType == @override.ProcessType)
            .FirstOrDefaultAsync(ct);

        ClientPolicyOverrideDocument document = PolicyMapper.MapToDocument(@override);

        // Preserve existing ObjectId if updating
        if (existingDocument != null)
        {
            document.Id = existingDocument.Id;
        }

        ReplaceOptions options = new ReplaceOptions { IsUpsert = true };
        await _clientOverrides.ReplaceOneAsync(
            o => o.ClientId == @override.ClientId && o.ProcessType == @override.ProcessType,
            document,
            options,
            ct);

        _logger.LogInformation(
            "Client policy override saved: ClientId={ClientId}, ProcessType={ProcessType}",
            @override.ClientId,
            @override.ProcessType);

        return @override;
    }

    /// <inheritdoc/>
    public async Task<bool> DeleteClientOverrideAsync(
        string clientId,
        string processType,
        CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(clientId);
        ArgumentException.ThrowIfNullOrWhiteSpace(processType);

        DeleteResult result = await _clientOverrides.DeleteOneAsync(
            o => o.ClientId == clientId && o.ProcessType == processType,
            ct);

        bool deleted = result.DeletedCount > 0;

        if (deleted)
        {
            _logger.LogInformation(
                "Client policy override deleted: ClientId={ClientId}, ProcessType={ProcessType}",
                clientId,
                processType);
        }
        else
        {
            _logger.LogDebug(
                "No client override to delete for ClientId={ClientId}, ProcessType={ProcessType}",
                clientId,
                processType);
        }

        return deleted;
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<ProcessTypePolicy>> ListProcessTypePoliciesAsync(
        CancellationToken ct = default)
    {
        List<ProcessTypePolicyDocument> documents = await _processTypePolicies
            .Find(_ => true)
            .ToListAsync(ct);

        _logger.LogDebug(
            "Retrieved {Count} process type policies",
            documents.Count);

        return documents.Select(PolicyMapper.MapToDomain).ToList();
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<ClientPolicyOverride>> ListClientOverridesAsync(
        string clientId,
        CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(clientId);

        List<ClientPolicyOverrideDocument> documents = await _clientOverrides
            .Find(o => o.ClientId == clientId)
            .ToListAsync(ct);

        _logger.LogDebug(
            "Retrieved {Count} client overrides for ClientId={ClientId}",
            documents.Count,
            clientId);

        return documents.Select(PolicyMapper.MapToDomain).ToList();
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<ClientPolicyOverride>> GetClientOverridesAsync(
        string? clientId = null,
        string? processType = null,
        CancellationToken ct = default)
    {
        // Build filter dynamically based on provided parameters
        FilterDefinition<ClientPolicyOverrideDocument> filter = Builders<ClientPolicyOverrideDocument>.Filter.Empty;

        if (!string.IsNullOrWhiteSpace(clientId))
        {
            filter &= Builders<ClientPolicyOverrideDocument>.Filter.Eq(o => o.ClientId, clientId);
            _logger.LogDebug("Filtering client overrides by ClientId={ClientId}", clientId);
        }

        if (!string.IsNullOrWhiteSpace(processType))
        {
            filter &= Builders<ClientPolicyOverrideDocument>.Filter.Eq(o => o.ProcessType, processType);
            _logger.LogDebug("Filtering client overrides by ProcessType={ProcessType}", processType);
        }

        List<ClientPolicyOverrideDocument> documents = await _clientOverrides
            .Find(filter)
            .ToListAsync(ct);

        _logger.LogDebug(
            "Retrieved {Count} client overrides with filters: ClientId={ClientId}, ProcessType={ProcessType}",
            documents.Count,
            clientId ?? "(all)",
            processType ?? "(all)");

        return documents.Select(PolicyMapper.MapToDomain).ToList();
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<ProcessTypePolicy>> GetProcessTypePoliciesAsync(
        IEnumerable<string>? processTypes = null,
        CancellationToken ct = default)
    {
        FilterDefinition<ProcessTypePolicyDocument> filter;

        if (processTypes != null && processTypes.Any())
        {
            // Filter by the specified process types
            var typesList = processTypes.ToList();
            filter = Builders<ProcessTypePolicyDocument>.Filter.In(p => p.ProcessType, typesList);
            _logger.LogDebug(
                "Filtering process type policies by ProcessTypes={ProcessTypes}",
                string.Join(", ", typesList));
        }
        else
        {
            // Return all policies if no filter is provided
            filter = Builders<ProcessTypePolicyDocument>.Filter.Empty;
            _logger.LogDebug("Retrieving all process type policies (no filter)");
        }

        List<ProcessTypePolicyDocument> documents = await _processTypePolicies
            .Find(filter)
            .ToListAsync(ct);

        _logger.LogDebug(
            "Retrieved {Count} process type policies",
            documents.Count);

        return documents.Select(PolicyMapper.MapToDomain).ToList();
    }
}
