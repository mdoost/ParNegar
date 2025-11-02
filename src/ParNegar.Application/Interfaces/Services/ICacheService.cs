namespace ParNegar.Application.Interfaces.Services;

public interface ICacheService
{
    /// <summary>
    /// Get value from cache or execute factory function and cache the result
    /// </summary>
    Task<T?> GetOrCreateAsync<T>(string key, Func<Task<T>> factory, TimeSpan? expiration = null);

    /// <summary>
    /// Get value from cache
    /// </summary>
    T? Get<T>(string key);

    /// <summary>
    /// Set value in cache
    /// </summary>
    void Set<T>(string key, T value, TimeSpan? expiration = null);

    /// <summary>
    /// Remove specific key from cache
    /// </summary>
    void Remove(string key);

    /// <summary>
    /// Remove all keys starting with prefix (for entity invalidation)
    /// </summary>
    void RemoveByPrefix(string prefix);

    /// <summary>
    /// Remove all cache entries for a specific entity type
    /// </summary>
    void InvalidateEntity<TEntity>() where TEntity : class;

    /// <summary>
    /// Clear all cache
    /// </summary>
    void Clear();

    /// <summary>
    /// Generate cache key for entity
    /// </summary>
    string GenerateKey<TEntity>(string suffix = "") where TEntity : class;
}
