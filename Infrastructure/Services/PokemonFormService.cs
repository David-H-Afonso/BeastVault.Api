using BeastVault.Api.Domain.Entities;

namespace BeastVault.Api.Infrastructure.Services;

/// <summary>
/// Service for determining Pokemon forms based on held items, format, and special flags
/// </summary>
public static class PokemonFormService
{
    /// <summary>
    /// Get the display form for a Pokemon, considering held items and special flags
    /// </summary>
    public static int GetDisplayForm(PokemonEntity pokemon, string fileFormat)
    {
        // Check for Mega Evolution forms (Gen 6+)
        if (HasMegaStone(pokemon.SpeciesId, pokemon.HeldItemId))
        {
            return GetMegaForm(pokemon.SpeciesId, pokemon.HeldItemId);
        }

        // Check for Gigantamax forms (Gen 8 files)
        if (fileFormat.ToLower() == "pk8" && CanGigantamax(pokemon))
        {
            return GetGigantamaxForm(pokemon.SpeciesId);
        }

        // Return original form if no special conditions
        return pokemon.Form;
    }

    /// <summary>
    /// Check if a Pokemon can Gigantamax (public method for DTO mapping)
    /// </summary>
    public static bool CheckCanGigantamax(PokemonEntity pokemon, string fileFormat)
    {
        return fileFormat.ToLower() == "pk8" && CanGigantamax(pokemon);
    }

    /// <summary>
    /// Check if a Pokemon has a Mega Stone equipped (public method for DTO mapping)
    /// </summary>
    public static bool CheckHasMegaStone(PokemonEntity pokemon)
    {
        return HasMegaStone(pokemon.SpeciesId, pokemon.HeldItemId);
    }

    /// <summary>
    /// Check if a Pokemon has a Mega Stone equipped
    /// </summary>
    private static bool HasMegaStone(int speciesId, int heldItemId)
    {
        // Map of species to their Mega Stone item IDs
        var megaStones = GetMegaStoneMapping();
        return megaStones.ContainsKey(speciesId) && megaStones[speciesId].Contains(heldItemId);
    }

    /// <summary>
    /// Get the Mega form for a species with a specific Mega Stone
    /// </summary>
    private static int GetMegaForm(int speciesId, int heldItemId)
    {
        return speciesId switch
        {
            // Venusaur
            3 when heldItemId == 659 => 1, // Venusaurite

            // Charizard  
            6 when heldItemId == 660 => 1, // Charizardite X
            6 when heldItemId == 678 => 2, // Charizardite Y

            // Blastoise
            9 when heldItemId == 661 => 1, // Blastoisinite

            // Alakazam
            65 when heldItemId == 679 => 1, // Alakazite

            // Gengar
            94 when heldItemId == 656 => 1, // Gengarite

            // Kangaskhan
            115 when heldItemId == 675 => 1, // Kangaskhanite

            // Pinsir
            127 when heldItemId == 671 => 1, // Pinsirite

            // Gyarados
            130 when heldItemId == 676 => 1, // Gyaradosite

            // Aerodactyl
            142 when heldItemId == 672 => 1, // Aerodactylite

            // Mewtwo
            150 when heldItemId == 662 => 1, // Mewtwonite X
            150 when heldItemId == 663 => 2, // Mewtwonite Y

            // Ampharos
            181 when heldItemId == 658 => 1, // Ampharosite

            // Scizor
            212 when heldItemId == 670 => 1, // Scizorite

            // Heracross
            214 when heldItemId == 680 => 1, // Heracronite

            // Houndoom
            229 when heldItemId == 666 => 1, // Houndoominite

            // Tyranitar
            248 when heldItemId == 669 => 1, // Tyranitarite

            // Blaziken
            257 when heldItemId == 664 => 1, // Blazikenite

            // Gardevoir
            282 when heldItemId == 657 => 1, // Gardevoirite

            // Mawile
            303 when heldItemId == 681 => 1, // Mawilite

            // Aggron
            306 when heldItemId == 667 => 1, // Aggronite

            // Medicham
            308 when heldItemId == 665 => 1, // Medichamite

            // Manectric
            310 when heldItemId == 682 => 1, // Manectite

            // Banette
            354 when heldItemId == 668 => 1, // Banettite

            // Absol
            359 when heldItemId == 677 => 1, // Absolite

            // Garchomp
            445 when heldItemId == 683 => 1, // Garchompite

            // Lucario
            448 when heldItemId == 673 => 1, // Lucarionite

            // Abomasnow
            460 when heldItemId == 674 => 1, // Abomasite

            _ => 1 // Default mega form
        };
    }

    /// <summary>
    /// Check if a Pokemon can Gigantamax
    /// </summary>
    private static bool CanGigantamax(PokemonEntity pokemon)
    {
        // Use the CanGigantamax flag from PKHeX
        var gigantamaxSpecies = GetGigantamaxSpecies();

        // Check if the species can Gigantamax and has the CanGigantamax flag set
        return gigantamaxSpecies.Contains(pokemon.SpeciesId) && pokemon.CanGigantamax;
    }    /// <summary>
         /// Get the Gigantamax form for a species
         /// </summary>
    private static int GetGigantamaxForm(int speciesId)
    {
        // Most Gigantamax forms are form 0 but with special rendering
        // Some Pokemon like Pikachu have special Gigantamax forms
        return speciesId switch
        {
            25 => 1, // Pikachu Gigantamax
            52 => 1, // Meowth Gigantamax  
            _ => 0   // Most Gigantamax use form 0 but are rendered differently
        };
    }

    /// <summary>
    /// Get mapping of species to their Mega Stone item IDs
    /// </summary>
    private static Dictionary<int, List<int>> GetMegaStoneMapping()
    {
        return new Dictionary<int, List<int>>
        {
            { 3, new List<int> { 659 } },      // Venusaur - Venusaurite
            { 6, new List<int> { 660, 678 } }, // Charizard - Charizardite X, Y
            { 9, new List<int> { 661 } },      // Blastoise - Blastoisinite
            { 65, new List<int> { 679 } },     // Alakazam - Alakazite
            { 94, new List<int> { 656 } },     // Gengar - Gengarite
            { 115, new List<int> { 675 } },    // Kangaskhan - Kangaskhanite
            { 127, new List<int> { 671 } },    // Pinsir - Pinsirite
            { 130, new List<int> { 676 } },    // Gyarados - Gyaradosite
            { 142, new List<int> { 672 } },    // Aerodactyl - Aerodactylite
            { 150, new List<int> { 662, 663 } }, // Mewtwo - Mewtwonite X, Y
            { 181, new List<int> { 658 } },    // Ampharos - Ampharosite
            { 212, new List<int> { 670 } },    // Scizor - Scizorite
            { 214, new List<int> { 680 } },    // Heracross - Heracronite
            { 229, new List<int> { 666 } },    // Houndoom - Houndoominite
            { 248, new List<int> { 669 } },    // Tyranitar - Tyranitarite
            { 257, new List<int> { 664 } },    // Blaziken - Blazikenite
            { 282, new List<int> { 657 } },    // Gardevoir - Gardevoirite
            { 303, new List<int> { 681 } },    // Mawile - Mawilite
            { 306, new List<int> { 667 } },    // Aggron - Aggronite
            { 308, new List<int> { 665 } },    // Medicham - Medichamite
            { 310, new List<int> { 682 } },    // Manectric - Manectite
            { 354, new List<int> { 668 } },    // Banette - Banettite
            { 359, new List<int> { 677 } },    // Absol - Absolite
            { 445, new List<int> { 683 } },    // Garchomp - Garchompite
            { 448, new List<int> { 673 } },    // Lucario - Lucarionite
            { 460, new List<int> { 674 } }     // Abomasnow - Abomasite
        };
    }

    /// <summary>
    /// Get list of species that can Gigantamax
    /// </summary>
    private static HashSet<int> GetGigantamaxSpecies()
    {
        return new HashSet<int>
        {
            25,  // Pikachu
            52,  // Meowth
            68,  // Machamp
            94,  // Gengar
            131, // Lapras
            143, // Snorlax
            569, // Garbodor
            809, // Melmetal
            812, // Rillaboom
            815, // Cinderace
            818, // Inteleon
            823, // Corviknight
            826, // Orbeetle
            834, // Drednaw
            839, // Coalossal
            841, // Flapple
            842, // Appletun
            844, // Sandaconda
            845, // Cramorant
            849, // Toxapex
            851, // Centiskorch
            858, // Hatterene
            861, // Grimmsnarl
            869, // Alcremie
            879, // Copperajah
            884  // Duraludon
        };
    }
}
