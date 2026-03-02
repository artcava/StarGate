namespace StarGate.Server.Factories;

using Microsoft.Extensions.Logging;
using StarGate.Core.Abstractions;
using System.Collections.Concurrent;

/// <summary>
/// Factory for managing process handler registration and retrieval.
/// </summary>
public class ProcessHandlerFactory : IProcessHandlerFactory
{
    private readonly ConcurrentDictionary<string, IProcessHandler> _handlers;
    private readonly ILogger<ProcessHandlerFactory> _logger;

    public ProcessHandlerFactory(ILogger<ProcessHandlerFactory> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _handlers = new ConcurrentDictionary<string, IProcessHandler>(StringComparer.OrdinalIgnoreCase);
    }

    public IProcessHandler? GetHandler(string processType)
    {
        if (string.IsNullOrWhiteSpace(processType))
        {
            _logger.LogWarning("GetHandler called with null or empty processType");
            return null;
        }

        if (_handlers.TryGetValue(processType, out var handler))
        {
            _logger.LogDebug(
                "Handler found for process type: ProcessType={ProcessType}, HandlerType={HandlerType}",
                processType,
                handler.GetType().Name);

            return handler;
        }

        _logger.LogWarning(
            "No handler registered for process type: ProcessType={ProcessType}",
            processType);

        return null;
    }

    public void RegisterHandler(string processType, IProcessHandler handler)
    {
        if (string.IsNullOrWhiteSpace(processType))
        {
            throw new ArgumentException(
                "Process type cannot be null or empty",
                nameof(processType));
        }

        if (handler == null)
        {
            throw new ArgumentNullException(nameof(handler));
        }

        // Validate handler process type matches
        if (!string.Equals(handler.ProcessType, processType, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"Handler ProcessType '{handler.ProcessType}' does not match registration key '{processType}'");
        }

        if (_handlers.TryAdd(processType, handler))
        {
            _logger.LogInformation(
                "Handler registered: ProcessType={ProcessType}, HandlerType={HandlerType}",
                processType,
                handler.GetType().Name);
        }
        else
        {
            _logger.LogWarning(
                "Handler already registered for process type: ProcessType={ProcessType}, ExistingHandlerType={ExistingHandlerType}",
                processType,
                _handlers[processType].GetType().Name);

            throw new InvalidOperationException(
                $"Handler already registered for process type '{processType}'");
        }
    }

    public IEnumerable<string> GetRegisteredProcessTypes()
    {
        return _handlers.Keys.ToList();
    }

    public bool IsRegistered(string processType)
    {
        if (string.IsNullOrWhiteSpace(processType))
        {
            return false;
        }

        return _handlers.ContainsKey(processType);
    }
}
