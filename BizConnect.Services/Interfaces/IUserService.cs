using BizConnect.Dal.Models;

namespace BizConnect.Services.Interfaces;

public interface IUserService
{
    Task<User?> AuthenticateAsync(string username, string password);
    Task<User?> GetByUsernameAsync(string username);
    Task<User?> GetByIdAsync(int id);
    Task<IEnumerable<User>> GetAllUsersAsync();
    Task<User> CreateUserAsync(string username, string password, string role);
    Task<bool> ResetPasswordAsync(int userId, string newPassword);
    Task<bool> UpdateUserAsync(User user);
    Task<bool> DeleteUserAsync(int userId);
    Task<bool> UsernameExistsAsync(string username);
}
