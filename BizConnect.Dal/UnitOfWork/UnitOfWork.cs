using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Logging;
using BizConnect.Dal.Models;
using BizConnect.Dal.Repositories;

namespace BizConnect.Dal.UnitOfWork;

/// <summary>
/// Unit of Work implementation providing centralized repository access and transaction management.
/// Implements the Unit of Work pattern to ensure consistency across multiple repository operations.
/// All repositories share the same DbContext instance to maintain transaction boundaries.
/// </summary>
public class UnitOfWork : IUnitOfWork
{
    private readonly BizConnectContext _context;
    private readonly ILogger<UnitOfWork> _logger;
    private readonly ILoggerFactory _loggerFactory;
    private readonly Dictionary<Type, object> _repositories = new();
    private IDbContextTransaction? _currentTransaction;
    private bool _disposed = false;

    // Lazy-loaded repositories for the main entity types
    private IRepository<KbankOddRegistration>? _kbankOddRegistrations;
    private IRepository<User>? _users;
    private IRepository<Branch>? _branches;

    /// <summary>
    /// Initializes a new instance of the UnitOfWork class.
    /// </summary>
    /// <param name="context">The Entity Framework database context</param>
    /// <param name="loggerFactory">Logger factory for creating repository loggers</param>
    public UnitOfWork(BizConnectContext context, ILoggerFactory loggerFactory)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _loggerFactory = loggerFactory ?? throw new ArgumentNullException(nameof(loggerFactory));
        _logger = loggerFactory.CreateLogger<UnitOfWork>();

        _logger.LogDebug("UnitOfWork instance created");
    }

    #region Repository Properties

    /// <inheritdoc />
    public IRepository<KbankOddRegistration> KbankOddRegistrations
    {
        get
        {
            ThrowIfDisposed();
            return _kbankOddRegistrations ??= new Repository<KbankOddRegistration>(_context, 
                _loggerFactory.CreateLogger<Repository<KbankOddRegistration>>());
        }
    }

    /// <inheritdoc />
    public IRepository<User> Users
    {
        get
        {
            ThrowIfDisposed();
            return _users ??= new Repository<User>(_context, 
                _loggerFactory.CreateLogger<Repository<User>>());
        }
    }

    /// <inheritdoc />
    public IRepository<Branch> Branches
    {
        get
        {
            ThrowIfDisposed();
            return _branches ??= new Repository<Branch>(_context, 
                _loggerFactory.CreateLogger<Repository<Branch>>());
        }
    }

    #endregion

    #region Transaction Management

    /// <inheritdoc />
    public async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();

        try
        {
            _logger.LogDebug("Saving changes to database");

            var changeCount = await _context.SaveChangesAsync(cancellationToken);

            _logger.LogDebug("Successfully saved {ChangeCount} changes to database", changeCount);

            return changeCount;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error saving changes to database");
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<IDbContextTransaction> BeginTransactionAsync(CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();

        try
        {
            if (_currentTransaction != null)
            {
                _logger.LogWarning("Transaction already exists. Returning current transaction.");
                return _currentTransaction;
            }

            _logger.LogDebug("Beginning new database transaction");

            _currentTransaction = await _context.Database.BeginTransactionAsync(cancellationToken);

            _logger.LogDebug("Database transaction started with ID: {TransactionId}", _currentTransaction.TransactionId);

            return _currentTransaction;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error beginning database transaction");
            throw;
        }
    }

    /// <inheritdoc />
    public async Task CommitTransactionAsync(CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();

        if (_currentTransaction == null)
        {
            _logger.LogWarning("No active transaction to commit");
            return;
        }

        try
        {
            _logger.LogDebug("Committing transaction with ID: {TransactionId}", _currentTransaction.TransactionId);

            await _currentTransaction.CommitAsync(cancellationToken);

            _logger.LogDebug("Transaction committed successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error committing transaction");
            await RollbackTransactionAsync(cancellationToken);
            throw;
        }
        finally
        {
            await DisposeTransactionAsync();
        }
    }

    /// <inheritdoc />
    public async Task RollbackTransactionAsync(CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();

        if (_currentTransaction == null)
        {
            _logger.LogWarning("No active transaction to rollback");
            return;
        }

        try
        {
            _logger.LogDebug("Rolling back transaction with ID: {TransactionId}", _currentTransaction.TransactionId);

            await _currentTransaction.RollbackAsync(cancellationToken);

            _logger.LogDebug("Transaction rolled back successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error rolling back transaction");
            throw;
        }
        finally
        {
            await DisposeTransactionAsync();
        }
    }

    #endregion

    #region Advanced Operations

    /// <inheritdoc />
    public async Task<TResult> ExecuteInTransactionAsync<TResult>(
        Func<IUnitOfWork, CancellationToken, Task<TResult>> operation,
        CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();

        if (operation == null)
            throw new ArgumentNullException(nameof(operation));

        var wasTransactionStartedHere = _currentTransaction == null;

        try
        {
            if (wasTransactionStartedHere)
            {
                await BeginTransactionAsync(cancellationToken);
                _logger.LogDebug("Started new transaction for operation execution");
            }
            else
            {
                _logger.LogDebug("Using existing transaction for operation execution");
            }

            var result = await operation(this, cancellationToken);

            if (wasTransactionStartedHere)
            {
                await CommitTransactionAsync(cancellationToken);
                _logger.LogDebug("Committed transaction after successful operation execution");
            }

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error executing operation in transaction");

            if (wasTransactionStartedHere)
            {
                await RollbackTransactionAsync(cancellationToken);
                _logger.LogDebug("Rolled back transaction after operation failure");
            }

            throw;
        }
    }

    /// <inheritdoc />
    public async Task ExecuteInTransactionAsync(
        Func<IUnitOfWork, CancellationToken, Task> operation,
        CancellationToken cancellationToken = default)
    {
        await ExecuteInTransactionAsync(async (uow, ct) =>
        {
            await operation(uow, ct);
            return 0; // Return dummy value for void operations
        }, cancellationToken);
    }

    /// <inheritdoc />
    public IRepository<TEntity> GetRepository<TEntity>() where TEntity : class
    {
        ThrowIfDisposed();

        var entityType = typeof(TEntity);

        if (_repositories.TryGetValue(entityType, out var existingRepository))
        {
            return (IRepository<TEntity>)existingRepository;
        }

        _logger.LogDebug("Creating new repository for entity type {EntityType}", entityType.Name);

        var repository = new Repository<TEntity>(_context, _loggerFactory.CreateLogger<Repository<TEntity>>());
        _repositories[entityType] = repository;

        return repository;
    }

    #endregion

    #region State Management

    /// <inheritdoc />
    public void DetachAllEntities()
    {
        ThrowIfDisposed();

        try
        {
            _logger.LogDebug("Detaching all tracked entities");

            var trackedEntities = _context.ChangeTracker.Entries().ToList();
            var entityCount = trackedEntities.Count;

            foreach (var entity in trackedEntities)
            {
                entity.State = EntityState.Detached;
            }

            _logger.LogDebug("Detached {EntityCount} entities from change tracker", entityCount);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error detaching entities");
            throw;
        }
    }

    /// <inheritdoc />
    public IDbContextTransaction? CurrentTransaction => _currentTransaction;

    /// <inheritdoc />
    public bool HasPendingChanges
    {
        get
        {
            ThrowIfDisposed();
            return _context.ChangeTracker.HasChanges();
        }
    }

    /// <inheritdoc />
    public int TrackedEntitiesCount
    {
        get
        {
            ThrowIfDisposed();
            return _context.ChangeTracker.Entries().Count();
        }
    }

    #endregion

    #region Context Access

    /// <inheritdoc />
    public DbContext Context
    {
        get
        {
            ThrowIfDisposed();
            return _context;
        }
    }

    #endregion

    #region IDisposable Implementation

    /// <summary>
    /// Disposes the Unit of Work and its resources.
    /// This will automatically rollback any uncommitted transactions.
    /// </summary>
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Protected implementation of Dispose pattern.
    /// </summary>
    /// <param name="disposing">True if disposing managed resources</param>
    protected virtual void Dispose(bool disposing)
    {
        if (!_disposed && disposing)
        {
            try
            {
                // Rollback any uncommitted transaction
                if (_currentTransaction != null)
                {
                    _logger.LogWarning("Disposing UnitOfWork with active transaction. Rolling back transaction.");
                    _currentTransaction.Rollback();
                    _currentTransaction.Dispose();
                    _currentTransaction = null;
                }

                // Clear repository cache
                _repositories.Clear();

                // Dispose context if we own it
                _context?.Dispose();

                _logger.LogDebug("UnitOfWork disposed successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error disposing UnitOfWork");
            }
            finally
            {
                _disposed = true;
            }
        }
    }

    /// <summary>
    /// Finalizer to ensure resources are cleaned up if Dispose is not called.
    /// </summary>
    ~UnitOfWork()
    {
        Dispose(false);
    }

    #endregion

    #region Private Helper Methods

    /// <summary>
    /// Throws ObjectDisposedException if the UnitOfWork has been disposed.
    /// </summary>
    private void ThrowIfDisposed()
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(UnitOfWork));
        }
    }

    /// <summary>
    /// Disposes the current transaction and sets it to null.
    /// </summary>
    private async Task DisposeTransactionAsync()
    {
        if (_currentTransaction != null)
        {
            await _currentTransaction.DisposeAsync();
            _currentTransaction = null;
            _logger.LogDebug("Transaction disposed");
        }
    }

    #endregion
}