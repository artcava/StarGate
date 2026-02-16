namespace StarGate.Application.Services;

/// <summary>
/// Represents differences between two policies.
/// Used for policy comparison and audit trail generation.
/// </summary>
public class PolicyDifference
{
    /// <summary>
    /// Indicates whether there are any differences.
    /// </summary>
    public required bool HasDifferences { get; init; }

    /// <summary>
    /// List of differences.
    /// Each string describes a specific field change.
    /// </summary>
    public required List<string> Differences { get; init; }

    /// <summary>
    /// Gets a formatted difference summary.
    /// </summary>
    /// <returns>Semicolon-separated list of differences, or "No differences" if none exist.</returns>
    public string GetSummary() =>
        HasDifferences
            ? string.Join("; ", Differences)
            : "No differences";
}
