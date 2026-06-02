namespace BeastVault.Api.Domain.Entities;

public class PokemonBoxSlotEntity
{
    public int BoxId { get; set; }
    public int SlotIndex { get; set; }
    public int PokemonId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public PokemonBoxEntity Box { get; set; } = null!;
    public PokemonEntity Pokemon { get; set; } = null!;
}
