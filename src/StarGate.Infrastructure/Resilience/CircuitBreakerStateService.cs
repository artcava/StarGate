using System.Collections.Concurrent;
using Polly.CircuitBreaker;

namespace StarGate.Infrastructure.Resilience;

/// <summary>
/// Service for tracking circuit breaker states.
/// </summary>
public class CircuitBreakerStateService
{
    private readonly ConcurrentDictionary<string, CircuitState> _states = new();

    /// <summary>
    /// Records circuit state change.
    /// </summary>
    /// <param name="circuitName">Name of the circuit.</param>
    /// <param name="state">New state of the circuit.</param>
    public void RecordStateChange(string circuitName, CircuitState state)
    {
        _states.AddOrUpdate(circuitName, state, (_, __) => state);
    }

    /// <summary>
    /// Gets current state of a circuit.
    /// </summary>
    /// <param name="circuitName">Name of the circuit.</param>
    /// <returns>Current state if circuit exists, null otherwise.</returns>
    public CircuitState? GetState(string circuitName)
    {
        return _states.TryGetValue(circuitName, out var state) ? state : null;
    }

    /// <summary>
    /// Gets all circuit states.
    /// </summary>
    /// <returns>Dictionary of circuit names and their states.</returns>
    public Dictionary<string, CircuitState> GetAllStates()
    {
        return new Dictionary<string, CircuitState>(_states);
    }

    /// <summary>
    /// Checks if any circuit is open.
    /// </summary>
    /// <returns>True if at least one circuit is open, false otherwise.</returns>
    public bool HasOpenCircuit()
    {
        return _states.Values.Any(state => state == CircuitState.Open);
    }
}
