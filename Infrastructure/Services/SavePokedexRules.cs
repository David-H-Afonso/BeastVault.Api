using System.Text.Json;
using BeastVault.Api.Domain.Entities;
using PKHeX.Core;

namespace BeastVault.Api.Infrastructure.Services;

public static class SavePokedexRules
{
    private static readonly Lazy<PokedexCatalog> Catalog = new(LoadCatalog);
    private static readonly IReadOnlySet<int> EmptySpecies = new HashSet<int>();

    public static int NationalMax(int generation) => generation switch
    {
        1 => 151,
        2 => 251,
        3 => 386,
        4 => 493,
        5 => 649,
        6 => 721,
        7 => 809,
        8 => 898,
        9 => 1025,
        _ => 0
    };

    public static IReadOnlySet<int> RegionalSpecies(int originGame, int generation, string gameName)
    {
        if (TryGetGameDefinition(originGame, out var definition) &&
            TryGetRanges(Catalog.Value.RegionalDexes, definition.RegionalDex, out var ranges))
        {
            return ExpandRanges(ranges);
        }

        return EmptySpecies;
    }

    public static IReadOnlySet<int> NationalSpecies(int originGame, int generation, int? maxSpeciesId = null)
    {
        if (TryGetGameDefinition(originGame, out var definition) &&
            TryGetRanges(Catalog.Value.NationalDexes, definition.NationalDex, out var ranges))
        {
            var configuredMax = ranges.Select(range => range.Length > 1 ? range[1] : 0).DefaultIfEmpty(0).Max();
            var saveMax = maxSpeciesId is > 0 ? maxSpeciesId.Value : configuredMax;
            var species = ExpandRanges(ranges, Math.Min(configuredMax, saveMax));
            return species.Where(id => IsSpeciesInGame(originGame, id)).ToHashSet();
        }

        var fallbackMax = NationalMax(generation);
        if (maxSpeciesId is > 0) fallbackMax = Math.Min(fallbackMax, maxSpeciesId.Value);
        return fallbackMax <= 0
            ? EmptySpecies
            : Enumerable.Range(1, fallbackMax).Where(id => IsSpeciesInGame(originGame, id)).ToHashSet();
    }

    public static bool IsSpeciesInGame(int originGame, int speciesId)
    {
        if (speciesId <= 0) return false;
        var table = GetPersonalTable((GameVersion)originGame);
        return table is not null && speciesId <= table.MaxSpeciesID && table.IsSpeciesInGame((ushort)speciesId);
    }

    public static bool IsVersionExclusive(int originGame, int speciesId) =>
        GetVersionExclusiveSpecies(originGame).Contains(speciesId);

    public static bool IsRegional(SaveFileEntity save, int speciesId) =>
        RegionalSpecies(save.OriginGame, save.Generation, save.GameName).Contains(speciesId);

    private static bool TryGetGameDefinition(int originGame, out GamePokedexDefinition definition) =>
        Catalog.Value.Games.TryGetValue(originGame.ToString(System.Globalization.CultureInfo.InvariantCulture), out definition!);

    private static bool TryGetRanges(
        IReadOnlyDictionary<string, List<int[]>> source,
        string name,
        out List<int[]> ranges) =>
        source.TryGetValue(name, out ranges!);

    private static IReadOnlySet<int> GetVersionExclusiveSpecies(int originGame) =>
        Catalog.Value.VersionExclusives.TryGetValue(
            originGame.ToString(System.Globalization.CultureInfo.InvariantCulture),
            out var species)
            ? species
            : EmptySpecies;

    private static HashSet<int> ExpandRanges(IEnumerable<int[]> ranges, int? max = null)
    {
        var result = new HashSet<int>();
        foreach (var range in ranges)
        {
            if (range.Length < 2) continue;
            var first = Math.Max(1, range[0]);
            var last = max.HasValue ? Math.Min(range[1], max.Value) : range[1];
            for (var speciesId = first; speciesId <= last; speciesId++)
                result.Add(speciesId);
        }
        return result;
    }

    private static PokedexCatalog LoadCatalog()
    {
        var assembly = typeof(SavePokedexRules).Assembly;
        var resourceName = assembly.GetManifestResourceNames()
            .FirstOrDefault(name => name.EndsWith("Data.SavePokedexCatalog.json", StringComparison.OrdinalIgnoreCase));
        if (resourceName is null) return new PokedexCatalog();

        using var stream = assembly.GetManifestResourceStream(resourceName);
        return stream is null
            ? new PokedexCatalog()
            : JsonSerializer.Deserialize<PokedexCatalog>(stream, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            }) ?? new PokedexCatalog();
    }

    private static IPersonalTable? GetPersonalTable(GameVersion version) => (int)version switch
    {
        35 or 36 or 37 => PersonalTable.RB,
        38 => PersonalTable.Y,
        39 or 40 => PersonalTable.GS,
        41 => PersonalTable.C,
        53 or 54 => PersonalTable.RB,
        55 or 56 => PersonalTable.GS,
        1 or 2 => PersonalTable.RS,
        3 => PersonalTable.E,
        4 => PersonalTable.FR,
        5 => PersonalTable.LG,
        57 or 60 or 61 or 62 => PersonalTable.RS,
        58 => PersonalTable.E,
        59 => PersonalTable.FR,
        7 or 8 => PersonalTable.HGSS,
        10 or 11 => PersonalTable.DP,
        12 => PersonalTable.Pt,
        63 => PersonalTable.DP,
        64 => PersonalTable.Pt,
        65 => PersonalTable.HGSS,
        20 or 21 => PersonalTable.BW,
        22 or 23 => PersonalTable.B2W2,
        66 => PersonalTable.BW,
        67 => PersonalTable.B2W2,
        24 or 25 => PersonalTable.XY,
        26 or 27 => PersonalTable.AO,
        68 => PersonalTable.XY,
        70 => PersonalTable.AO,
        30 or 31 => PersonalTable.SM,
        32 or 33 => PersonalTable.USUM,
        71 => PersonalTable.SM,
        72 => PersonalTable.USUM,
        42 or 43 => PersonalTable.GG,
        73 => PersonalTable.GG,
        44 or 45 => PersonalTable.SWSH,
        74 => PersonalTable.SWSH,
        47 => PersonalTable.LA,
        48 or 49 => PersonalTable.BDSP,
        75 => PersonalTable.BDSP,
        50 or 51 => PersonalTable.SV,
        76 => PersonalTable.SV,
        52 => PersonalTable.ZA,
        _ => null
    };

    private sealed class PokedexCatalog
    {
        public Dictionary<string, List<int[]>> RegionalDexes { get; init; } = new(StringComparer.OrdinalIgnoreCase);
        public Dictionary<string, List<int[]>> NationalDexes { get; init; } = new(StringComparer.OrdinalIgnoreCase);
        public Dictionary<string, GamePokedexDefinition> Games { get; init; } = new(StringComparer.OrdinalIgnoreCase);
        public Dictionary<string, HashSet<int>> VersionExclusives { get; init; } = new(StringComparer.OrdinalIgnoreCase);
    }

    private sealed class GamePokedexDefinition
    {
        public string RegionalDex { get; init; } = string.Empty;
        public string NationalDex { get; init; } = string.Empty;
    }
}
