using PKHeX.Core;

namespace BeastVault.Api.Infrastructure.Services
{
    /// <summary>
    /// Helper service to get localized names from PKHeX Core strings
    /// </summary>
    public static class PkHexStringService
    {
        /// <summary>
        /// Get species name by ID using PKHeX's built-in method
        /// </summary>
        public static string GetSpeciesName(int speciesId, int language = 2)
        {
            if (speciesId <= 0) return "Unknown";

            try
            {
                // Use PKHeX's built-in species name resolver
                return GameInfo.Strings.Species[speciesId];
            }
            catch
            {
                return $"Species#{speciesId}";
            }
        }

        /// <summary>
        /// Get form name by species and form ID
        /// </summary>
        public static string GetFormName(int speciesId, int formId, int language = 2)
        {
            if (speciesId <= 0 || formId <= 0) return "";

            try
            {
                // Use PKHeX's form names
                var formNames = FormConverter.GetFormList((ushort)speciesId, GameInfo.Strings.Types, GameInfo.Strings.forms, GameInfo.GenderSymbolASCII, EntityContext.Gen9);

                if (formNames != null && formId < formNames.Length)
                {
                    var formName = formNames[formId];
                    // Return empty string for base form (usually empty or just the species name)
                    if (string.IsNullOrEmpty(formName) || formName == GetSpeciesName(speciesId))
                        return "";
                    return formName;
                }

                return "";
            }
            catch
            {
                return "";
            }
        }

        /// <summary>
        /// Get ability name by ID
        /// </summary>
        public static string GetAbilityName(int abilityId, int language = 2)
        {
            if (abilityId <= 0) return "Unknown";

            try
            {
                return GameInfo.Strings.Ability[abilityId];
            }
            catch
            {
                return $"Ability#{abilityId}";
            }
        }

        /// <summary>
        /// Get move name by ID
        /// </summary>
        public static string GetMoveName(int moveId, int language = 2)
        {
            if (moveId <= 0) return "Unknown";

            try
            {
                return GameInfo.Strings.Move[moveId];
            }
            catch
            {
                return $"Move#{moveId}";
            }
        }

        /// <summary>
        /// Get nature name by ID
        /// </summary>
        public static string GetNatureName(int natureId, int language = 2)
        {
            if (natureId < 0) return "Unknown";

            try
            {
                return GameInfo.Strings.Natures[natureId];
            }
            catch
            {
                return $"Nature#{natureId}";
            }
        }

        /// <summary>
        /// Get item name by ID
        /// </summary>
        public static string GetItemName(int itemId, int language = 2)
        {
            if (itemId <= 0) return "None";

            try
            {
                return GameInfo.Strings.Item[itemId];
            }
            catch
            {
                return $"Item#{itemId}";
            }
        }

        /// <summary>
        /// Get ball name by ID (balls use the Ball enum, not item indices)
        /// </summary>
        public static string GetBallName(int ballId, int language = 2)
        {
            if (ballId <= 0) return "Unknown";

            try
            {
                var ballList = GameInfo.Strings.balllist;
                if (ballId < ballList.Length && !string.IsNullOrEmpty(ballList[ballId]))
                    return ballList[ballId];
                return $"Ball#{ballId}";
            }
            catch
            {
                return $"Ball#{ballId}";
            }
        }

        /// <summary>
        /// Get type name by ID (for Tera types, move types, etc.)
        /// </summary>
        public static string GetTypeName(int typeId, int language = 2)
        {
            if (typeId < 0) return "Unknown";

            try
            {
                return GameInfo.Strings.Types[typeId];
            }
            catch
            {
                return $"Type#{typeId}";
            }
        }

        /// <summary>
        /// Get version/game name by ID — values from PKHeX.Core 25.12.21 GameVersion enum
        /// </summary>
        public static string GetVersionName(int versionId, int language = 2)
        {
            if (versionId <= 0) return "Unknown";

            return versionId switch
            {
                // Gen 3
                1 => "Sapphire",
                2 => "Ruby",
                3 => "Emerald",
                4 => "FireRed",
                5 => "LeafGreen",
                // Gen 4
                7 => "HeartGold",
                8 => "SoulSilver",
                10 => "Diamond",
                11 => "Pearl",
                12 => "Platinum",
                // Gen 3 spinoffs
                15 => "Colosseum / XD",
                16 => "Battle Revolution",
                // Gen 5
                20 => "White",
                21 => "Black",
                22 => "White 2",
                23 => "Black 2",
                // Gen 6
                24 => "X",
                25 => "Y",
                26 => "Alpha Sapphire",
                27 => "Omega Ruby",
                // Gen 7
                30 => "Sun",
                31 => "Moon",
                32 => "Ultra Sun",
                33 => "Ultra Moon",
                34 => "Pokémon GO",
                // Gen 1 (VC / original)
                35 => "Red",
                36 => "Green",
                37 => "Blue",
                38 => "Yellow",
                // Gen 2 (VC / original)
                39 => "Gold",
                40 => "Silver",
                41 => "Crystal",
                // LGPE
                42 => "Let's Go Pikachu",
                43 => "Let's Go Eevee",
                // Gen 8
                44 => "Sword",
                45 => "Shield",
                47 => "Legends: Arceus",
                48 => "Brilliant Diamond",
                49 => "Shining Pearl",
                // Gen 9
                50 => "Scarlet",
                51 => "Violet",
                52 => "Legends: Z-A",
                // Multi-game groups (Home / Bank transfers)
                53 => "Red / Blue",
                54 => "Red / Blue / Yellow",
                55 => "Gold / Silver",
                56 => "Gold / Silver / Crystal",
                57 => "Ruby / Sapphire",
                58 => "Ruby / Sapphire / Emerald",
                59 => "FireRed / LeafGreen",
                60 => "Pokémon Box",
                61 => "Colosseum",
                62 => "XD: Gale of Darkness",
                63 => "Diamond / Pearl",
                64 => "Diamond / Pearl / Platinum",
                65 => "HeartGold / SoulSilver",
                66 => "Black / White",
                67 => "Black 2 / White 2",
                68 => "X / Y",
                70 => "Omega Ruby / Alpha Sapphire",
                71 => "Sun / Moon",
                72 => "Ultra Sun / Ultra Moon",
                73 => "Let's Go Pikachu / Eevee",
                74 => "Sword / Shield",
                75 => "Brilliant Diamond / Shining Pearl",
                76 => "Scarlet / Violet",
                _ => $"Version #{versionId}"
            };
        }

        /// <summary>
        /// Get location name by ID and version context
        /// </summary>
        public static string GetLocationName(int locationId, int version = 0, int language = 2)
        {
            if (locationId <= 0) return "Unknown";

            try
            {
                // For now, return a simple format - PKHeX location resolution is complex
                return $"Location#{locationId}";
            }
            catch
            {
                return $"Location#{locationId}";
            }
        }

        /// <summary>
        /// Convert PKHeX language ID to language code string
        /// </summary>
        public static string GetLanguageCode(int languageId)
        {
            return languageId switch
            {
                1 => "JPN",
                2 => "ENG",
                3 => "FRE",
                4 => "ITA",
                5 => "GER",
                6 => "SPA",
                7 => "KOR",
                8 => "CHS",
                9 => "CHT",
                _ => "UNK"
            };
        }

        /// <summary>
        /// Get full language name from language code
        /// </summary>
        public static string GetLanguageFullName(string code)
        {
            return code switch
            {
                "ENG" => "English",
                "SPA" => "Spanish",
                "JPN" => "Japanese",
                "FRE" => "French",
                "ITA" => "Italian",
                "GER" => "German",
                "KOR" => "Korean",
                "CHS" => "Chinese (Simplified)",
                "CHT" => "Chinese (Traditional)",
                "UNK" => "Unknown",
                _ => string.IsNullOrEmpty(code) ? "Unknown" : code
            };
        }

        /// <summary>
        /// Get the stat boosted (+10%) by a nature. Returns null for neutral natures.
        /// Nature ID = (boosted_stat * 5) + reduced_stat
        /// Stats: 0=Atk, 1=Def, 2=Spe, 3=SpA, 4=SpD
        /// </summary>
        public static string? GetNatureBoostedStat(int natureId)
        {
            if (natureId < 0 || natureId > 24) return null;
            var boosted = natureId / 5;
            var reduced = natureId % 5;
            if (boosted == reduced) return null; // neutral nature
            return boosted switch
            {
                0 => "Atk",
                1 => "Def",
                2 => "Spe",
                3 => "SpA",
                4 => "SpD",
                _ => null
            };
        }

        /// <summary>
        /// Get the stat reduced (-10%) by a nature. Returns null for neutral natures.
        /// </summary>
        public static string? GetNatureReducedStat(int natureId)
        {
            if (natureId < 0 || natureId > 24) return null;
            var boosted = natureId / 5;
            var reduced = natureId % 5;
            if (boosted == reduced) return null; // neutral nature
            return reduced switch
            {
                0 => "Atk",
                1 => "Def",
                2 => "Spe",
                3 => "SpA",
                4 => "SpD",
                _ => null
            };
        }
    }
}
