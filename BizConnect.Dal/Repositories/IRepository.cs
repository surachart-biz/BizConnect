using System.Linq.Expressions;

namespace BizConnect.Dal.Repositories;

/// <summary>
/// Generic repository interface providing standard CRUD operations and querying capabilities.
/// Follows async-first approach with proper Entity Framework Core integration patterns.
/// </summary>
/// <typeparam name="TEntity">The entity type this repository manages</typeparam>
public interface IRepository<TEntity> where TEntity : class
{
    #region Single Entity Operations

    /// <summary>
    /// Retrieves an entity by its primary key asynchronously.
    /// Uses AsNoTracking() for read-only operations to improve performance.
    /// </summary>
    /// <param name="id">The primary key value</param>
    /// <param name="cancellationToken">Cancellation token for async operation</param>
    /// <returns>The entity if found, null otherwise</returns>
    Task<TEntity?> GetByIdAsync(object id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves an entity by its primary key with change tracking enabled.
    /// Use this when you plan to modify the entity after retrieval.
    /// </summary>
    /// <param name="id">The primary key value</param>
    /// <param name="cancellationToken">Cancellation token for async operation</param>
    /// <returns>The tracked entity if found, null otherwise</returns>
    Task<TEntity?> GetByIdWithTrackingAsync(object id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Finds the first entity matching the specified predicate.
    /// Uses AsNoTracking() for read-only operations.
    /// </summary>
    /// <param name="predicate">Expression to filter entities</param>
    /// <param name="cancellationToken">Cancellation token for async operation</param>
    /// <returns>The first matching entity if found, null otherwise</returns>
    Task<TEntity?> FirstOrDefaultAsync(Expression<Func<TEntity, bool>> predicate, CancellationToken cancellationToken = default);

    /// <summary>
    /// Finds the first entity matching the specified predicate with change tracking enabled.
    /// Use this when you plan to modify the entity after retrieval.
    /// </summary>
    /// <param name="predicate">Expression to filter entities</param>
    /// <param name="cancellationToken">Cancellation token for async operation</param>
    /// <returns>The first matching tracked entity if found, null otherwise</returns>
    Task<TEntity?> FirstOrDefaultWithTrackingAsync(Expression<Func<TEntity, bool>> predicate, CancellationToken cancellationToken = default);

    #endregion

    #region Collection Operations

    /// <summary>
    /// Retrieves all entities from the repository.
    /// Uses AsNoTracking() for read-only operations.
    /// WARNING: Use with caution on large datasets - consider pagination instead.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token for async operation</param>
    /// <returns>Collection of all entities</returns>
    Task<IEnumerable<TEntity>> GetAllAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Finds all entities matching the specified predicate.
    /// Uses AsNoTracking() for read-only operations.
    /// </summary>
    /// <param name="predicate">Expression to filter entities</param>
    /// <param name="cancellationToken">Cancellation token for async operation</param>
    /// <returns>Collection of matching entities</returns>
    Task<IEnumerable<TEntity>> FindAsync(Expression<Func<TEntity, bool>> predicate, CancellationToken cancellationToken = default);

    /// <summary>
    /// Finds all entities matching the specified predicate with change tracking enabled.
    /// Use this when you plan to modify the entities after retrieval.
    /// </summary>
    /// <param name="predicate">Expression to filter entities</param>
    /// <param name="cancellationToken">Cancellation token for async operation</param>
    /// <returns>Collection of matching tracked entities</returns>
    Task<IEnumerable<TEntity>> FindWithTrackingAsync(Expression<Func<TEntity, bool>> predicate, CancellationToken cancellationToken = default);

    #endregion

    #region Pagination Support

    /// <summary>
    /// Retrieves a paginated subset of entities with optional filtering and ordering.
    /// Uses AsNoTracking() for read-only operations.
    /// </summary>
    /// <param name="pageNumber">Page number (1-based)</param>
    /// <param name="pageSize">Number of items per page</param>
    /// <param name="predicate">Optional filter expression</param>
    /// <param name="orderBy">Optional ordering expression</param>
    /// <param name="cancellationToken">Cancellation token for async operation</param>
    /// <returns>Paginated result with entities and metadata</returns>
    Task<PagedResult<TEntity>> GetPagedAsync(
        int pageNumber,
        int pageSize,
        Expression<Func<TEntity, bool>>? predicate = null,
        Expression<Func<TEntity, object>>? orderBy = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the total count of entities matching the optional predicate.
    /// </summary>
    /// <param name="predicate">Optional filter expression</param>
    /// <param name="cancellationToken">Cancellation token for async operation</param>
    /// <returns>Total count of matching entities</returns>
    Task<int> CountAsync(Expression<Func<TEntity, bool>>? predicate = null, CancellationToken cancellationToken = default);

    #endregion

    #region Query Operations

    /// <summary>
    /// Provides direct access to the underlying IQueryable for complex queries.
    /// Uses AsNoTracking() for read-only operations.
    /// Advanced users can build complex LINQ queries on top of this.
    /// </summary>
    /// <returns>IQueryable for building complex queries</returns>
    IQueryable<TEntity> Query();

    /// <summary>
    /// Provides direct access to the underlying IQueryable with change tracking enabled.
    /// Use this when building complex queries that will modify entities.
    /// </summary>
    /// <returns>IQueryable with change tracking for building complex queries</returns>
    IQueryable<TEntity> QueryWithTracking();

    #endregion

    #region Existence Checks

    /// <summary>
    /// Checks if any entity exists matching the specified predicate.
    /// </summary>
    /// <param name="predicate">Expression to filter entities</param>
    /// <param name="cancellationToken">Cancellation token for async operation</param>
    /// <returns>True if any matching entity exists, false otherwise</returns>
    Task<bool> AnyAsync(Expression<Func<TEntity, bool>> predicate, CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks if any entities exist in the repository.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token for async operation</param>
    /// <returns>True if any entities exist, false otherwise</returns>
    Task<bool> AnyAsync(CancellationToken cancellationToken = default);

    #endregion

    #region Modification Operations

    /// <summary>
    /// Adds a new entity to the repository.
    /// The entity will be tracked but not persisted until UnitOfWork.SaveChangesAsync() is called.
    /// </summary>
    /// <param name="entity">The entity to add</param>
    /// <returns>The added entity (may have generated keys populated)</returns>
    Task<TEntity> AddAsync(TEntity entity);

    /// <summary>
    /// Adds multiple entities to the repository.
    /// The entities will be tracked but not persisted until UnitOfWork.SaveChangesAsync() is called.
    /// </summary>
    /// <param name="entities">The entities to add</param>
    /// <returns>Task representing the async operation</returns>
    Task AddRangeAsync(IEnumerable<TEntity> entities);

    /// <summary>
    /// Updates an existing entity in the repository.
    /// The entity will be tracked and changes persisted when UnitOfWork.SaveChangesAsync() is called.
    /// </summary>
    /// <param name="entity">The entity to update</param>
    void Update(TEntity entity);

    /// <summary>
    /// Updates multiple entities in the repository.
    /// The entities will be tracked and changes persisted when UnitOfWork.SaveChangesAsync() is called.
    /// </summary>
    /// <param name="entities">The entities to update</param>
    void UpdateRange(IEnumerable<TEntity> entities);

    /// <summary>
    /// Removes an entity from the repository.
    /// The entity will be marked for deletion and removed when UnitOfWork.SaveChangesAsync() is called.
    /// </summary>
    /// <param name="entity">The entity to remove</param>
    void Delete(TEntity entity);

    /// <summary>
    /// Removes an entity by its primary key.
    /// The entity will be loaded, marked for deletion, and removed when UnitOfWork.SaveChangesAsync() is called.
    /// </summary>
    /// <param name="id">The primary key of the entity to remove</param>
    /// <param name="cancellationToken">Cancellation token for async operation</param>
    /// <returns>True if entity was found and marked for deletion, false if not found</returns>
    Task<bool> DeleteAsync(object id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Removes multiple entities from the repository.
    /// The entities will be marked for deletion and removed when UnitOfWork.SaveChangesAsync() is called.
    /// </summary>
    /// <param name="entities">The entities to remove</param>
    void DeleteRange(IEnumerable<TEntity> entities);

    /// <summary>
    /// Removes all entities matching the specified predicate.
    /// The entities will be loaded, marked for deletion, and removed when UnitOfWork.SaveChangesAsync() is called.
    /// </summary>
    /// <param name="predicate">Expression to filter entities for deletion</param>
    /// <param name="cancellationToken">Cancellation token for async operation</param>
    /// <returns>Number of entities marked for deletion</returns>
    Task<int> DeleteWhereAsync(Expression<Func<TEntity, bool>> predicate, CancellationToken cancellationToken = default);

    #endregion
}

/// <summary>
/// Represents a paginated result set with metadata about the pagination.
/// </summary>
/// <typeparam name="T">The type of entities in the result set</typeparam>
public class PagedResult<T>
{
    /// <summary>
    /// The entities for the current page.
    /// </summary>
    public IEnumerable<T> Items { get; set; } = Enumerable.Empty<T>();

    /// <summary>
    /// The current page number (1-based).
    /// </summary>
    public int PageNumber { get; set; }

    /// <summary>
    /// The number of items per page.
    /// </summary>
    public int PageSize { get; set; }

    /// <summary>
    /// The total number of items across all pages.
    /// </summary>
    public int TotalCount { get; set; }

    /// <summary>
    /// The total number of pages.
    /// </summary>
    public int TotalPages => (int)Math.Ceiling((double)TotalCount / PageSize);

    /// <summary>
    /// Whether there is a previous page.
    /// </summary>
    public bool HasPreviousPage => PageNumber > 1;

    /// <summary>
    /// Whether there is a next page.
    /// </summary>
    public bool HasNextPage => PageNumber < TotalPages;

    /// <summary>
    /// The index of the first item on the current page (1-based).
    /// </summary>
    public int FirstItemIndex => TotalCount == 0 ? 0 : (PageNumber - 1) * PageSize + 1;

    /// <summary>
    /// The index of the last item on the current page (1-based).
    /// </summary>
    public int LastItemIndex => Math.Min(PageNumber * PageSize, TotalCount);
}