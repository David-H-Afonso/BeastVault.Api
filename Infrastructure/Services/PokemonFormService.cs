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

    public static int[] GetMegaStoneItemIds()
    {
        return GetMegaStoneMapping()
            .SelectMany(x => x.Value)
            .Where(id => id > 0)
            .Distinct()
            .ToArray();
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

            // Absol - Check Z form first
            359 when heldItemId == 2638 => 2, // Absolite Z (MD)
            359 when heldItemId == 677 => 1, // Absolite (base)

            // Garchomp - Check Z form first
            445 when heldItemId == 2640 => 2, // Garchompite Z (MD)
            445 when heldItemId == 683 => 1, // Garchompite (base)

            // Lucario - Check Z form first
            448 when heldItemId == 2641 => 2, // Lucarionite Z (MD)
            448 when heldItemId == 673 => 1, // Lucarionite (base)

            // Abomasnow
            460 when heldItemId == 674 => 1, // Abomasite

            // Beedrill
            15 when heldItemId == 770 => 1, // Beedrillite

            // Pidgeot
            18 when heldItemId == 762 => 1, // Pidgeotite

            // Slowbro
            80 when heldItemId == 760 => 1, // Slowbronite

            // Steelix
            208 when heldItemId == 761 => 1, // Steelixite

            // Sceptile
            254 when heldItemId == 753 => 1, // Sceptilite

            // Swampert
            260 when heldItemId == 752 => 1, // Swampertite

            // Sableye
            302 when heldItemId == 754 => 1, // Sablenite

            // Sharpedo
            319 when heldItemId == 759 => 1, // Sharpedonite

            // Camerupt
            323 when heldItemId == 767 => 1, // Cameruptite

            // Altaria
            334 when heldItemId == 755 => 1, // Altarianite

            // Glalie
            362 when heldItemId == 763 => 1, // Glalitite

            // Salamence
            373 when heldItemId == 769 => 1, // Salamencite

            // Metagross
            376 when heldItemId == 758 => 1, // Metagrossite

            // Latias
            380 when heldItemId == 684 => 1, // Latiasite

            // Latios
            381 when heldItemId == 685 => 1, // Latiosite

            // Rayquaza (doesn't need a stone, but included for completeness)
            384 when heldItemId == 0 => 1, // No item needed

            // Lopunny
            428 when heldItemId == 768 => 1, // Lopunnite

            // Gallade
            475 when heldItemId == 756 => 1, // Galladite

            // Audino
            531 when heldItemId == 757 => 1, // Audinite

            // Diancie
            719 when heldItemId == 764 => 1, // Diancite

            // ===== POKEMON LEGENDS: Z-A NEW MEGA EVOLUTIONS =====

            // Clefable
            36 when heldItemId == 2559 => 1, // Clefablite

            // Victreebel
            71 when heldItemId == 2560 => 1, // Victreebelite

            // Starmie
            121 when heldItemId == 2561 => 1, // Starminite

            // Dragonite
            149 when heldItemId == 2562 => 1, // Dragoninite

            // Meganium
            154 when heldItemId == 2563 => 1, // Meganiumite

            // Feraligatr
            160 when heldItemId == 2564 => 1, // Feraligite

            // Skarmory
            227 when heldItemId == 2565 => 1, // Skarmorite

            // Froslass
            478 when heldItemId == 2566 => 1, // Froslassite

            // Emboar
            500 when heldItemId == 2569 => 1, // Emboarite

            // Excadrill
            530 when heldItemId == 2570 => 1, // Excadrite

            // Scolipede
            545 when heldItemId == 2571 => 1, // Scolipite

            // Scrafty
            560 when heldItemId == 2572 => 1, // Scraftinite

            // Eelektross
            604 when heldItemId == 2573 => 1, // Eelektrossite

            // Chandelure
            609 when heldItemId == 2574 => 1, // Chandelurite

            // Chesnaught
            652 when heldItemId == 2575 => 1, // Chesnaughtite

            // Delphox
            655 when heldItemId == 2576 => 1, // Delphoxite

            // Greninja
            658 when heldItemId == 2577 => 1, // Greninjite

            // Pyroar
            668 when heldItemId == 2578 => 1, // Pyroarite

            // Note: Floette mega form removed - needs special handling due to color variants

            // Malamar
            687 when heldItemId == 2580 => 1, // Malamarite

            // Barbaracle
            689 when heldItemId == 2581 => 1, // Barbaracite

            // Dragalge
            691 when heldItemId == 2582 => 1, // Dragalgite

            // Hawlucha
            701 when heldItemId == 2583 => 1, // Hawluchanite

            // Zygarde (Complete Forme)
            718 when heldItemId == 2584 => 1, // Zygardite

            // Drampa
            780 when heldItemId == 2585 => 1, // Drampanite

            // Falinks
            870 when heldItemId == 2587 => 1, // Falinksite

            // ===== MEGA DIMENSION DLC MEGA EVOLUTIONS =====

            // Raichu X form
            26 when heldItemId == 2635 => 1, // Raichunite X

            // Raichu Y form
            26 when heldItemId == 2636 => 2, // Raichunite Y

            // Chimecho
            358 when heldItemId == 2637 => 1, // Chimechite

            // Staraptor
            398 when heldItemId == 2639 => 1, // Staraptite

            // Golurk
            623 when heldItemId == 2642 => 1, // Golurkite

            // Meowstic (Female form)
            678 when heldItemId == 2643 => 2, // Meowsticite

            // Crabominable
            740 when heldItemId == 2644 => 1, // Crabominite

            // Golisopod
            768 when heldItemId == 2645 => 1, // Golisopite

            // Magearna
            801 when heldItemId == 2646 => 1, // Magearnite

            // Zeraora
            807 when heldItemId == 2586 => 1, // Zeraorite

            // Scovillain
            952 when heldItemId == 2647 => 1, // Scovillainite

            // Baxcalibur
            998 when heldItemId == 2648 => 1, // Baxcalibrite

            // Tatsugiri
            978 when heldItemId == 2649 => 1, // Tatsugirinite

            // Glimmora
            970 when heldItemId == 2650 => 1, // Glimmoranite

            // Heatran (Mega Dimension version, though also appears in base game)
            485 when heldItemId == 2567 => 1, // Heatranite

            // Darkrai (Mega Dimension version)
            491 when heldItemId == 2568 => 1, // Darkranite

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
            { 359, new List<int> { 677, 2638 } }, // Absol - Absolite (base), Absolite Z (MD)
            { 445, new List<int> { 683, 2640 } }, // Garchomp - Garchompite (base), Garchompite Z (MD)
            { 448, new List<int> { 673, 2641 } }, // Lucario - Lucarionite (base), Lucarionite Z (MD)
            { 460, new List<int> { 674 } },    // Abomasnow - Abomasite
            { 15, new List<int> { 770 } },     // Beedrill - Beedrillite
            { 18, new List<int> { 762 } },     // Pidgeot - Pidgeotite
            { 80, new List<int> { 760 } },     // Slowbro - Slowbronite
            { 208, new List<int> { 761 } },    // Steelix - Steelixite
            { 254, new List<int> { 753 } },    // Sceptile - Sceptilite
            { 260, new List<int> { 752 } },    // Swampert - Swampertite
            { 302, new List<int> { 754 } },    // Sableye - Sablenite
            { 319, new List<int> { 759 } },    // Sharpedo - Sharpedonite
            { 323, new List<int> { 767 } },    // Camerupt - Cameruptite
            { 334, new List<int> { 755 } },    // Altaria - Altarianite
            { 362, new List<int> { 763 } },    // Glalie - Glalitite
            { 373, new List<int> { 769 } },    // Salamence - Salamencite
            { 376, new List<int> { 758 } },    // Metagross - Metagrossite
            { 380, new List<int> { 684 } },    // Latias - Latiasite
            { 381, new List<int> { 685 } },    // Latios - Latiosite
            { 384, new List<int> { 0 } },      // Rayquaza - No item needed
            { 428, new List<int> { 768 } },    // Lopunny - Lopunnite
            { 475, new List<int> { 756 } },    // Gallade - Galladite
            { 531, new List<int> { 757 } },    // Audino - Audinite
            { 719, new List<int> { 764 } },    // Diancie - Diancite
            
            // ===== POKEMON LEGENDS: Z-A NEW MEGA EVOLUTIONS =====
            { 36, new List<int> { 2559 } },    // Clefable - Clefablite
            { 71, new List<int> { 2560 } },    // Victreebel - Victreebelite
            { 121, new List<int> { 2561 } },   // Starmie - Starminite
            { 149, new List<int> { 2562 } },   // Dragonite - Dragoninite
            { 154, new List<int> { 2563 } },   // Meganium - Meganiumite
            { 160, new List<int> { 2564 } },   // Feraligatr - Feraligite
            { 227, new List<int> { 2565 } },   // Skarmory - Skarmorite
            { 478, new List<int> { 2566 } },   // Froslass - Froslassite
            { 500, new List<int> { 2569 } },   // Emboar - Emboarite
            { 530, new List<int> { 2570 } },   // Excadrill - Excadrite
            { 545, new List<int> { 2571 } },   // Scolipede - Scolipite
            { 560, new List<int> { 2572 } },   // Scrafty - Scraftinite
            { 604, new List<int> { 2573 } },   // Eelektross - Eelektrossite
            { 609, new List<int> { 2574 } },   // Chandelure - Chandelurite
            { 652, new List<int> { 2575 } },   // Chesnaught - Chesnaughtite
            { 655, new List<int> { 2576 } },   // Delphox - Delphoxite
            { 658, new List<int> { 2577 } },   // Greninja - Greninjite
            { 668, new List<int> { 2578 } },   // Pyroar - Pyroarite
            // Note: Floette (670) removed - mega form uses eternal flower sprite regardless of base color form
            { 687, new List<int> { 2580 } },   // Malamar - Malamarite
            { 689, new List<int> { 2581 } },   // Barbaracle - Barbaracite
            { 691, new List<int> { 2582 } },   // Dragalge - Dragalgite
            { 701, new List<int> { 2583 } },   // Hawlucha - Hawluchanite
            { 718, new List<int> { 2584 } },   // Zygarde (Complete Forme) - Zygardite
            { 780, new List<int> { 2585 } },   // Drampa - Drampanite
            { 870, new List<int> { 2587 } },   // Falinks - Falinksite
            
            // ===== MEGA DIMENSION DLC MEGA EVOLUTIONS =====
            { 26, new List<int> { 2635, 2636 } }, // Raichu - Raichunite X, Y
            { 358, new List<int> { 2637 } },   // Chimecho - Chimechite
            { 398, new List<int> { 2639 } },   // Staraptor - Staraptite
            { 485, new List<int> { 2567 } },   // Heatran - Heatranite (MD version)
            { 491, new List<int> { 2568 } },   // Darkrai - Darkranite
            { 623, new List<int> { 2642 } },   // Golurk - Golurkite
            { 678, new List<int> { 2643 } },   // Meowstic - Meowsticite
            { 740, new List<int> { 2644 } },   // Crabominable - Crabominite
            { 768, new List<int> { 2645 } },   // Golisopod - Golisopite
            { 801, new List<int> { 2646 } },   // Magearna - Magearnite
            { 807, new List<int> { 2586 } },   // Zeraora - Zeraorite
            { 952, new List<int> { 2647 } },   // Scovillain - Scovillainite
            { 970, new List<int> { 2650 } },   // Glimmora - Glimmoranite
            { 978, new List<int> { 2649 } },   // Tatsugiri - Tatsugirinite
            { 998, new List<int> { 2648 } }    // Baxcalibur - Baxcalibrite
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
