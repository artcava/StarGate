namespace StarGate.Api.Models;

/// <summary>
/// Response model containing the generated JWT token.
/// </summary>
public sealed record GenerateTokenResponse
{
    /// <summary>
    /// The generated JWT token.
    /// </summary>
    public required string Token { get; init; }

    /// <summary>
    /// Token expiration timestamp.
    /// </summary>
    public required DateTime ExpiresAt { get; init; }

    /// <summary>
    /// Client identifier included in the token.
    /// </summary>
    public required string ClientId { get; init; }

    /// <summary>
    /// Process types included in the token.
    /// </summary>
    public required IReadOnlyList<string> ProcessTypes { get; init; }
}
