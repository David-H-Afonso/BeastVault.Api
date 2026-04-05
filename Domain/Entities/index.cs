namespace BeastVault.Api.Domain.Entities
{
    public enum UserRole { Standard = 0, Admin = 1 }

    public class UserEntity
    {
        public int Id { get; set; }
        public required string Username { get; set; }
        public string? PasswordHash { get; set; }
        public UserRole Role { get; set; } = UserRole.Standard;
        public bool IsDefault { get; set; }

        public ICollection<FileEntity> Files { get; set; } = new List<FileEntity>();
        public ICollection<PokemonEntity> Pokemon { get; set; } = new List<PokemonEntity>();
        public ICollection<TagEntity> Tags { get; set; } = new List<TagEntity>();
    }

    public class FileEntity
    {
        public int Id { get; set; }
        public int UserId { get; set; }
        public required string Sha256 { get; set; }
        public required string FileName { get; set; }
        public string? OriginalFileName { get; set; }
        public required string Format { get; set; }
        public long Size { get; set; }
        public required string StoredPath { get; set; }
        public DateTime ImportedAt { get; set; } = DateTime.UtcNow;
        public byte[]? RawBlob { get; set; }

        public UserEntity User { get; set; } = null!;
        public ICollection<FileTagEntity> FileTags { get; set; } = new List<FileTagEntity>();
    }

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
        public int? TeraType { get; set; } // nullable si no aplica
        public int HeldItemId { get; set; } // Nuevo: ID del objeto equipado
        public int OriginGame { get; set; }
        public string Language { get; set; } = ""; // por simplicidad
        public DateTime? MetDate { get; set; }
        public string? MetLocation { get; set; }
        public string SpriteKey { get; set; } = string.Empty; // especie+forma+shiny
        public bool Favorite { get; set; }
        public string? Notes { get; set; }

        // Gender: 0 = Male, 1 = Female, 2 = Genderless
        public int Gender { get; set; } = 0;
        // OT Gender: 0 = Male, 1 = Female, 2 = Genderless
        public int OTGender { get; set; } = 0;
        public string OTLanguage { get; set; } = "";

        // Enhanced fields from PK9 structure
        public uint EncryptionConstant { get; set; }
        public uint PersonalityId { get; set; } // PID
        public uint Experience { get; set; }
        public int CurrentFriendship { get; set; }
        public int Form { get; set; } = 0; // Alternative form
        public uint FormArgument { get; set; } = 0; // Form-specific data
        public int DynamaxLevel { get; set; } = 0; // Dynamax Level (0-10)
        public bool CanGigantamax { get; set; } = false; // Gigantamax Flag
        public bool IsEgg { get; set; } = false;
        public bool FatefulEncounter { get; set; } = false;
        public int EggLocation { get; set; } = 0;
        public DateTime? EggMetDate { get; set; }

        // Physical properties
        public int HeightScalar { get; set; } = 0;
        public int WeightScalar { get; set; } = 0;
        public int Scale { get; set; } = 0;

        // Pokerus
        public int PokerusState { get; set; } = 0;
        public int PokerusDays { get; set; } = 0;
        public int PokerusStrain { get; set; } = 0;

        // Contest stats
        public int ContestCool { get; set; } = 0;
        public int ContestBeauty { get; set; } = 0;
        public int ContestCute { get; set; } = 0;
        public int ContestSmart { get; set; } = 0;
        public int ContestTough { get; set; } = 0;
        public int ContestSheen { get; set; } = 0;

        // Handler info (current vs original trainer)
        public int CurrentHandler { get; set; } = 0; // 0 = OT, 1 = Not OT
        public string HandlingTrainerName { get; set; } = "";
        public int HandlingTrainerGender { get; set; } = 0;
        public int HandlingTrainerLanguage { get; set; } = 0;
        public int HandlingTrainerFriendship { get; set; } = 0;

        // Memory system
        public int OriginalTrainerMemory { get; set; } = 0;
        public int OriginalTrainerMemoryIntensity { get; set; } = 0;
        public int OriginalTrainerMemoryFeeling { get; set; } = 0;
        public int OriginalTrainerMemoryVariable { get; set; } = 0;
        public int HandlingTrainerMemory { get; set; } = 0;
        public int HandlingTrainerMemoryIntensity { get; set; } = 0;
        public int HandlingTrainerMemoryFeeling { get; set; } = 0;
        public int HandlingTrainerMemoryVariable { get; set; } = 0;

        public ICollection<PokemonTagEntity> PokemonTags { get; set; } = new List<PokemonTagEntity>();
        public UserEntity User { get; set; } = null!;
        public FileEntity File { get; set; } = null!;
    }

    public class StatsEntity
    {
        public int PokemonId { get; set; }
        public int IvHp { get; set; }
        public int IvAtk { get; set; }
        public int IvDef { get; set; }
        public int IvSpa { get; set; }
        public int IvSpd { get; set; }
        public int IvSpe { get; set; }
        public int EvHp { get; set; }
        public int EvAtk { get; set; }
        public int EvDef { get; set; }
        public int EvSpa { get; set; }
        public int EvSpd { get; set; }
        public int EvSpe { get; set; }
        public bool HyperTrainedHp { get; set; }
        public bool HyperTrainedAtk { get; set; }
        public bool HyperTrainedDef { get; set; }
        public bool HyperTrainedSpa { get; set; }
        public bool HyperTrainedSpd { get; set; }
        public bool HyperTrainedSpe { get; set; }

        // Current calculated stats
        public int StatHp { get; set; }
        public int StatAtk { get; set; }
        public int StatDef { get; set; }
        public int StatSpa { get; set; }
        public int StatSpd { get; set; }
        public int StatSpe { get; set; }
        public int StatHpCurrent { get; set; } // Current HP
    }

    public class MoveEntity
    {
        public int PokemonId { get; set; }
        public int Slot { get; set; } // 1..4
        public int MoveId { get; set; }
        public int PpUps { get; set; }
        public int CurrentPp { get; set; } // Current PP remaining
    }

    public class RelearnMoveEntity
    {
        public int PokemonId { get; set; }
        public int Slot { get; set; } // 1..4
        public int MoveId { get; set; }
    }

    public class TagEntity
    {
        public int Id { get; set; }
        public int UserId { get; set; }
        public required string Name { get; set; }
        public string? ImagePath { get; set; }

        public UserEntity User { get; set; } = null!;
        public ICollection<PokemonTagEntity> PokemonTags { get; set; } = new List<PokemonTagEntity>();
        public ICollection<FileTagEntity> FileTags { get; set; } = new List<FileTagEntity>();
    }

    public class PokemonTagEntity
    {
        public int PokemonId { get; set; }
        public int TagId { get; set; }

        // Navigation properties
        public PokemonEntity Pokemon { get; set; } = null!;
        public TagEntity Tag { get; set; } = null!;
    }

    public class FileTagEntity
    {
        public int FileId { get; set; }
        public int TagId { get; set; }

        // Navigation properties
        public FileEntity File { get; set; } = null!;
        public TagEntity Tag { get; set; } = null!;
    }
}
