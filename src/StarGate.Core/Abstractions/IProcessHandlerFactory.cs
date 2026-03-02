namespace StarGate.Core.Abstractions;

/// <summary>
/// Factory for creating and retrieving process handlers.
/// </summary>
public interface IProcessHandlerFactory
{
    /// <summary>
    /// Gets a handler for the specified process type.
    /// </summary>
    /// <param name="processType">The process type.</param>
    /// <returns>The handler, or null if no handler is registered.</returns>
    IProcessHandler? GetHandler(string processType);

    /// <summary>
    /// Registers a handler for a process type.
    /// </summary>
    /// <param name="processType">The process type.</param>
    /// <param name="handler">The handler instance.</param>
    void RegisterHandler(string processType, IProcessHandler handler);

    /// <summary>
    /// Gets all registered process types.
    /// </summary>
    /// <returns>Collection of registered process types.</returns>
    IEnumerable<string> GetRegisteredProcessTypes();

    /// <summary>
    /// Checks if a handler is registered for the specified process type.
    /// </summary>
    /// <param name="processType">The process type to check.</param>
    /// <returns>True if a handler is registered; otherwise, false.</returns>
    bool IsRegistered(string processType);
}
