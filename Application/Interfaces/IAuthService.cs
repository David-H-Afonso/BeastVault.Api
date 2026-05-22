using BeastVault.Api.Contracts;
using BeastVault.Api.Domain.Entities;

namespace BeastVault.Api.Application.Interfaces;

public interface IAuthService
{
    Task<LoginResponse?> LoginAsync(string username, string? password);
    Task<User?> CreateUserAsync(string username, string password, UserRole role = UserRole.Standard);
    Task<List<UserDto>> GetAllUsersAsync();
    Task<bool> DeleteUserAsync(int id);
    Task<bool> UpdatePasswordAsync(int userId, string? currentPassword, string newPassword);
    string HashPassword(string password);
    bool VerifyPassword(string password, string hash);
}
