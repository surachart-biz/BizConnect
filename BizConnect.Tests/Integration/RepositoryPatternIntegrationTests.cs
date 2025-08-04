using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Xunit;
using BizConnect.Dal.Models;
using BizConnect.Dal.Repositories;
using BizConnect.Dal.UnitOfWork;
using BizConnect.Extensions;

namespace BizConnect.Tests.Integration;

/// <summary>
/// Integration tests demonstrating the Repository and Unit of Work patterns in action.
/// These tests show realistic usage scenarios and verify proper dependency injection setup.
/// Uses in-memory database for full integration testing without external dependencies.
/// </summary>
public class RepositoryPatternIntegrationTests : IDisposable
{
    private readonly ServiceProvider _serviceProvider;
    private readonly BizConnectContext _context;
    private bool _disposed = false;

    public RepositoryPatternIntegrationTests()
    {
        // Setup dependency injection container with repository pattern
        var services = new ServiceCollection();

        // Add in-memory database
        services.AddDbContext<BizConnectContext>(options =>
            options.UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString()));

        // Add logging
        services.AddLogging(builder => builder.AddConsole());

        // Add repository pattern using extension method
        services.AddRepositoryPattern();

        _serviceProvider = services.BuildServiceProvider();
        _context = _serviceProvider.GetRequiredService<BizConnectContext>();

        // Seed test data
        SeedTestData();
    }

    #region Setup and Teardown

    private void SeedTestData()
    {
        var branch = new Branch
        {
            BranchId = 1,
            Name = "Main Branch",
            Code = "MB001",
            Address = "123 Main Street",
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };

        var user = new User
        {
            Id = 1,
            Username = "admin",
            PasswordHash = "hashedpassword",
            Role = "Admin",
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _context.Branches.Add(branch);
        _context.Users.Add(user);
        _context.SaveChanges();
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
            _serviceProvider?.Dispose();
            _disposed = true;
        }
    }

    #endregion

    #region Dependency Injection Tests

    [Fact]
    public void DependencyInjection_ShouldResolveGenericRepository()
    {
        // Act
        var userRepository = _serviceProvider.GetService<IRepository<User>>();
        var branchRepository = _serviceProvider.GetService<IRepository<Branch>>();

        // Assert
        Assert.NotNull(userRepository);
        Assert.NotNull(branchRepository);
        Assert.IsType<Repository<User>>(userRepository);
        Assert.IsType<Repository<Branch>>(branchRepository);
    }

    [Fact]
    public void DependencyInjection_ShouldResolveUnitOfWork()
    {
        // Act
        var unitOfWork = _serviceProvider.GetService<IUnitOfWork>();

        // Assert
        Assert.NotNull(unitOfWork);
        Assert.IsType<Dal.UnitOfWork.UnitOfWork>(unitOfWork);
    }

    [Fact]
    public void DependencyInjection_ShouldUseSameDbContextInstance()
    {
        // Act
        var unitOfWork = _serviceProvider.GetRequiredService<IUnitOfWork>();
        var repository = _serviceProvider.GetRequiredService<IRepository<User>>();

        // Assert
        Assert.Same(_context, unitOfWork.Context);
        
        // Both should use the same context instance within the same scope
        using var scope = _serviceProvider.CreateScope();
        var scopedUnitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
        var scopedRepository = scope.ServiceProvider.GetRequiredService<IRepository<User>>();
        
        Assert.Same(scopedUnitOfWork.Context, scopedRepository.GetType()
            .GetField("Context", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
            ?.GetValue(scopedRepository));
    }

    #endregion

    #region Complete KBank Registration Workflow Test

    [Fact]
    public async Task CompleteKBankRegistrationWorkflow_ShouldSucceed()
    {
        // Arrange
        var unitOfWork = _serviceProvider.GetRequiredService<IUnitOfWork>();

        // Act & Assert - Execute complete workflow in transaction
        var result = await unitOfWork.ExecuteInTransactionAsync(async (uow, ct) =>
        {
            // Step 1: Get existing user and branch
            var user = await uow.Users.FirstOrDefaultAsync(u => u.Username == "admin", ct);
            var branch = await uow.Branches.FirstOrDefaultAsync(b => b.Code == "MB001", ct);

            Assert.NotNull(user);
            Assert.NotNull(branch);

            // Step 2: Create new KBank registration
            var registration = new KbankOddRegistration
            {
                ExternalReference = $"BIZ{DateTime.Now:yyyyMMddHHmmssfff}",
                RegId = "REG001",
                Status = "Pending",
                CreatedAt = DateTime.UtcNow,
                MobileNo = "0812345678",
                IdType = "National ID",
                IdValue = "1234567890123",
                AccountNo = "1234567890",
                BranchId = branch.BranchId,
                FullName = "John Doe",
                OtacCode = "ABC12345",
                OtacState = "Generated",
                GeneratedByUserId = user.Id,
                OtacExpiresAt = DateTime.UtcNow.AddMinutes(30)
            };

            var addedRegistration = await uow.KbankOddRegistrations.AddAsync(registration);

            // Step 3: Save changes
            var changeCount = await uow.SaveChangesAsync(ct);

            // Step 4: Verify registration was created
            var savedRegistration = await uow.KbankOddRegistrations
                .FirstOrDefaultAsync(r => r.ExternalReference == registration.ExternalReference, ct);

            Assert.NotNull(savedRegistration);
            Assert.Equal("Pending", savedRegistration.Status);
            Assert.Equal("Generated", savedRegistration.OtacState);

            return new { ChangeCount = changeCount, Registration = savedRegistration };
        });

        // Verify transaction completed successfully
        Assert.True(result.ChangeCount > 0);
        Assert.NotNull(result.Registration);

        // Verify data persisted after transaction
        var persistedRegistration = await unitOfWork.KbankOddRegistrations
            .FirstOrDefaultAsync(r => r.Id == result.Registration.Id);
        Assert.NotNull(persistedRegistration);
    }

    #endregion

    #region Multi-Repository Operations Test

    [Fact]
    public async Task MultiRepositoryOperations_WithinTransaction_ShouldMaintainConsistency()
    {
        // Arrange
        var unitOfWork = _serviceProvider.GetRequiredService<IUnitOfWork>();

        // Act - Perform operations across multiple repositories
        await unitOfWork.ExecuteInTransactionAsync(async (uow, ct) =>
        {
            // Create new branch
            var newBranch = new Branch
            {
                Name = "New Branch",
                Code = "NB001",
                Address = "456 New Street",
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            };
            await uow.Branches.AddAsync(newBranch);

            // Create new user
            var newUser = new User
            {
                Username = "newuser",
                PasswordHash = "newhash",
                Role = "User",
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
            await uow.Users.AddAsync(newUser);

            // Save changes to get generated IDs
            await uow.SaveChangesAsync(ct);

            // Create registration linking both new entities
            var registration = new KbankOddRegistration
            {
                ExternalReference = $"BIZ{DateTime.Now:yyyyMMddHHmmssfff}",
                RegId = "REG002",
                Status = "Pending",
                CreatedAt = DateTime.UtcNow,
                BranchId = newBranch.BranchId,
                GeneratedByUserId = newUser.Id,
                OtacCode = "XYZ67890",
                OtacState = "Generated",
                OtacExpiresAt = DateTime.UtcNow.AddMinutes(30)
            };
            await uow.KbankOddRegistrations.AddAsync(registration);

            await uow.SaveChangesAsync(ct);
        });

        // Verify all entities were created and properly linked
        var branches = await unitOfWork.Branches.FindAsync(b => b.Code == "NB001");
        var users = await unitOfWork.Users.FindAsync(u => u.Username == "newuser");
        var registrations = await unitOfWork.KbankOddRegistrations.FindAsync(r => r.RegId == "REG002");

        Assert.Single(branches);
        Assert.Single(users);
        Assert.Single(registrations);

        var registration = registrations.First();
        var branch = branches.First();
        var user = users.First();

        Assert.Equal(branch.BranchId, registration.BranchId);
        Assert.Equal(user.Id, registration.GeneratedByUserId);
    }

    #endregion

    #region Pagination Integration Test

    [Fact]
    public async Task PaginationWithFiltering_ShouldWorkCorrectly()
    {
        // Arrange
        var unitOfWork = _serviceProvider.GetRequiredService<IUnitOfWork>();

        // Create multiple test registrations
        await unitOfWork.ExecuteAndSaveAsync(async (uow) =>
        {
            var user = await uow.Users.FirstOrDefaultAsync(u => u.Username == "admin");
            var branch = await uow.Branches.FirstOrDefaultAsync(b => b.Code == "MB001");

            for (int i = 1; i <= 15; i++)
            {
                var registration = new KbankOddRegistration
                {
                    ExternalReference = $"BIZ{DateTime.Now:yyyyMMddHHmmssfff}{i:D3}",
                    RegId = $"REG{i:D3}",
                    Status = i % 2 == 0 ? "Success" : "Pending",
                    CreatedAt = DateTime.UtcNow.AddMinutes(-i),
                    BranchId = branch!.BranchId,
                    GeneratedByUserId = user!.Id,
                    OtacCode = $"CODE{i:D4}",
                    OtacState = "Generated",
                    OtacExpiresAt = DateTime.UtcNow.AddMinutes(30)
                };
                await uow.KbankOddRegistrations.AddAsync(registration);
            }
        });

        // Act - Test pagination with filtering
        var pagedResult = await unitOfWork.KbankOddRegistrations.GetPagedAsync(
            pageNumber: 1,
            pageSize: 5,
            predicate: r => r.Status == "Pending",
            orderBy: r => r.CreatedAt
        );

        // Assert
        Assert.NotNull(pagedResult);
        Assert.Equal(1, pagedResult.PageNumber);
        Assert.Equal(5, pagedResult.PageSize);
        Assert.Equal(8, pagedResult.TotalCount); // 8 pending registrations (1,3,5,7,9,11,13,15)
        Assert.Equal(2, pagedResult.TotalPages);
        Assert.Equal(5, pagedResult.Items.Count());
        Assert.True(pagedResult.HasNextPage);
        Assert.False(pagedResult.HasPreviousPage);
        Assert.All(pagedResult.Items, r => Assert.Equal("Pending", r.Status));
    }

    #endregion

    #region Error Handling and Rollback Test

    [Fact]
    public async Task TransactionRollback_OnError_ShouldNotPersistChanges()
    {
        // Arrange
        var unitOfWork = _serviceProvider.GetRequiredService<IUnitOfWork>();
        var initialUserCount = await unitOfWork.Users.CountAsync();

        // Act & Assert - Exception should cause rollback
        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
        {
            await unitOfWork.ExecuteInTransactionAsync(async (uow, ct) =>
            {
                // Add a valid user
                var newUser = new User
                {
                    Username = "rollbacktest",
                    PasswordHash = "hash",
                    Role = "User",
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };
                await uow.Users.AddAsync(newUser);
                await uow.SaveChangesAsync(ct);

                // Verify user was added (within transaction)
                var userExists = await uow.Users.AnyAsync(u => u.Username == "rollbacktest", ct);
                Assert.True(userExists);

                // Throw exception to trigger rollback
                throw new InvalidOperationException("Test rollback");
            });
        });

        // Verify rollback occurred - user count should be unchanged
        var finalUserCount = await unitOfWork.Users.CountAsync();
        Assert.Equal(initialUserCount, finalUserCount);

        // Verify specific user was not persisted
        var rolledBackUser = await unitOfWork.Users.FirstOrDefaultAsync(u => u.Username == "rollbacktest");
        Assert.Null(rolledBackUser);
    }

    #endregion

    #region Performance and Memory Test

    [Fact]
    public async Task LargeDataOperations_ShouldHandleEfficiently()
    {
        // Arrange
        var unitOfWork = _serviceProvider.GetRequiredService<IUnitOfWork>();
        const int recordCount = 100;

        // Act - Create many records efficiently
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        
        await unitOfWork.ExecuteAndSaveAsync(async (uow) =>
        {
            var user = await uow.Users.FirstOrDefaultAsync(u => u.Username == "admin");
            var branch = await uow.Branches.FirstOrDefaultAsync(b => b.Code == "MB001");

            var registrations = new List<KbankOddRegistration>();
            for (int i = 1; i <= recordCount; i++)
            {
                registrations.Add(new KbankOddRegistration
                {
                    ExternalReference = $"PERF{DateTime.Now:yyyyMMddHHmmssfff}{i:D4}",
                    RegId = $"PERFTEST{i:D4}",
                    Status = "Pending",
                    CreatedAt = DateTime.UtcNow,
                    BranchId = branch!.BranchId,
                    GeneratedByUserId = user!.Id,
                    OtacCode = $"PERF{i:D4}",
                    OtacState = "Generated",
                    OtacExpiresAt = DateTime.UtcNow.AddMinutes(30)
                });
            }

            // Use bulk insert for better performance
            await uow.KbankOddRegistrations.AddRangeAsync(registrations);
        });

        stopwatch.Stop();

        // Assert - Verify all records were created
        var createdCount = await unitOfWork.KbankOddRegistrations.CountAsync(r => r.RegId.StartsWith("PERFTEST"));
        Assert.Equal(recordCount, createdCount);

        // Performance assertion - should complete within reasonable time
        Assert.True(stopwatch.ElapsedMilliseconds < 5000, $"Operation took {stopwatch.ElapsedMilliseconds}ms");

        // Memory management test - detach entities to free memory
        var trackedBefore = unitOfWork.TrackedEntitiesCount;
        unitOfWork.DetachAllEntities();
        var trackedAfter = unitOfWork.TrackedEntitiesCount;
        
        Assert.True(trackedAfter < trackedBefore, "DetachAllEntities should reduce tracked entity count");
    }

    #endregion

    #region Repository Pattern Validation Test

    [Fact]
    public void ServiceCollection_ShouldHaveRepositoryPatternRegistered()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddRepositoryPattern();

        // Act
        var isRegistered = services.IsRepositoryPatternRegistered();

        // Assert
        Assert.True(isRegistered);
    }

    [Fact]
    public void RepositoryPattern_ShouldUseCorrectServiceLifetimes()
    {
        // Act
        using var scope1 = _serviceProvider.CreateScope();
        using var scope2 = _serviceProvider.CreateScope();

        var uow1 = scope1.ServiceProvider.GetRequiredService<IUnitOfWork>();
        var uow2 = scope2.ServiceProvider.GetRequiredService<IUnitOfWork>();

        var repo1 = scope1.ServiceProvider.GetRequiredService<IRepository<User>>();
        var repo2 = scope2.ServiceProvider.GetRequiredService<IRepository<User>>();

        // Assert - Different scopes should have different instances (Scoped lifetime)
        Assert.NotSame(uow1, uow2);
        Assert.NotSame(repo1, repo2);

        // Within same scope, repositories accessed through UnitOfWork should be consistent
        var uowRepo1 = uow1.Users;
        var uowRepo2 = uow1.Users;
        Assert.Same(uowRepo1, uowRepo2);
    }

    #endregion
}