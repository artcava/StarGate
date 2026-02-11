namespace StarGate.Core.Abstractions;

/// <summary>
/// Factory for retrieving process handlers by type.
/// Enables dynamic handler registration and resolution.
/// Typically implemented using dependency injection container.
/// </summary>
public interface IProcessHandlerFactory
{
    /// <summary>
    /// Gets handler for specified process type.
    /// Throws exception if handler not found to fail fast.
    /// </summary>
    /// <param name="processType">Process type identifier.</param>
    /// <returns>Handler instance.</returns>
    /// <exception cref="InvalidOperationException">If process type is not supported.</exception>
    /// <exception cref="ArgumentNullException">If processType is null.</exception>
    public IProcessHandler GetHandler(string processType);

    /// <summary>
    /// Checks if a handler exists for the specified process type.
    /// Use this before GetHandler to avoid exceptions.
    /// </summary>
    /// <param name="processType">Process type identifier.</param>
    /// <returns>True if handler exists, false otherwise.</returns>
    /// <exception cref="ArgumentNullException">If processType is null.</exception>
    public bool HasHandler(string processType);

    /// <summary>
    /// Gets all supported process types.
    /// Useful for validation and API documentation.
    /// </summary>
    /// <returns>List of supported process type identifiers.</returns>
    public IReadOnlyList<string> GetSupportedProcessTypes();
}
