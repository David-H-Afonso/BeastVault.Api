namespace BeastVault.Api.Domain.Entities;

public class PokemonEntity
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public int FileId { get; set; }
    public required int SpeciesId { get; set; }
    public string? Nickname { get; set; }
    public string OtName { get; set; } = string.Empty;
    public int Tid { get; set; }
    public int Sid { get; set; }
    public int Level { get; set; }
    public bool IsShiny { get; set; }
    public int Nature { get; set; }
    public int AbilityId { get; set; }
    public int BallId { get; set; }
    public int? TeraType { get; set; }
    public int HeldItemId { get; set; }
    public int OriginGame { get; set; }
    public string Language { get; set; } = "";
    public DateTime? MetDate { get; set; }
    public string? MetLocation { get; set; }
    public string SpriteKey { get; set; } = string.Empty;
    public bool Favorite { get; set; }
    public string? Notes { get; set; }

    public int Gender { get; set; } = 0;
    public int OTGender { get; set; } = 0;
    public string OTLanguage { get; set; } = "";

    public uint EncryptionConstant { get; set; }
    public uint PersonalityId { get; set; }
    public uint Experience { get; set; }
    public int CurrentFriendship { get; set; }
    public int Form { get; set; } = 0;
    public uint FormArgument { get; set; } = 0;
    public int DynamaxLevel { get; set; } = 0;
    public bool CanGigantamax { get; set; } = false;
    public bool IsEgg { get; set; } = false;
    public bool FatefulEncounter { get; set; } = false;
    public int EggLocation { get; set; } = 0;
    public DateTime? EggMetDate { get; set; }

    public int HeightScalar { get; set; } = 0;
    public int WeightScalar { get; set; } = 0;
    public int Scale { get; set; } = 0;

    public int PokerusState { get; set; } = 0;
    public int PokerusDays { get; set; } = 0;
    public int PokerusStrain { get; set; } = 0;

    public int ContestCool { get; set; } = 0;
    public int ContestBeauty { get; set; } = 0;
    public int ContestCute { get; set; } = 0;
    public int ContestSmart { get; set; } = 0;
    public int ContestTough { get; set; } = 0;
    public int ContestSheen { get; set; } = 0;

    public int CurrentHandler { get; set; } = 0;
    public string HandlingTrainerName { get; set; } = "";
    public int HandlingTrainerGender { get; set; } = 0;
    public int HandlingTrainerLanguage { get; set; } = 0;
    public int HandlingTrainerFriendship { get; set; } = 0;

    public int OriginalTrainerMemory { get; set; } = 0;
    public int OriginalTrainerMemoryIntensity { get; set; } = 0;
    public int OriginalTrainerMemoryFeeling { get; set; } = 0;
    public int OriginalTrainerMemoryVariable { get; set; } = 0;
    public int HandlingTrainerMemory { get; set; } = 0;
    public int HandlingTrainerMemoryIntensity { get; set; } = 0;
    public int HandlingTrainerMemoryFeeling { get; set; } = 0;
    public int HandlingTrainerMemoryVariable { get; set; } = 0;

    public ICollection<PokemonTagEntity> PokemonTags { get; set; } = new List<PokemonTagEntity>();
    public FileEntity File { get; set; } = null!;
    public User User { get; set; } = null!;
}
