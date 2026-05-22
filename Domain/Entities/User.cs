namespace BeastVault.Api.Domain.Entities;

public enum UserRole
{
    Standard = 0,
    Admin = 1
}

public class User
{
    public int Id { get; set; }
    public string Username { get; set; } = string.Empty;
    public string? PasswordHash { get; set; }
    public UserRole Role { get; set; } = UserRole.Standard;
    public bool IsDefault { get; set; } = false;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<FileEntity> Files { get; set; } = [];
    public ICollection<PokemonEntity> Pokemon { get; set; } = [];
    public ICollection<TagEntity> Tags { get; set; } = [];
    public UserPreference? Preferences { get; set; }
}
