using StarGate.Core.Domain;

namespace StarGate.Core.Abstractions;

/// <summary>
/// Interface for process handlers that execute business logic for specific process types.
/// </summary>
public interface IProcessHandler
{
    /// <summary>
    /// Gets the process type this handler supports.
    /// </summary>
    public string ProcessType { get; }

    /// <summary>
    /// Executes the business logic for the process.
    /// </summary>
    /// <param name="context">Process execution context.</param>
    /// <returns>Task representing the asynchronous operation.</returns>
    /// <exception cref="InvalidOperationException">Thrown when process cannot be executed.</exception>
    /// <exception cref="TimeoutException">Thrown when execution exceeds timeout.</exception>
    public Task ExecuteAsync(ProcessContext context);
}
