using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace BizConnect.Dal.Repositories;

/// <summary>
/// Generic repository implementation providing standard CRUD operations and querying capabilities.
/// Implements async-first approach with proper Entity Framework Core integration patterns.
/// All methods are virtual to support mocking and testing scenarios.
/// </summary>
/// <typeparam name="TEntity">The entity type this repository manages</typeparam>
public class Repository<TEntity> : IRepository<TEntity> where TEntity : class
{
    protected readonly DbContext Context;
    protected readonly DbSet<TEntity> DbSet;
    private readonly ILogger<Repository<TEntity>>? _logger;

    /// <summary>
    /// Initializes a new instance of the Repository class.
    /// </summary>
    /// <param name="context">The Entity Framework database context</param>
    /// <param name="logger">Optional logger for operation tracking and error reporting</param>
    public Repository(DbContext context, ILogger<Repository<TEntity>>? logger = null)
    {
        Context = context ?? throw new ArgumentNullException(nameof(context));
        DbSet = context.Set<TEntity>();
        _logger = logger;
    }

    #region Single Entity Operations

    /// <inheritdoc />
    public virtual async Task<TEntity?> GetByIdAsync(object id, CancellationToken cancellationToken = default)
    {
        try
        {
            if (id == null)
            {
                _logger?.LogWarning("GetByIdAsync called with null id for entity type {EntityType}", typeof(TEntity).Name);
                return null;
            }

            _logger?.LogDebug("Getting entity {EntityType} by id {Id}", typeof(TEntity).Name, id);

            // For read-only operations, use AsNoTracking for better performance
            var keyProperty = Context.Model.FindEntityType(typeof(TEntity))?.FindPrimaryKey()?.Properties.FirstOrDefault();
            if (keyProperty == null)
            {
                _logger?.LogError("No primary key found for entity type {EntityType}", typeof(TEntity).Name);
                throw new InvalidOperationException($"Entity type {typeof(TEntity).Name} does not have a primary key defined.");
            }

            var parameter = Expression.Parameter(typeof(TEntity), "e");
            var property = Expression.Property(parameter, keyProperty.Name);
            var constant = Expression.Constant(id);
            var equal = Expression.Equal(property, Expression.Convert(constant, property.Type));
            var lambda = Expression.Lambda<Func<TEntity, bool>>(equal, parameter);

            return await DbSet.AsNoTracking().FirstOrDefaultAsync(lambda, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error getting entity {EntityType} by id {Id}", typeof(TEntity).Name, id);
            throw;
        }
    }

    /// <inheritdoc />
    public virtual async Task<TEntity?> GetByIdWithTrackingAsync(object id, CancellationToken cancellationToken = default)
    {
        try
        {
            if (id == null)
            {
                _logger?.LogWarning("GetByIdWithTrackingAsync called with null id for entity type {EntityType}", typeof(TEntity).Name);
                return null;
            }

            _logger?.LogDebug("Getting tracked entity {EntityType} by id {Id}", typeof(TEntity).Name, id);

            return await DbSet.FindAsync(new[] { id }, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error getting tracked entity {EntityType} by id {Id}", typeof(TEntity).Name, id);
            throw;
        }
    }

    /// <inheritdoc />
    public virtual async Task<TEntity?> FirstOrDefaultAsync(Expression<Func<TEntity, bool>> predicate, CancellationToken cancellationToken = default)
    {
        try
        {
            if (predicate == null)
                throw new ArgumentNullException(nameof(predicate));

            _logger?.LogDebug("Getting first entity {EntityType} with predicate", typeof(TEntity).Name);

            return await DbSet.AsNoTracking().FirstOrDefaultAsync(predicate, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error getting first entity {EntityType} with predicate", typeof(TEntity).Name);
            throw;
        }
    }

    /// <inheritdoc />
    public virtual async Task<TEntity?> FirstOrDefaultWithTrackingAsync(Expression<Func<TEntity, bool>> predicate, CancellationToken cancellationToken = default)
    {
        try
        {
            if (predicate == null)
                throw new ArgumentNullException(nameof(predicate));

            _logger?.LogDebug("Getting first tracked entity {EntityType} with predicate", typeof(TEntity).Name);

            return await DbSet.FirstOrDefaultAsync(predicate, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error getting first tracked entity {EntityType} with predicate", typeof(TEntity).Name);
            throw;
        }
    }

    #endregion

    #region Collection Operations

    /// <inheritdoc />
    public virtual async Task<IEnumerable<TEntity>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            _logger?.LogDebug("Getting all entities of type {EntityType}", typeof(TEntity).Name);

            return await DbSet.AsNoTracking().ToListAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error getting all entities of type {EntityType}", typeof(TEntity).Name);
            throw;
        }
    }

    /// <inheritdoc />
    public virtual async Task<IEnumerable<TEntity>> FindAsync(Expression<Func<TEntity, bool>> predicate, CancellationToken cancellationToken = default)
    {
        try
        {
            if (predicate == null)
                throw new ArgumentNullException(nameof(predicate));

            _logger?.LogDebug("Finding entities {EntityType} with predicate", typeof(TEntity).Name);

            return await DbSet.AsNoTracking().Where(predicate).ToListAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error finding entities {EntityType} with predicate", typeof(TEntity).Name);
            throw;
        }
    }

    /// <inheritdoc />
    public virtual async Task<IEnumerable<TEntity>> FindWithTrackingAsync(Expression<Func<TEntity, bool>> predicate, CancellationToken cancellationToken = default)
    {
        try
        {
            if (predicate == null)
                throw new ArgumentNullException(nameof(predicate));

            _logger?.LogDebug("Finding tracked entities {EntityType} with predicate", typeof(TEntity).Name);

            return await DbSet.Where(predicate).ToListAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error finding tracked entities {EntityType} with predicate", typeof(TEntity).Name);
            throw;
        }
    }

    #endregion

    #region Pagination Support

    /// <inheritdoc />
    public virtual async Task<PagedResult<TEntity>> GetPagedAsync(
        int pageNumber,
        int pageSize,
        Expression<Func<TEntity, bool>>? predicate = null,
        Expression<Func<TEntity, object>>? orderBy = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            if (pageNumber < 1)
                throw new ArgumentException("Page number must be greater than 0", nameof(pageNumber));

            if (pageSize < 1)
                throw new ArgumentException("Page size must be greater than 0", nameof(pageSize));

            _logger?.LogDebug("Getting paged entities {EntityType} - Page: {PageNumber}, Size: {PageSize}",
                typeof(TEntity).Name, pageNumber, pageSize);

            IQueryable<TEntity> query = DbSet.AsNoTracking();

            // Apply filter if provided
            if (predicate != null)
            {
                query = query.Where(predicate);
            }

            // Get total count before pagination
            var totalCount = await query.CountAsync(cancellationToken);

            // Apply ordering - default to primary key if no order specified
            if (orderBy != null)
            {
                query = query.OrderBy(orderBy);
            }
            else
            {
                // Try to order by primary key as default
                var keyProperty = Context.Model.FindEntityType(typeof(TEntity))?.FindPrimaryKey()?.Properties.FirstOrDefault();
                if (keyProperty != null)
                {
                    var parameter = Expression.Parameter(typeof(TEntity), "e");
                    var property = Expression.Property(parameter, keyProperty.Name);
                    var lambda = Expression.Lambda<Func<TEntity, object>>(
                        Expression.Convert(property, typeof(object)), parameter);
                    query = query.OrderBy(lambda);
                }
            }

            // Apply pagination
            var items = await query
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync(cancellationToken);

            return new PagedResult<TEntity>
            {
                Items = items,
                PageNumber = pageNumber,
                PageSize = pageSize,
                TotalCount = totalCount
            };
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error getting paged entities {EntityType} - Page: {PageNumber}, Size: {PageSize}",
                typeof(TEntity).Name, pageNumber, pageSize);
            throw;
        }
    }

    /// <inheritdoc />
    public virtual async Task<int> CountAsync(Expression<Func<TEntity, bool>>? predicate = null, CancellationToken cancellationToken = default)
    {
        try
        {
            _logger?.LogDebug("Counting entities {EntityType}", typeof(TEntity).Name);

            IQueryable<TEntity> query = DbSet;

            if (predicate != null)
            {
                query = query.Where(predicate);
            }

            return await query.CountAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error counting entities {EntityType}", typeof(TEntity).Name);
            throw;
        }
    }

    #endregion

    #region Query Operations

    /// <inheritdoc />
    public virtual IQueryable<TEntity> Query()
    {
        _logger?.LogDebug("Providing query access for entity type {EntityType}", typeof(TEntity).Name);
        return DbSet.AsNoTracking();
    }

    /// <inheritdoc />
    public virtual IQueryable<TEntity> QueryWithTracking()
    {
        _logger?.LogDebug("Providing tracked query access for entity type {EntityType}", typeof(TEntity).Name);
        return DbSet;
    }

    #endregion

    #region Existence Checks

    /// <inheritdoc />
    public virtual async Task<bool> AnyAsync(Expression<Func<TEntity, bool>> predicate, CancellationToken cancellationToken = default)
    {
        try
        {
            if (predicate == null)
                throw new ArgumentNullException(nameof(predicate));

            _logger?.LogDebug("Checking existence of entities {EntityType} with predicate", typeof(TEntity).Name);

            return await DbSet.AnyAsync(predicate, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error checking existence of entities {EntityType} with predicate", typeof(TEntity).Name);
            throw;
        }
    }

    /// <inheritdoc />
    public virtual async Task<bool> AnyAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            _logger?.LogDebug("Checking existence of any entities {EntityType}", typeof(TEntity).Name);

            return await DbSet.AnyAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error checking existence of any entities {EntityType}", typeof(TEntity).Name);
            throw;
        }
    }

    #endregion

    #region Modification Operations

    /// <inheritdoc />
    public virtual async Task<TEntity> AddAsync(TEntity entity)
    {
        try
        {
            if (entity == null)
                throw new ArgumentNullException(nameof(entity));

            _logger?.LogDebug("Adding entity {EntityType}", typeof(TEntity).Name);

            var entityEntry = await DbSet.AddAsync(entity);
            return entityEntry.Entity;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error adding entity {EntityType}", typeof(TEntity).Name);
            throw;
        }
    }

    /// <inheritdoc />
    public virtual async Task AddRangeAsync(IEnumerable<TEntity> entities)
    {
        try
        {
            if (entities == null)
                throw new ArgumentNullException(nameof(entities));

            var entitiesList = entities.ToList();
            if (!entitiesList.Any())
                return;

            _logger?.LogDebug("Adding {Count} entities of type {EntityType}", entitiesList.Count, typeof(TEntity).Name);

            await DbSet.AddRangeAsync(entitiesList);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error adding entities {EntityType}", typeof(TEntity).Name);
            throw;
        }
    }

    /// <inheritdoc />
    public virtual void Update(TEntity entity)
    {
        try
        {
            if (entity == null)
                throw new ArgumentNullException(nameof(entity));

            _logger?.LogDebug("Updating entity {EntityType}", typeof(TEntity).Name);

            DbSet.Update(entity);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error updating entity {EntityType}", typeof(TEntity).Name);
            throw;
        }
    }

    /// <inheritdoc />
    public virtual void UpdateRange(IEnumerable<TEntity> entities)
    {
        try
        {
            if (entities == null)
                throw new ArgumentNullException(nameof(entities));

            var entitiesList = entities.ToList();
            if (!entitiesList.Any())
                return;

            _logger?.LogDebug("Updating {Count} entities of type {EntityType}", entitiesList.Count, typeof(TEntity).Name);

            DbSet.UpdateRange(entitiesList);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error updating entities {EntityType}", typeof(TEntity).Name);
            throw;
        }
    }

    /// <inheritdoc />
    public virtual void Delete(TEntity entity)
    {
        try
        {
            if (entity == null)
                throw new ArgumentNullException(nameof(entity));

            _logger?.LogDebug("Deleting entity {EntityType}", typeof(TEntity).Name);

            DbSet.Remove(entity);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error deleting entity {EntityType}", typeof(TEntity).Name);
            throw;
        }
    }

    /// <inheritdoc />
    public virtual async Task<bool> DeleteAsync(object id, CancellationToken cancellationToken = default)
    {
        try
        {
            if (id == null)
            {
                _logger?.LogWarning("DeleteAsync called with null id for entity type {EntityType}", typeof(TEntity).Name);
                return false;
            }

            _logger?.LogDebug("Deleting entity {EntityType} by id {Id}", typeof(TEntity).Name, id);

            var entity = await GetByIdWithTrackingAsync(id, cancellationToken);
            if (entity == null)
            {
                _logger?.LogWarning("Entity {EntityType} with id {Id} not found for deletion", typeof(TEntity).Name, id);
                return false;
            }

            Delete(entity);
            return true;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error deleting entity {EntityType} by id {Id}", typeof(TEntity).Name, id);
            throw;
        }
    }

    /// <inheritdoc />
    public virtual void DeleteRange(IEnumerable<TEntity> entities)
    {
        try
        {
            if (entities == null)
                throw new ArgumentNullException(nameof(entities));

            var entitiesList = entities.ToList();
            if (!entitiesList.Any())
                return;

            _logger?.LogDebug("Deleting {Count} entities of type {EntityType}", entitiesList.Count, typeof(TEntity).Name);

            DbSet.RemoveRange(entitiesList);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error deleting entities {EntityType}", typeof(TEntity).Name);
            throw;
        }
    }

    /// <inheritdoc />
    public virtual async Task<int> DeleteWhereAsync(Expression<Func<TEntity, bool>> predicate, CancellationToken cancellationToken = default)
    {
        try
        {
            if (predicate == null)
                throw new ArgumentNullException(nameof(predicate));

            _logger?.LogDebug("Deleting entities {EntityType} with predicate", typeof(TEntity).Name);

            // Find entities to delete (need to track them for deletion)
            var entitiesToDelete = await DbSet.Where(predicate).ToListAsync(cancellationToken);

            if (entitiesToDelete.Any())
            {
                DbSet.RemoveRange(entitiesToDelete);
                _logger?.LogDebug("Marked {Count} entities of type {EntityType} for deletion", entitiesToDelete.Count, typeof(TEntity).Name);
            }

            return entitiesToDelete.Count;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error deleting entities {EntityType} with predicate", typeof(TEntity).Name);
            throw;
        }
    }

    #endregion
}