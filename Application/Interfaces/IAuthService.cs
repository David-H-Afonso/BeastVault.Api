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
    Task<bool> AdminResetPasswordAsync(int userId, string newPassword);
    Task<bool> RenameUserAsync(int userId, string newUsername);
    Task<bool> UpdateRoleAsync(int requestingUserId, int targetUserId, UserRole newRole);
    Task<UserPreferencesDto> GetPreferencesAsync(int userId);
    Task<UserPreferencesDto> UpdatePreferencesAsync(int userId, UpdatePreferencesRequest request);
    string HashPassword(string password);
    bool VerifyPassword(string password, string hash);
}
