namespace StarGate.Infrastructure.Persistence;

/// <summary>
/// Configuration options for MongoDB connection and behavior.
/// Binds to "MongoDB" section in appsettings.json.
/// </summary>
public class MongoDbOptions
{
    /// <summary>
    /// Configuration section name.
    /// </summary>
    public const string SectionName = "MongoDB";

    /// <summary>
    /// MongoDB connection string.
    /// Example: "mongodb://localhost:27017" or "mongodb+srv://user:pass@cluster.mongodb.net"
    /// </summary>
    public required string ConnectionString { get; init; }

    /// <summary>
    /// Database name for StarGate collections.
    /// Default: "stargate"
    /// </summary>
    public string DatabaseName { get; init; } = "stargate";

    /// <summary>
    /// Whether to automatically create indexes on startup.
    /// Default: true
    /// </summary>
    public bool CreateIndexesOnStartup { get; init; } = true;

    /// <summary>
    /// Connection timeout in milliseconds.
    /// Default: 30000 (30 seconds)
    /// </summary>
    public int ConnectionTimeoutMs { get; init; } = 30000;

    /// <summary>
    /// Server selection timeout in milliseconds.
    /// Default: 30000 (30 seconds)
    /// </summary>
    public int ServerSelectionTimeoutMs { get; init; } = 30000;
}
