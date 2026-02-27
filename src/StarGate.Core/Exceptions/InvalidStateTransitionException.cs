namespace StarGate.Core.Exceptions;

using StarGate.Core.Domain;

/// <summary>
/// Exception thrown when attempting an invalid process status transition.
/// </summary>
public class InvalidStateTransitionException : DomainException
{
    /// <summary>
    /// Gets the current status of the process.
    /// </summary>
    public ProcessStatus CurrentStatus { get; }

    /// <summary>
    /// Gets the target status that was attempted.
    /// </summary>
    public ProcessStatus NewStatus { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="InvalidStateTransitionException"/> class.
    /// </summary>
    /// <param name="currentStatus">Current process status.</param>
    /// <param name="newStatus">Target process status.</param>
    /// <param name="message">Error message.</param>
    public InvalidStateTransitionException(
        ProcessStatus currentStatus,
        ProcessStatus newStatus,
        string message)
        : base(message)
    {
        CurrentStatus = currentStatus;
        NewStatus = newStatus;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="InvalidStateTransitionException"/> class
    /// with a default message.
    /// </summary>
    /// <param name="currentStatus">Current process status.</param>
    /// <param name="newStatus">Target process status.</param>
    public InvalidStateTransitionException(
        ProcessStatus currentStatus,
        ProcessStatus newStatus)
        : base($"Cannot transition from {currentStatus} to {newStatus}")
    {
        CurrentStatus = currentStatus;
        NewStatus = newStatus;
    }
}
