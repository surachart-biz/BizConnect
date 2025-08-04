using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using BizConnect.Dal.Models;
using BizConnect.Dal.Repositories;

namespace BizConnect.Dal.UnitOfWork;

/// <summary>
/// Unit of Work interface providing centralized repository access and transaction management.
/// Implements the Unit of Work pattern to ensure consistency across multiple repository operations.
/// All repositories share the same DbContext instance to maintain transaction boundaries.
/// </summary>
public interface IUnitOfWork : IDisposable
{
    #region Repository Properties

    /// <summary>
    /// Repository for managing KbankOddRegistration entities.
    /// Provides access to KBank Online Direct Debit registration data with integrated OTAC functionality.
    /// </summary>
    IRepository<KbankOddRegistration> KbankOddRegistrations { get; }

    /// <summary>
    /// Repository for managing User entities.
    /// Provides access to application user authentication and authorization data.
    /// </summary>
    IRepository<User> Users { get; }

    /// <summary>
    /// Repository for managing Branch entities.
    /// Provides access to bank branch information for ODD registration management.
    /// </summary>
    IRepository<Branch> Branches { get; }

    #endregion

    #region Transaction Management

    /// <summary>
    /// Saves all pending changes to the database asynchronously.
    /// This is the primary method for persisting changes made through repositories.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token for async operation</param>
    /// <returns>The number of state entries written to the database</returns>
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Begins a new database transaction asynchronously.
    /// Use this for operations that require explicit transaction control.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token for async operation</param>
    /// <returns>A database transaction that can be committed or rolled back</returns>
    Task<IDbContextTransaction> BeginTransactionAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Commits the current transaction if one exists.
    /// This persists all changes made within the transaction boundary.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token for async operation</param>
    /// <returns>Task representing the async operation</returns>
    Task CommitTransactionAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Rolls back the current transaction if one exists.
    /// This discards all changes made within the transaction boundary.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token for async operation</param>
    /// <returns>Task representing the async operation</returns>
    Task RollbackTransactionAsync(CancellationToken cancellationToken = default);

    #endregion

    #region Advanced Operations

    /// <summary>
    /// Executes the provided operation within a database transaction.
    /// Automatically commits on success or rolls back on exception.
    /// </summary>
    /// <typeparam name="TResult">The return type of the operation</typeparam>
    /// <param name="operation">The operation to execute within the transaction</param>
    /// <param name="cancellationToken">Cancellation token for async operation</param>
    /// <returns>The result of the operation</returns>
    Task<TResult> ExecuteInTransactionAsync<TResult>(
        Func<IUnitOfWork, CancellationToken, Task<TResult>> operation,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Executes the provided operation within a database transaction.
    /// Automatically commits on success or rolls back on exception.
    /// </summary>
    /// <param name="operation">The operation to execute within the transaction</param>
    /// <param name="cancellationToken">Cancellation token for async operation</param>
    /// <returns>Task representing the async operation</returns>
    Task ExecuteInTransactionAsync(
        Func<IUnitOfWork, CancellationToken, Task> operation,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a repository for the specified entity type.
    /// This is useful for generic operations or when working with entities not explicitly defined.
    /// </summary>
    /// <typeparam name="TEntity">The entity type</typeparam>
    /// <returns>Repository instance for the specified entity type</returns>
    IRepository<TEntity> GetRepository<TEntity>() where TEntity : class;

    #endregion

    #region State Management

    /// <summary>
    /// Detaches all tracked entities from the context.
    /// Use this to clear the change tracker and reduce memory usage.
    /// WARNING: This will lose all pending changes that haven't been saved.
    /// </summary>
    void DetachAllEntities();

    /// <summary>
    /// Gets the current transaction if one is active.
    /// Returns null if no transaction is currently active.
    /// </summary>
    IDbContextTransaction? CurrentTransaction { get; }

    /// <summary>
    /// Indicates whether the Unit of Work has pending changes that need to be saved.
    /// </summary>
    bool HasPendingChanges { get; }

    /// <summary>
    /// Gets the number of entities currently being tracked by the change tracker.
    /// Useful for monitoring memory usage and performance.
    /// </summary>
    int TrackedEntitiesCount { get; }

    #endregion

    #region Context Access

    /// <summary>
    /// Provides direct access to the underlying DbContext.
    /// Use with caution - direct context access may bypass Unit of Work patterns.
    /// This is primarily intended for advanced scenarios and raw SQL operations.
    /// </summary>
    DbContext Context { get; }

    #endregion
}

/// <summary>
/// Extension methods for IUnitOfWork to provide additional convenience methods.
/// </summary>
public static class UnitOfWorkExtensions
{
    /// <summary>
    /// Saves changes and returns whether any changes were actually persisted.
    /// </summary>
    /// <param name="unitOfWork">The unit of work instance</param>
    /// <param name="cancellationToken">Cancellation token for async operation</param>
    /// <returns>True if changes were saved, false if no changes were pending</returns>
    public static async Task<bool> SaveChangesIfNeededAsync(this IUnitOfWork unitOfWork, CancellationToken cancellationToken = default)
    {
        if (!unitOfWork.HasPendingChanges)
            return false;

        var changeCount = await unitOfWork.SaveChangesAsync(cancellationToken);
        return changeCount > 0;
    }

    /// <summary>
    /// Executes multiple operations within a single transaction and saves changes.
    /// This is a convenience method for common scenarios.
    /// </summary>
    /// <param name="unitOfWork">The unit of work instance</param>
    /// <param name="operations">The operations to execute</param>
    /// <param name="cancellationToken">Cancellation token for async operation</param>
    /// <returns>The number of changes saved to the database</returns>
    public static async Task<int> ExecuteAndSaveAsync(this IUnitOfWork unitOfWork, 
        Func<IUnitOfWork, Task> operations, 
        CancellationToken cancellationToken = default)
    {
        return await unitOfWork.ExecuteInTransactionAsync(async (uow, ct) =>
        {
            await operations(uow);
            return await uow.SaveChangesAsync(ct);
        }, cancellationToken);
    }
}