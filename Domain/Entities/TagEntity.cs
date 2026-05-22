namespace BeastVault.Api.Domain.Entities;

public class TagEntity
{
    public int Id { get; set; }
    public int? UserId { get; set; }
    public required string Name { get; set; }
    public string? ImagePath { get; set; }

    public ICollection<PokemonTagEntity> PokemonTags { get; set; } = new List<PokemonTagEntity>();
    public ICollection<FileTagEntity> FileTags { get; set; } = new List<FileTagEntity>();
    public User? User { get; set; }
}
