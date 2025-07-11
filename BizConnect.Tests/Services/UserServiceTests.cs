using BizConnect.Dal;
using BizConnect.Dal.Models;
using BizConnect.Services;
using Microsoft.EntityFrameworkCore;

namespace BizConnect.Tests.Services;

public class UserServiceTests : IDisposable
{
    private readonly BizConnectContext _context;
    private readonly UserService _userService;

    public UserServiceTests()
    {
        var options = new DbContextOptionsBuilder<BizConnectContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _context = new BizConnectContext(options);
        _userService = new UserService(_context);

        // Seed test data
        SeedTestData();
    }

    private void SeedTestData()
    {
        var users = new List<User>
        {
            new User
            {
                Id = 1,
                Username = "admin",
                PasswordHash = BCrypt.Net.BCrypt.HashPassword("admin123"),
                Role = "Admin",
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            },
            new User
            {
                Id = 2,
                Username = "user1",
                PasswordHash = BCrypt.Net.BCrypt.HashPassword("user123"),
                Role = "User",
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            },
            new User
            {
                Id = 3,
                Username = "inactive",
                PasswordHash = BCrypt.Net.BCrypt.HashPassword("inactive123"),
                Role = "User",
                IsActive = false,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            }
        };

        _context.Users.AddRange(users);
        _context.SaveChanges();
    }

    [Fact]
    public async Task AuthenticateAsync_ValidCredentials_ReturnsUser()
    {
        // Act
        var result = await _userService.AuthenticateAsync("admin", "admin123");

        // Assert
        Assert.NotNull(result);
        Assert.Equal("admin", result.Username);
        Assert.Equal("Admin", result.Role);
    }

    [Fact]
    public async Task AuthenticateAsync_InvalidPassword_ReturnsNull()
    {
        // Act
        var result = await _userService.AuthenticateAsync("admin", "wrongpassword");

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task AuthenticateAsync_InactiveUser_ReturnsNull()
    {
        // Act
        var result = await _userService.AuthenticateAsync("inactive", "inactive123");

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task AuthenticateAsync_NonExistentUser_ReturnsNull()
    {
        // Act
        var result = await _userService.AuthenticateAsync("nonexistent", "password");

        // Assert
        Assert.Null(result);
    }

    [Theory]
    [InlineData("", "password")]
    [InlineData("username", "")]
    [InlineData(null, "password")]
    [InlineData("username", null)]
    public async Task AuthenticateAsync_EmptyCredentials_ReturnsNull(string username, string password)
    {
        // Act
        var result = await _userService.AuthenticateAsync(username, password);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task GetByUsernameAsync_ExistingUser_ReturnsUser()
    {
        // Act
        var result = await _userService.GetByUsernameAsync("admin");

        // Assert
        Assert.NotNull(result);
        Assert.Equal("admin", result.Username);
    }

    [Fact]
    public async Task GetByUsernameAsync_NonExistentUser_ReturnsNull()
    {
        // Act
        var result = await _userService.GetByUsernameAsync("nonexistent");

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task GetByIdAsync_ExistingUser_ReturnsUser()
    {
        // Act
        var result = await _userService.GetByIdAsync(1);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(1, result.Id);
        Assert.Equal("admin", result.Username);
    }

    [Fact]
    public async Task GetByIdAsync_NonExistentUser_ReturnsNull()
    {
        // Act
        var result = await _userService.GetByIdAsync(999);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task GetAllUsersAsync_ReturnsActiveUsers()
    {
        // Act
        var result = await _userService.GetAllUsersAsync();
        var users = result.ToList();

        // Assert
        Assert.Equal(2, users.Count); // Only active users
        Assert.All(users, u => Assert.True(u.IsActive));
        Assert.Contains(users, u => u.Username == "admin");
        Assert.Contains(users, u => u.Username == "user1");
    }

    [Fact]
    public async Task CreateUserAsync_ValidData_CreatesUser()
    {
        // Act
        var result = await _userService.CreateUserAsync("newuser", "password123", "User");

        // Assert
        Assert.NotNull(result);
        Assert.Equal("newuser", result.Username);
        Assert.Equal("User", result.Role);
        Assert.True(result.IsActive);
        Assert.True(BCrypt.Net.BCrypt.Verify("password123", result.PasswordHash));
    }

    [Fact]
    public async Task CreateUserAsync_DuplicateUsername_ThrowsException()
    {
        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => _userService.CreateUserAsync("admin", "password123", "User"));
    }

    [Theory]
    [InlineData("", "password", "User")]
    [InlineData("username", "", "User")]
    [InlineData("username", "password", "")]
    [InlineData(null, "password", "User")]
    [InlineData("username", null, "User")]
    [InlineData("username", "password", null)]
    public async Task CreateUserAsync_InvalidData_ThrowsArgumentException(string username, string password, string role)
    {
        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(
            () => _userService.CreateUserAsync(username, password, role));
    }

    [Fact]
    public async Task CreateUserAsync_InvalidRole_ThrowsArgumentException()
    {
        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(
            () => _userService.CreateUserAsync("newuser", "password123", "InvalidRole"));
    }

    [Fact]
    public async Task ResetPasswordAsync_ExistingUser_ResetsPassword()
    {
        // Act
        var result = await _userService.ResetPasswordAsync(1, "newpassword123");

        // Assert
        Assert.True(result);
        
        // Verify password was changed
        var user = await _userService.GetByIdAsync(1);
        Assert.True(BCrypt.Net.BCrypt.Verify("newpassword123", user!.PasswordHash));
    }

    [Fact]
    public async Task ResetPasswordAsync_NonExistentUser_ReturnsFalse()
    {
        // Act
        var result = await _userService.ResetPasswordAsync(999, "newpassword123");

        // Assert
        Assert.False(result);
    }

    [Fact]
    public async Task UsernameExistsAsync_ExistingUsername_ReturnsTrue()
    {
        // Act
        var result = await _userService.UsernameExistsAsync("admin");

        // Assert
        Assert.True(result);
    }

    [Fact]
    public async Task UsernameExistsAsync_NonExistentUsername_ReturnsFalse()
    {
        // Act
        var result = await _userService.UsernameExistsAsync("nonexistent");

        // Assert
        Assert.False(result);
    }

    [Fact]
    public async Task DeleteUserAsync_ExistingUser_SoftDeletesUser()
    {
        // Act
        var result = await _userService.DeleteUserAsync(2);

        // Assert
        Assert.True(result);
        
        // Verify user is soft deleted
        var user = await _userService.GetByIdAsync(2);
        Assert.False(user!.IsActive);
    }

    [Fact]
    public async Task DeleteUserAsync_NonExistentUser_ReturnsFalse()
    {
        // Act
        var result = await _userService.DeleteUserAsync(999);

        // Assert
        Assert.False(result);
    }

    public void Dispose()
    {
        _context.Dispose();
    }
}
