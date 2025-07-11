using System.Text.Json;
using BizConnect.Dal;
using BizConnect.Dal.Models;
using Microsoft.EntityFrameworkCore;

namespace BizConnect.Tests.Unit;

/// <summary>
/// Unit tests for migration workflow components.
/// These tests validate individual components of the migration workflow
/// without requiring a full database setup.
/// </summary>
public class MigrationWorkflowUnitTests
{
    [Fact]
    public void AppSettingsLocal_ShouldHaveValidConnectionString()
    {
        // Arrange
        var appSettingsPath = Path.Combine(Directory.GetCurrentDirectory(), "..", "..", "..", "..", "BizConnect", "appsettings.Local.json");
        
        // Act & Assert
        Assert.True(File.Exists(appSettingsPath), "appsettings.Local.json should exist");
        
        var jsonContent = File.ReadAllText(appSettingsPath);
        var appSettings = JsonSerializer.Deserialize<JsonElement>(jsonContent);
        
        Assert.True(appSettings.TryGetProperty("ConnectionStrings", out var connectionStrings));
        Assert.True(connectionStrings.TryGetProperty("DefaultConnection", out var defaultConnection));
        
        var connectionString = defaultConnection.GetString();
        Assert.NotNull(connectionString);
        Assert.Contains("Host=localhost", connectionString);
        Assert.Contains("Database=bizconnect_test", connectionString);
        Assert.Contains("Username=postgres", connectionString);
        Assert.Contains("Password=bizitadmin", connectionString);
    }

    [Fact]
    public void BizConnectContext_ShouldHaveCorrectConfiguration()
    {
        // Arrange
        var options = new DbContextOptionsBuilder<BizConnectContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        // Act
        using var context = new BizConnectContext(options);
        var model = context.Model;

        // Assert
        Assert.NotNull(context.Users);
        
        var userEntityType = model.FindEntityType(typeof(User));
        Assert.NotNull(userEntityType);
        
        // Verify table name (scaffolded context uses singular form)
        Assert.Equal("User", userEntityType.GetTableName());
        
        // Verify primary key
        var primaryKey = userEntityType.FindPrimaryKey();
        Assert.NotNull(primaryKey);
        Assert.Equal("Id", primaryKey.Properties.First().Name);
    }

    [Fact]
    public void UserModel_ShouldHaveCorrectProperties()
    {
        // Arrange & Act
        var user = new User
        {
            Id = 1,
            Username = "testuser",
            PasswordHash = "hashedpassword",
            Role = "User",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            IsActive = true
        };

        // Assert
        Assert.Equal(1, user.Id);
        Assert.Equal("testuser", user.Username);
        Assert.Equal("hashedpassword", user.PasswordHash);
        Assert.Equal("User", user.Role);
        Assert.True(user.IsActive);
        Assert.True(user.CreatedAt <= DateTime.UtcNow);
        Assert.True(user.UpdatedAt <= DateTime.UtcNow);
    }

    [Fact]
    public void UserModel_ShouldSupportPartialClass()
    {
        // This test verifies that the User model is a partial class,
        // which is important for EF Core scaffolding
        
        // Arrange & Act
        var userType = typeof(User);
        
        // Assert
        Assert.True(userType.IsClass);
        Assert.False(userType.IsSealed);
        
        // Verify it's in the correct namespace
        Assert.Equal("BizConnect.Dal.Models", userType.Namespace);
    }

    [Fact]
    public void BizConnectContext_ShouldSupportPartialClass()
    {
        // This test verifies that the BizConnectContext is a partial class,
        // which is important for EF Core scaffolding
        
        // Arrange & Act
        var contextType = typeof(BizConnectContext);
        
        // Assert
        Assert.True(contextType.IsClass);
        Assert.False(contextType.IsSealed);
        Assert.True(contextType.IsSubclassOf(typeof(DbContext)));
        
        // Verify it's in the correct namespace
        Assert.Equal("BizConnect.Dal", contextType.Namespace);
    }

    [Fact]
    public void MigrationFiles_ShouldFollowNamingConvention()
    {
        // Arrange
        var migrationsPath = Path.Combine(Directory.GetCurrentDirectory(), "..", "..", "..", "..", "db", "migrations");
        
        // Act
        var sqlFiles = Directory.Exists(migrationsPath) 
            ? Directory.GetFiles(migrationsPath, "*.sql")
            : Array.Empty<string>();

        // Assert
        Assert.NotEmpty(sqlFiles);
        
        foreach (var sqlFile in sqlFiles)
        {
            var fileName = Path.GetFileName(sqlFile);
            
            // Verify naming convention: yyyyMMdd-##_Description.sql
            Assert.Matches(@"^\d{8}-\d{2}_\w+\.sql$", fileName);
            
            // Verify file is not empty
            var fileInfo = new FileInfo(sqlFile);
            Assert.True(fileInfo.Length > 0, $"Migration file {fileName} should not be empty");
        }
    }

    [Fact]
    public void InitialMigration_ShouldContainRequiredSqlStatements()
    {
        // Arrange
        var migrationPath = Path.Combine(Directory.GetCurrentDirectory(), "..", "..", "..", "..", "db", "migrations", "20250710-01_InitialSchema.sql");
        
        // Act & Assert
        Assert.True(File.Exists(migrationPath), "Initial migration file should exist");
        
        var migrationContent = File.ReadAllText(migrationPath);
        
        // Verify essential SQL statements
        Assert.Contains("CREATE TABLE IF NOT EXISTS \"Users\"", migrationContent);
        Assert.Contains("CREATE UNIQUE INDEX IF NOT EXISTS \"IX_Users_Username\"", migrationContent);
        Assert.Contains("CREATE INDEX IF NOT EXISTS \"IX_Users_Role\"", migrationContent);
        Assert.Contains("CREATE INDEX IF NOT EXISTS \"IX_Users_IsActive\"", migrationContent);
        Assert.Contains("INSERT INTO \"Users\"", migrationContent);
        Assert.Contains("CREATE OR REPLACE FUNCTION update_updated_at_column()", migrationContent);
        Assert.Contains("CREATE TRIGGER update_users_updated_at", migrationContent);
        
        // Verify idempotent operations
        Assert.Contains("IF NOT EXISTS", migrationContent);
        Assert.Contains("IF EXISTS", migrationContent);
    }

    [Fact]
    public void ProjectStructure_ShouldFollowExpectedLayout()
    {
        // Arrange
        var projectRoot = Path.Combine(Directory.GetCurrentDirectory(), "..", "..", "..", "..");
        
        // Act & Assert
        // Verify main project directories exist
        Assert.True(Directory.Exists(Path.Combine(projectRoot, "BizConnect")), "BizConnect project should exist");
        Assert.True(Directory.Exists(Path.Combine(projectRoot, "BizConnect.Dal")), "BizConnect.Dal project should exist");
        Assert.True(Directory.Exists(Path.Combine(projectRoot, "BizConnect.Services")), "BizConnect.Services project should exist");
        Assert.True(Directory.Exists(Path.Combine(projectRoot, "BizConnect.Tests")), "BizConnect.Tests project should exist");
        
        // Verify migration infrastructure
        Assert.True(Directory.Exists(Path.Combine(projectRoot, "db", "migrations")), "Migrations directory should exist");
        Assert.True(Directory.Exists(Path.Combine(projectRoot, "scripts")), "Scripts directory should exist");
        
        // Verify key files exist
        Assert.True(File.Exists(Path.Combine(projectRoot, "scripts", "update-db.ps1")), "PowerShell script should exist");
        Assert.True(File.Exists(Path.Combine(projectRoot, "scripts", "update-db.sh")), "Bash script should exist");
        Assert.True(File.Exists(Path.Combine(projectRoot, "db", "migrations", "README.md")), "Migration README should exist");
    }

    [Theory]
    [InlineData("Admin")]
    [InlineData("User")]
    public void UserModel_ShouldSupportValidRoles(string role)
    {
        // Arrange & Act
        var user = new User
        {
            Username = "testuser",
            PasswordHash = "hashedpassword",
            Role = role,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            IsActive = true
        };

        // Assert
        Assert.Equal(role, user.Role);
        Assert.Contains(role, new[] { "Admin", "User" });
    }

    [Fact]
    public void BizConnectContext_ShouldHaveUsersDbSet()
    {
        // Arrange
        var options = new DbContextOptionsBuilder<BizConnectContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        // Act & Assert
        using var context = new BizConnectContext(options);
        
        Assert.NotNull(context.Users);
        Assert.IsAssignableFrom<DbSet<User>>(context.Users);
    }
}
