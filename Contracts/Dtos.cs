using BeastVault.Api.Domain.Entities;
using System.ComponentModel.DataAnnotations;

namespace BeastVault.Api.Contracts
{
    /// <summary>
    /// Result of importing a PKM file
    /// </summary>
    public record ImportResultDto
    {
        /// <summary>
        /// Name of the uploaded file
        /// </summary>
        public required string FileName { get; init; }

        /// <summary>
        /// Import status: "imported", "duplicate", or "error"
        /// </summary>
        public required string Status { get; init; }

        /// <summary>
        /// ID of the created Pokémon (only if Status is "imported")
        /// </summary>
        public int? PokemonId { get; init; }

        /// <summary>
        /// Error message (only if Status is "error")
        /// </summary>
        public string? Message { get; init; }
    }

    /// <summary>
    /// Query parameters for filtering the Pokémon list
    /// </summary>
    public record PokemonQuery
    {
        /// <summary>
        /// Text search in nickname or original trainer name
        /// </summary>
        public string? Search { get; init; }

        /// <summary>
        /// Filter by species ID (e.g., 1 = Bulbasaur)
        /// </summary>
        public int? SpeciesId { get; init; }

        /// <summary>
        /// Filter by form ID (e.g., 0 = Normal, 1 = Alolan, 2 = Galarian)
        /// </summary>
        public int? Form { get; init; }

        /// <summary>
        /// Filter by shiny Pokémon
        /// </summary>
        public bool? IsShiny { get; init; }

        /// <summary>
        /// Filter by Pokéball ID
        /// </summary>
        public int? BallId { get; init; }

        /// <summary>
        /// Filter by origin game
        /// </summary>
        public int? OriginGame { get; init; }

        /// <summary>
        /// Filter by Tera type (Gen 9)
        /// </summary>
        public int? TeraType { get; init; }

        /// <summary>
        /// Number of items to skip (pagination)
        /// </summary>
        public int Skip { get; init; } = 0;

        /// <summary>
        /// Number of items to return (recommended max: 100)
        /// </summary>
        public int Take { get; init; } = 50;

        /// <summary>
        /// Filter by tag IDs (Pokémon must have ALL specified tags)
        /// </summary>
        public int[]? TagIds { get; init; }

        /// <summary>
        /// Filter Pokémon that have no tags
        /// </summary>
        public bool? HasNoTags { get; init; }
    }

    /// <summary>
    /// Paginated result with items and total count
    /// </summary>
    /// <typeparam name="T">Type of items in the list</typeparam>
    public record PagedResult<T>(IReadOnlyList<T> Items, int Total);

    /// <summary>
    /// Basic Pokémon information for lists
    /// </summary>
    public record PokemonListItemDto
    {
        /// <summary>
        /// Unique Pokémon ID in the database
        /// </summary>
        public int Id { get; init; }

        /// <summary>
        /// Species ID (e.g., 1 = Bulbasaur, 25 = Pikachu)
        /// </summary>
        public int SpeciesId { get; init; }

        /// <summary>
        /// Form ID (e.g., 0 = Normal Meowth, 1 = Alolan Meowth, 2 = Galarian Meowth)
        /// </summary>
        public int Form { get; init; }

        /// <summary>
        /// Pokémon nickname (null if using species name)
        /// </summary>
        public string? Nickname { get; init; }

        /// <summary>
        /// Pokémon level (1-100)
        /// </summary>
        public int Level { get; init; }

        /// <summary>
        /// Whether it's shiny
        /// </summary>
        public bool IsShiny { get; init; }

        /// <summary>
        /// ID of the Pokéball it was caught in
        /// </summary>
        public int BallId { get; init; }

        /// <summary>
        /// Tera type (Gen 9), null if not applicable
        /// </summary>
        public int? TeraType { get; init; }

        /// <summary>
        /// ID of the held item (important for form changes like Zacian/Zamazenta)
        /// </summary>
        public int HeldItemId { get; init; }

        /// <summary>
        /// Gender: 0 = Male, 1 = Female, 2 = Genderless
        /// </summary>
        public int Gender { get; init; }

        /// <summary>
        /// Key to identify the sprite (species+form+shiny)
        /// </summary>
        public string SpriteKey { get; init; } = "";

        /// <summary>
        /// Generation where the species was first introduced (calculated field)
        /// </summary>
        public int OriginGeneration { get; init; }

        /// <summary>
        /// Generation where this specific Pokemon was caught/obtained (calculated field)
        /// </summary>
        public int CapturedGeneration { get; init; }

        /// <summary>
        /// Whether this Pokemon can Gigantamax (Gen 8+ files only)
        /// </summary>
        public bool CanGigantamax { get; init; }

        /// <summary>
        /// Whether this Pokemon has a Mega Stone equipped (affects form display)
        /// </summary>
        public bool HasMegaStone { get; init; }

        /// <summary>
        /// List of tags assigned to this Pokemon
        /// </summary>
        public List<TagDto> Tags { get; init; } = new();
    }

    /// <summary>
    /// Complete Pokémon information including all its data
    /// </summary>
    public record PokemonDetailDto
    {
        public int Id { get; init; }
        public int SpeciesId { get; init; }

        /// <summary>
        /// Form ID (e.g., 0 = Normal, 1 = Alolan, 2 = Galarian, etc.)
        /// </summary>
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

        // Enhanced PK9 fields
        public uint EncryptionConstant { get; init; }
        public uint PersonalityId { get; init; }
        public uint Experience { get; init; }
        public int CurrentFriendship { get; init; }
        // Form field moved up for better visibility
        public uint FormArgument { get; init; }
        public bool IsEgg { get; init; }
        public bool FatefulEncounter { get; init; }
        public int Gender { get; init; }
        public int OTGender { get; init; }
        public string OTLanguage { get; init; } = "";
        public int HeldItemId { get; init; }

        // Physical properties
        public int HeightScalar { get; init; }
        public int WeightScalar { get; init; }
        public int Scale { get; init; }

        // Pokerus
        public int PokerusState { get; init; }
        public int PokerusDays { get; init; }
        public int PokerusStrain { get; init; }

        // Contest stats
        public int ContestCool { get; init; }
        public int ContestBeauty { get; init; }
        public int ContestCute { get; init; }
        public int ContestSmart { get; init; }
        public int ContestTough { get; init; }
        public int ContestSheen { get; init; }

        // Relationship data
        public StatsDto? Stats { get; init; }
        public IReadOnlyList<MoveDto> Moves { get; init; } = Array.Empty<MoveDto>();
        public IReadOnlyList<RelearnMoveDto> RelearnMoves { get; init; } = Array.Empty<RelearnMoveDto>();

        public PokemonDetailDto(PokemonEntity p, StatsEntity? s, List<MoveEntity> moves, List<RelearnMoveEntity> relearnMoves)
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

            // Enhanced fields
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

        // Current calculated stats
        public int StatHp { get; init; }
        public int StatAtk { get; init; }
        public int StatDef { get; init; }
        public int StatSpa { get; init; }
        public int StatSpd { get; init; }
        public int StatSpe { get; init; }
        public int StatHpCurrent { get; init; }

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
        public int PpUps { get; init; }
        public int CurrentPp { get; init; }
        public MoveDto(MoveEntity m) { Slot = m.Slot; MoveId = m.MoveId; PpUps = m.PpUps; CurrentPp = m.CurrentPp; }
    }

    public record RelearnMoveDto
    {
        public int Slot { get; init; }
        public int MoveId { get; init; }
        public RelearnMoveDto(RelearnMoveEntity rm) { Slot = rm.Slot; MoveId = rm.MoveId; }
    }

    /// <summary>
    /// DTO for updating editable Pokémon properties
    /// </summary>
    public record UpdatePokemonDto
    {
        /// <summary>
        /// Mark or unmark as favorite (null = no change)
        /// </summary>
        public bool? Favorite { get; init; }

        /// <summary>
        /// Personal notes about the Pokémon (null = no change, string.Empty = clear)
        /// </summary>
        public string? Notes { get; init; }
    }

    /// <summary>
    /// Tag information
    /// </summary>
    public record TagDto
    {
        /// <summary>
        /// Tag ID
        /// </summary>
        public int Id { get; init; }

        /// <summary>
        /// Tag name (case-sensitive)
        /// </summary>
        public required string Name { get; init; }

        /// <summary>
        /// Optional path to tag image (PNG)
        /// </summary>
        public string? ImagePath { get; init; }

        /// <summary>
        /// Number of Pokemon that have this tag
        /// </summary>
        public int PokemonCount { get; init; }
    }

    /// <summary>
    /// Request to create a new tag
    /// </summary>
    public record CreateTagRequest
    {
        /// <summary>
        /// Tag name (case-sensitive, must be unique)
        /// </summary>
        [Required]
        [StringLength(50, MinimumLength = 1)]
        public required string Name { get; init; }
    }

    /// <summary>
    /// Request to update an existing tag
    /// </summary>
    public record UpdateTagRequest
    {
        /// <summary>
        /// New tag name (case-sensitive, must be unique)
        /// </summary>
        [Required]
        [StringLength(50, MinimumLength = 1)]
        public required string Name { get; init; }
    }

    /// <summary>
    /// Request to assign/remove tags to/from a Pokémon
    /// </summary>
    public record PokemonTagsRequest
    {
        /// <summary>
        /// List of tag IDs to assign to the Pokémon
        /// </summary>
        public required int[] TagIds { get; init; }
    }
}
