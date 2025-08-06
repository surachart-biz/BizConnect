using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;

namespace BizConnect.Dal.Repositories
{
    /// <summary>
    /// Generic repository implementation providing standard CRUD operations
    /// </summary>
    /// <typeparam name="T">Entity type</typeparam>
    public class Repository<T> : IRepository<T> where T : class
    {
        protected readonly DbContext _context;
        protected readonly DbSet<T> _dbSet;

        public Repository(DbContext context)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _dbSet = _context.Set<T>();
        }

        /// <summary>
        /// Get all entities as queryable for advanced filtering and projection
        /// </summary>
        /// <returns>IQueryable for further query composition</returns>
        public virtual IQueryable<T> GetAll()
        {
            return _dbSet;
        }

        /// <summary>
        /// Get all entities as read-only queryable (no tracking for better performance)
        /// </summary>
        /// <returns>IQueryable for read-only query composition</returns>
        public virtual IQueryable<T> Query()
        {
            return _dbSet.AsNoTracking();
        }

        /// <summary>
        /// Get all entities as queryable with change tracking enabled
        /// </summary>
        /// <returns>IQueryable with change tracking for updates</returns>
        public virtual IQueryable<T> QueryWithTracking()
        {
            return _dbSet.AsQueryable();
        }

        /// <summary>
        /// Get entity by primary key
        /// </summary>
        /// <param name="id">Primary key value</param>
        /// <returns>Entity or null if not found</returns>
        public virtual async Task<T> GetByIdAsync(object id)
        {
            return await _dbSet.FindAsync(id);
        }

        /// <summary>
        /// Find entities matching the given predicate
        /// </summary>
        /// <param name="predicate">Search condition</param>
        /// <returns>Matching entities</returns>
        public virtual IQueryable<T> Find(Expression<Func<T, bool>> predicate)
        {
            return _dbSet.Where(predicate);
        }

        /// <summary>
        /// Get the first entity matching the predicate, or null
        /// </summary>
        /// <param name="predicate">Search condition</param>
        /// <returns>First matching entity or null</returns>
        public virtual async Task<T> FirstOrDefaultAsync(Expression<Func<T, bool>> predicate)
        {
            return await _dbSet.FirstOrDefaultAsync(predicate);
        }

        /// <summary>
        /// Check if any entity matches the predicate
        /// </summary>
        /// <param name="predicate">Search condition</param>
        /// <returns>True if any entity matches</returns>
        public virtual async Task<bool> AnyAsync(Expression<Func<T, bool>> predicate)
        {
            return await _dbSet.AnyAsync(predicate);
        }

        /// <summary>
        /// Count entities matching the predicate
        /// </summary>
        /// <param name="predicate">Search condition (optional)</param>
        /// <returns>Number of matching entities</returns>
        public virtual async Task<int> CountAsync(Expression<Func<T, bool>> predicate = null)
        {
            if (predicate == null)
                return await _dbSet.CountAsync();
            
            return await _dbSet.CountAsync(predicate);
        }

        /// <summary>
        /// Add entity to the context (not saved until SaveChanges)
        /// </summary>
        /// <param name="entity">Entity to add</param>
        /// <returns>Added entity</returns>
        public virtual async Task<T> AddAsync(T entity)
        {
            if (entity == null)
                throw new ArgumentNullException(nameof(entity));

            await _dbSet.AddAsync(entity);
            return entity;
        }

        /// <summary>
        /// Add multiple entities to the context
        /// </summary>
        /// <param name="entities">Entities to add</param>
        public virtual async Task AddRangeAsync(IEnumerable<T> entities)
        {
            if (entities == null)
                throw new ArgumentNullException(nameof(entities));

            await _dbSet.AddRangeAsync(entities);
        }

        /// <summary>
        /// Update entity (changes tracked automatically if entity is being tracked)
        /// </summary>
        /// <param name="entity">Entity to update</param>
        /// <returns>Updated entity</returns>
        public virtual T Update(T entity)
        {
            if (entity == null)
                throw new ArgumentNullException(nameof(entity));

            _dbSet.Update(entity);
            return entity;
        }

        /// <summary>
        /// Update multiple entities
        /// </summary>
        /// <param name="entities">Entities to update</param>
        public virtual void UpdateRange(IEnumerable<T> entities)
        {
            if (entities == null)
                throw new ArgumentNullException(nameof(entities));

            _dbSet.UpdateRange(entities);
        }

        /// <summary>
        /// Remove entity from the context
        /// </summary>
        /// <param name="entity">Entity to remove</param>
        public virtual void Remove(T entity)
        {
            if (entity == null)
                throw new ArgumentNullException(nameof(entity));

            _dbSet.Remove(entity);
        }

        /// <summary>
        /// Remove multiple entities from the context
        /// </summary>
        /// <param name="entities">Entities to remove</param>
        public virtual void RemoveRange(IEnumerable<T> entities)
        {
            if (entities == null)
                throw new ArgumentNullException(nameof(entities));

            _dbSet.RemoveRange(entities);
        }

        /// <summary>
        /// Remove entity by primary key
        /// </summary>
        /// <param name="id">Primary key value</param>
        /// <returns>True if entity was removed</returns>
        public virtual async Task<bool> RemoveByIdAsync(object id)
        {
            var entity = await GetByIdAsync(id);
            if (entity == null)
                return false;

            Remove(entity);
            return true;
        }
    }
}