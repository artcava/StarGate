namespace StarGate.Infrastructure.Persistence.Documents;

using MongoDB.Bson.Serialization.Attributes;

/// <summary>
/// MongoDB document for ClientPolicyOverride configuration.
/// Allows clients to override default process type policies.
/// </summary>
public record ClientPolicyOverrideDocument
{
    /// <summary>
    /// Client identifier.
    /// </summary>
    [BsonElement("clientId")]
    public required string ClientId { get; init; }

    /// <summary>
    /// Process type being overridden.
    /// </summary>
    [BsonElement("processType")]
    public required string ProcessType { get; init; }

    /// <summary>
    /// Override timeout in seconds (optional).
    /// </summary>
    [BsonElement("timeoutSeconds")]
    [BsonIgnoreIfNull]
    public int? TimeoutSeconds { get; init; }

    /// <summary>
    /// Override result retention in days (optional).
    /// </summary>
    [BsonElement("resultRetentionDays")]
    [BsonIgnoreIfNull]
    public int? ResultRetentionDays { get; init; }

    /// <summary>
    /// Override max concurrent processes (optional).
    /// </summary>
    [BsonElement("maxConcurrentProcesses")]
    [BsonIgnoreIfNull]
    public int? MaxConcurrentProcesses { get; init; }

    /// <summary>
    /// Last update timestamp.
    /// </summary>
    [BsonElement("updatedAt")]
    [BsonDateTimeOptions(Kind = DateTimeKind.Utc)]
    public required DateTime UpdatedAt { get; init; }
}
