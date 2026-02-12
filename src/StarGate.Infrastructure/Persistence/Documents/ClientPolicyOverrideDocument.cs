using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using MongoDB.Bson.Serialization.Options;

namespace StarGate.Infrastructure.Persistence.Documents;

/// <summary>
/// MongoDB document for ClientPolicyOverride configuration.
/// Allows clients to override default process type policies.
/// </summary>
public record ClientPolicyOverrideDocument
{
    /// <summary>
    /// Document identifier.
    /// </summary>
    [BsonId]
    [BsonElement("_id")]
    [BsonRepresentation(BsonType.String)]
    public required string Id { get; init; }

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
    /// Override timeout (optional).
    /// </summary>
    [BsonElement("timeout")]
    [BsonTimeSpanOptions(BsonType.Int64, TimeSpanUnits.Seconds)]
    [BsonIgnoreIfNull]
    public TimeSpan? Timeout { get; init; }

    /// <summary>
    /// Override result retention (optional).
    /// </summary>
    [BsonElement("resultRetention")]
    [BsonTimeSpanOptions(BsonType.Int64, TimeSpanUnits.Days)]
    [BsonIgnoreIfNull]
    public TimeSpan? ResultRetention { get; init; }

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
