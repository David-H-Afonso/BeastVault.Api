namespace BeastVault.Api.Contracts;

public record DexHuntGameDto(int Id, string Name, int Generation);

public record DexHuntListSummaryDto(
    int Id,
    string Name,
    int GameId,
    string GameName,
    string? Description,
    int SortOrder,
    int TotalCount,
    int CaughtCount,
    DateTime CreatedAt,
    DateTime UpdatedAt);

public record DexHuntItemDto(
    int Id,
    int SpeciesId,
    string SpeciesName,
    int Generation,
    string[] Types,
    PokemonSpritesDto? Sprites,
    int Priority,
    bool IsCaught,
    string? Notes,
    int SortOrder,
    DateTime AddedAt,
    DateTime UpdatedAt,
    DateTime? CaughtAt);

public record DexHuntListDetailDto(DexHuntListSummaryDto List, IReadOnlyList<DexHuntItemDto> Items);

public record CreateDexHuntListRequest(string? Name, int GameId, string? Description);
public record UpdateDexHuntListRequest(string? Name, int? GameId, string? Description);
public record AddDexHuntItemRequest(int SpeciesId, int Priority = 1, string? Notes = null);
public record UpdateDexHuntItemRequest(bool? IsCaught, int? Priority, string? Notes);
public record ReorderDexHuntListsRequest(IReadOnlyList<int> ListIds);
public record ReorderDexHuntItemsRequest(IReadOnlyList<int> ItemIds);

public record DexHuntExportDto(
    int SchemaVersion,
    DateTime ExportedAt,
    DexHuntExportListDto List);

public record DexHuntExportListDto(
    string Name,
    DexHuntExportGameDto Game,
    string? Description,
    IReadOnlyList<DexHuntExportItemDto> Items);

public record DexHuntExportGameDto(int Id, string Name);

public record DexHuntExportItemDto(
    int SpeciesId,
    string SpeciesName,
    int Priority,
    bool Caught,
    string? Notes,
    DateTime? CaughtAt);
