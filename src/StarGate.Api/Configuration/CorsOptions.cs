namespace StarGate.Api.Configuration;

/// <summary>
/// Configuration options for CORS.
/// </summary>
public class CorsOptions
{
    public const string SectionName = "Cors";

    /// <summary>
    /// Whether CORS is enabled.
    /// </summary>
    public bool Enabled { get; init; } = true;

    /// <summary>
    /// List of allowed origins.
    /// </summary>
    public List<string> AllowedOrigins { get; init; } = new();

    /// <summary>
    /// Whether to allow any origin (for development only).
    /// </summary>
    public bool AllowAnyOrigin { get; init; } = false;

    /// <summary>
    /// List of allowed HTTP methods.
    /// </summary>
    public List<string> AllowedMethods { get; init; } = new() { "GET", "POST", "PUT", "DELETE", "OPTIONS" };

    /// <summary>
    /// List of allowed headers.
    /// </summary>
    public List<string> AllowedHeaders { get; init; } = new() { "*" };

    /// <summary>
    /// List of exposed headers.
    /// </summary>
    public List<string> ExposedHeaders { get; init; } = new();

    /// <summary>
    /// Whether to allow credentials (cookies, authorization headers).
    /// </summary>
    public bool AllowCredentials { get; init; } = true;

    /// <summary>
    /// Maximum age for preflight cache in seconds.
    /// </summary>
    public int PreflightMaxAgeSeconds { get; init; } = 600; // 10 minutes
}
