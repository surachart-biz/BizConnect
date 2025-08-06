using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading.Tasks;

namespace BizConnect.Dal.Repositories
{
    /// <summary>
    /// Generic repository interface providing standard CRUD operations
    /// </summary>
    /// <typeparam name="T">Entity type</typeparam>
    public interface IRepository<T> where T : class
    {
        /// <summary>
        /// Get all entities as queryable for advanced filtering and projection
        /// </summary>
        /// <returns>IQueryable for further query composition</returns>
        IQueryable<T> GetAll();

        /// <summary>
        /// Get all entities as read-only queryable (no tracking for better performance)
        /// </summary>
        /// <returns>IQueryable for read-only query composition</returns>
        IQueryable<T> Query();

        /// <summary>
        /// Get all entities as queryable with change tracking enabled
        /// </summary>
        /// <returns>IQueryable with change tracking for updates</returns>
        IQueryable<T> QueryWithTracking();

        /// <summary>
        /// Get entity by primary key
        /// </summary>
        /// <param name="id">Primary key value</param>
        /// <returns>Entity or null if not found</returns>
        Task<T> GetByIdAsync(object id);

        /// <summary>
        /// Find entities matching the given predicate
        /// </summary>
        /// <param name="predicate">Search condition</param>
        /// <returns>Matching entities</returns>
        IQueryable<T> Find(Expression<Func<T, bool>> predicate);

        /// <summary>
        /// Get the first entity matching the predicate, or null
        /// </summary>
        /// <param name="predicate">Search condition</param>
        /// <returns>First matching entity or null</returns>
        Task<T> FirstOrDefaultAsync(Expression<Func<T, bool>> predicate);

        /// <summary>
        /// Check if any entity matches the predicate
        /// </summary>
        /// <param name="predicate">Search condition</param>
        /// <returns>True if any entity matches</returns>
        Task<bool> AnyAsync(Expression<Func<T, bool>> predicate);

        /// <summary>
        /// Count entities matching the predicate
        /// </summary>
        /// <param name="predicate">Search condition (optional)</param>
        /// <returns>Number of matching entities</returns>
        Task<int> CountAsync(Expression<Func<T, bool>> predicate = null);

        /// <summary>
        /// Add entity to the context (not saved until SaveChanges)
        /// </summary>
        /// <param name="entity">Entity to add</param>
        /// <returns>Added entity</returns>
        Task<T> AddAsync(T entity);

        /// <summary>
        /// Add multiple entities to the context
        /// </summary>
        /// <param name="entities">Entities to add</param>
        Task AddRangeAsync(IEnumerable<T> entities);

        /// <summary>
        /// Update entity (changes tracked automatically if entity is being tracked)
        /// </summary>
        /// <param name="entity">Entity to update</param>
        /// <returns>Updated entity</returns>
        T Update(T entity);

        /// <summary>
        /// Update multiple entities
        /// </summary>
        /// <param name="entities">Entities to update</param>
        void UpdateRange(IEnumerable<T> entities);

        /// <summary>
        /// Remove entity from the context
        /// </summary>
        /// <param name="entity">Entity to remove</param>
        void Remove(T entity);

        /// <summary>
        /// Remove multiple entities from the context
        /// </summary>
        /// <param name="entities">Entities to remove</param>
        void RemoveRange(IEnumerable<T> entities);

        /// <summary>
        /// Remove entity by primary key
        /// </summary>
        /// <param name="id">Primary key value</param>
        /// <returns>True if entity was removed</returns>
        Task<bool> RemoveByIdAsync(object id);
    }
}