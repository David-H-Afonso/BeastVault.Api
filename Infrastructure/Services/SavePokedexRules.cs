using BeastVault.Api.Domain.Entities;
using PKHeX.Core;

namespace BeastVault.Api.Infrastructure.Services;

public static class SavePokedexRules
{
    public static int NationalMax(int generation) => generation switch
    {
        1 => 151,
        2 => 251,
        3 => 386,
        4 => 493,
        5 => 649,
        6 => 721,
        7 => 809,
        8 => 905,
        9 => 1025,
        _ => 0
    };

    public static IReadOnlySet<int> RegionalSpecies(int originGame, int generation, string gameName)
    {
        var max = generation switch
        {
            1 => 151,
            2 => 251,
            3 => gameName is "FireRed" or "LeafGreen" ? 151 : 386,
            4 when originGame is 7 or 8 => 251,
            4 when gameName.Equals("Platinum", StringComparison.OrdinalIgnoreCase) => 210,
            4 => 151,
            5 => 649,
            6 when gameName is "Omega Ruby" or "Alpha Sapphire" => 386,
            6 => 721,
            7 when gameName.StartsWith("Let's Go", StringComparison.OrdinalIgnoreCase) => 151,
            7 => 809,
            8 when gameName.Contains("Legends", StringComparison.OrdinalIgnoreCase) => 242,
            8 when gameName is "Sword" or "Shield" => 400,
            8 => 493,
            9 => 400,
            _ => 0
        };

        return max == 0
            ? new HashSet<int>()
            : Enumerable.Range(1, max).Where(id => IsSpeciesInGame(originGame, id)).ToHashSet();
    }

    public static IReadOnlySet<int> NationalSpecies(int originGame, int generation) =>
        Enumerable.Range(1, NationalMax(generation))
            .Where(id => IsSpeciesInGame(originGame, id))
            .ToHashSet();

    public static bool IsSpeciesInGame(int originGame, int speciesId)
    {
        if (speciesId <= 0) return false;
        var table = GetPersonalTable((GameVersion)originGame);
        return table is not null && speciesId <= table.MaxSpeciesID && table.IsSpeciesInGame((ushort)speciesId);
    }

    public static bool IsVersionExclusive(int originGame, int speciesId) =>
        GetVersionExclusiveSpecies((GameVersion)originGame).Contains(speciesId);

    public static bool IsRegional(SaveFileEntity save, int speciesId) =>
        RegionalSpecies(save.OriginGame, save.Generation, save.GameName).Contains(speciesId);

    private static IPersonalTable? GetPersonalTable(GameVersion version) => (int)version switch
    {
        35 or 36 or 37 => PersonalTable.RB,
        38 => PersonalTable.Y,
        39 or 40 => PersonalTable.GS,
        41 => PersonalTable.C,
        1 or 2 => PersonalTable.RS,
        3 => PersonalTable.E,
        4 => PersonalTable.FR,
        5 => PersonalTable.LG,
        7 or 8 => PersonalTable.HGSS,
        10 or 11 => PersonalTable.DP,
        12 => PersonalTable.Pt,
        20 or 21 => PersonalTable.BW,
        22 or 23 => PersonalTable.B2W2,
        24 or 25 => PersonalTable.XY,
        26 or 27 => PersonalTable.AO,
        30 or 31 => PersonalTable.SM,
        32 or 33 => PersonalTable.USUM,
        42 or 43 => PersonalTable.GG,
        44 or 45 => PersonalTable.SWSH,
        47 => PersonalTable.LA,
        48 or 49 => PersonalTable.BDSP,
        50 or 51 => PersonalTable.SV,
        52 => PersonalTable.ZA,
        _ => null
    };

    private static IReadOnlySet<int> GetVersionExclusiveSpecies(GameVersion version) => (int)version switch
    {
        50 => ScarletExclusives,
        51 => VioletExclusives,
        44 => SwordExclusives,
        45 => ShieldExclusives,
        7 => HeartGoldExclusives,
        8 => SoulSilverExclusives,
        _ => EmptyExclusives
    };

    private static readonly IReadOnlySet<int> EmptyExclusives = new HashSet<int>();
    private static readonly IReadOnlySet<int> ScarletExclusives = new HashSet<int>
    {
        37, 38, 128, 207, 208, 246, 247, 248, 313, 314, 316, 317, 434, 435, 425, 426,
        633, 634, 635, 690, 691, 765, 874, 978, 984, 985, 986, 987, 988, 989, 990,
        991, 1005, 1007
    };
    private static readonly IReadOnlySet<int> VioletExclusives = new HashSet<int>
    {
        128, 200, 211, 227, 303, 304, 305, 316, 317, 371, 372, 373, 434, 435, 690, 691,
        692, 693, 765, 766, 875, 885, 886, 887, 992, 993, 994, 995, 996, 997, 998,
        999, 1000, 1001, 1002, 1003, 1004, 1006, 1008
    };
    private static readonly IReadOnlySet<int> SwordExclusives = new HashSet<int> { 56, 57, 58, 129, 130, 138, 139, 140, 141, 236, 237, 239, 240, 270, 271, 272, 303, 304, 305, 343, 344, 347, 348, 369, 370, 453, 454, 559, 560, 561, 684, 685, 749, 750, 766, 782, 783, 784, 819, 820, 824, 825, 826, 874 };
    private static readonly IReadOnlySet<int> ShieldExclusives = new HashSet<int> { 58, 59, 131, 132, 222, 223, 224, 270, 271, 272, 315, 316, 317, 343, 344, 453, 454, 554, 555, 559, 560, 684, 685, 749, 750, 765, 782, 783, 784, 859, 860, 868, 869, 874 };
    private static readonly IReadOnlySet<int> HeartGoldExclusives = new HashSet<int> { 155, 156, 157, 185, 228, 229, 231, 232, 240, 241, 243, 244, 245, 250 };
    private static readonly IReadOnlySet<int> SoulSilverExclusives = new HashSet<int> { 155, 156, 157, 209, 210, 215, 225, 226, 231, 232, 238, 239, 240, 249 };
}
