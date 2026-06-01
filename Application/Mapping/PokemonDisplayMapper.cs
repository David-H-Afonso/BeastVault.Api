using BeastVault.Api.Domain.Entities;
using BeastVault.Api.Infrastructure.Services;

namespace BeastVault.Api.Application.Mapping;

/// <summary>
/// Centralizes all display-formatting logic for Pokémon data.
/// Resolves form names, game names, location names, hex IDs, icons, etc.
/// Frontend receives clean, render-ready strings.
/// </summary>
public static class PokemonDisplayMapper
{
    /// <summary>
    /// Human-readable form name. Returns null if form is default with no alternate forms.
    /// Hisui/Galar/Mega/Gmax etc. get human names, never raw numbers.
    /// </summary>
    public static string? ResolveDisplayFormName(int speciesId, int form, string? rawFormName, bool canGigantamax, int heldItemId)
    {
        // Gigantamax override
        if (canGigantamax)
        {
            var speciesName = PkHexStringService.GetSpeciesName(speciesId);
            return $"{speciesName} (Gigantamax)";
        }

        // Mega override
        if (PokemonFormService.CheckHasMegaStone(new PokemonEntity { SpeciesId = speciesId, HeldItemId = heldItemId }))
        {
            var megaForm = rawFormName;
            if (string.IsNullOrEmpty(megaForm))
                megaForm = "Mega";
            return megaForm;
        }

        // Form 0 — only show "Normal" if the species actually has multiple forms
        if (form == 0)
        {
            var formList = PKHeX.Core.FormConverter.GetFormList(
                (ushort)speciesId,
                PKHeX.Core.GameInfo.Strings.Types,
                PKHeX.Core.GameInfo.Strings.forms,
                PKHeX.Core.GameInfo.GenderSymbolASCII,
                PKHeX.Core.EntityContext.Gen9);

            // Only 1 form or no forms → hide
            if (formList == null || formList.Length <= 1)
                return null;

            // Has multiple forms but form 0 → "Normal"
            return null;
        }

        // Non-zero form — try PKHeX name first
        if (!string.IsNullOrEmpty(rawFormName))
            return HumanizeFormName(rawFormName);

        // Fallback: PKHeX resolution
        var resolved = PkHexStringService.GetFormName(speciesId, form);
        if (!string.IsNullOrEmpty(resolved))
            return HumanizeFormName(resolved);

        return $"Form {form}";
    }

    /// <summary>
    /// Converts raw PKHeX form slugs to human-readable names.
    /// "hisui" → "Hisuian", "galar" → "Galarian", "alola" → "Alolan", etc.
    /// </summary>
    private static string HumanizeFormName(string raw)
    {
        var lower = raw.ToLowerInvariant().Trim();
        return lower switch
        {
            "hisui" or "hisuian" => "Hisuian",
            "galar" or "galarian" => "Galarian",
            "alola" or "alolan" => "Alolan",
            "paldea" or "paldean" => "Paldean",
            "mega" => "Mega",
            "mega-x" or "mega x" => "Mega X",
            "mega-y" or "mega y" => "Mega Y",
            "gmax" or "gigantamax" => "Gigantamax",
            "eternamax" => "Eternamax",
            "primal" => "Primal",
            "origin" => "Origin Forme",
            "altered" => "Altered Forme",
            "sky" => "Sky Forme",
            "land" => "Land Forme",
            "therian" => "Therian Forme",
            "incarnate" => "Incarnate Forme",
            "white" => "White",
            "black" => "Black",
            "crowned" => "Crowned",
            "ice" => "Ice Rider",
            "shadow" => "Shadow Rider",
            "bloodmoon" => "Blood Moon",
            _ => raw // Keep as-is if already readable
        };
    }

    /// <summary>
    /// Origin game display name from game version ID.
    /// </summary>
    public static string ResolveOriginGameName(int originGame)
    {
        return PkHexStringService.GetVersionName(originGame);
    }

    /// <summary>
    /// PID as uppercase hex without 0x prefix, 8-char padded.
    /// </summary>
    public static string FormatPidHex(uint pid)
    {
        return pid.ToString("X8");
    }

    /// <summary>
    /// EC as uppercase hex without 0x prefix, 8-char padded.
    /// </summary>
    public static string FormatEcHex(uint ec)
    {
        return ec.ToString("X8");
    }

    /// <summary>
    /// Effective friendship: if CurrentHandler == 1 (traded), use HT friendship.
    /// Otherwise use OT friendship (which is CurrentFriendship from PKHeX).
    /// </summary>
    public static int ResolveEffectiveFriendship(PokemonEntity p)
    {
        return p.CurrentHandler == 1
            ? p.HandlingTrainerFriendship
            : p.CurrentFriendship;
    }

    /// <summary>
    /// Ball sprite URL path.
    /// </summary>
    public static string ResolveBallSpriteUrl(int ballId, string ballName)
    {
        var slug = ballName.ToLowerInvariant().Replace(" ", "-").Replace("'", "");
        // Legends Arceus balls
        if (ballId >= 27 && ballId <= 36)
        {
            var laSlug = ballId switch
            {
                27 => "la-pokeball",
                28 => "la-greatball",
                29 => "la-ultraball",
                30 => "la-featherball",
                31 => "la-wingball",
                32 => "la-jetball",
                33 => "la-heavyball",
                34 => "la-leadenball",
                35 => "la-gigaton-ball",
                36 => "la-originball",
                _ => slug
            };
            return $"/sprites/balls/{laSlug}.png";
        }
        return $"/sprites/balls/{slug}.png";
    }

    /// <summary>
    /// Held item sprite URL, or null if no item.
    /// </summary>
    public static string? ResolveHeldItemSpriteUrl(int heldItemId, string itemName)
    {
        if (heldItemId <= 0 || string.IsNullOrEmpty(itemName) || itemName == "None" || itemName == "(None)")
            return null;
        var slug = itemName.ToLowerInvariant().Replace(" ", "-").Replace("'", "");
        return $"/sprites/items/{slug}.png";
    }

    /// <summary>
    /// Format a raw PokeAPI game version slug to human-readable.
    /// "fireredleafgreen" → "FireRed / LeafGreen"
    /// </summary>
    public static string FormatGameVersionSlug(string slug)
    {
        if (string.IsNullOrEmpty(slug)) return slug;

        // Known compound slugs
        return slug.ToLowerInvariant() switch
        {
            "red" => "Red",
            "blue" => "Blue",
            "yellow" => "Yellow",
            "gold" => "Gold",
            "silver" => "Silver",
            "crystal" => "Crystal",
            "ruby" => "Ruby",
            "sapphire" => "Sapphire",
            "emerald" => "Emerald",
            "firered" => "FireRed",
            "leafgreen" => "LeafGreen",
            "firered-leafgreen" or "fireredleafgreen" => "FireRed / LeafGreen",
            "diamond" => "Diamond",
            "pearl" => "Pearl",
            "platinum" => "Platinum",
            "heartgold" => "HeartGold",
            "soulsilver" => "SoulSilver",
            "heartgold-soulsilver" or "heartgoldsoulsilver" => "HeartGold / SoulSilver",
            "black" => "Black",
            "white" => "White",
            "black-2" or "black2" => "Black 2",
            "white-2" or "white2" => "White 2",
            "black-2-white-2" or "black2white2" => "Black 2 / White 2",
            "x" => "X",
            "y" => "Y",
            "omega-ruby" or "omegaruby" => "Omega Ruby",
            "alpha-sapphire" or "alphasapphire" => "Alpha Sapphire",
            "sun" => "Sun",
            "moon" => "Moon",
            "ultra-sun" or "ultrasun" => "Ultra Sun",
            "ultra-moon" or "ultramoon" => "Ultra Moon",
            "lets-go-pikachu" or "letsgopikachu" => "Let's Go Pikachu",
            "lets-go-eevee" or "letsgoeevee" => "Let's Go Eevee",
            "sword" => "Sword",
            "shield" => "Shield",
            "sword-shield" or "swordshield" => "Sword / Shield",
            "brilliant-diamond" or "brilliantdiamond" => "Brilliant Diamond",
            "shining-pearl" or "shiningpearl" => "Shining Pearl",
            "legends-arceus" or "legendsarceus" => "Legends: Arceus",
            "scarlet" => "Scarlet",
            "violet" => "Violet",
            "scarlet-violet" or "scarletviolet" => "Scarlet / Violet",
            _ => FormatSlugGeneric(slug)
        };
    }

    /// <summary>
    /// Format a PokeAPI pokemon form slug.
    /// "blastoise-gmax" → "Blastoise (Gigantamax)"
    /// </summary>
    public static string FormatPokemonFormSlug(string slug)
    {
        if (string.IsNullOrEmpty(slug)) return slug;

        // Split on dash to find form suffix
        var parts = slug.Split('-');
        if (parts.Length <= 1) return CapitalizeFirst(slug);

        var baseName = CapitalizeFirst(parts[0]);
        var suffix = string.Join("-", parts[1..]).ToLowerInvariant();

        var formLabel = suffix switch
        {
            "gmax" => "Gigantamax",
            "mega" => "Mega",
            "mega-x" => "Mega X",
            "mega-y" => "Mega Y",
            "alola" or "alolan" => "Alolan",
            "galar" or "galarian" => "Galarian",
            "hisui" or "hisuian" => "Hisuian",
            "paldea" or "paldean" => "Paldean",
            _ => CapitalizeFirst(suffix)
        };

        return $"{baseName} ({formLabel})";
    }

    /// <summary>
    /// Format a PokeAPI base stat name.
    /// "special-attack" → "Sp. Atk", "hp" → "HP"
    /// </summary>
    public static string FormatStatName(string raw)
    {
        if (string.IsNullOrEmpty(raw)) return raw;
        return raw.ToLowerInvariant().Replace("_", "-") switch
        {
            "hp" => "HP",
            "attack" => "Attack",
            "defense" => "Defense",
            "special-attack" => "Sp. Atk",
            "special-defense" => "Sp. Def",
            "speed" => "Speed",
            _ => CapitalizeFirst(raw)
        };
    }

    private static string FormatSlugGeneric(string slug)
    {
        return string.Join(" ", slug.Split('-').Select(CapitalizeFirst));
    }

    private static string CapitalizeFirst(string s)
    {
        if (string.IsNullOrEmpty(s)) return s;
        return char.ToUpperInvariant(s[0]) + s[1..];
    }
}
