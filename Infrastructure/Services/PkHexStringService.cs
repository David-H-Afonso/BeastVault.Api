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
        /// Get ball name by ID (balls are items in PKHeX)
        /// </summary>
        public static string GetBallName(int ballId, int language = 2)
        {
            if (ballId <= 0) return "Unknown";

            try
            {
                // Ball names are in the items array
                return GameInfo.Strings.Item[ballId];
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
        /// Get version/game name by ID
        /// </summary>
        public static string GetVersionName(int versionId, int language = 2)
        {
            if (versionId <= 0) return "Unknown";

            try
            {
                // Simple fallback for version names
                return $"Version#{versionId}";
            }
            catch
            {
                return $"Version#{versionId}";
            }
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
                _ => code
            };
        }
    }
}
