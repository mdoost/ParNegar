using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using ParNegar.Application.Interfaces.Services;
using ParNegar.Domain.Entities;
using ParNegar.Domain.Interfaces;
using ParNegar.Infrastructure.Data;

namespace ParNegar.Infrastructure.Repositories;

public class BaseRepository<T> : IRepository<T> where T : BaseEntity
{
    protected readonly ApplicationDbContext _context;
    protected readonly DbSet<T> _dbSet;
    protected readonly ILogger<BaseRepository<T>> _logger;
    private readonly ICacheService _cacheService;

    public BaseRepository(
        ApplicationDbContext context,
        ILogger<BaseRepository<T>> logger,
        ICacheService cacheService)
    {
        _context = context;
        _dbSet = context.Set<T>();
        _logger = logger;
        _cacheService = cacheService;
    }

    public virtual async Task<T?> GetByIdAsync(long id, CancellationToken cancellationToken = default)
    {
        return await _dbSet
            .Where(e => e.ID == id && e.IsActive)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public virtual async Task<T?> GetByGuidAsync(Guid guid, CancellationToken cancellationToken = default)
    {
        return await _dbSet
            .Where(e => e.GUID == guid && e.IsActive)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public virtual async Task<IEnumerable<T>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return await _dbSet
            .Where(e => e.IsActive)
            .OrderByDescending(e => e.CreatedDate)
            .ToListAsync(cancellationToken);
    }

    public virtual async Task<IEnumerable<T>> GetPagedAsync(
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        return await _dbSet
            .Where(e => e.IsActive)
            .OrderByDescending(e => e.CreatedDate)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);
    }

    public virtual async Task<IEnumerable<T>> FindAsync(
        Expression<Func<T, bool>> predicate,
        CancellationToken cancellationToken = default)
    {
        return await _dbSet
            .Where(predicate)
            .Where(e => e.IsActive)
            .ToListAsync(cancellationToken);
    }

    public virtual IQueryable<T> Query()
    {
        return _dbSet.Where(e => e.IsActive);
    }

    public virtual async Task<T> AddAsync(T entity, CancellationToken cancellationToken = default)
    {
        await _dbSet.AddAsync(entity, cancellationToken);
        _logger.LogInformation("Entity {EntityType} added", typeof(T).Name);

        // Invalidate cache for this entity type
        _cacheService.InvalidateEntity<T>();

        return entity;
    }

    public virtual async Task<IEnumerable<T>> AddRangeAsync(
        IEnumerable<T> entities,
        CancellationToken cancellationToken = default)
    {
        var entityList = entities.ToList();
        await _dbSet.AddRangeAsync(entityList, cancellationToken);
        _logger.LogInformation("Added {Count} entities of type {EntityType}", entityList.Count, typeof(T).Name);

        // Invalidate cache for this entity type
        _cacheService.InvalidateEntity<T>();

        return entityList;
    }

    public virtual Task UpdateAsync(T entity, CancellationToken cancellationToken = default)
    {
        _dbSet.Update(entity);
        _logger.LogInformation("Entity {EntityType} with ID {EntityId} updated", typeof(T).Name, entity.ID);

        // Invalidate cache for this entity type
        _cacheService.InvalidateEntity<T>();

        return Task.CompletedTask;
    }

    public virtual Task UpdateRangeAsync(
        IEnumerable<T> entities,
        CancellationToken cancellationToken = default)
    {
        var entityList = entities.ToList();
        _dbSet.UpdateRange(entityList);
        _logger.LogInformation("Updated {Count} entities of type {EntityType}", entityList.Count, typeof(T).Name);

        // Invalidate cache for this entity type
        _cacheService.InvalidateEntity<T>();

        return Task.CompletedTask;
    }

    public virtual Task DeleteAsync(T entity, CancellationToken cancellationToken = default)
    {
        entity.IsActive = false;
        _dbSet.Update(entity);
        _logger.LogInformation("Entity {EntityType} with ID {EntityId} soft deleted", typeof(T).Name, entity.ID);

        // Invalidate cache for this entity type
        _cacheService.InvalidateEntity<T>();

        return Task.CompletedTask;
    }

    public virtual Task DeleteRangeAsync(
        IEnumerable<T> entities,
        CancellationToken cancellationToken = default)
    {
        var entityList = entities.ToList();
        foreach (var entity in entityList)
        {
            entity.IsActive = false;
        }
        _dbSet.UpdateRange(entityList);
        _logger.LogInformation("Soft deleted {Count} entities of type {EntityType}", entityList.Count, typeof(T).Name);

        // Invalidate cache for this entity type
        _cacheService.InvalidateEntity<T>();

        return Task.CompletedTask;
    }

    public virtual async Task<bool> ExistsAsync(
        Expression<Func<T, bool>> predicate,
        CancellationToken cancellationToken = default)
    {
        return await _dbSet.Where(e => e.IsActive).AnyAsync(predicate, cancellationToken);
    }

    public virtual async Task<int> CountAsync(
        Expression<Func<T, bool>>? predicate = null,
        CancellationToken cancellationToken = default)
    {
        var query = _dbSet.Where(e => e.IsActive);
        return predicate == null
            ? await query.CountAsync(cancellationToken)
            : await query.CountAsync(predicate, cancellationToken);
    }
}
