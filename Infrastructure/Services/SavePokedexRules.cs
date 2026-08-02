using BeastVault.Api.Domain.Entities;

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

        return max == 0 ? new HashSet<int>() : Enumerable.Range(1, max).ToHashSet();
    }

    public static bool IsRegional(SaveFileEntity save, int speciesId) =>
        RegionalSpecies(save.OriginGame, save.Generation, save.GameName).Contains(speciesId);
}
