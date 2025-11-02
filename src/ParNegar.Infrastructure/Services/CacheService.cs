using ParNegar.Application.Interfaces.Services;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;

namespace ParNegar.Infrastructure.Services;

public class CacheService : ICacheService
{
    private readonly IMemoryCache _cache;
    private readonly ILogger<CacheService> _logger;
    private static readonly ConcurrentDictionary<string, byte> _cacheKeys = new();
    private static readonly TimeSpan DefaultExpiration = TimeSpan.FromMinutes(30);

    public CacheService(IMemoryCache cache, ILogger<CacheService> logger)
    {
        _cache = cache;
        _logger = logger;
    }

    public async Task<T?> GetOrCreateAsync<T>(string key, Func<Task<T>> factory, TimeSpan? expiration = null)
    {
        if (_cache.TryGetValue(key, out T? cachedValue))
        {
            _logger.LogDebug("Cache HIT for key: {Key}", key);
            return cachedValue;
        }

        _logger.LogDebug("Cache MISS for key: {Key}", key);

        var value = await factory();

        if (value != null)
        {
            Set(key, value, expiration);
        }

        return value;
    }

    public T? Get<T>(string key)
    {
        if (_cache.TryGetValue(key, out T? value))
        {
            _logger.LogDebug("Cache HIT for key: {Key}", key);
            return value;
        }

        _logger.LogDebug("Cache MISS for key: {Key}", key);
        return default;
    }

    public void Set<T>(string key, T value, TimeSpan? expiration = null)
    {
        var cacheExpiration = expiration ?? DefaultExpiration;

        var cacheEntryOptions = new MemoryCacheEntryOptions()
            .SetSlidingExpiration(cacheExpiration)
            .RegisterPostEvictionCallback((k, v, r, s) =>
            {
                _cacheKeys.TryRemove(k.ToString()!, out _);
                _logger.LogDebug("Cache entry evicted: {Key}, Reason: {Reason}", k, r);
            });

        _cache.Set(key, value, cacheEntryOptions);
        _cacheKeys.TryAdd(key, 0);

        _logger.LogDebug("Cache SET for key: {Key} with expiration: {Expiration}", key, cacheExpiration);
    }

    public void Remove(string key)
    {
        _cache.Remove(key);
        _cacheKeys.TryRemove(key, out _);
        _logger.LogDebug("Cache REMOVED for key: {Key}", key);
    }

    public void RemoveByPrefix(string prefix)
    {
        var keysToRemove = _cacheKeys.Keys
            .Where(k => k.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            .ToList();

        foreach (var key in keysToRemove)
        {
            Remove(key);
        }

        _logger.LogInformation("Cache INVALIDATED by prefix: {Prefix}, Removed {Count} entries", prefix, keysToRemove.Count);
    }

    public void InvalidateEntity<TEntity>() where TEntity : class
    {
        var entityName = typeof(TEntity).Name;
        RemoveByPrefix($"Entity:{entityName}:");
        _logger.LogInformation("Cache INVALIDATED for entity: {Entity}", entityName);
    }

    public void Clear()
    {
        var allKeys = _cacheKeys.Keys.ToList();
        foreach (var key in allKeys)
        {
            Remove(key);
        }

        _logger.LogWarning("Cache CLEARED. Removed {Count} entries", allKeys.Count);
    }

    public string GenerateKey<TEntity>(string suffix = "") where TEntity : class
    {
        var entityName = typeof(TEntity).Name;
        var key = string.IsNullOrEmpty(suffix)
            ? $"Entity:{entityName}"
            : $"Entity:{entityName}:{suffix}";

        return key;
    }
}
