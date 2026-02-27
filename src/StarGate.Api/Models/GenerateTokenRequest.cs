namespace StarGate.Api.Models;

/// <summary>
/// Request model for generating a development JWT token.
/// </summary>
public sealed record GenerateTokenRequest
{
    /// <summary>
    /// Client identifier to include in the token.
    /// </summary>
    public required string ClientId { get; init; }

    /// <summary>
    /// Process types that the client is authorized for.
    /// </summary>
    public required IReadOnlyList<string> ProcessTypes { get; init; }

    /// <summary>
    /// Token expiration time in minutes. Default is 60 minutes.
    /// </summary>
    public int ExpirationMinutes { get; init; } = 60;
}
