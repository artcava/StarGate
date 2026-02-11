namespace StarGate.Infrastructure.Persistence;

using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

public class ProcessTypePolicyDocument
{
    [BsonId]
    [BsonElement("_id")]
    public required string ProcessType { get; set; }

    [BsonElement("timeout")]
    [BsonRequired]
    public required TimeSpan Timeout { get; set; }

    [BsonElement("retryPolicy")]
    [BsonRequired]
    public required RetryPolicyDocument RetryPolicy { get; set; }

    [BsonElement("resultRetention")]
    [BsonRequired]
    public required TimeSpan ResultRetention { get; set; }

    [BsonElement("maxConcurrentProcesses")]
    public int? MaxConcurrentProcesses { get; set; }

    [BsonElement("updatedAt")]
    [BsonRequired]
    public required DateTime UpdatedAt { get; set; }
}

public class ClientPolicyOverrideDocument
{
    [BsonId]
    public required ObjectId Id { get; set; }

    [BsonElement("clientId")]
    [BsonRequired]
    public required string ClientId { get; set; }

    [BsonElement("processType")]
    [BsonRequired]
    public required string ProcessType { get; set; }

    [BsonElement("timeout")]
    public TimeSpan? Timeout { get; set; }

    [BsonElement("retryPolicy")]
    public RetryPolicyDocument? RetryPolicy { get; set; }

    [BsonElement("resultRetention")]
    public TimeSpan? ResultRetention { get; set; }

    [BsonElement("maxConcurrentProcesses")]
    public int? MaxConcurrentProcesses { get; set; }

    [BsonElement("updatedAt")]
    [BsonRequired]
    public required DateTime UpdatedAt { get; set; }
}

public class RetryPolicyDocument
{
    [BsonElement("enabled")]
    [BsonRequired]
    public required bool Enabled { get; set; }

    [BsonElement("maxAttempts")]
    [BsonRequired]
    public required int MaxAttempts { get; set; }

    [BsonElement("initialDelay")]
    [BsonRequired]
    public required TimeSpan InitialDelay { get; set; }

    [BsonElement("backoffStrategy")]
    [BsonRequired]
    public required string BackoffStrategy { get; set; }

    [BsonElement("maxDelay")]
    [BsonRequired]
    public required TimeSpan MaxDelay { get; set; }
}
