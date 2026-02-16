namespace StarGate.Core.Abstractions;

/// <summary>
/// Generic cache store abstraction for any cacheable entity.
/// Complements IStateStore which is specific to Process entities.
/// Typically implemented with Redis or other distributed caching systems.
/// </summary>
/// <remarks>
/// This interface provides generic caching capabilities for configuration data,
/// policy information, and other non-process entities that benefit from caching.
/// For process-specific caching, use <see cref="IStateStore"/> instead.
/// </remarks>
public interface ICacheStore
{
    /// <summary>
    /// Retrieves a cached value.
    /// </summary>
    /// <typeparam name="T">Type of cached value.</typeparam>
    /// <param name="key">Cache key.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Cached value if found, null otherwise.</returns>
    public Task<T?> GetAsync<T>(string key, CancellationToken ct = default) where T : class;

    /// <summary>
    /// Stores a value in cache with TTL.
    /// </summary>
    /// <typeparam name="T">Type of value to cache.</typeparam>
    /// <param name="key">Cache key.</param>
    /// <param name="value">Value to cache.</param>
    /// <param name="ttl">Time-to-live for the cached value.</param>
    /// <param name="ct">Cancellation token.</param>
    public Task SetAsync<T>(string key, T value, TimeSpan ttl, CancellationToken ct = default) where T : class;

    /// <summary>
    /// Deletes a specific key from cache.
    /// </summary>
    /// <param name="key">Cache key to delete.</param>
    /// <param name="ct">Cancellation token.</param>
    public Task DeleteAsync(string key, CancellationToken ct = default);

    /// <summary>
    /// Deletes all keys matching a pattern (e.g., "policy:*").
    /// Supports Redis-style glob patterns:
    /// - * matches any characters
    /// - ? matches single character
    /// - [abc] matches a, b, or c
    /// </summary>
    /// <param name="pattern">Key pattern with wildcards.</param>
    /// <param name="ct">Cancellation token.</param>
    public Task DeleteByPatternAsync(string pattern, CancellationToken ct = default);

    /// <summary>
    /// Checks if a key exists in cache without retrieving its value.
    /// Lightweight operation for existence checks.
    /// </summary>
    /// <param name="key">Cache key.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>True if key exists, false otherwise.</returns>
    public Task<bool> ExistsAsync(string key, CancellationToken ct = default);
}
