using BeastVault.Api.Domain.Entities;
using BeastVault.Api.Infrastructure.Services;
using BeastVault.Api.Application.Mapping;

namespace BeastVault.Api.Contracts;

public record PokemonQuery
{
    public string? Search { get; init; }
    public int? SpeciesId { get; init; }
    public int? Form { get; init; }
    public bool? IsShiny { get; init; }
    public int? BallId { get; init; }
    public int? OriginGame { get; init; }
    public int? TeraType { get; init; }
    public int Skip { get; init; } = 0;
    public int Take { get; init; } = 50;
    public int[]? TagIds { get; init; }
    public bool? HasNoTags { get; init; }
}

public record PagedResult<T>(IReadOnlyList<T> Items, int Total);

public record PokemonListItemDto
{
    public int Id { get; init; }
    public int SpeciesId { get; init; }
    public string SpeciesName { get; init; } = "";
    public int Form { get; init; }
    public string FormName { get; init; } = "";
    public string? Nickname { get; init; }
    public int Level { get; init; }
    public bool IsShiny { get; init; }
    public int BallId { get; init; }
    public int? TeraType { get; init; }
    public int HeldItemId { get; init; }
    public int Gender { get; init; }
    public string SpriteKey { get; init; } = "";
    public int OriginGeneration { get; init; }
    public int CapturedGeneration { get; init; }
    public bool CanGigantamax { get; init; }
    public bool HasMegaStone { get; init; }
    public List<TagDto> Tags { get; init; } = new();

    // Enriched fields from Pokédex cache (no frontend PokeAPI calls needed)
    public string? Type1 { get; init; }
    public string? Type2 { get; init; }
    public string BallName { get; init; } = "";
    public string BallSpriteUrl { get; init; } = "";
    public PokemonSpritesDto? Sprites { get; init; }
}

public record PokemonSpritesDto
{
    public string Default { get; init; } = "";
    public string Shiny { get; init; } = "";
    public string Official { get; init; } = "";
    public string OfficialShiny { get; init; } = "";
    public string Home { get; init; } = "";
    public string HomeShiny { get; init; } = "";
    public string Showdown { get; init; } = "";
    public string ShowdownShiny { get; init; } = "";
    public string Github { get; init; } = "";
    public string GithubShiny { get; init; } = "";

    /// <summary>
    /// Builds all sprite endpoint URLs for a given Pokémon.
    /// All URLs point to local endpoints — sprites are downloaded and cached in DB.
    /// </summary>
    public static PokemonSpritesDto ForPokemonId(int id, string? name = null) => new()
    {
        Default = $"/sprites/pokemon/{id}.png",
        Shiny = $"/sprites/pokemon/shiny/{id}.png",
        Official = $"/sprites/pokemon/artwork/{id}.png",
        OfficialShiny = $"/sprites/pokemon/artwork/shiny/{id}.png",
        Home = $"/sprites/pokemon/home/{id}.png",
        HomeShiny = $"/sprites/pokemon/home/shiny/{id}.png",
        Showdown = $"/sprites/pokemon/showdown/{id}.gif",
        ShowdownShiny = $"/sprites/pokemon/showdown/shiny/{id}.gif",
        Github = $"/sprites/pokemon/github/{id}.png",
        GithubShiny = $"/sprites/pokemon/github/shiny/{id}.png",
    };
}

public record PokemonDetailDto
{
    public int Id { get; init; }
    public int SpeciesId { get; init; }
    public int Form { get; init; }
    public string? Nickname { get; init; }
    public string OtName { get; init; } = "";
    public int Tid { get; init; }
    public int Sid { get; init; }
    public int Level { get; init; }
    public bool IsShiny { get; init; }
    public int Nature { get; init; }
    public int AbilityId { get; init; }
    public int BallId { get; init; }
    public int? TeraType { get; init; }
    public int OriginGame { get; init; }
    public string Language { get; init; } = "";
    public DateTime? MetDate { get; init; }
    public string? MetLocation { get; init; }
    public string SpriteKey { get; init; } = "";
    public bool Favorite { get; init; }
    public string? Notes { get; init; }

    // Enriched name fields resolved from PKHeX
    public string SpeciesName { get; init; } = "";
    public string FormName { get; init; } = "";
    public string NatureName { get; init; } = "";
    public string AbilityName { get; init; } = "";
    public string BallName { get; init; } = "";
    public string GenderName { get; init; } = "";
    public string? TeraTypeName { get; init; }
    public string LanguageName { get; init; } = "";
    public string HeldItemName { get; init; } = "";
    public string OtGenderName { get; init; } = "";
    public string OtLanguageName { get; init; } = "";
    public string? NatureBoostedStat { get; init; }
    public string? NatureReducedStat { get; init; }
    public int OriginGeneration { get; init; }

    // Clean display fields — frontend renders these directly
    public string OriginGameName { get; init; } = "";
    public int MetLevel { get; init; }
    public string? MetLocationName { get; init; }
    public string BallSpriteUrl { get; init; } = "";
    public string? HeldItemSpriteUrl { get; init; }
    public string? DisplayFormName { get; init; }
    public string PersonalityIdHex { get; init; } = "";
    public string EncryptionConstantHex { get; init; } = "";
    public int EffectiveFriendship { get; init; }

    public uint EncryptionConstant { get; init; }
    public uint PersonalityId { get; init; }
    public uint Experience { get; init; }
    public int CurrentFriendship { get; init; }
    public uint FormArgument { get; init; }
    public bool IsEgg { get; init; }
    public bool FatefulEncounter { get; init; }
    public int Gender { get; init; }
    public int OTGender { get; init; }
    public string OTLanguage { get; init; } = "";
    public int HeldItemId { get; init; }

    public int HeightScalar { get; init; }
    public int WeightScalar { get; init; }
    public int Scale { get; init; }

    public int PokerusState { get; init; }
    public int PokerusDays { get; init; }
    public int PokerusStrain { get; init; }

    public int ContestCool { get; init; }
    public int ContestBeauty { get; init; }
    public int ContestCute { get; init; }
    public int ContestSmart { get; init; }
    public int ContestTough { get; init; }
    public int ContestSheen { get; init; }

    public StatsDto? Stats { get; init; }
    public IReadOnlyList<MoveDto> Moves { get; init; } = Array.Empty<MoveDto>();
    public IReadOnlyList<RelearnMoveDto> RelearnMoves { get; init; } = Array.Empty<RelearnMoveDto>();

    public PokemonDetailDto(PokemonEntity p, StatsEntity? s, List<MoveEntity> moves, List<RelearnMoveEntity> relearnMoves, string fileFormat = "")
    {
        Id = p.Id;
        SpeciesId = p.SpeciesId;
        Nickname = p.Nickname;
        OtName = p.OtName;
        Tid = p.Tid;
        Sid = p.Sid;
        Level = p.Level;
        IsShiny = p.IsShiny;
        Nature = p.Nature;
        AbilityId = p.AbilityId;
        BallId = p.BallId;
        TeraType = p.TeraType;
        OriginGame = p.OriginGame;
        Language = p.Language;
        MetDate = p.MetDate;
        MetLocation = p.MetLocation;
        SpriteKey = p.SpriteKey;
        Favorite = p.Favorite;
        Notes = p.Notes;

        // Enriched name fields
        SpeciesName = PkHexStringService.GetSpeciesName(p.SpeciesId);
        FormName = PkHexStringService.GetFormName(p.SpeciesId, p.Form);
        NatureName = PkHexStringService.GetNatureName(p.Nature);
        AbilityName = PkHexStringService.GetAbilityName(p.AbilityId);
        BallName = PkHexStringService.GetBallName(p.BallId);
        GenderName = p.Gender switch { 0 => "Male", 1 => "Female", _ => "Genderless" };
        TeraTypeName = p.TeraType.HasValue && p.TeraType.Value >= 0 ? PkHexStringService.GetTypeName(p.TeraType.Value) : null;
        LanguageName = PkHexStringService.GetLanguageFullName(p.Language);
        HeldItemName = p.HeldItemId > 0 ? PkHexStringService.GetItemName(p.HeldItemId) : "";
        OtGenderName = p.OTGender switch { 0 => "Male", 1 => "Female", _ => "Unknown" };
        OtLanguageName = PkHexStringService.GetLanguageFullName(p.OTLanguage);
        NatureBoostedStat = PkHexStringService.GetNatureBoostedStat(p.Nature);
        NatureReducedStat = PkHexStringService.GetNatureReducedStat(p.Nature);
        OriginGeneration = !string.IsNullOrEmpty(fileFormat)
            ? PokemonGameInfoService.GetCapturedGeneration(p.OriginGame, fileFormat)
            : PokemonGameInfoService.GetSpeciesOriginGeneration(p.SpeciesId);

        // Clean display fields via mapper
        OriginGameName = PokemonDisplayMapper.ResolveOriginGameName(p.OriginGame);
        MetLevel = p.MetLevel;
        MetLocationName = p.MetLocation;
        BallSpriteUrl = PokemonDisplayMapper.ResolveBallSpriteUrl(p.BallId, BallName);
        HeldItemSpriteUrl = PokemonDisplayMapper.ResolveHeldItemSpriteUrl(p.HeldItemId, HeldItemName);
        DisplayFormName = PokemonDisplayMapper.ResolveDisplayFormName(p.SpeciesId, p.Form, FormName, p.CanGigantamax, p.HeldItemId);
        PersonalityIdHex = PokemonDisplayMapper.FormatPidHex(p.PersonalityId);
        EncryptionConstantHex = PokemonDisplayMapper.FormatEcHex(p.EncryptionConstant);
        EffectiveFriendship = PokemonDisplayMapper.ResolveEffectiveFriendship(p);

        EncryptionConstant = p.EncryptionConstant;
        PersonalityId = p.PersonalityId;
        Experience = p.Experience;
        CurrentFriendship = p.CurrentFriendship;
        Form = p.Form;
        FormArgument = p.FormArgument;
        IsEgg = p.IsEgg;
        FatefulEncounter = p.FatefulEncounter;
        Gender = p.Gender;
        OTGender = p.OTGender;
        OTLanguage = p.OTLanguage;
        HeldItemId = p.HeldItemId;

        HeightScalar = p.HeightScalar;
        WeightScalar = p.WeightScalar;
        Scale = p.Scale;

        PokerusState = p.PokerusState;
        PokerusDays = p.PokerusDays;
        PokerusStrain = p.PokerusStrain;

        ContestCool = p.ContestCool;
        ContestBeauty = p.ContestBeauty;
        ContestCute = p.ContestCute;
        ContestSmart = p.ContestSmart;
        ContestTough = p.ContestTough;
        ContestSheen = p.ContestSheen;

        Stats = s is null ? null : new StatsDto(s);
        Moves = moves.Select(m => new MoveDto(m)).ToList();
        RelearnMoves = relearnMoves.Select(rm => new RelearnMoveDto(rm)).ToList();
    }
}

public record StatsDto
{
    public int IvHp { get; init; }
    public int IvAtk { get; init; }
    public int IvDef { get; init; }
    public int IvSpa { get; init; }
    public int IvSpd { get; init; }
    public int IvSpe { get; init; }
    public int EvHp { get; init; }
    public int EvAtk { get; init; }
    public int EvDef { get; init; }
    public int EvSpa { get; init; }
    public int EvSpd { get; init; }
    public int EvSpe { get; init; }
    public bool HyperTrainedHp { get; init; }
    public bool HyperTrainedAtk { get; init; }
    public bool HyperTrainedDef { get; init; }
    public bool HyperTrainedSpa { get; init; }
    public bool HyperTrainedSpd { get; init; }
    public bool HyperTrainedSpe { get; init; }

    public int StatHp { get; init; }
    public int StatAtk { get; init; }
    public int StatDef { get; init; }
    public int StatSpa { get; init; }
    public int StatSpd { get; init; }
    public int StatSpe { get; init; }
    public int StatHpCurrent { get; init; }

    public StatsDto() { }

    public StatsDto(StatsEntity s)
    {
        IvHp = s.IvHp; IvAtk = s.IvAtk; IvDef = s.IvDef; IvSpa = s.IvSpa; IvSpd = s.IvSpd; IvSpe = s.IvSpe;
        EvHp = s.EvHp; EvAtk = s.EvAtk; EvDef = s.EvDef; EvSpa = s.EvSpa; EvSpd = s.EvSpd; EvSpe = s.EvSpe;
        HyperTrainedHp = s.HyperTrainedHp; HyperTrainedAtk = s.HyperTrainedAtk; HyperTrainedDef = s.HyperTrainedDef;
        HyperTrainedSpa = s.HyperTrainedSpa; HyperTrainedSpd = s.HyperTrainedSpd; HyperTrainedSpe = s.HyperTrainedSpe;
        StatHp = s.StatHp; StatAtk = s.StatAtk; StatDef = s.StatDef; StatSpa = s.StatSpa; StatSpd = s.StatSpd; StatSpe = s.StatSpe;
        StatHpCurrent = s.StatHpCurrent;
    }
}

public record MoveDto
{
    public int Slot { get; init; }
    public int MoveId { get; init; }
    public string MoveName { get; init; } = "";
    public int PpUps { get; init; }
    public int CurrentPp { get; init; }

    public MoveDto() { }
    public MoveDto(MoveEntity m) { Slot = m.Slot; MoveId = m.MoveId; MoveName = PkHexStringService.GetMoveName(m.MoveId); PpUps = m.PpUps; CurrentPp = m.CurrentPp; }
}

public record RelearnMoveDto
{
    public int Slot { get; init; }
    public int MoveId { get; init; }
    public string MoveName { get; init; } = "";

    public RelearnMoveDto() { }
    public RelearnMoveDto(RelearnMoveEntity rm) { Slot = rm.Slot; MoveId = rm.MoveId; MoveName = PkHexStringService.GetMoveName(rm.MoveId); }
}

public record UpdatePokemonDto
{
    public bool? Favorite { get; init; }
    public string? Notes { get; init; }
}
