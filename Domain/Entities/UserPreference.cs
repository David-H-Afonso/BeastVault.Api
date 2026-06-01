namespace BeastVault.Api.Domain.Entities;

public class UserPreference
{
    public int UserId { get; set; }
    public string Theme { get; set; } = "dark";
    public string ViewMode { get; set; } = "grid";
    public string SpriteType { get; set; } = "sprites";
    public string BackgroundType { get; set; } = "diagonal-45";
    public string OrganizeDensity { get; set; } = "expanded";
    public string KanbanDragMode { get; set; } = "move";

    public User User { get; set; } = null!;
}
