using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using BizConnect.Dal;
using BizConnect.Dal.Models;
using BizConnect.Dal.Repositories;

namespace BizConnect.Tests.Unit.Repositories;

/// <summary>
/// Unit tests for the generic Repository implementation.
/// Tests cover all major functionality including CRUD operations, querying, pagination, and error handling.
/// Uses in-memory database for isolation and performance.
/// </summary>
public class RepositoryTests : IDisposable
{
    private readonly BizConnectContext _context;
    private readonly Mock<ILogger<Repository<User>>> _mockLogger;
    private readonly Repository<User> _repository;
    private bool _disposed = false;

    public RepositoryTests()
    {
        // Setup in-memory database with unique name for test isolation
        var options = new DbContextOptionsBuilder<BizConnectContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _context = new BizConnectContext(options);
        _mockLogger = new Mock<ILogger<Repository<User>>>();
        _repository = new Repository<User>(_context, _mockLogger.Object);

        // Seed test data
        SeedTestData();
    }

    #region Setup and Teardown

    private void SeedTestData()
    {
        var users = new List<User>
        {
            new User
            {
                Id = 1,
                Username = "testuser1",
                PasswordHash = "hash1",
                Role = "Admin",
                IsActive = true,
                CreatedAt = DateTime.UtcNow.AddDays(-10),
                UpdatedAt = DateTime.UtcNow.AddDays(-5)
            },
            new User
            {
                Id = 2,
                Username = "testuser2",
                PasswordHash = "hash2",
                Role = "User",
                IsActive = true,
                CreatedAt = DateTime.UtcNow.AddDays(-8),
                UpdatedAt = DateTime.UtcNow.AddDays(-3)
            },
            new User
            {
                Id = 3,
                Username = "testuser3",
                PasswordHash = "hash3",
                Role = "User",
                IsActive = false,
                CreatedAt = DateTime.UtcNow.AddDays(-6),
                UpdatedAt = DateTime.UtcNow.AddDays(-1)
            }
        };

        _context.Users.AddRange(users);
        _context.SaveChanges();
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
            _context?.Dispose();
            _disposed = true;
        }
    }

    #endregion

    #region Constructor Tests

    [Fact]
    public void Constructor_WithValidContext_ShouldCreateRepository()
    {
        // Act & Assert
        Assert.NotNull(_repository);
    }

    [Fact]
    public void Constructor_WithNullContext_ShouldThrowArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => new Repository<User>(null!, _mockLogger.Object));
    }

    #endregion

    #region GetByIdAsync Tests

    [Fact]
    public async Task GetByIdAsync_WithValidId_ShouldReturnEntity()
    {
        // Act
        var result = await _repository.GetByIdAsync(1);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(1, result.Id);
        Assert.Equal("testuser1", result.Username);
    }

    [Fact]
    public async Task GetByIdAsync_WithInvalidId_ShouldReturnNull()
    {
        // Act
        var result = await _repository.GetByIdAsync(999);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task GetByIdAsync_WithNullId_ShouldReturnNull()
    {
        // Act
        var result = await _repository.GetByIdAsync(null!);

        // Assert
        Assert.Null(result);
    }

    #endregion

    #region GetByIdWithTrackingAsync Tests

    [Fact]
    public async Task GetByIdWithTrackingAsync_WithValidId_ShouldReturnTrackedEntity()
    {
        // Act
        var result = await _repository.GetByIdWithTrackingAsync(1);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(1, result.Id);
        
        // Verify entity is tracked
        var entry = _context.Entry(result);
        Assert.Equal(EntityState.Unchanged, entry.State);
    }

    [Fact]
    public async Task GetByIdWithTrackingAsync_WithInvalidId_ShouldReturnNull()
    {
        // Act
        var result = await _repository.GetByIdWithTrackingAsync(999);

        // Assert
        Assert.Null(result);
    }

    #endregion

    #region FirstOrDefaultAsync Tests

    [Fact]
    public async Task FirstOrDefaultAsync_WithValidPredicate_ShouldReturnMatchingEntity()
    {
        // Arrange
        Expression<Func<User, bool>> predicate = u => u.Username == "testuser2";

        // Act
        var result = await _repository.FirstOrDefaultAsync(predicate);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("testuser2", result.Username);
    }

    [Fact]
    public async Task FirstOrDefaultAsync_WithNoMatch_ShouldReturnNull()
    {
        // Arrange
        Expression<Func<User, bool>> predicate = u => u.Username == "nonexistent";

        // Act
        var result = await _repository.FirstOrDefaultAsync(predicate);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task FirstOrDefaultAsync_WithNullPredicate_ShouldThrowArgumentNullException()
    {
        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(() => _repository.FirstOrDefaultAsync(null!));
    }

    #endregion

    #region GetAllAsync Tests

    [Fact]
    public async Task GetAllAsync_ShouldReturnAllEntities()
    {
        // Act
        var result = await _repository.GetAllAsync();

        // Assert
        Assert.NotNull(result);
        Assert.Equal(3, result.Count());
    }

    #endregion

    #region FindAsync Tests

    [Fact]
    public async Task FindAsync_WithValidPredicate_ShouldReturnMatchingEntities()
    {
        // Arrange
        Expression<Func<User, bool>> predicate = u => u.Role == "User";

        // Act
        var result = await _repository.FindAsync(predicate);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(2, result.Count());
        Assert.All(result, u => Assert.Equal("User", u.Role));
    }

    [Fact]
    public async Task FindAsync_WithNoMatches_ShouldReturnEmptyCollection()
    {
        // Arrange
        Expression<Func<User, bool>> predicate = u => u.Role == "NonexistentRole";

        // Act
        var result = await _repository.FindAsync(predicate);

        // Assert
        Assert.NotNull(result);
        Assert.Empty(result);
    }

    #endregion

    #region Pagination Tests

    [Fact]
    public async Task GetPagedAsync_WithValidParameters_ShouldReturnPagedResult()
    {
        // Act
        var result = await _repository.GetPagedAsync(1, 2);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(1, result.PageNumber);
        Assert.Equal(2, result.PageSize);
        Assert.Equal(3, result.TotalCount);
        Assert.Equal(2, result.TotalPages);
        Assert.Equal(2, result.Items.Count());
        Assert.True(result.HasNextPage);
        Assert.False(result.HasPreviousPage);
    }

    [Fact]
    public async Task GetPagedAsync_WithPredicate_ShouldReturnFilteredPagedResult()
    {
        // Arrange
        Expression<Func<User, bool>> predicate = u => u.IsActive;

        // Act
        var result = await _repository.GetPagedAsync(1, 10, predicate);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(2, result.TotalCount);
        Assert.Equal(2, result.Items.Count());
        Assert.All(result.Items, u => Assert.True(u.IsActive));
    }

    [Fact]
    public async Task GetPagedAsync_WithInvalidPageNumber_ShouldThrowArgumentException()
    {
        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() => _repository.GetPagedAsync(0, 10));
    }

    [Fact]
    public async Task GetPagedAsync_WithInvalidPageSize_ShouldThrowArgumentException()
    {
        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() => _repository.GetPagedAsync(1, 0));
    }

    #endregion

    #region CountAsync Tests

    [Fact]
    public async Task CountAsync_WithoutPredicate_ShouldReturnTotalCount()
    {
        // Act
        var result = await _repository.CountAsync();

        // Assert
        Assert.Equal(3, result);
    }

    [Fact]
    public async Task CountAsync_WithPredicate_ShouldReturnFilteredCount()
    {
        // Arrange
        Expression<Func<User, bool>> predicate = u => u.IsActive;

        // Act
        var result = await _repository.CountAsync(predicate);

        // Assert
        Assert.Equal(2, result);
    }

    #endregion

    #region Query Tests

    [Fact]
    public void Query_ShouldReturnQueryableWithNoTracking()
    {
        // Act
        var queryable = _repository.Query();

        // Assert
        Assert.NotNull(queryable);
        Assert.IsAssignableFrom<IQueryable<User>>(queryable);
    }

    [Fact]
    public void QueryWithTracking_ShouldReturnQueryableWithTracking()
    {
        // Act
        var queryable = _repository.QueryWithTracking();

        // Assert
        Assert.NotNull(queryable);
        Assert.IsAssignableFrom<IQueryable<User>>(queryable);
    }

    #endregion

    #region AnyAsync Tests

    [Fact]
    public async Task AnyAsync_WithMatchingPredicate_ShouldReturnTrue()
    {
        // Arrange
        Expression<Func<User, bool>> predicate = u => u.Role == "Admin";

        // Act
        var result = await _repository.AnyAsync(predicate);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public async Task AnyAsync_WithNonMatchingPredicate_ShouldReturnFalse()
    {
        // Arrange
        Expression<Func<User, bool>> predicate = u => u.Role == "NonexistentRole";

        // Act
        var result = await _repository.AnyAsync(predicate);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public async Task AnyAsync_WithoutPredicate_ShouldReturnTrue()
    {
        // Act
        var result = await _repository.AnyAsync();

        // Assert
        Assert.True(result);
    }

    #endregion

    #region AddAsync Tests

    [Fact]
    public async Task AddAsync_WithValidEntity_ShouldAddEntity()
    {
        // Arrange
        var newUser = new User
        {
            Username = "newuser",
            PasswordHash = "newhash",
            Role = "User",
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        // Act
        var result = await _repository.AddAsync(newUser);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("newuser", result.Username);
        
        // Verify entity is tracked as added
        var entry = _context.Entry(result);
        Assert.Equal(EntityState.Added, entry.State);
    }

    [Fact]
    public async Task AddAsync_WithNullEntity_ShouldThrowArgumentNullException()
    {
        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(() => _repository.AddAsync(null!));
    }

    #endregion

    #region Update Tests

    [Fact]
    public void Update_WithValidEntity_ShouldUpdateEntity()
    {
        // Arrange
        var user = _context.Users.First();
        user.Username = "updateduser";

        // Act
        _repository.Update(user);

        // Assert
        var entry = _context.Entry(user);
        Assert.Equal(EntityState.Modified, entry.State);
    }

    [Fact]
    public void Update_WithNullEntity_ShouldThrowArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => _repository.Update(null!));
    }

    #endregion

    #region Delete Tests

    [Fact]
    public void Delete_WithValidEntity_ShouldMarkForDeletion()
    {
        // Arrange
        var user = _context.Users.First();

        // Act
        _repository.Delete(user);

        // Assert
        var entry = _context.Entry(user);
        Assert.Equal(EntityState.Deleted, entry.State);
    }

    [Fact]
    public void Delete_WithNullEntity_ShouldThrowArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => _repository.Delete(null!));
    }

    [Fact]
    public async Task DeleteAsync_WithValidId_ShouldReturnTrueAndMarkForDeletion()
    {
        // Act
        var result = await _repository.DeleteAsync(1);

        // Assert
        Assert.True(result);
        
        // Verify entity is marked for deletion
        var user = _context.Users.Find(1);
        var entry = _context.Entry(user!);
        Assert.Equal(EntityState.Deleted, entry.State);
    }

    [Fact]
    public async Task DeleteAsync_WithInvalidId_ShouldReturnFalse()
    {
        // Act
        var result = await _repository.DeleteAsync(999);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public async Task DeleteWhereAsync_WithValidPredicate_ShouldDeleteMatchingEntities()
    {
        // Arrange
        Expression<Func<User, bool>> predicate = u => !u.IsActive;

        // Act
        var result = await _repository.DeleteWhereAsync(predicate);

        // Assert
        Assert.Equal(1, result); // One inactive user should be deleted
    }

    #endregion

    #region AddRangeAsync Tests

    [Fact]
    public async Task AddRangeAsync_WithValidEntities_ShouldAddAllEntities()
    {
        // Arrange
        var newUsers = new List<User>
        {
            new User { Username = "bulk1", PasswordHash = "hash1", Role = "User", IsActive = true, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow },
            new User { Username = "bulk2", PasswordHash = "hash2", Role = "User", IsActive = true, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow }
        };

        // Act
        await _repository.AddRangeAsync(newUsers);

        // Assert
        var addedEntries = _context.ChangeTracker.Entries<User>().Where(e => e.State == EntityState.Added);
        Assert.Equal(2, addedEntries.Count());
    }

    [Fact]
    public async Task AddRangeAsync_WithNullCollection_ShouldThrowArgumentNullException()
    {
        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(() => _repository.AddRangeAsync(null!));
    }

    #endregion
}