namespace BeastVault.Api.Domain.Entities;

public class PokemonBoxEntity
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public string Name { get; set; } = "Box";
    public int SortOrder { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public User User { get; set; } = null!;
    public ICollection<PokemonBoxSlotEntity> Slots { get; set; } = new List<PokemonBoxSlotEntity>();
}
