using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using PKHeX.Core;
using BeastVault.Api.Domain.Entities;


namespace BeastVault.Api.Infrastructure.Services
{
    public class PkhexCoreParser
    {
        public class ParseResult
        {
            public required FileEntity File { get; init; }
            public required PokemonEntity Pokemon { get; init; }
            public StatsEntity? Stats { get; init; }
            public List<MoveEntity> Moves { get; init; } = new();
            public List<RelearnMoveEntity> RelearnMoves { get; init; } = new();
        }

        private static int GetDisplayTID(PKM pk)
        {
            if (pk.Format >= 7)
                return (int)(pk.ID32 % 1_000_000);
            return pk.TID16;
        }

        private static int GetDisplaySID(PKM pk)
        {
            if (pk.Format >= 7)
                return (int)(pk.ID32 / 1_000_000);
            return pk.SID16;
        }

        public async Task<ParseResult?> ParseAsync(byte[] bytes, string fileName, FileStorageService? storageService = null)
        {
            return await Task.Run(() =>
            {
                PKM? pk;
                try
                {
                    pk = EntityFormat.GetFromBytes(bytes);
                }
                catch
                {
                    return null;
                }
                if (pk == null)
                    return null;

                var sha = FileStorageService.ComputeSha256(bytes);
                var ext = Path.GetExtension(fileName)?.TrimStart('.')?.ToLowerInvariant() ?? "pk";
                var file = new FileEntity
                {
                    Sha256 = sha,
                    FileName = fileName,
                    OriginalFileName = fileName, // NUEVO: Guardar nombre original para backup
                    Format = ext,
                    Size = bytes.LongLength,
                    StoredPath = string.Empty // Se asigna tras guardar
                };

                // Helper function to safely get property values
                T GetProp<T>(string propName, T defaultValue = default(T)!)
                {
                    var pi = pk.GetType().GetProperty(propName);
                    if (pi != null && pi.PropertyType == typeof(T))
                    {
                        var val = pi.GetValue(pk);
                        if (val is T t) return t;
                    }
                    return defaultValue;
                }

                // Helper to try multiple property names for fallback
                int GetIntProp(params string[] propNames)
                {
                    foreach (var propName in propNames)
                    {
                        var result = GetProp<int>(propName, -1);
                        if (result != -1) return result;
                    }
                    return 0;
                }

                // Extract OT name from various possible properties
                string otName = string.Empty;
                var otProps = new[] { "OriginalTrainerName", "OT_Name", "OT", "TrainerName" };
                foreach (var prop in otProps)
                {
                    var pi = pk.GetType().GetProperty(prop);
                    if (pi != null)
                    {
                        var val = pi.GetValue(pk);
                        if (val is string otStr && !string.IsNullOrWhiteSpace(otStr))
                        {
                            otName = otStr;
                            break;
                        }
                    }
                }

                // Extract enhanced data
                var teraType = pk is ITeraType t ? (int?)t.TeraType : null;
                var form = pk.Form;
                var formArg = GetProp<uint>("FormArgument");
                var metLoc = GetProp<int>("MetLocation", GetProp<int>("Met_Location"));

                var p = new PokemonEntity
                {
                    // Basic identification
                    SpeciesId = pk.Species,
                    Nickname = string.IsNullOrWhiteSpace(pk.Nickname) ? null : pk.Nickname,
                    OtName = string.IsNullOrWhiteSpace(otName) ? string.Empty : otName,
                    Tid = GetDisplayTID(pk),
                    Sid = GetDisplaySID(pk),
                    Level = pk.CurrentLevel,
                    IsShiny = pk.IsShiny,
                    Nature = (int)pk.Nature,
                    AbilityId = pk.Ability,
                    BallId = pk.Ball,
                    TeraType = teraType,
                    HeldItemId = pk.HeldItem,
                    OriginGame = (int)pk.Version,
                    Language = GetLanguageCode(pk.Language),
                    MetDate = BuildMetDate(pk.MetYear, pk.MetMonth, pk.MetDay),
                    MetLocation = metLoc > 0 ? $"LOC#{metLoc}" : null,
                    SpriteKey = $"{pk.Species}_{(pk.IsShiny ? "s" : "n")}_{form}",
                    Favorite = GetProp<bool>("IsFavorite"),
                    Notes = null,
                    Gender = GetGender(pk),
                    OTGender = GetOTGender(pk),
                    OTLanguage = GetOTLanguage(pk),

                    // Enhanced fields from PK9 structure
                    EncryptionConstant = GetProp<uint>("EncryptionConstant"),
                    PersonalityId = GetProp<uint>("PID"),
                    Experience = GetProp<uint>("EXP"),
                    CurrentFriendship = GetProp<int>("CurrentFriendship"),
                    Form = form,
                    FormArgument = formArg,
                    DynamaxLevel = GetProp<int>("DynamaxLevel"),
                    CanGigantamax = GetProp<bool>("CanGigantamax"),
                    IsEgg = GetProp<bool>("IsEgg"),
                    FatefulEncounter = GetProp<bool>("FatefulEncounter"),
                    EggLocation = GetProp<int>("EggLocation"),
                    EggMetDate = BuildMetDate(GetProp<int>("EggYear"), GetProp<int>("EggMonth"), GetProp<int>("EggDay")),

                    // Physical properties
                    HeightScalar = GetProp<int>("HeightScalar"),
                    WeightScalar = GetProp<int>("WeightScalar"),
                    Scale = GetProp<int>("Scale"),

                    // Pokerus
                    PokerusState = GetProp<int>("PokerusState"),
                    PokerusDays = GetProp<int>("PokerusDays"),
                    PokerusStrain = GetProp<int>("PokerusStrain"),

                    // Contest stats
                    ContestCool = GetProp<int>("ContestCool"),
                    ContestBeauty = GetProp<int>("ContestBeauty"),
                    ContestCute = GetProp<int>("ContestCute"),
                    ContestSmart = GetProp<int>("ContestSmart"),
                    ContestTough = GetProp<int>("ContestTough"),
                    ContestSheen = GetProp<int>("ContestSheen"),

                    // Handler info
                    CurrentHandler = GetProp<int>("CurrentHandler"),
                    HandlingTrainerName = GetProp<string>("HandlingTrainerName") ?? "",
                    HandlingTrainerGender = GetProp<int>("HandlingTrainerGender"),
                    HandlingTrainerLanguage = GetProp<int>("HandlingTrainerLanguage"),
                    HandlingTrainerFriendship = GetProp<int>("HandlingTrainerFriendship"),

                    // Memory system
                    OriginalTrainerMemory = GetIntProp("OriginalTrainerMemory", "OT_Memory"),
                    OriginalTrainerMemoryIntensity = GetIntProp("OriginalTrainerMemoryIntensity", "OT_Intensity"),
                    OriginalTrainerMemoryFeeling = GetIntProp("OriginalTrainerMemoryFeeling", "OT_Feeling"),
                    OriginalTrainerMemoryVariable = GetIntProp("OriginalTrainerMemoryVariable", "OT_TextVar"),
                    HandlingTrainerMemory = GetIntProp("HandlingTrainerMemory", "HT_Memory"),
                    HandlingTrainerMemoryIntensity = GetIntProp("HandlingTrainerMemoryIntensity", "HT_Intensity"),
                    HandlingTrainerMemoryFeeling = GetIntProp("HandlingTrainerMemoryFeeling", "HT_Feeling"),
                    HandlingTrainerMemoryVariable = GetIntProp("HandlingTrainerMemoryVariable", "HT_TextVar")
                };

                // Handle form changes for Pokémon with specific held items
                // Zacian with Rusted Sword (ID 1103) → Form 1 (Crowned Sword)
                // Zamazenta with Rusted Shield (ID 1104) → Form 1 (Crowned Shield)
                if (p.SpeciesId == 888 && p.HeldItemId == 1103) // Zacian with Rusted Sword
                {
                    p.Form = 1; // Crowned Sword form
                    p.SpriteKey = $"{p.SpeciesId}_{(p.IsShiny ? "s" : "n")}_1";
                }
                else if (p.SpeciesId == 889 && p.HeldItemId == 1104) // Zamazenta with Rusted Shield
                {
                    p.Form = 1; // Crowned Shield form
                    p.SpriteKey = $"{p.SpeciesId}_{(p.IsShiny ? "s" : "n")}_1";
                }

                // Enhanced stats with current calculated values
                bool GetHT(string prop)
                {
                    var pi = pk.GetType().GetProperty(prop);
                    if (pi != null && pi.PropertyType == typeof(bool))
                        return (bool)(pi.GetValue(pk) ?? false);
                    return false;
                }

                var s = new StatsEntity
                {
                    IvHp = pk.IV_HP,
                    IvAtk = pk.IV_ATK,
                    IvDef = pk.IV_DEF,
                    IvSpa = pk.IV_SPA,
                    IvSpd = pk.IV_SPD,
                    IvSpe = pk.IV_SPE,
                    EvHp = pk.EV_HP,
                    EvAtk = pk.EV_ATK,
                    EvDef = pk.EV_DEF,
                    EvSpa = pk.EV_SPA,
                    EvSpd = pk.EV_SPD,
                    EvSpe = pk.EV_SPE,
                    HyperTrainedHp = GetHT("HT_HP"),
                    HyperTrainedAtk = GetHT("HT_ATK"),
                    HyperTrainedDef = GetHT("HT_DEF"),
                    HyperTrainedSpa = GetHT("HT_SPA"),
                    HyperTrainedSpd = GetHT("HT_SPD"),
                    HyperTrainedSpe = GetHT("HT_SPE"),

                    // Current calculated stats
                    StatHp = GetProp<int>("Stat_HPMax"),
                    StatAtk = GetProp<int>("Stat_ATK"),
                    StatDef = GetProp<int>("Stat_DEF"),
                    StatSpa = GetProp<int>("Stat_SPA"),
                    StatSpd = GetProp<int>("Stat_SPD"),
                    StatSpe = GetProp<int>("Stat_SPE"),
                    StatHpCurrent = GetProp<int>("Stat_HPCurrent")
                };

                // Enhanced moves with current PP
                var moves = new List<MoveEntity>();
                for (int slot = 1; slot <= 4; slot++)
                {
                    int moveId = slot switch
                    {
                        1 => pk.Move1,
                        2 => pk.Move2,
                        3 => pk.Move3,
                        4 => pk.Move4,
                        _ => 0
                    };
                    if (moveId <= 0) continue;

                    int ppUps = GetProp<int>($"Move{slot}_PPUps");
                    int currentPp = GetProp<int>($"Move{slot}_PP");

                    moves.Add(new MoveEntity
                    {
                        Slot = slot,
                        MoveId = moveId,
                        PpUps = ppUps,
                        CurrentPp = currentPp
                    });
                }

                // Relearn moves
                var relearnMoves = new List<RelearnMoveEntity>();
                for (int slot = 1; slot <= 4; slot++)
                {
                    int relearnMoveId = GetProp<int>($"RelearnMove{slot}");
                    if (relearnMoveId <= 0) continue;

                    relearnMoves.Add(new RelearnMoveEntity
                    {
                        Slot = slot,
                        MoveId = relearnMoveId
                    });
                }

                // Save to file storage if service is provided
                if (storageService != null)
                {
                    var pokemonName = PkHexStringService.GetSpeciesName(pk.Species) ?? "Pokemon";
                    // NUEVO: Pasar el nombre del archivo original para el backup
                    var storedPath = storageService.Save(sha, ext, bytes, pokemonName, DateTime.UtcNow, fileName);
                    file.StoredPath = storedPath;
                    Console.WriteLine($"Saved file: {storedPath}, Size: {bytes.Length} bytes, SHA256: {sha}");

                    // Verify save
                    if (File.Exists(storedPath))
                    {
                        var savedBytes = File.ReadAllBytes(storedPath);
                        var savedSha256 = FileStorageService.ComputeSha256(savedBytes);
                        Console.WriteLine($"Are identical? {savedSha256 == sha}");
                    }
                }

                return new ParseResult
                {
                    File = file,
                    Pokemon = p,
                    Stats = s,
                    Moves = moves,
                    RelearnMoves = relearnMoves
                };
            });
        }
        // (removed duplicate misplaced code)

        // Gender: 0 = macho, 1 = hembra
        private static int GetGender(PKM pk)
        {
            // Try property "Gender" (0 = macho, 1 = hembra)
            var pi = pk.GetType().GetProperty("Gender");
            if (pi != null && pi.PropertyType == typeof(int))
            {
                var val = pi.GetValue(pk);
                if (val is int i) return i;
            }
            // Try property "IsFemale" (bool)
            pi = pk.GetType().GetProperty("IsFemale");
            if (pi != null && pi.PropertyType == typeof(bool))
            {
                var val = pi.GetValue(pk);
                if (val is bool b) return b ? 1 : 0;
            }
            return 0; // default macho
        }

        // OTGender: 0 = macho, 1 = hembra
        private static int GetOTGender(PKM pk)
        {
            var pi = pk.GetType().GetProperty("OT_Gender");
            if (pi != null && pi.PropertyType == typeof(int))
            {
                var val = pi.GetValue(pk);
                if (val is int i) return i;
            }
            // Try property "TrainerGender" (int)
            pi = pk.GetType().GetProperty("TrainerGender");
            if (pi != null && pi.PropertyType == typeof(int))
            {
                var val = pi.GetValue(pk);
                if (val is int i) return i;
            }
            return 0; // default macho
        }

        // OTLanguage: try OT_Language, TrainerLanguage, fallback to Language
        private static string GetOTLanguage(PKM pk)
        {
            var pi = pk.GetType().GetProperty("OT_Language");
            if (pi != null)
            {
                var val = pi.GetValue(pk);
                if (val is int i) return GetLanguageCode(i);
                if (val is string s && !string.IsNullOrWhiteSpace(s)) return s;
            }
            pi = pk.GetType().GetProperty("TrainerLanguage");
            if (pi != null)
            {
                var val = pi.GetValue(pk);
                if (val is int i) return GetLanguageCode(i);
                if (val is string s && !string.IsNullOrWhiteSpace(s)) return s;
            }
            // fallback to Language
            if (pk.Language is int lang) return PkHexStringService.GetLanguageCode(lang);
            return "";
        }

        private static string GetLanguageCode(int lang)
        {
            return PkHexStringService.GetLanguageCode(lang);
        }

        private static DateTime? BuildMetDate(int year, int month, int day)
        {
            if (year <= 0 || month <= 0 || day <= 0) return null;
            try { return new DateTime(year, month, day); }
            catch { return null; }
        }
    }
}
