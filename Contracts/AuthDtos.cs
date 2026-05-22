namespace BeastVault.Api.Contracts;

public record LoginRequest(string Username, string? Password = null);

public record LoginResponse(
    int UserId,
    string Username,
    string Role,
    string Token);

public record RegisterRequest(string Username, string Password);

public record UserDto(
    int Id,
    string Username,
    string Role,
    bool IsDefault,
    DateTime CreatedAt);

public record MeResponse(int UserId, string Username, string Role);

public record UpdatePasswordRequest(string? CurrentPassword, string NewPassword);

public record AdminResetPasswordRequest(string NewPassword);

public record RenameUserRequest(string NewUsername);

public record UpdateRoleRequest(string Role);

public record UserPreferencesDto(
    string Theme,
    string ViewMode,
    string SpriteType,
    string BackgroundType);

public record UpdatePreferencesRequest(
    string? Theme = null,
    string? ViewMode = null,
    string? SpriteType = null,
    string? BackgroundType = null);
