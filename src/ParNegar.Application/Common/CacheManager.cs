using ParNegar.Application.Interfaces.Services;

namespace ParNegar.Application.Common;

/// <summary>
/// Static cache manager for easy access to caching functionality
/// </summary>
public static class CacheManager
{
    private static ICacheService? _cacheService;

    /// <summary>
    /// Initialize the cache manager (called automatically from DI)
    /// </summary>
    public static void Initialize(ICacheService cacheService)
    {
        _cacheService = cacheService;
    }

    /// <summary>
    /// Get the cache service instance
    /// </summary>
    public static ICacheService Instance
    {
        get
        {
            if (_cacheService == null)
            {
                throw new InvalidOperationException("CacheManager not initialized. Make sure cache service is registered in Program.cs");
            }
            return _cacheService;
        }
    }

    /// <summary>
    /// Generate cache key for entity
    /// </summary>
    public static string GenerateKey<TEntity>(string suffix = "") where TEntity : class
    {
        return Instance.GenerateKey<TEntity>(suffix);
    }

    /// <summary>
    /// Invalidate all cache entries for an entity
    /// </summary>
    public static void InvalidateEntity<TEntity>() where TEntity : class
    {
        Instance.InvalidateEntity<TEntity>();
    }
}
