using System;
using System.Threading.Tasks;
using BizConnect.Dal.Models;
using BizConnect.Dal.Repositories;
using Microsoft.EntityFrameworkCore;

namespace BizConnect.Dal.UnitOfWork
{
    /// <summary>
    /// Unit of Work pattern implementation for coordinated data operations
    /// Provides transactional consistency across multiple repository operations
    /// </summary>
    public interface IUnitOfWork : IDisposable
    {
        /// <summary>
        /// Access to the underlying database context for advanced operations
        /// Use sparingly - prefer repository methods for standard operations
        /// </summary>
        BizConnectContext Context { get; }

        /// <summary>
        /// Repository for KBank ODD Registration operations
        /// </summary>
        IRepository<KbankOddRegistration> KbankOddRegistrations { get; }

        /// <summary>
        /// Repository for User operations
        /// </summary>
        IRepository<User> Users { get; }

        /// <summary>
        /// Repository for Branch operations
        /// </summary>
        IRepository<Branch> Branches { get; }

        /// <summary>
        /// Get repository for any entity type T
        /// Provides generic access to repository operations for any entity
        /// </summary>
        /// <typeparam name="T">Entity type</typeparam>
        /// <returns>Repository instance for the specified entity type</returns>
        IRepository<T> GetRepository<T>() where T : class;

        /// <summary>
        /// Test database connection asynchronously
        /// </summary>
        /// <returns>True if connection is successful, false otherwise</returns>
        Task<bool> TestConnectionAsync();

        /// <summary>
        /// Save all pending changes across all repositories in a single transaction
        /// </summary>
        /// <returns>Number of affected entities</returns>
        Task<int> SaveChangesAsync();

        /// <summary>
        /// Save all pending changes across all repositories in a single transaction
        /// </summary>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Number of affected entities</returns>
        Task<int> SaveChangesAsync(System.Threading.CancellationToken cancellationToken);

        /// <summary>
        /// Begin a new database transaction
        /// Use this when you need explicit transaction control beyond SaveChangesAsync
        /// </summary>
        /// <returns>Database transaction</returns>
        Task<Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction> BeginTransactionAsync();

        /// <summary>
        /// Execute operation within a transaction scope with unit of work and cancellation token
        /// </summary>
        /// <typeparam name="TResult">Return type</typeparam>
        /// <param name="operation">Operation to execute with unit of work and cancellation token</param>
        /// <returns>Operation result</returns>
        Task<TResult> ExecuteInTransactionAsync<TResult>(Func<IUnitOfWork, System.Threading.CancellationToken, Task<TResult>> operation);
    }
}