using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace StarGate.Infrastructure.Persistence;

/// <summary>
/// MongoDB document representation of a Process.
/// </summary>
public class ProcessDocument
{
    [BsonId]
    [BsonGuidRepresentation(GuidRepresentation.Standard)]
    public required Guid ProcessId { get; set; }

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
