using BCrypt.Net;
using BizConnect.Dal;
using BizConnect.Dal.Models;
using BizConnect.Services.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace BizConnect.Services;

public class UserService : IUserService
{
    private readonly BizConnectContext _context;

    public UserService(BizConnectContext context)
    {
        _context = context;
    }

    public async Task<User?> AuthenticateAsync(string username, string password)
    {
        if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
            return null;

        var user = await _context.Users
            .FirstOrDefaultAsync(u => u.Username == username && u.IsActive);

        if (user == null)
            return null;

        // Verify password using BCrypt
        if (!BCrypt.Net.BCrypt.Verify(password, user.PasswordHash))
            return null;

        return user;
    }

    public async Task<User?> GetByUsernameAsync(string username)
    {
        if (string.IsNullOrWhiteSpace(username))
            return null;

        return await _context.Users
            .FirstOrDefaultAsync(u => u.Username == username);
    }

    public async Task<User?> GetByIdAsync(int id)
    {
        return await _context.Users
            .FirstOrDefaultAsync(u => u.Id == id);
    }

    public async Task<IEnumerable<User>> GetAllUsersAsync()
    {
        return await _context.Users
            .Where(u => u.IsActive)
            .OrderBy(u => u.Username)
            .ToListAsync();
    }

    public async Task<User> CreateUserAsync(string username, string password, string role)
    {
        if (string.IsNullOrWhiteSpace(username))
            throw new ArgumentException("Username cannot be empty", nameof(username));

        if (string.IsNullOrWhiteSpace(password))
            throw new ArgumentException("Password cannot be empty", nameof(password));

        if (string.IsNullOrWhiteSpace(role))
            throw new ArgumentException("Role cannot be empty", nameof(role));

        if (await UsernameExistsAsync(username))
            throw new InvalidOperationException($"Username '{username}' already exists");

        // Validate role
        if (role != "Admin" && role != "User")
            throw new ArgumentException("Role must be either 'Admin' or 'User'", nameof(role));

        var user = new User
        {
            Username = username,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(password),
            Role = role,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            IsActive = true
        };

        _context.Users.Add(user);
        await _context.SaveChangesAsync();

        return user;
    }

    public async Task<bool> ResetPasswordAsync(int userId, string newPassword)
    {
        if (string.IsNullOrWhiteSpace(newPassword))
            throw new ArgumentException("Password cannot be empty", nameof(newPassword));

        var user = await GetByIdAsync(userId);
        if (user == null)
            return false;

        user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(newPassword);
        user.UpdatedAt = DateTime.UtcNow;

        _context.Users.Update(user);
        await _context.SaveChangesAsync();

        return true;
    }

    public async Task<bool> UpdateUserAsync(User user)
    {
        if (user == null)
            throw new ArgumentNullException(nameof(user));

        var existingUser = await GetByIdAsync(user.Id);
        if (existingUser == null)
            return false;

        existingUser.Username = user.Username;
        existingUser.Role = user.Role;
        existingUser.IsActive = user.IsActive;
        existingUser.UpdatedAt = DateTime.UtcNow;

        _context.Users.Update(existingUser);
        await _context.SaveChangesAsync();

        return true;
    }

    public async Task<bool> DeleteUserAsync(int userId)
    {
        var user = await GetByIdAsync(userId);
        if (user == null)
            return false;

        // Soft delete - set IsActive to false
        user.IsActive = false;
        user.UpdatedAt = DateTime.UtcNow;

        _context.Users.Update(user);
        await _context.SaveChangesAsync();

        return true;
    }

    public async Task<bool> UsernameExistsAsync(string username)
    {
        if (string.IsNullOrWhiteSpace(username))
            return false;

        return await _context.Users
            .AnyAsync(u => u.Username == username);
    }
}
