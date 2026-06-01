namespace BeastVault.Api.Domain.Entities;

public enum TagCategory
{
    Uncategorized = 0,
    Run = 1,
    Team = 2,
    Collection = 3,
    Personal = 4,
    Utility = 5
}

public class TagEntity
{
    public int Id { get; set; }
    public int? UserId { get; set; }
    public required string Name { get; set; }
    public string? ImagePath { get; set; }
    public TagCategory Category { get; set; } = TagCategory.Uncategorized;
    public string? ColorHex { get; set; }
    public int SortOrder { get; set; } = 0;
    public string? Description { get; set; }

    public ICollection<PokemonTagEntity> PokemonTags { get; set; } = new List<PokemonTagEntity>();
    public ICollection<FileTagEntity> FileTags { get; set; } = new List<FileTagEntity>();
    public User? User { get; set; }
}
