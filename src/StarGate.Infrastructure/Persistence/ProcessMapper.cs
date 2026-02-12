using MongoDB.Bson;
using StarGate.Core.Domain;
using System.Text.Json;

namespace StarGate.Infrastructure.Persistence;

/// <summary>
/// Maps between domain Process entities and MongoDB ProcessDocument.
/// </summary>
public static class ProcessMapper
{
    /// <summary>
    /// Converts a domain Process entity to a MongoDB ProcessDocument.
    /// </summary>
    /// <param name="process">The process to convert.</param>
    /// <returns>A ProcessDocument for MongoDB persistence.</returns>
    /// <exception cref="ArgumentNullException">Thrown when process is null.</exception>
    /// <exception cref="JsonException">Thrown when data serialization fails.</exception>
    public static ProcessDocument MapToDocument(Process process)
    {
        ArgumentNullException.ThrowIfNull(process);

        return new ProcessDocument
        {
            ProcessId = process.ProcessId,
            ClientProcessId = process.ClientProcessId,
            ProcessType = process.ProcessType,
            ClientId = process.ClientId,
            Status = process.Status.ToString(),
            Progress = process.Progress,
            CurrentStep = process.CurrentStep,
            Data = ConvertToBsonDocument(process.Data),
            Result = ConvertToBsonDocument(process.Result),
            Error = process.Error != null ? new ErrorDocument
            {
                Code = process.Error.Code,
                Message = process.Error.Message,
                Details = ConvertToBsonDocument(process.Error.Details)
            } : null,
            CreatedAt = process.CreatedAt,
            UpdatedAt = process.UpdatedAt,
            CompletedAt = process.CompletedAt,
            IdempotencyKey = process.IdempotencyKey,
            Retryable = process.Retryable
        };
    }

    /// <summary>
    /// Converts a MongoDB ProcessDocument to a domain Process entity.
    /// </summary>
    /// <param name="document">The document to convert.</param>
    /// <returns>A Process domain entity.</returns>
    /// <exception cref="ArgumentNullException">Thrown when document is null.</exception>
    /// <exception cref="InvalidOperationException">Thrown when status enum parsing fails.</exception>
    public static Process MapToDomain(ProcessDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);

        return new Process
        {
            ProcessId = document.ProcessId,
            ClientProcessId = document.ClientProcessId,
            ProcessType = document.ProcessType,
            ClientId = document.ClientId,
            Status = ParseEnum<ProcessStatus>(document.Status, nameof(document.Status)),
            Progress = document.Progress,
            CurrentStep = document.CurrentStep,
            Data = ConvertToJsonDocument(document.Data),
            Result = ConvertToJsonDocument(document.Result),
            Error = document.Error != null ? new ProcessError(
                document.Error.Code,
                document.Error.Message,
                ConvertToJsonDocument(document.Error.Details)) : null,
            CreatedAt = document.CreatedAt,
            UpdatedAt = document.UpdatedAt,
            CompletedAt = document.CompletedAt,
            IdempotencyKey = document.IdempotencyKey,
            Retryable = document.Retryable
        };
    }

    /// <summary>
    /// Converts a JsonDocument to a BSON document.
    /// </summary>
    private static BsonDocument? ConvertToBsonDocument(JsonDocument? jsonDocument)
    {
        if (jsonDocument == null)
        {
            return null;
        }

        string json = JsonSerializer.Serialize(jsonDocument.RootElement);
        return BsonDocument.Parse(json);
    }

    /// <summary>
    /// Converts a BSON document to a JsonDocument.
    /// </summary>
    private static JsonDocument? ConvertToJsonDocument(BsonDocument? bsonDocument)
    {
        if (bsonDocument == null)
        {
            return null;
        }

        string json = bsonDocument.ToJson();
        return JsonDocument.Parse(json);
    }

    /// <summary>
    /// Parses an enum value with error handling.
    /// </summary>
    private static TEnum ParseEnum<TEnum>(string value, string paramName) where TEnum : struct, Enum
    {
        if (Enum.TryParse<TEnum>(value, ignoreCase: true, out TEnum result))
        {
            return result;
        }

        throw new InvalidOperationException(
            $"Invalid {typeof(TEnum).Name} value '{value}' in {paramName}");
    }
}
