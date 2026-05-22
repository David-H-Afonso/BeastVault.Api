using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using BeastVault.Api.Configuration;
using BeastVault.Api.Contracts;
using BeastVault.Api.Domain.Entities;
using BeastVault.Api.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using BeastVault.Api.Application.Interfaces;

namespace BeastVault.Api.Application.Services;

public class AuthService : IAuthService
{
    private readonly AppDbContext _context;
    private readonly JwtSettings _jwtSettings;

    public AuthService(AppDbContext context, IOptions<JwtSettings> jwtSettings)
    {
        _context = context;
        _jwtSettings = jwtSettings.Value;
    }

    public async Task<LoginResponse?> LoginAsync(string username, string? password)
    {
        var user = await _context.Users
            .FirstOrDefaultAsync(u => u.Username.ToLower() == username.ToLower());

        if (user == null)
            return null;

        if (user.PasswordHash != null)
        {
            if (string.IsNullOrEmpty(password) || !VerifyPassword(password, user.PasswordHash))
                return null;
        }

        var token = GenerateAccessToken(user);

        return new LoginResponse(
            UserId: user.Id,
            Username: user.Username,
            Role: user.Role.ToString(),
            Token: token);
    }

    public async Task<User?> CreateUserAsync(string username, string password, UserRole role = UserRole.Standard)
    {
        if (await _context.Users.AnyAsync(u => u.Username.ToLower() == username.ToLower()))
            return null;

        var user = new User
        {
            Username = username,
            PasswordHash = HashPassword(password),
            Role = role,
            CreatedAt = DateTime.UtcNow
        };

        _context.Users.Add(user);
        await _context.SaveChangesAsync();
        return user;
    }

    public async Task<List<UserDto>> GetAllUsersAsync() =>
        await _context.Users
            .OrderBy(u => u.CreatedAt)
            .Select(u => new UserDto(u.Id, u.Username, u.Role.ToString(), u.IsDefault, u.CreatedAt))
            .ToListAsync();

    public async Task<bool> DeleteUserAsync(int id)
    {
        var user = await _context.Users.FindAsync(id);
        if (user == null || user.IsDefault)
            return false;

        _context.Users.Remove(user);
        await _context.SaveChangesAsync();
        return true;
    }

    public async Task<bool> UpdatePasswordAsync(int userId, string? currentPassword, string newPassword)
    {
        var user = await _context.Users.FindAsync(userId);
        if (user == null) return false;

        if (user.PasswordHash != null && !string.IsNullOrEmpty(currentPassword))
        {
            if (!VerifyPassword(currentPassword, user.PasswordHash))
                return false;
        }

        user.PasswordHash = HashPassword(newPassword);
        await _context.SaveChangesAsync();
        return true;
    }

    public string HashPassword(string password) =>
        BCrypt.Net.BCrypt.HashPassword(password, workFactor: 12);

    public bool VerifyPassword(string password, string hash) =>
        BCrypt.Net.BCrypt.Verify(password, hash);

    private string GenerateAccessToken(User user)
    {
        var tokenHandler = new JwtSecurityTokenHandler();
        var key = Encoding.UTF8.GetBytes(_jwtSettings.SecretKey);
        var expires = DateTime.UtcNow.AddMinutes(_jwtSettings.AccessTokenMinutes);

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new(ClaimTypes.Name, user.Username),
            new(ClaimTypes.Role, user.Role.ToString())
        };

        var tokenDescriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(claims),
            Expires = expires,
            Issuer = _jwtSettings.Issuer,
            Audience = _jwtSettings.Audience,
            SigningCredentials = new SigningCredentials(
                new SymmetricSecurityKey(key),
                SecurityAlgorithms.HmacSha256Signature)
        };

        var token = tokenHandler.CreateToken(tokenDescriptor);
        return tokenHandler.WriteToken(token);
    }
}
