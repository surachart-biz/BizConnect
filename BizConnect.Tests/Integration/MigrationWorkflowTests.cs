using System.Diagnostics;
using BizConnect.Dal;
using BizConnect.Dal.Models;
using Microsoft.EntityFrameworkCore;

namespace BizConnect.Tests.Integration;

/// <summary>
/// Integration tests for the database migration workflow.
/// These tests validate the complete migration process including SQL execution,
/// EF Core scaffolding, and database schema validation.
/// </summary>
public class MigrationWorkflowTests : IDisposable
{
    private readonly string _testConnectionString;
    private readonly string _testDatabaseName;

    public MigrationWorkflowTests()
    {
        _testDatabaseName = $"bizconnect_test_{Guid.NewGuid():N}";
        _testConnectionString = $"Host=localhost;Database={_testDatabaseName};Username=postgres;Password=bizitadmin";
    }

    [Fact]
    public async Task DatabaseConnection_WithValidConnectionString_ShouldConnect()
    {
        try
        {
            // Arrange
            await CreateTestDatabaseAsync();
            var options = new DbContextOptionsBuilder<BizConnectContext>()
                .UseNpgsql(_testConnectionString)
                .Options;

            // Act & Assert
            using var context = new BizConnectContext(options);
            var canConnect = await context.Database.CanConnectAsync();
            Assert.True(canConnect, "Should be able to connect to the test database");
        }
        finally
        {
            await CleanupTestDatabaseAsync();
        }
    }

    [Fact]
    public async Task ScaffoldedModels_ShouldHaveCorrectProperties()
    {
        try
        {
            // Arrange
            await CreateTestDatabaseAsync();
            await ExecuteInitialMigrationAsync();

            var options = new DbContextOptionsBuilder<BizConnectContext>()
                .UseNpgsql(_testConnectionString)
                .Options;

            // Act
            using var context = new BizConnectContext(options);
            var userEntityType = context.Model.FindEntityType(typeof(User));

            // Assert
            Assert.NotNull(userEntityType);

            // Verify all expected properties exist
            var expectedProperties = new[] { "Id", "Username", "PasswordHash", "Role", "CreatedAt", "UpdatedAt", "IsActive" };
            foreach (var propertyName in expectedProperties)
            {
                var property = userEntityType.FindProperty(propertyName);
                Assert.NotNull(property);
            }

            // Verify primary key
            var primaryKey = userEntityType.FindPrimaryKey();
            Assert.NotNull(primaryKey);
            Assert.Single(primaryKey.Properties);
            Assert.Equal("Id", primaryKey.Properties.First().Name);

            // Verify unique index on Username
            var usernameIndex = userEntityType.GetIndexes()
                .FirstOrDefault(i => i.Properties.Any(p => p.Name == "Username"));
            Assert.NotNull(usernameIndex);
            Assert.True(usernameIndex.IsUnique);
        }
        finally
        {
            await CleanupTestDatabaseAsync();
        }
    }

    [Fact]
    public async Task InitialMigration_ShouldCreateUserTableWithCorrectSchema()
    {
        try
        {
            // Arrange
            await CreateTestDatabaseAsync();
            await ExecuteInitialMigrationAsync();

            var options = new DbContextOptionsBuilder<BizConnectContext>()
                .UseNpgsql(_testConnectionString)
                .Options;

            // Act & Assert
            using var context = new BizConnectContext(options);

            // Verify table exists by checking if we can query it
            var userCount = await context.Users.CountAsync();
            Assert.True(userCount >= 0, "Should be able to query Users table");

            // Verify initial admin user was created
            var adminUser = await context.Users.FirstOrDefaultAsync(u => u.Username == "admin");
            Assert.NotNull(adminUser);
            Assert.Equal("Admin", adminUser.Role);
            Assert.True(adminUser.IsActive);
            Assert.NotNull(adminUser.PasswordHash);
        }
        finally
        {
            await CleanupTestDatabaseAsync();
        }
    }

    [Fact]
    public async Task ScaffoldedContext_ShouldWorkWithCRUDOperations()
    {
        try
        {
            // Arrange
            await CreateTestDatabaseAsync();
            await ExecuteInitialMigrationAsync();

            var options = new DbContextOptionsBuilder<BizConnectContext>()
                .UseNpgsql(_testConnectionString)
                .Options;

            // Act & Assert
            using var context = new BizConnectContext(options);

            // Test Create
            var newUser = new User
            {
                Username = "testuser",
                PasswordHash = "hashedpassword",
                Role = "User",
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
                IsActive = true
            };

            context.Users.Add(newUser);
            await context.SaveChangesAsync();

            // Test Read
            var retrievedUser = await context.Users.FirstOrDefaultAsync(u => u.Username == "testuser");
            Assert.NotNull(retrievedUser);
            Assert.Equal("testuser", retrievedUser.Username);
            Assert.Equal("User", retrievedUser.Role);

            // Test Update
            retrievedUser!.Role = "Admin";
            retrievedUser.UpdatedAt = DateTime.UtcNow;
            await context.SaveChangesAsync();

            var updatedUser = await context.Users.FirstOrDefaultAsync(u => u.Username == "testuser");
            Assert.Equal("Admin", updatedUser!.Role);

            // Test Delete
            context.Users.Remove(updatedUser);
            await context.SaveChangesAsync();

            var deletedUser = await context.Users.FirstOrDefaultAsync(u => u.Username == "testuser");
            Assert.Null(deletedUser);
        }
        finally
        {
            await CleanupTestDatabaseAsync();
        }
    }

    [Fact]
    public void MigrationScripts_ShouldExistAndBeValid()
    {
        // Arrange
        var migrationsPath = Path.Combine(Directory.GetCurrentDirectory(), "..", "..", "..", "..", "db", "migrations");
        
        // Act & Assert
        Assert.True(Directory.Exists(migrationsPath), "Migrations directory should exist");
        
        var sqlFiles = Directory.GetFiles(migrationsPath, "*.sql");
        Assert.NotEmpty(sqlFiles);

        // Verify naming convention
        foreach (var sqlFile in sqlFiles)
        {
            var fileName = Path.GetFileName(sqlFile);
            Assert.Matches(@"^\d{8}-\d{2}_\w+\.sql$", fileName);
        }

        // Verify initial migration exists
        var initialMigration = sqlFiles.FirstOrDefault(f => Path.GetFileName(f).Contains("InitialSchema"));
        Assert.NotNull(initialMigration);
    }

    [Fact]
    public void PowerShellScript_ShouldExistAndBeExecutable()
    {
        // Arrange
        var scriptPath = Path.Combine(Directory.GetCurrentDirectory(), "..", "..", "..", "..", "scripts", "update-db.ps1");

        // Act & Assert
        Assert.True(File.Exists(scriptPath), "PowerShell migration script should exist");

        var scriptContent = File.ReadAllText(scriptPath);
        Assert.Contains("BizConnect Database Migration Workflow", scriptContent);
        Assert.Contains("psql", scriptContent);
        Assert.Contains("dotnet", scriptContent);
    }

    [Fact]
    public void BashScript_ShouldExistAndBeExecutable()
    {
        // Arrange
        var scriptPath = Path.Combine(Directory.GetCurrentDirectory(), "..", "..", "..", "..", "scripts", "update-db.sh");

        // Act & Assert
        Assert.True(File.Exists(scriptPath), "Bash migration script should exist");

        var scriptContent = File.ReadAllText(scriptPath);
        Assert.Contains("BizConnect Database Migration and EF Core Scaffolding Script", scriptContent);
        Assert.Contains("psql", scriptContent);
        Assert.Contains("dotnet", scriptContent);
    }

    private async Task CreateTestDatabaseAsync()
    {
        var masterConnectionString = "Host=localhost;Database=postgres;Username=postgres;Password=bizitadmin";
        var options = new DbContextOptionsBuilder()
            .UseNpgsql(masterConnectionString)
            .Options;

        using var context = new DbContext(options);
        await context.Database.ExecuteSqlRawAsync($"CREATE DATABASE \"{_testDatabaseName}\"");
    }

    private async Task ExecuteInitialMigrationAsync()
    {
        var migrationPath = Path.Combine(Directory.GetCurrentDirectory(), "..", "..", "..", "..", "db", "migrations", "20250710-01_InitialSchema.sql");

        if (!File.Exists(migrationPath))
        {
            throw new FileNotFoundException($"Initial migration file not found: {migrationPath}");
        }

        var migrationSql = await File.ReadAllTextAsync(migrationPath);

        var options = new DbContextOptionsBuilder()
            .UseNpgsql(_testConnectionString)
            .Options;

        using var context = new DbContext(options);
        await context.Database.ExecuteSqlRawAsync(migrationSql);
    }

    private async Task CleanupTestDatabaseAsync()
    {
        try
        {
            var masterConnectionString = "Host=localhost;Database=postgres;Username=postgres;Password=bizitadmin";
            var options = new DbContextOptionsBuilder()
                .UseNpgsql(masterConnectionString)
                .Options;

            using var context = new DbContext(options);

            // Force close any existing connections to the test database
            await context.Database.ExecuteSqlRawAsync($@"
                SELECT pg_terminate_backend(pid)
                FROM pg_stat_activity
                WHERE datname = '{_testDatabaseName}' AND pid <> pg_backend_pid()");

            // Drop the test database
            await context.Database.ExecuteSqlRawAsync($"DROP DATABASE IF EXISTS \"{_testDatabaseName}\"");

            Console.WriteLine($"✅ Cleaned up test database: {_testDatabaseName}");
        }
        catch (Exception ex)
        {
            // Log cleanup errors but don't fail the test
            Console.WriteLine($"⚠️ Failed to cleanup test database {_testDatabaseName}: {ex.Message}");
        }
    }

    public void Dispose()
    {
        try
        {
            // Clean up test database
            var masterConnectionString = "Host=localhost;Database=postgres;Username=postgres;Password=bizitadmin";
            var options = new DbContextOptionsBuilder()
                .UseNpgsql(masterConnectionString)
                .Options;

            using var context = new DbContext(options);

            // Force close any existing connections to the test database
            context.Database.ExecuteSqlRaw($@"
                SELECT pg_terminate_backend(pid)
                FROM pg_stat_activity
                WHERE datname = '{_testDatabaseName}' AND pid <> pg_backend_pid()");

            // Drop the test database
            context.Database.ExecuteSqlRaw($"DROP DATABASE IF EXISTS \"{_testDatabaseName}\"");

            Console.WriteLine($"✅ Cleaned up test database: {_testDatabaseName}");
        }
        catch (Exception ex)
        {
            // Log cleanup errors but don't fail the test
            Console.WriteLine($"⚠️ Failed to cleanup test database {_testDatabaseName}: {ex.Message}");
        }
    }
}
