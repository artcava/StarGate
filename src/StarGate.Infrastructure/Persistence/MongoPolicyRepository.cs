namespace StarGate.Infrastructure.Persistence;

using Microsoft.Extensions.Logging;
using MongoDB.Driver;
using StarGate.Core.Abstractions;
using StarGate.Core.Domain.Configuration;
using StarGate.Infrastructure.Persistence.Documents;
using StarGate.Infrastructure.Persistence.Mappers;

/// <summary>
/// MongoDB implementation of IPolicyRepository.
/// Manages ProcessTypePolicy defaults and ClientPolicyOverride customizations.
/// </summary>
public class MongoPolicyRepository : IPolicyRepository
{
    private readonly IMongoCollection<ProcessTypePolicyDocument> _processTypePolicies;
    private readonly IMongoCollection<ClientPolicyOverrideDocument> _clientOverrides;
    private readonly ILogger<MongoPolicyRepository> _logger;

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

    /// <inheritdoc />
    public async Task<ProcessTypePolicy> GetProcessTypePolicyAsync(
        string processType,
        CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(processType);

        var document = await _processTypePolicies
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

        return PolicyMapper.MapToDomain(document);
    }

    /// <inheritdoc />
    public async Task<ClientPolicyOverride?> GetClientOverrideAsync(
        string clientId,
        string processType,
        CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(clientId);
        ArgumentException.ThrowIfNullOrWhiteSpace(processType);

        var document = await _clientOverrides
            .Find(o => o.ClientId == clientId && o.ProcessType == processType)
            .FirstOrDefaultAsync(ct);

        return document != null ? PolicyMapper.MapToDomain(document) : null;
    }

    /// <inheritdoc />
    public async Task<ProcessTypePolicy> SaveProcessTypePolicyAsync(
        ProcessTypePolicy policy,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(policy);

        var document = PolicyMapper.MapToDocument(policy);

        var options = new ReplaceOptions { IsUpsert = true };
        await _processTypePolicies.ReplaceOneAsync(
            p => p.ProcessType == policy.ProcessType,
            document,
            options,
            ct);

        _logger.LogInformation(
            "Process type policy saved: {ProcessType}",
            policy.ProcessType);

        return policy;
    }

    /// <inheritdoc />
    public async Task<ClientPolicyOverride> SaveClientOverrideAsync(
        ClientPolicyOverride @override,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(@override);

        var document = PolicyMapper.MapToDocument(@override);

        var options = new ReplaceOptions { IsUpsert = true };
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

    /// <inheritdoc />
    public async Task<bool> DeleteClientOverrideAsync(
        string clientId,
        string processType,
        CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(clientId);
        ArgumentException.ThrowIfNullOrWhiteSpace(processType);

        var result = await _clientOverrides.DeleteOneAsync(
            o => o.ClientId == clientId && o.ProcessType == processType,
            ct);

        var deleted = result.DeletedCount > 0;

        if (deleted)
        {
            _logger.LogInformation(
                "Client policy override deleted: ClientId={ClientId}, ProcessType={ProcessType}",
                clientId,
                processType);
        }

        return deleted;
    }

    /// <inheritdoc />
    public async Task<List<ProcessTypePolicy>> ListProcessTypePoliciesAsync(
        CancellationToken ct = default)
    {
        var documents = await _processTypePolicies
            .Find(_ => true)
            .ToListAsync(ct);

        return documents.Select(PolicyMapper.MapToDomain).ToList();
    }

    /// <inheritdoc />
    public async Task<List<ClientPolicyOverride>> ListClientOverridesAsync(
        string clientId,
        CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(clientId);

        var documents = await _clientOverrides
            .Find(o => o.ClientId == clientId)
            .ToListAsync(ct);

        return documents.Select(PolicyMapper.MapToDomain).ToList();
    }
}
