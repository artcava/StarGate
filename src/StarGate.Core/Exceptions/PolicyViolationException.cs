namespace StarGate.Core.Exceptions;

/// <summary>
/// Exception thrown when a policy constraint is violated.
/// This occurs when a client attempts to perform an action that would
/// exceed the limits defined by their policy (e.g., concurrency limits,
/// retry limits, timeout constraints).
/// </summary>
public class PolicyViolationException : DomainException
{
    /// <summary>
    /// Gets the client identifier that violated the policy.
    /// </summary>
    public string ClientId { get; }

    /// <summary>
    /// Gets the process type associated with the policy violation.
    /// </summary>
    public string ProcessType { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="PolicyViolationException"/> class.
    /// </summary>
    /// <param name="clientId">The client identifier.</param>
    /// <param name="processType">The process type.</param>
    /// <param name="message">A detailed message describing the policy violation.</param>
    public PolicyViolationException(string clientId, string processType, string message)
        : base(message)
    {
        ClientId = clientId;
        ProcessType = processType;
    }
}
