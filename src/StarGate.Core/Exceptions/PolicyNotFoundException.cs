namespace StarGate.Core.Exceptions;

/// <summary>
/// Exception thrown when a policy is not found for a client and process type.
/// This typically indicates a configuration issue where a process type
/// has not been properly configured with a default policy.
/// </summary>
public class PolicyNotFoundException : DomainException
{
    /// <summary>
    /// Gets the client identifier for which the policy was not found.
    /// </summary>
    public string ClientId { get; }

    /// <summary>
    /// Gets the process type for which the policy was not found.
    /// </summary>
    public string ProcessType { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="PolicyNotFoundException"/> class.
    /// </summary>
    /// <param name="clientId">The client identifier.</param>
    /// <param name="processType">The process type.</param>
    public PolicyNotFoundException(string clientId, string processType)
        : base($"No policy found for ClientId='{clientId}', ProcessType='{processType}'")
    {
        ClientId = clientId;
        ProcessType = processType;
    }
}
