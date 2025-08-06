using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using BizConnect.Dal.Models;
using BizConnect.Dal.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace BizConnect.Dal.UnitOfWork
{
    /// <summary>
    /// Unit of Work pattern implementation providing coordinated access to repositories
    /// with transactional consistency across multiple data operations
    /// </summary>
    public class UnitOfWork : IUnitOfWork
    {
        private readonly BizConnectContext _context;
        private bool _disposed = false;

        // Lazy-loaded repositories
        private IRepository<KbankOddRegistration> _kbankOddRegistrations;
        private IRepository<User> _users;
        private IRepository<Branch> _branches;

        // Generic repository cache
        private readonly Dictionary<Type, object> _repositories = new Dictionary<Type, object>();

        public UnitOfWork(BizConnectContext context)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
        }

        /// <summary>
        /// Access to the underlying database context
        /// </summary>
        public BizConnectContext Context => _context;

        /// <summary>
        /// Repository for KBank ODD Registration operations
        /// </summary>
        public IRepository<KbankOddRegistration> KbankOddRegistrations
        {
            get
            {
                if (_kbankOddRegistrations == null)
                {
                    _kbankOddRegistrations = new Repository<KbankOddRegistration>(_context);
                }
                return _kbankOddRegistrations;
            }
        }

        /// <summary>
        /// Repository for User operations
        /// </summary>
        public IRepository<User> Users
        {
            get
            {
                if (_users == null)
                {
                    _users = new Repository<User>(_context);
                }
                return _users;
            }
        }

        /// <summary>
        /// Repository for Branch operations
        /// </summary>
        public IRepository<Branch> Branches
        {
            get
            {
                if (_branches == null)
                {
                    _branches = new Repository<Branch>(_context);
                }
                return _branches;
            }
        }

        /// <summary>
        /// Save all pending changes across all repositories in a single transaction
        /// </summary>
        /// <returns>Number of affected entities</returns>
        public async Task<int> SaveChangesAsync()
        {
            return await _context.SaveChangesAsync();
        }

        /// <summary>
        /// Save all pending changes across all repositories in a single transaction
        /// </summary>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Number of affected entities</returns>
        public async Task<int> SaveChangesAsync(System.Threading.CancellationToken cancellationToken)
        {
            return await _context.SaveChangesAsync(cancellationToken);
        }

        /// <summary>
        /// Begin a new database transaction for explicit transaction control
        /// </summary>
        /// <returns>Database transaction</returns>
        public async Task<IDbContextTransaction> BeginTransactionAsync()
        {
            return await _context.Database.BeginTransactionAsync();
        }

        /// <summary>
        /// Get repository for any entity type T
        /// Uses caching to ensure single instance per entity type per unit of work
        /// </summary>
        /// <typeparam name="T">Entity type</typeparam>
        /// <returns>Repository instance for the specified entity type</returns>
        public IRepository<T> GetRepository<T>() where T : class
        {
            var entityType = typeof(T);

            if (_repositories.ContainsKey(entityType))
            {
                return (IRepository<T>)_repositories[entityType];
            }

            var repository = new Repository<T>(_context);
            _repositories.Add(entityType, repository);
            return repository;
        }

        /// <summary>
        /// Test database connection asynchronously
        /// </summary>
        /// <returns>True if connection is successful, false otherwise</returns>
        public async Task<bool> TestConnectionAsync()
        {
            try
            {
                return await _context.Database.CanConnectAsync();
            }
            catch (Exception)
            {
                return false;
            }
        }

        /// <summary>
        /// Execute operation within a transaction scope with unit of work and cancellation token
        /// </summary>
        /// <typeparam name="TResult">Return type</typeparam>
        /// <param name="operation">Operation to execute with unit of work and cancellation token</param>
        /// <returns>Operation result</returns>
        public async Task<TResult> ExecuteInTransactionAsync<TResult>(Func<IUnitOfWork, System.Threading.CancellationToken, Task<TResult>> operation)
        {
            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                var cancellationToken = new System.Threading.CancellationToken();
                var result = await operation(this, cancellationToken);
                await transaction.CommitAsync();
                return result;
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        }

        /// <summary>
        /// Dispose of the Unit of Work and its context
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Protected dispose method for proper cleanup
        /// </summary>
        /// <param name="disposing">Whether disposing from Dispose() call</param>
        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed && disposing)
            {
                _context?.Dispose();
                _disposed = true;
            }
        }
    }
}