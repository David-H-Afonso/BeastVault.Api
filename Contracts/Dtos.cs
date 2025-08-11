using PokemonBank.Api.Domain.Entities;
using System.ComponentModel.DataAnnotations;

namespace PokemonBank.Api.Contracts
{
    public record ImportResultDto
    {
        public required string FileName { get; init; }
        public required string Status { get; init; } // imported | duplicate | error
        public int? PokemonId { get; init; }
        public string? Message { get; init; }
    }

    public record PokemonQuery
    {
        public string? Search { get; init; }
        public int? SpeciesId { get; init; }
        public bool? IsShiny { get; init; }
        public int? BallId { get; init; }
        public int? OriginGame { get; init; }
        public int? TeraType { get; init; }
        public int Skip { get; init; } = 0;
        public int Take { get; init; } = 50;
    }

    public record PagedResult<T>(IReadOnlyList<T> Items, int Total);

    public record PokemonListItemDto
    {
        public int Id { get; init; }
        public int SpeciesId { get; init; }
        public string? Nickname { get; init; }
        public int Level { get; init; }
        public bool IsShiny { get; init; }
        public int BallId { get; init; }
        public int? TeraType { get; init; }
        public string SpriteKey { get; init; } = ""; // especie+forma+shiny
    }

    public record PokemonDetailDto
    {
        public int Id { get; init; }
        public int SpeciesId { get; init; }
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
        public int Form { get; init; }
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

    public record UpdatePokemonDto
    {
        public bool? Favorite { get; init; }
        public string? Notes { get; init; }
    }
}