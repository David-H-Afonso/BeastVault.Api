namespace BeastVault.Api.Domain.Entities;

public enum SavePokemonLocation
{
    Party = 0,
    Box = 1
}

public class SaveFileEntity
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public required string Sha256 { get; set; }
    public required string FileName { get; set; }
    public required string OriginalFileName { get; set; }
    public required string Format { get; set; }
    public long Size { get; set; }
    public required string StoredPath { get; set; }
    public byte[] RawBlob { get; set; } = [];
    public int Generation { get; set; }
    public int OriginGame { get; set; }
    public required string GameName { get; set; }
    public required string SaveType { get; set; }
    public bool ChecksumsValid { get; set; }
    public string? Title { get; set; }
    public string? Notes { get; set; }
    public DateTime ImportedAt { get; set; } = DateTime.UtcNow;

    public User User { get; set; } = null!;
    public SaveTrainerEntity Trainer { get; set; } = null!;
    public ICollection<SavePokedexEntryEntity> PokedexEntries { get; set; } = [];
    public ICollection<SavePokemonPreviewEntity> PokemonPreviews { get; set; } = [];
}

public class SaveTrainerEntity
{
    public int SaveFileId { get; set; }
    public string TrainerName { get; set; } = string.Empty;
    public uint TrainerId { get; set; }
    public uint SecretId { get; set; }
    public int Gender { get; set; }
    public string Language { get; set; } = string.Empty;
    public uint Money { get; set; }
    public int PlayTimeHours { get; set; }
    public int PlayTimeMinutes { get; set; }
    public int PlayTimeSeconds { get; set; }
    public int? BadgeCount { get; set; }
    public int DexSeen { get; set; }
    public int DexCaught { get; set; }

    public SaveFileEntity SaveFile { get; set; } = null!;
}

public class SavePokedexEntryEntity
{
    public int SaveFileId { get; set; }
    public int SpeciesId { get; set; }
    public required string SpeciesName { get; set; }
    public bool Seen { get; set; }
    public bool Caught { get; set; }

    public SaveFileEntity SaveFile { get; set; } = null!;
}

public class SavePokemonPreviewEntity
{
    public int Id { get; set; }
    public int SaveFileId { get; set; }
    public SavePokemonLocation Location { get; set; }
    public int? BoxIndex { get; set; }
    public int SlotIndex { get; set; }
    public int SpeciesId { get; set; }
    public required string SpeciesName { get; set; }
    public string? Nickname { get; set; }
    public int Level { get; set; }
    public bool IsShiny { get; set; }
    public bool IsEgg { get; set; }
    public int Form { get; set; }
    public int Gender { get; set; }
    public int Nature { get; set; }
    public required string NatureName { get; set; }
    public required string AbilityName { get; set; }
    public required string HeldItemName { get; set; }
    public required string MovesJson { get; set; }
    public required string PokemonHash { get; set; }
    public required string PokemonStoredHash { get; set; }

    public SaveFileEntity SaveFile { get; set; } = null!;
}
