using ParNegar.Application.Common;

namespace ParNegar.Application.Extensions;

/// <summary>
/// Simple cache extensions - just add .Cached("key") to any async query!
/// </summary>
public static class CacheExtensions
{
    private static readonly TimeSpan DefaultExpiration = TimeSpan.FromMinutes(30);

    /// <summary>
    /// Cache any async result - super simple!
    /// Usage: await GetDataAsync().Cached("mykey")
    /// </summary>
    public static async Task<T> Cached<T>(
        this Task<T> task,
        string cacheKey,
        TimeSpan? expiration = null) where T : class
    {
        var cacheService = CacheManager.Instance;
        var exp = expiration ?? DefaultExpiration;

        return await cacheService.GetOrCreateAsync(
            cacheKey,
            async () => await task,
            exp) ?? await task;
    }

    /// <summary>
    /// Cache with entity-based key generation
    /// Usage: await GetDataAsync().Cached<Company>("combobox")
    /// </summary>
    public static async Task<T> Cached<T, TEntity>(
        this Task<T> task,
        string suffix,
        TimeSpan? expiration = null)
        where T : class
        where TEntity : class
    {
        var cacheKey = CacheManager.GenerateKey<TEntity>(suffix);
        return await task.Cached(cacheKey, expiration);
    }

    /// <summary>
    /// Cache a list result - even simpler!
    /// Usage: await query.ToListAsync().CachedList("key")
    /// </summary>
    public static async Task<List<T>> CachedList<T>(
        this Task<List<T>> task,
        string cacheKey,
        TimeSpan? expiration = null)
    {
        var cacheService = CacheManager.Instance;
        var exp = expiration ?? DefaultExpiration;

        return await cacheService.GetOrCreateAsync(
            cacheKey,
            async () => await task,
            exp) ?? await task;
    }

    /// <summary>
    /// Cache list with entity-based key
    /// Usage: await query.ToListAsync().CachedList<Company>("list")
    /// </summary>
    public static async Task<List<T>> CachedList<T, TEntity>(
        this Task<List<T>> task,
        string suffix,
        TimeSpan? expiration = null)
        where TEntity : class
    {
        var cacheKey = CacheManager.GenerateKey<TEntity>(suffix);
        return await task.CachedList(cacheKey, expiration);
    }

    /// <summary>
    /// Cache list with custom expiration - shortest syntax!
    /// Usage: await query.ToListAsync().CachedList("key", minutes: 15)
    /// </summary>
    public static Task<List<T>> CachedList<T>(
        this Task<List<T>> task,
        string cacheKey,
        int minutes)
    {
        return task.CachedList(cacheKey, TimeSpan.FromMinutes(minutes));
    }
}
