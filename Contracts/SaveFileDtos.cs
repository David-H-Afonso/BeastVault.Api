namespace BeastVault.Api.Contracts;

public sealed record SaveFileSummaryDto(
    int Id,
    string OriginalFileName,
    string Format,
    long Size,
    int Generation,
    int OriginGame,
    string GameName,
    string SaveType,
    DateTime ImportedAt,
    string? Notes,
    string TrainerName,
    uint TrainerId,
    uint SecretId,
    string PlayTime,
    int? BadgeCount,
    int DexSeen,
    int DexCaught,
    int PartyCount,
    int StoredPokemonCount,
    bool ChecksumsValid,
    string? Title = null,
    string DisplayTitle = "",
    int TrainerGender = 0,
    int? BadgeTotal = null);

public sealed record SaveTrainerDto(
    string TrainerName,
    uint TrainerId,
    uint SecretId,
    int Gender,
    string Language,
    uint Money,
    int PlayTimeHours,
    int PlayTimeMinutes,
    int PlayTimeSeconds,
    string PlayTime,
    int? BadgeCount,
    int DexSeen,
    int DexCaught);

public sealed record SavePokedexEntryDto(
    int SpeciesId,
    string SpeciesName,
    bool Seen,
    bool Caught,
    bool IsVersionExclusive = false);

public sealed record SavePokedexProgressDto(
    IReadOnlyList<SavePokedexEntryDto> Entries,
    int Seen,
    int Caught,
    int Total);

public sealed record SavePokemonPreviewDto(
    int Id,
    string Location,
    int? BoxNumber,
    int SlotNumber,
    int SpeciesId,
    string SpeciesName,
    string? Nickname,
    int Level,
    bool IsShiny,
    bool IsEgg,
    int Form,
    int Gender,
    int Nature,
    string NatureName,
    string AbilityName,
    string HeldItemName,
    IReadOnlyList<string> Moves,
    string PokemonHash,
    int? ExistingPokemonId);

public sealed record SaveFileDetailDto(
    SaveFileSummaryDto Summary,
    SaveTrainerDto Trainer,
    IReadOnlyList<SavePokedexEntryDto> Pokedex,
    IReadOnlyList<SavePokemonPreviewDto> Pokemon,
    SavePokedexProgressDto? RegionalPokedex = null,
    SavePokedexProgressDto? NationalPokedex = null);

public sealed record SaveFileUploadResultDto(
    string FileName,
    string Status,
    int? SaveFileId = null,
    string? Message = null);

public sealed record ImportSavePokemonRequest(IReadOnlyList<int> PreviewIds);

public sealed record SavePokemonImportResultDto(
    int PreviewId,
    string Status,
    int? PokemonId = null,
    string? Message = null);

public sealed record UpdateSaveFileRequest(string? Title, string? Notes);
