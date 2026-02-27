namespace StarGate.Api.Authorization;

/// <summary>
/// Authorization policy constants.
/// </summary>
public static class Policies
{
    /// <summary>
    /// Policy for creating processes.
    /// </summary>
    public const string CreateProcess = "CreateProcess";

    /// <summary>
    /// Policy for reading own processes.
    /// </summary>
    public const string ReadOwnProcesses = "ReadOwnProcesses";

    /// <summary>
    /// Policy for reading all processes (admin).
    /// </summary>
    public const string ReadAllProcesses = "ReadAllProcesses";

    /// <summary>
    /// Policy for administrative operations.
    /// </summary>
    public const string AdminOnly = "AdminOnly";
}

/// <summary>
/// Role constants.
/// </summary>
public static class Roles
{
    public const string Client = "client";
    public const string Admin = "admin";
    public const string ServiceAccount = "service-account";
}

/// <summary>
/// Claim type constants.
/// </summary>
public static class ClaimTypes
{
    public const string ClientId = "client_id";
    public const string Scope = "scope";
    public const string Permission = "permission";
}
