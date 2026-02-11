namespace StarGate.Infrastructure.Persistence;

using MongoDB.Bson;
using StarGate.Core.Domain;
using System.Text.Json;

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
            Data = document.Data?.ToJson(),
            Result = document.Result?.ToJson(),
            Error = document.Error != null ? new ProcessError(
                document.Error.Code,
                document.Error.Message,
                document.Error.Details?.ToJson()) : null,
            CreatedAt = document.CreatedAt,
            UpdatedAt = document.UpdatedAt,
            CompletedAt = document.CompletedAt,
            IdempotencyKey = document.IdempotencyKey,
            Retryable = document.Retryable
        };
    }

    /// <summary>
    /// Converts an object to a BSON document.
    /// Handles both string JSON and object serialization.
    /// </summary>
    private static BsonDocument? ConvertToBsonDocument(object? data)
    {
        return data switch
        {
            null => null,
            string json when !string.IsNullOrWhiteSpace(json) => BsonDocument.Parse(json),
            string => null,
            _ => BsonDocument.Parse(JsonSerializer.Serialize(data))
        };
    }

    /// <summary>
    /// Parses an enum value with error handling.
    /// </summary>
    private static TEnum ParseEnum<TEnum>(string value, string paramName) where TEnum : struct, Enum
    {
        if (Enum.TryParse<TEnum>(value, ignoreCase: true, out var result))
        {
            return result;
        }

        throw new InvalidOperationException(
            $"Invalid {typeof(TEnum).Name} value '{value}' in {paramName}");
    }
}
