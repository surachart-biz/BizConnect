using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using BizConnect.Dal;
using BizConnect.Dal.Models;
using BizConnect.Dal.UnitOfWork;
using BizConnect.Dal.Repositories;

namespace BizConnect.Tests.Unit.UnitOfWork;

/// <summary>
/// Unit tests for the UnitOfWork implementation.
/// Tests cover repository access, transaction management, and proper disposal patterns.
/// Uses in-memory database for isolation and performance.
/// </summary>
public class UnitOfWorkTests : IDisposable
{
    private readonly BizConnectContext _context;
    private readonly Mock<ILoggerFactory> _mockLoggerFactory;
    private readonly Dal.UnitOfWork.UnitOfWork _unitOfWork;
    private bool _disposed = false;

    public UnitOfWorkTests()
    {
        // Setup in-memory database with unique name for test isolation
        var options = new DbContextOptionsBuilder<BizConnectContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _context = new BizConnectContext(options);
        _mockLoggerFactory = new Mock<ILoggerFactory>();
        _mockLoggerFactory.Setup(x => x.CreateLogger(It.IsAny<string>())).Returns(Mock.Of<ILogger>());
        _unitOfWork = new Dal.UnitOfWork.UnitOfWork(_context, _mockLoggerFactory.Object);

        // Seed test data
        SeedTestData();
    }

    #region Setup and Teardown

    private void SeedTestData()
    {
        var branch = new Branch
        {
            BranchId = 1,
            Name = "Test Branch",
            Code = "TB001",
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };

        var user = new User
        {
            Id = 1,
            Username = "testuser",
            PasswordHash = "hash",
            Role = "Admin",
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        var registration = new KbankOddRegistration
        {
            Id = 1,
            ExternalReference = "BIZ202501010001",
            RegId = "REG001",
            Status = "Pending",
            CreatedAt = DateTime.UtcNow,
            OtacCode = "12345678",
            OtacState = "Generated",
            GeneratedByUserId = 1,
            BranchId = 1
        };

        _context.Branches.Add(branch);
        _context.Users.Add(user);
        _context.KbankOddRegistrations.Add(registration);
        _context.SaveChanges();

        // Clear change tracker to start fresh
        _context.ChangeTracker.Clear();
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (!_disposed && disposing)
        {
            _unitOfWork?.Dispose();
            _context?.Dispose();
            _disposed = true;
        }
    }

    #endregion

    #region Constructor Tests

    [Fact]
    public void Constructor_WithValidParameters_ShouldCreateUnitOfWork()
    {
        // Act & Assert
        Assert.NotNull(_unitOfWork);
        Assert.NotNull(_unitOfWork.Context);
        Assert.Equal(_context, _unitOfWork.Context);
    }

    [Fact]
    public void Constructor_WithNullContext_ShouldThrowArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => new Dal.UnitOfWork.UnitOfWork(null!, _mockLoggerFactory.Object));
    }

    [Fact]
    public void Constructor_WithNullLoggerFactory_ShouldThrowArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => new Dal.UnitOfWork.UnitOfWork(_context, (ILoggerFactory)null!));
    }

    #endregion

    #region Repository Property Tests

    [Fact]
    public void KbankOddRegistrations_ShouldReturnRepositoryInstance()
    {
        // Act
        var repository = _unitOfWork.KbankOddRegistrations;

        // Assert
        Assert.NotNull(repository);
        Assert.IsAssignableFrom<IRepository<KbankOddRegistration>>(repository);
    }

    [Fact]
    public void Users_ShouldReturnRepositoryInstance()
    {
        // Act
        var repository = _unitOfWork.Users;

        // Assert
        Assert.NotNull(repository);
        Assert.IsAssignableFrom<IRepository<User>>(repository);
    }

    [Fact]
    public void Branches_ShouldReturnRepositoryInstance()
    {
        // Act
        var repository = _unitOfWork.Branches;

        // Assert
        Assert.NotNull(repository);
        Assert.IsAssignableFrom<IRepository<Branch>>(repository);
    }

    [Fact]
    public void RepositoryProperties_ShouldReturnSameInstanceOnMultipleCalls()
    {
        // Act
        var repository1 = _unitOfWork.Users;
        var repository2 = _unitOfWork.Users;

        // Assert
        Assert.Same(repository1, repository2);
    }

    #endregion

    #region Generic Repository Tests

    [Fact]
    public void GetRepository_WithValidEntityType_ShouldReturnRepositoryInstance()
    {
        // Act
        var repository = _unitOfWork.GetRepository<User>();

        // Assert
        Assert.NotNull(repository);
        Assert.IsAssignableFrom<IRepository<User>>(repository);
    }

    [Fact]
    public void GetRepository_SameEntityType_ShouldReturnSameInstance()
    {
        // Act
        var repository1 = _unitOfWork.GetRepository<User>();
        var repository2 = _unitOfWork.GetRepository<User>();

        // Assert
        Assert.Same(repository1, repository2);
    }

    #endregion

    #region SaveChanges Tests

    [Fact]
    public async Task SaveChangesAsync_WithPendingChanges_ShouldReturnChangeCount()
    {
        // Arrange
        var newUser = new User
        {
            Username = "newuser",
            PasswordHash = "hash",
            Role = "User",
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        await _unitOfWork.Users.AddAsync(newUser);

        // Act
        var result = await _unitOfWork.SaveChangesAsync();

        // Assert
        Assert.True(result > 0);
    }

    [Fact]
    public async Task SaveChangesAsync_WithoutPendingChanges_ShouldReturnZero()
    {
        // Act
        var result = await _unitOfWork.SaveChangesAsync();

        // Assert
        Assert.Equal(0, result);
    }

    #endregion

    #region Transaction Tests

    [Fact]
    public async Task BeginTransactionAsync_ShouldCreateTransaction()
    {
        // Act
        var transaction = await _unitOfWork.BeginTransactionAsync();

        // Assert
        Assert.NotNull(transaction);
        Assert.Same(transaction, _unitOfWork.CurrentTransaction);
    }

    [Fact]
    public async Task BeginTransactionAsync_WhenTransactionExists_ShouldReturnCurrentTransaction()
    {
        // Arrange
        var transaction1 = await _unitOfWork.BeginTransactionAsync();

        // Act
        var transaction2 = await _unitOfWork.BeginTransactionAsync();

        // Assert
        Assert.Same(transaction1, transaction2);
    }

    [Fact]
    public async Task CommitTransactionAsync_WithActiveTransaction_ShouldCommitAndClearTransaction()
    {
        // Arrange
        await _unitOfWork.BeginTransactionAsync();
        var newUser = new User
        {
            Username = "commituser",
            PasswordHash = "hash",
            Role = "User",
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        await _unitOfWork.Users.AddAsync(newUser);
        await _unitOfWork.SaveChangesAsync();

        // Act
        await _unitOfWork.CommitTransactionAsync();

        // Assert
        Assert.Null(_unitOfWork.CurrentTransaction);
        
        // Verify changes were persisted
        var savedUser = await _unitOfWork.Users.FirstOrDefaultAsync(u => u.Username == "commituser");
        Assert.NotNull(savedUser);
    }

    [Fact]
    public async Task RollbackTransactionAsync_WithActiveTransaction_ShouldRollbackAndClearTransaction()
    {
        // Arrange
        await _unitOfWork.BeginTransactionAsync();
        var newUser = new User
        {
            Username = "rollbackuser",
            PasswordHash = "hash",
            Role = "User",
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        await _unitOfWork.Users.AddAsync(newUser);

        // Act
        await _unitOfWork.RollbackTransactionAsync();

        // Assert
        Assert.Null(_unitOfWork.CurrentTransaction);
        
        // Verify changes were not persisted
        var savedUser = await _unitOfWork.Users.FirstOrDefaultAsync(u => u.Username == "rollbackuser");
        Assert.Null(savedUser);
    }

    #endregion

    #region ExecuteInTransaction Tests

    [Fact]
    public async Task ExecuteInTransactionAsync_WithSuccessfulOperation_ShouldCommitTransaction()
    {
        // Arrange
        var operationExecuted = false;

        // Act
        var result = await _unitOfWork.ExecuteInTransactionAsync(async (uow, ct) =>
        {
            operationExecuted = true;
            var newUser = new User
            {
                Username = "transactionuser",
                PasswordHash = "hash",
                Role = "User",
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
            await uow.Users.AddAsync(newUser);
            await uow.SaveChangesAsync(ct);
            return 42;
        });

        // Assert
        Assert.True(operationExecuted);
        Assert.Equal(42, result);
        Assert.Null(_unitOfWork.CurrentTransaction);
        
        // Verify changes were persisted
        var savedUser = await _unitOfWork.Users.FirstOrDefaultAsync(u => u.Username == "transactionuser");
        Assert.NotNull(savedUser);
    }

    [Fact]
    public async Task ExecuteInTransactionAsync_WithFailingOperation_ShouldRollbackTransaction()
    {
        // Arrange
        var operationExecuted = false;

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
        {
            await _unitOfWork.ExecuteInTransactionAsync(async (uow, ct) =>
            {
                operationExecuted = true;
                var newUser = new User
                {
                    Username = "faileduser",
                    PasswordHash = "hash",
                    Role = "User",
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };
                await uow.Users.AddAsync(newUser);
                throw new InvalidOperationException("Test exception");
            });
        });

        // Assert
        Assert.True(operationExecuted);
        Assert.Null(_unitOfWork.CurrentTransaction);
        
        // Verify changes were rolled back
        var savedUser = await _unitOfWork.Users.FirstOrDefaultAsync(u => u.Username == "faileduser");
        Assert.Null(savedUser);
    }

    [Fact]
    public async Task ExecuteInTransactionAsync_VoidOperation_ShouldExecuteSuccessfully()
    {
        // Arrange
        var operationExecuted = false;

        // Act
        await _unitOfWork.ExecuteInTransactionAsync(async (uow, ct) =>
        {
            operationExecuted = true;
            await Task.Delay(1, ct); // Simulate async work
        });

        // Assert
        Assert.True(operationExecuted);
        Assert.Null(_unitOfWork.CurrentTransaction);
    }

    #endregion

    #region State Management Tests

    [Fact]
    public void HasPendingChanges_WithoutChanges_ShouldReturnFalse()
    {
        // Act
        var result = _unitOfWork.HasPendingChanges;

        // Assert
        Assert.False(result);
    }

    [Fact]
    public async Task HasPendingChanges_WithPendingChanges_ShouldReturnTrue()
    {
        // Arrange
        var newUser = new User
        {
            Username = "pendinguser",
            PasswordHash = "hash",
            Role = "User",
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        // Act
        await _unitOfWork.Users.AddAsync(newUser);
        var result = _unitOfWork.HasPendingChanges;

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void TrackedEntitiesCount_ShouldReturnCorrectCount()
    {
        // Act - Load some entities to track them
        var user = _unitOfWork.Users.GetByIdWithTrackingAsync(1).Result;
        var branch = _unitOfWork.Branches.GetByIdWithTrackingAsync(1).Result;
        
        var result = _unitOfWork.TrackedEntitiesCount;

        // Assert
        Assert.True(result >= 2); // At least the loaded entities
    }

    [Fact]
    public void DetachAllEntities_ShouldClearChangeTracker()
    {
        // Arrange - Load some entities to track them
        var user = _unitOfWork.Users.GetByIdWithTrackingAsync(1).Result;
        var initialCount = _unitOfWork.TrackedEntitiesCount;
        Assert.True(initialCount > 0);

        // Act
        _unitOfWork.DetachAllEntities();

        // Assert
        var finalCount = _unitOfWork.TrackedEntitiesCount;
        Assert.True(finalCount < initialCount);
    }

    #endregion

    #region Extension Method Tests

    [Fact]
    public async Task SaveChangesIfNeededAsync_WithPendingChanges_ShouldReturnTrue()
    {
        // Arrange
        var newUser = new User
        {
            Username = "extensionuser",
            PasswordHash = "hash",
            Role = "User",
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        await _unitOfWork.Users.AddAsync(newUser);

        // Act
        var result = await _unitOfWork.SaveChangesIfNeededAsync();

        // Assert
        Assert.True(result);
        Assert.False(_unitOfWork.HasPendingChanges);
    }

    [Fact]
    public async Task SaveChangesIfNeededAsync_WithoutPendingChanges_ShouldReturnFalse()
    {
        // Act
        var result = await _unitOfWork.SaveChangesIfNeededAsync();

        // Assert
        Assert.False(result);
    }

    [Fact]
    public async Task ExecuteAndSaveAsync_ShouldExecuteOperationAndSave()
    {
        // Arrange
        var operationExecuted = false;

        // Act
        var changeCount = await _unitOfWork.ExecuteAndSaveAsync(async (uow) =>
        {
            operationExecuted = true;
            var newUser = new User
            {
                Username = "executeandsave",
                PasswordHash = "hash",
                Role = "User",
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
            await uow.Users.AddAsync(newUser);
        });

        // Assert
        Assert.True(operationExecuted);
        Assert.True(changeCount > 0);
        Assert.False(_unitOfWork.HasPendingChanges);
        
        // Verify changes were saved
        var savedUser = await _unitOfWork.Users.FirstOrDefaultAsync(u => u.Username == "executeandsave");
        Assert.NotNull(savedUser);
    }

    #endregion

    #region Disposal Tests

    [Fact]
    public void Dispose_AfterDisposal_PropertyAccessShouldThrowObjectDisposedException()
    {
        // Arrange
        _unitOfWork.Dispose();

        // Act & Assert
        Assert.Throws<ObjectDisposedException>(() => _unitOfWork.Users);
        Assert.Throws<ObjectDisposedException>(() => _unitOfWork.HasPendingChanges);
        Assert.Throws<ObjectDisposedException>(() => _unitOfWork.TrackedEntitiesCount);
    }

    [Fact]
    public async Task Dispose_WithActiveTransaction_ShouldRollbackTransaction()
    {
        // Arrange
        await _unitOfWork.BeginTransactionAsync();
        var newUser = new User
        {
            Username = "disposetest",
            PasswordHash = "hash",
            Role = "User",
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        await _unitOfWork.Users.AddAsync(newUser);

        // Act
        _unitOfWork.Dispose();

        // Create new context to verify rollback
        var options = new DbContextOptionsBuilder<BizConnectContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        using var newContext = new BizConnectContext(options);
        
        // Assert - Changes should not be persisted due to rollback
        var savedUser = await newContext.Users.FirstOrDefaultAsync(u => u.Username == "disposetest");
        Assert.Null(savedUser);
    }

    #endregion
}