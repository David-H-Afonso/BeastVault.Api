namespace BeastVault.Api.Domain.Entities;

public class UserPreference
{
    public int UserId { get; set; }
    public string Theme { get; set; } = "dark";
    public string ViewMode { get; set; } = "grid";
    public string SpriteType { get; set; } = "sprites";
    public string BackgroundType { get; set; } = "diagonal-45";

    public User User { get; set; } = null!;
}
