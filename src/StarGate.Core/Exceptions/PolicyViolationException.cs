namespace StarGate.Core.Exceptions;

/// <summary>
/// Exception thrown when a policy constraint is violated.
/// Used to enforce policy-based limits such as maximum concurrent executions,
/// timeout constraints, or other policy-defined boundaries.
/// </summary>
public class PolicyViolationException : Exception
{
    /// <summary>
    /// Initializes a new instance of the <see cref="PolicyViolationException"/> class.
    /// </summary>
    /// <param name="message">Message describing the policy violation.</param>
    public PolicyViolationException(string message)
        : base(message)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="PolicyViolationException"/> class with an inner exception.
    /// </summary>
    /// <param name="message">Message describing the policy violation.</param>
    /// <param name="innerException">Inner exception that caused this policy violation.</param>
    public PolicyViolationException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
