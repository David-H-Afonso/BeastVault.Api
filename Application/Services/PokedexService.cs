using System.Text.Json;
using BeastVault.Api.Application.Interfaces;
using BeastVault.Api.Contracts;
using BeastVault.Api.Domain.Entities;
using BeastVault.Api.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace BeastVault.Api.Application.Services;

public class PokedexService : IPokedexService
{
    private readonly AppDbContext _context;
    private readonly HttpClient _httpClient;
    private const string POKEAPI_BASE = "https://pokeapi.co/api/v2";

    public PokedexService(AppDbContext context, IHttpClientFactory httpClientFactory)
    {
        _context = context;
        _httpClient = httpClientFactory.CreateClient("PokeApi");
    }

    public async Task<PokedexEntry?> GetSpeciesAsync(int speciesId)
    {
        return await _context.PokedexEntries.FindAsync(speciesId);
    }

    public async Task<PokedexPokemon?> GetPokemonAsync(int pokemonId)
    {
        return await _context.PokedexPokemon.FindAsync(pokemonId);
    }

    public async Task<List<PokedexPokemon>> GetPokemonBySpeciesAsync(int speciesId)
    {
        return await _context.PokedexPokemon
            .Where(p => p.SpeciesId == speciesId)
            .OrderBy(p => p.PokemonId)
            .ToListAsync();
    }

    public async Task<SpeciesWithFormsResponse> GetSpeciesWithFormsAsync(int speciesId)
    {
        var species = await _context.PokedexEntries.FindAsync(speciesId);
        if (species == null) return new SpeciesWithFormsResponse(false);

        var forms = await _context.PokedexPokemon
            .Where(p => p.SpeciesId == speciesId)
            .OrderBy(p => p.PokemonId)
            .ToListAsync();

        var speciesDto = new SpeciesDto(
            species.SpeciesId,
            species.Name,
            JsonSerializer.Deserialize<object>(species.LocalizedNames)!,
            species.Genus,
            species.FlavorText,
            species.Generation,
            species.Color,
            species.Shape,
            species.Habitat,
            species.GrowthRate,
            species.CaptureRate,
            species.BaseHappiness,
            species.HatchCounter,
            species.GenderRate,
            species.IsLegendary,
            species.IsMythical,
            species.IsBaby,
            species.HasGenderDifferences,
            species.FormsSwitchable,
            JsonSerializer.Deserialize<object>(species.EggGroups)!,
            JsonSerializer.Deserialize<object>(species.Varieties)!,
            species.EvolutionChainUrl
        );

        var formDtos = forms.Select(f => new PokemonFormDto(
            f.PokemonId,
            f.SpeciesId,
            f.Name,
            f.Height,
            f.Weight,
            f.BaseExperience,
            f.IsDefault,
            JsonSerializer.Deserialize<object>(f.Types)!,
            JsonSerializer.Deserialize<object>(f.Abilities)!,
            JsonSerializer.Deserialize<object>(f.BaseStats)!,
            JsonSerializer.Deserialize<object>(f.Sprites)!,
            JsonSerializer.Deserialize<object>(f.Cries)!
        ));

        return new SpeciesWithFormsResponse(true, speciesDto, formDtos);
    }

    public async Task<PopulationStatusResponse> GetPopulationStatusAsync()
    {
        var totalSpecies = await _context.PokedexEntries.CountAsync();
        var totalForms = await _context.PokedexPokemon.CountAsync();
        var maxSpeciesId = totalSpecies > 0
            ? await _context.PokedexEntries.MaxAsync(e => e.SpeciesId)
            : 0;
        var lastUpdated = totalSpecies > 0
            ? await _context.PokedexEntries.MaxAsync(e => e.CachedAt)
            : (DateTime?)null;

        return new PopulationStatusResponse(totalSpecies, totalForms, maxSpeciesId, lastUpdated);
    }

    public async Task<int> PopulateSpeciesRangeAsync(int startId, int endId, IProgress<string>? progress = null)
    {
        int populated = 0;

        for (int speciesId = startId; speciesId <= endId; speciesId++)
        {
            try
            {
                progress?.Report($"Fetching species {speciesId}/{endId}...");

                var existing = await _context.PokedexEntries.FindAsync(speciesId);
                if (existing != null)
                {
                    populated++;
                    continue;
                }

                var speciesData = await FetchJsonAsync($"{POKEAPI_BASE}/pokemon-species/{speciesId}");
                if (speciesData == null) continue;

                var entry = ParseSpecies(speciesId, speciesData.Value);
                _context.PokedexEntries.Add(entry);

                var varieties = speciesData.Value.GetProperty("varieties").EnumerateArray();
                foreach (var variety in varieties)
                {
                    var pokemonUrl = variety.GetProperty("pokemon").GetProperty("url").GetString()!;
                    var pokemonIdStr = pokemonUrl.TrimEnd('/').Split('/').Last();
                    if (!int.TryParse(pokemonIdStr, out var pokemonId)) continue;

                    if (await _context.PokedexPokemon.FindAsync(pokemonId) != null) continue;

                    var pokemonData = await FetchJsonAsync($"{POKEAPI_BASE}/pokemon/{pokemonId}");
                    if (pokemonData == null) continue;

                    var pokemon = ParsePokemon(pokemonId, speciesId, pokemonData.Value);
                    _context.PokedexPokemon.Add(pokemon);

                    await Task.Delay(50);
                }

                await _context.SaveChangesAsync();
                populated++;

                // Rate limit: PokeAPI allows 100 requests/min
                await Task.Delay(200);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error populating species {speciesId}: {ex.Message}");
                progress?.Report($"Error on species {speciesId}: {ex.Message}");
            }
        }

        return populated;
    }

    private async Task<JsonElement?> FetchJsonAsync(string url)
    {
        try
        {
            var response = await _httpClient.GetAsync(url);
            if (!response.IsSuccessStatusCode) return null;
            var json = await response.Content.ReadAsStringAsync();
            return JsonSerializer.Deserialize<JsonElement>(json);
        }
        catch
        {
            return null;
        }
    }

    private static PokedexEntry ParseSpecies(int speciesId, JsonElement data)
    {
        var names = new Dictionary<string, string>();
        foreach (var nameEntry in data.GetProperty("names").EnumerateArray())
        {
            var lang = nameEntry.GetProperty("language").GetProperty("name").GetString()!;
            var name = nameEntry.GetProperty("name").GetString()!;
            names[lang] = name;
        }

        var genus = "";
        foreach (var g in data.GetProperty("genera").EnumerateArray())
        {
            if (g.GetProperty("language").GetProperty("name").GetString() == "en")
            {
                genus = g.GetProperty("genus").GetString() ?? "";
                break;
            }
        }

        var flavorText = "";
        foreach (var ft in data.GetProperty("flavor_text_entries").EnumerateArray())
        {
            if (ft.GetProperty("language").GetProperty("name").GetString() == "en")
                flavorText = ft.GetProperty("flavor_text").GetString() ?? "";
        }
        flavorText = flavorText.Replace("\f", " ").Replace("\n", " ").Replace("  ", " ").Trim();

        var genUrl = data.GetProperty("generation").GetProperty("url").GetString() ?? "";
        var genStr = genUrl.TrimEnd('/').Split('/').Last();
        var generation = genStr switch
        {
            "generation-i" => 1,
            "generation-ii" => 2,
            "generation-iii" => 3,
            "generation-iv" => 4,
            "generation-v" => 5,
            "generation-vi" => 6,
            "generation-vii" => 7,
            "generation-viii" => 8,
            "generation-ix" => 9,
            _ => int.TryParse(genStr.Replace("generation-", ""), out var g) ? g : 0
        };

        var eggGroups = data.GetProperty("egg_groups").EnumerateArray()
            .Select(eg => eg.GetProperty("name").GetString() ?? "")
            .ToList();

        var varieties = data.GetProperty("varieties").EnumerateArray()
            .Select(v =>
            {
                var pokemonUrl = v.GetProperty("pokemon").GetProperty("url").GetString()!;
                var pokemonIdStr = pokemonUrl.TrimEnd('/').Split('/').Last();
                int.TryParse(pokemonIdStr, out var pokemonId);
                return new
                {
                    name = v.GetProperty("pokemon").GetProperty("name").GetString(),
                    id = pokemonId,
                    isDefault = v.GetProperty("is_default").GetBoolean()
                };
            })
            .Cast<object>()
            .ToList();

        return new PokedexEntry
        {
            SpeciesId = speciesId,
            Name = data.GetProperty("name").GetString() ?? "",
            LocalizedNames = JsonSerializer.Serialize(names),
            Genus = genus,
            FlavorText = flavorText,
            Generation = generation,
            Color = SafeGetName(data, "color"),
            Shape = SafeGetName(data, "shape"),
            Habitat = SafeGetName(data, "habitat"),
            GrowthRate = SafeGetName(data, "growth_rate"),
            CaptureRate = data.GetProperty("capture_rate").GetInt32(),
            BaseHappiness = data.GetProperty("base_happiness").GetInt32(),
            HatchCounter = data.GetProperty("hatch_counter").GetInt32(),
            GenderRate = data.GetProperty("gender_rate").GetInt32(),
            IsLegendary = data.GetProperty("is_legendary").GetBoolean(),
            IsMythical = data.GetProperty("is_mythical").GetBoolean(),
            IsBaby = data.GetProperty("is_baby").GetBoolean(),
            HasGenderDifferences = data.GetProperty("has_gender_differences").GetBoolean(),
            FormsSwitchable = data.GetProperty("forms_switchable").GetBoolean(),
            EggGroups = JsonSerializer.Serialize(eggGroups),
            Varieties = JsonSerializer.Serialize(varieties),
            EvolutionChainUrl = data.TryGetProperty("evolution_chain", out var ec) && ec.ValueKind != JsonValueKind.Null
                ? ec.GetProperty("url").GetString() ?? ""
                : "",
            CachedAt = DateTime.UtcNow
        };
    }

    private static PokedexPokemon ParsePokemon(int pokemonId, int speciesId, JsonElement data)
    {
        var types = data.GetProperty("types").EnumerateArray()
            .Select(t => new
            {
                slot = t.GetProperty("slot").GetInt32(),
                name = t.GetProperty("type").GetProperty("name").GetString()
            })
            .Cast<object>()
            .ToList();

        var abilities = data.GetProperty("abilities").EnumerateArray()
            .Select(a => new
            {
                name = a.GetProperty("ability").GetProperty("name").GetString(),
                isHidden = a.GetProperty("is_hidden").GetBoolean(),
                slot = a.GetProperty("slot").GetInt32()
            })
            .Cast<object>()
            .ToList();

        var stats = new Dictionary<string, int>();
        foreach (var s in data.GetProperty("stats").EnumerateArray())
        {
            var statName = s.GetProperty("stat").GetProperty("name").GetString() ?? "";
            stats[statName] = s.GetProperty("base_stat").GetInt32();
        }

        var sprites = data.GetProperty("sprites").GetRawText();

        var cries = data.TryGetProperty("cries", out var criesEl)
            ? criesEl.GetRawText() : "{}";

        var gameIndices = data.TryGetProperty("game_indices", out var giEl)
            ? giEl.GetRawText() : "[]";

        return new PokedexPokemon
        {
            PokemonId = pokemonId,
            SpeciesId = speciesId,
            Name = data.GetProperty("name").GetString() ?? "",
            Height = data.GetProperty("height").GetInt32(),
            Weight = data.GetProperty("weight").GetInt32(),
            BaseExperience = data.TryGetProperty("base_experience", out var be) && be.ValueKind == JsonValueKind.Number
                ? be.GetInt32() : 0,
            Order = data.TryGetProperty("order", out var ord) && ord.ValueKind == JsonValueKind.Number
                ? ord.GetInt32() : 0,
            IsDefault = data.GetProperty("is_default").GetBoolean(),
            Types = JsonSerializer.Serialize(types),
            Abilities = JsonSerializer.Serialize(abilities),
            BaseStats = JsonSerializer.Serialize(stats),
            Sprites = sprites,
            Cries = cries,
            GameIndices = gameIndices,
            CachedAt = DateTime.UtcNow
        };
    }

    private static string SafeGetName(JsonElement data, string property)
    {
        if (!data.TryGetProperty(property, out var prop) || prop.ValueKind == JsonValueKind.Null)
            return "";
        return prop.GetProperty("name").GetString() ?? "";
    }
}
