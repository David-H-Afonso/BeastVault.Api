namespace BeastVault.Api.Domain.Entities;

public class MoveEntity
{
    public int PokemonId { get; set; }
    public int Slot { get; set; }
    public int MoveId { get; set; }
    public int PpUps { get; set; }
    public int CurrentPp { get; set; }
}
