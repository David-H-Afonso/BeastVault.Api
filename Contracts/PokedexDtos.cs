namespace BeastVault.Api.Contracts;

public record SpeciesWithFormsResponse(
    bool Found,
    SpeciesDto? Species = null,
    IEnumerable<PokemonFormDto>? Forms = null);

public record SpeciesDto(
    int SpeciesId,
    string Name,
    object LocalizedNames,
    string Genus,
    string FlavorText,
    int Generation,
    string Color,
    string Shape,
    string Habitat,
    string GrowthRate,
    int CaptureRate,
    int BaseHappiness,
    int HatchCounter,
    int GenderRate,
    bool IsLegendary,
    bool IsMythical,
    bool IsBaby,
    bool HasGenderDifferences,
    bool FormsSwitchable,
    object EggGroups,
    object Varieties,
    string EvolutionChainUrl);

public record PokemonFormDto(
    int PokemonId,
    int SpeciesId,
    string Name,
    int Height,
    int Weight,
    int BaseExperience,
    bool IsDefault,
    object Types,
    object Abilities,
    object BaseStats,
    object Sprites,
    object Cries);

public record PopulationStatusResponse(
    int TotalSpecies,
    int TotalForms,
    int MaxSpeciesId,
    DateTime? LastUpdated);

public record PopulateResponse(
    string Message,
    int Populated,
    int StartId,
    int EndId);
