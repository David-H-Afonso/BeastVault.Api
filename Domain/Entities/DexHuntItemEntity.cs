namespace BeastVault.Api.Domain.Entities;

public class DexHuntItemEntity
{
    public int Id { get; set; }
    public int HuntListId { get; set; }
    public int SpeciesId { get; set; }
    public int Priority { get; set; }
    public bool IsCaught { get; set; }
    public string? Notes { get; set; }
    public int SortOrder { get; set; }
    public DateTime AddedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? CaughtAt { get; set; }

    public DexHuntListEntity HuntList { get; set; } = null!;
}
