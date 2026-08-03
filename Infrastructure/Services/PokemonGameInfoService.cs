using BeastVault.Api.Domain.ValueObjects;
using BeastVault.Api.Infrastructure.Services;
using PKHeX.Core;
using GameInfoDomain = BeastVault.Api.Domain.ValueObjects.GameInfo;

namespace BeastVault.Api.Infrastructure.Services;

/// <summary>
/// Service for providing Pokemon game information and type data
/// </summary>
public static class PokemonGameInfoService
{
    /// <summary>
    /// Get all games by generation (based on PKHeX GameVersion enum)
    /// </summary>
    public static IEnumerable<GameInfoDomain> GetGamesByGeneration(int generation)
    {
        return generation switch
        {
            1 => new[]
            {
                new GameInfoDomain(35, "Red (VC)", 1),        // RD Virtual Console
                new GameInfoDomain(36, "Green/Blue (VC)", 1), // GN Virtual Console  
                new GameInfoDomain(37, "Blue (VC)", 1),       // BU Virtual Console
                new GameInfoDomain(38, "Yellow (VC)", 1)      // YW Virtual Console
            },
            2 => new[]
            {
                new GameInfoDomain(39, "Gold (VC)", 2),       // GD Virtual Console
                new GameInfoDomain(40, "Silver (VC)", 2),     // SI Virtual Console
                new GameInfoDomain(41, "Crystal (VC)", 2)     // C Virtual Console
            },
            3 => new[]
            {
                new GameInfoDomain(1, "Sapphire", 3),         // S
                new GameInfoDomain(2, "Ruby", 3),             // R
                new GameInfoDomain(3, "Emerald", 3),          // E
                new GameInfoDomain(4, "FireRed", 3),          // FR
                new GameInfoDomain(5, "LeafGreen", 3),        // LG
                new GameInfoDomain(15, "Colosseum/XD", 3)     // CXD
            },
            4 => new[]
            {
                new GameInfoDomain(10, "Diamond", 4),         // D
                new GameInfoDomain(11, "Pearl", 4),           // P
                new GameInfoDomain(12, "Platinum", 4),        // Pt
                new GameInfoDomain(7, "HeartGold", 4),        // HG
                new GameInfoDomain(8, "SoulSilver", 4),       // SS
                new GameInfoDomain(16, "Battle Revolution", 4) // BATREV
            },
            5 => new[]
            {
                new GameInfoDomain(20, "White", 5),           // W
                new GameInfoDomain(21, "Black", 5),           // B
                new GameInfoDomain(22, "White 2", 5),         // W2
                new GameInfoDomain(23, "Black 2", 5)          // B2
            },
            6 => new[]
            {
                new GameInfoDomain(24, "X", 6),               // X
                new GameInfoDomain(25, "Y", 6),               // Y
                new GameInfoDomain(26, "Alpha Sapphire", 6),  // AS
                new GameInfoDomain(27, "Omega Ruby", 6)       // OR
            },
            7 => new[]
            {
                new GameInfoDomain(30, "Sun", 7),             // SN
                new GameInfoDomain(31, "Moon", 7),            // MN
                new GameInfoDomain(32, "Ultra Sun", 7),       // US
                new GameInfoDomain(33, "Ultra Moon", 7),      // UM
                new GameInfoDomain(34, "GO", 7),              // GO
                new GameInfoDomain(42, "Let's Go Pikachu", 7), // GP
                new GameInfoDomain(43, "Let's Go Eevee", 7)   // GE
            },
            8 => new[]
            {
                new GameInfoDomain(44, "Sword", 8),           // SW
                new GameInfoDomain(45, "Shield", 8),          // SH
                new GameInfoDomain(47, "Legends Arceus", 8),  // PLA
                new GameInfoDomain(48, "Brilliant Diamond", 8), // BD
                new GameInfoDomain(49, "Shining Pearl", 8)    // SP
            },
            9 => new[]
            {
                new GameInfoDomain(50, "Scarlet", 9),         // SL
                new GameInfoDomain(51, "Violet", 9)           // VL
            },
            _ => Array.Empty<GameInfoDomain>()
        };
    }

    /// <summary>
    /// Get the generation where a specific species was introduced
    /// </summary>
    public static int GetSpeciesOriginGeneration(int speciesId)
    {
        return speciesId switch
        {
            >= 1 and <= 151 => 1,      // Gen 1: Bulbasaur to Mew
            >= 152 and <= 251 => 2,    // Gen 2: Chikorita to Celebi
            >= 252 and <= 386 => 3,    // Gen 3: Treecko to Deoxys
            >= 387 and <= 493 => 4,    // Gen 4: Turtwig to Arceus
            >= 494 and <= 649 => 5,    // Gen 5: Victini to Genesect
            >= 650 and <= 721 => 6,    // Gen 6: Chespin to Volcanion
            >= 722 and <= 809 => 7,    // Gen 7: Rowlet to Melmetal
            >= 810 and <= 905 => 8,    // Gen 8: Grookey to Enamorus
            >= 906 and <= 1017 => 9,   // Gen 9: Sprigatito to current
            _ => 1 // Default fallback
        };
    }

    /// <summary>
    /// Get the generation where a Pokemon was captured, considering both OriginGame and file format
    /// </summary>
    public static int GetCapturedGeneration(int originGame, string fileFormat)
    {
        // For legacy formats, derive generation from file format instead of OriginGame
        // because PKHeX assigns default/incorrect OriginGame values for old formats
        var formatGeneration = GetGenerationFromFileFormat(fileFormat);

        // If file format indicates a legacy generation, trust that over OriginGame
        if (formatGeneration <= 3)
        {
            return formatGeneration;
        }

        // If format generation is unknown (returned 0), use OriginGame
        if (formatGeneration == 0)
        {
            var gameGen = GetGameGeneration(originGame);
            // If game generation is also 0, default to 9 for modern unknown formats
            return gameGen > 0 ? gameGen : 9;
        }

        // For modern formats (Gen 4+), prefer the format generation
        // but validate with OriginGame if it's reasonable
        var originGameGen = GetGameGeneration(originGame);
        if (originGameGen > 0 && Math.Abs(originGameGen - formatGeneration) <= 1)
        {
            return originGameGen; // Use OriginGame if it's close to format generation
        }

        return formatGeneration;
    }

    /// <summary>
    /// Get generation based on file format
    /// </summary>
    private static int GetGenerationFromFileFormat(string format)
    {
        // Normalize format: remove leading dot and convert to lowercase
        var normalizedFormat = format?.TrimStart('.').ToLower() ?? string.Empty;

        return normalizedFormat switch
        {
            "pk1" => 1,
            "pk2" => 2,
            "pk3" => 3,
            "pk4" => 4,
            "pk5" => 5,
            "pk6" => 6,
            "pk7" or "pb7" => 7,
            "pk8" or "pb8" or "pa8" => 8, // pa8 is Legends Arceus (Gen 8)
            "pk9" or "pb9" or "pa9" => 9, // pk9 is standard Gen 9, pb9 is Legends Z-A box, pa9 is Legends Z-A
            _ => 0 // Unknown format - will be resolved by OriginGame
        };
    }

    /// <summary>
    /// Get the generation of a game based on its ID (PKM file OriginGame field)
    /// Based on actual values stored in PKM files, not PKHeX enum values
    /// </summary>
    public static int GetGameGeneration(int gameId)
    {
        return gameId switch
        {
            // Generation 1 (Red/Blue/Yellow)
            1 or 2 or 3 => 1,

            // Generation 2 (Gold/Silver/Crystal) 
            4 or 5 or 7 => 2,

            // Generation 3 (Ruby/Sapphire/Emerald/FireRed/LeafGreen)
            8 or 9 or 10 or 11 or 12 or 15 => 3,

            // Generation 4 (Diamond/Pearl/Platinum/HeartGold/SoulSilver)
            13 or 14 or 15 or 16 or 17 => 4,

            // Generation 5 (Black/White/Black2/White2)
            18 or 19 or 20 or 21 => 5,

            // Generation 6 (X/Y/OmegaRuby/AlphaSapphire)
            24 or 25 or 26 or 27 => 6,

            // Generation 7 (Sun/Moon/UltraSun/UltraMoon/Let's Go)
            30 or 31 or 32 or 33 or 42 or 43 => 7,

            // Generation 8 (Sword/Shield/BDSP/Legends Arceus)
            44 or 45 or 48 or 49 => 8,

            // Generation 9 (Scarlet/Violet)
            50 or 51 or 52 or 53 => 9,  // 50=Scarlet, 51=Violet, 52-53 might be DLC

            // Virtual Console games (map to their original generation)
            35 or 36 or 37 or 38 => 1,  // VC Gen1
            39 or 40 or 41 => 2,        // VC Gen2

            // PKHeX grouped save versions
            53 or 54 => 1,               // Red/Blue/Yellow groups
            55 or 56 => 2,               // Gold/Silver/Crystal groups
            57 or 58 or 59 or 60 or 61 or 62 => 3,
            63 or 64 or 65 => 4,
            66 or 67 => 5,
            68 or 69 or 70 => 6,
            71 or 72 or 73 => 7,
            74 or 75 => 8,
            76 => 9,

            // Pokemon GO
            34 => 7,

            // Fallback for unknown values
            _ when gameId >= 54 => 9,   // Future Gen 9 DLC
            _ => 1                      // Very old/unknown values default to Gen 1
        };
    }

    /// <summary>
    /// Get species that were introduced in a specific generation
    /// </summary>
    public static IEnumerable<int> GetSpeciesInGeneration(int generation)
    {
        return generation switch
        {
            1 => Enumerable.Range(1, 151),     // Bulbasaur to Mew
            2 => Enumerable.Range(152, 100),   // Chikorita to Celebi
            3 => Enumerable.Range(252, 135),   // Treecko to Deoxys
            4 => Enumerable.Range(387, 107),   // Turtwig to Arceus
            5 => Enumerable.Range(494, 156),   // Victini to Genesect
            6 => Enumerable.Range(650, 72),    // Chespin to Volcanion
            7 => Enumerable.Range(722, 81),    // Rowlet to Marshadow
            8 => Enumerable.Range(810, 89),    // Grookey to Eternatus
            9 => Enumerable.Range(906, 112),   // Sprigatito to current
            _ => Array.Empty<int>()
        };
    }

    /// <summary>
    /// Get species IDs by name (case-insensitive search)
    /// </summary>
    public static IEnumerable<int> GetSpeciesIdsByName(string name)
    {
        var results = new List<int>();

        // Search through all species names
        for (int i = 1; i < PKHeX.Core.GameInfo.Strings.Species.Count; i++)
        {
            var speciesName = PkHexStringService.GetSpeciesName(i);
            if (speciesName.Contains(name, StringComparison.OrdinalIgnoreCase))
            {
                results.Add(i);
            }
        }

        return results;
    }

    /// <summary>
    /// Get species with specific types based on filter options
    /// </summary>
    public static IEnumerable<int> GetSpeciesWithTypes(TypeFilterOptions options)
    {
        var results = new List<int>();

        // Get personal table for type information - use SV (Gen9) for most up-to-date data
        var pt = PersonalTable.SV; // Using Scarlet/Violet (Gen9) for latest Pokemon data

        for (int i = 1; i < pt.MaxSpeciesID; i++)
        {
            var personal = pt[i];
            if (personal == null) continue;

            var primaryType = personal.Type1;
            var secondaryType = personal.Type2;

            if (MatchesTypeFilter(primaryType, secondaryType, options))
            {
                results.Add(i);
            }
        }

        return results;
    }

    /// <summary>
    /// Get generation number from game ID
    /// </summary>
    public static int GetGenerationFromGame(int gameId)
    {
        return gameId switch
        {
            >= 1 and <= 3 => 1,       // RBY
            >= 4 and <= 7 => 2,       // GSC
            >= 8 and <= 12 => 3,      // RSE, FRLG
            >= 13 and <= 17 => 4,     // DPPt, HGSS
            >= 18 and <= 21 => 5,     // BW, B2W2
            >= 24 and <= 27 => 6,     // XY, ORAS
            >= 30 and <= 33 => 7,     // SM, USUM
            >= 44 and <= 50 => 8,     // SwSh, BDSP, LA
            >= 51 and <= 52 => 9,     // SV
            _ => 0
        };
    }

    /// <summary>
    /// Get type name by ID
    /// </summary>
    public static string GetTypeName(int typeId)
    {
        return PkHexStringService.GetTypeName(typeId);
    }

    /// <summary>
    /// Get all available types
    /// </summary>
    public static IEnumerable<(int Id, string Name)> GetAllTypes()
    {
        var types = new List<(int, string)>();

        for (int i = 0; i < PKHeX.Core.GameInfo.Strings.Types.Count; i++)
        {
            var typeName = PkHexStringService.GetTypeName(i);
            if (!string.IsNullOrEmpty(typeName) && typeName != "???" && !typeName.StartsWith("Type#"))
            {
                types.Add((i, typeName));
            }
        }

        return types;
    }

    /// <summary>
    /// Check if types match the filter criteria
    /// </summary>
    private static bool MatchesTypeFilter(int primaryType, int secondaryType, TypeFilterOptions options)
    {
        var hasSecondaryType = secondaryType != primaryType;

        return options.Mode switch
        {
            TypeFilterMode.HasAnyType =>
                (options.PrimaryType == null || primaryType == options.PrimaryType ||
                 (hasSecondaryType && secondaryType == options.PrimaryType)) &&
                (options.SecondaryType == null || primaryType == options.SecondaryType ||
                 (hasSecondaryType && secondaryType == options.SecondaryType)),

            TypeFilterMode.HasAllTypes =>
                (options.PrimaryType == null || primaryType == options.PrimaryType ||
                 (hasSecondaryType && secondaryType == options.PrimaryType)) &&
                (options.SecondaryType == null || primaryType == options.SecondaryType ||
                 (hasSecondaryType && secondaryType == options.SecondaryType)),

            TypeFilterMode.HasOnlyTypes =>
                (!hasSecondaryType && options.PrimaryType == primaryType && options.SecondaryType == null) ||
                (hasSecondaryType &&
                 ((primaryType == options.PrimaryType && secondaryType == options.SecondaryType) ||
                  (!options.EnforceTypeOrder && primaryType == options.SecondaryType && secondaryType == options.PrimaryType))),

            TypeFilterMode.PrimaryTypeOnly =>
                !hasSecondaryType && options.PrimaryType == primaryType,

            TypeFilterMode.ExactTypeOrder =>
                hasSecondaryType && primaryType == options.PrimaryType && secondaryType == options.SecondaryType,

            TypeFilterMode.BothTypesAnyOrder =>
                hasSecondaryType &&
                ((primaryType == options.PrimaryType && secondaryType == options.SecondaryType) ||
                 (primaryType == options.SecondaryType && secondaryType == options.PrimaryType)),

            _ => false
        };
    }
}
