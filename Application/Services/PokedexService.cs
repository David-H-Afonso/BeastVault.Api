using System.Text.Json;
using BeastVault.Api.Application.Interfaces;
using BeastVault.Api.Contracts;
using BeastVault.Api.Domain.Entities;
using BeastVault.Api.Helpers;
using BeastVault.Api.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace BeastVault.Api.Application.Services;

public class PokedexService : IPokedexService
{
    private readonly AppDbContext _context;
    private readonly HttpClient _httpClient;
    private readonly ImageCacheService _imageCache;
    private const string POKEAPI_BASE = "https://pokeapi.co/api/v2";

    // Static progress tracking for background population
    private static volatile bool _isPopulating;
    private static int _populatingCurrent;
    private static int _populatingTotal;
    private static readonly object _populateLock = new();

    // Items progress tracking
    private static volatile bool _isPopulatingItems;
    private static int _populatingItemsCurrent;
    private static int _populatingItemsTotal;
    private static readonly object _populateItemsLock = new();

    // Moves progress tracking
    private static volatile bool _isPopulatingMoves;
    private static int _populatingMovesCurrent;
    private static int _populatingMovesTotal;
    private static readonly object _populateMovesLock = new();

    // Abilities progress tracking
    private static volatile bool _isPopulatingAbilities;
    private static int _populatingAbilitiesCurrent;
    private static int _populatingAbilitiesTotal;
    private static readonly object _populateAbilitiesLock = new();

    // Evolution chains progress tracking
    private static volatile bool _isPopulatingChains;
    private static int _populatingChainsCurrent;
    private static int _populatingChainsTotal;
    private static readonly object _populateChainsLock = new();

    // Types progress tracking
    private static volatile bool _isPopulatingTypes;
    private static readonly object _populateTypesLock = new();

    public static bool IsPopulating => _isPopulating;
    public static int PopulatingCurrent => _populatingCurrent;
    public static int PopulatingTotal => _populatingTotal;

    public static bool IsPopulatingItems => _isPopulatingItems;
    public static int PopulatingItemsCurrent => _populatingItemsCurrent;
    public static int PopulatingItemsTotal => _populatingItemsTotal;

    public static bool IsPopulatingMoves => _isPopulatingMoves;
    public static int PopulatingMovesCurrent => _populatingMovesCurrent;
    public static int PopulatingMovesTotal => _populatingMovesTotal;

    public static bool IsPopulatingAbilities => _isPopulatingAbilities;
    public static int PopulatingAbilitiesCurrent => _populatingAbilitiesCurrent;
    public static int PopulatingAbilitiesTotal => _populatingAbilitiesTotal;

    public static bool IsPopulatingChains => _isPopulatingChains;
    public static int PopulatingChainsCurrent => _populatingChainsCurrent;
    public static int PopulatingChainsTotal => _populatingChainsTotal;

    public static bool IsPopulatingTypes => _isPopulatingTypes;

    public PokedexService(AppDbContext context, IHttpClientFactory httpClientFactory, ImageCacheService imageCache)
    {
        _context = context;
        _httpClient = httpClientFactory.CreateClient("PokeApi");
        _imageCache = imageCache;
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
        var totalBulbapediaCached = await _context.BulbapediaCache.CountAsync();
        var totalBulbapediaNormalized = await _context.BulbapediaCache.CountAsync(c =>
            c.NormalizedStatus == ParseStatus.Success || c.NormalizedStatus == ParseStatus.PartialSuccess);
        var totalBulbapediaFlavorEntries = await _context.PokedexFlavorEntries.CountAsync(f => f.Source == CacheSource.Bulbapedia);
        var totalBulbapediaLocations = await _context.PokedexLocations.CountAsync(l => l.Source == CacheSource.Bulbapedia);
        var totalBulbapediaSprites = await _context.PokedexSpriteEntries.CountAsync(s => s.Source == CacheSource.Bulbapedia);

        return new PopulationStatusResponse(totalSpecies, totalForms, maxSpeciesId, lastUpdated,
            _isPopulating, _populatingCurrent, _populatingTotal,
            await _context.PokedexItems.CountAsync(),
            _isPopulatingItems, _populatingItemsCurrent, _populatingItemsTotal,
            await _context.PokedexMoves.CountAsync(),
            _isPopulatingMoves, _populatingMovesCurrent, _populatingMovesTotal,
            await _context.PokedexAbilities.CountAsync(),
            _isPopulatingAbilities, _populatingAbilitiesCurrent, _populatingAbilitiesTotal,
            await _context.PokedexEvolutionChains.CountAsync(),
            _isPopulatingChains, _populatingChainsCurrent, _populatingChainsTotal,
            await _context.PokedexTypes.CountAsync(),
            _isPopulatingTypes,
            totalBulbapediaCached,
            totalBulbapediaNormalized,
            totalBulbapediaFlavorEntries,
            totalBulbapediaLocations,
            totalBulbapediaSprites);
    }

    public async Task<SpriteDownloadStatusResponse> GetSpriteDownloadStatusAsync()
    {
        var spritesOnDisk = await _context.PokedexPokemon.CountAsync(p => p.SpriteLocalPath != null);
        var artworkOnDisk = await _context.PokedexPokemon.CountAsync(p => p.ArtworkLocalPath != null);
        var itemSpritesOnDisk = await _context.PokedexItems.CountAsync(i => i.SpriteLocalPath != null);

        return new SpriteDownloadStatusResponse(
            ImageCacheService.IsDownloading,
            ImageCacheService.DownloadCurrent,
            ImageCacheService.DownloadTotal,
            spritesOnDisk,
            artworkOnDisk,
            itemSpritesOnDisk
        );
    }

    public async Task<int> PopulateSpeciesRangeAsync(int startId, int endId, IProgress<string>? progress = null)
    {
        lock (_populateLock)
        {
            if (_isPopulating)
                return 0; // Already running
            _isPopulating = true;
            _populatingCurrent = 0;
            _populatingTotal = endId - startId + 1;
        }

        int populated = 0;

        try
        {
            for (int speciesId = startId; speciesId <= endId; speciesId++)
            {
                _populatingCurrent = speciesId - startId + 1;

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

                    // Save all per-game flavor text entries from PokeAPI
                    SaveFlavorEntries(speciesId, speciesData.Value);

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
                        await _imageCache.DownloadSpritesForPokemonAsync(pokemon);

                        // Fetch encounter locations from PokeAPI
                        await FetchAndSaveEncountersAsync(speciesId, pokemonId, pokemon.IsDefault);

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
                    _context.ChangeTracker.Clear();
                }
            }
        }
        finally
        {
            _isPopulating = false;
            _populatingCurrent = 0;
            _populatingTotal = 0;
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
            {
                var candidate = PokedexTextFilters.CleanFlavorText(ft.GetProperty("flavor_text").GetString());
                if (PokedexTextFilters.IsDisplayableFlavorText(candidate))
                    flavorText = candidate;
            }
        }

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

        var evolutionChainUrl = data.TryGetProperty("evolution_chain", out var ec) && ec.ValueKind != JsonValueKind.Null
            ? ec.GetProperty("url").GetString() ?? ""
            : "";

        // Extract numeric chain ID from URL (e.g. "https://pokeapi.co/api/v2/evolution-chain/1/")
        int? evolutionChainId = null;
        if (!string.IsNullOrEmpty(evolutionChainUrl))
        {
            var chainIdStr = evolutionChainUrl.TrimEnd('/').Split('/').Last();
            if (int.TryParse(chainIdStr, out var cid)) evolutionChainId = cid;
        }

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
            EvolutionChainUrl = evolutionChainUrl,
            EvolutionChainId = evolutionChainId,
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

        var movesJson = data.TryGetProperty("moves", out var mvEl)
            ? mvEl.GetRawText() : "[]";

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
            MovesJson = movesJson,
            CachedAt = DateTime.UtcNow
        };
    }

    /// <summary>
    /// Extracts all flavor_text_entries from PokeAPI species data and saves to PokedexFlavorEntries table.
    /// </summary>
    private int SaveFlavorEntries(int speciesId, JsonElement speciesData)
    {
        if (!speciesData.TryGetProperty("flavor_text_entries", out var entries)) return 0;

        var existingKeys = _context.PokedexFlavorEntries
            .Where(f => f.SpeciesId == speciesId && f.Source == CacheSource.PokeApi)
            .Select(f => new { f.Language, f.GameVersion })
            .ToList()
            .Select(f => $"{PokedexTextFilters.NormalizeFlavorLanguage(f.Language)}|{f.GameVersion}")
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var added = 0;

        foreach (var ft in entries.EnumerateArray())
        {
            var lang = PokedexTextFilters.NormalizeFlavorLanguage(
                ft.GetProperty("language").GetProperty("name").GetString());
            if (!PokedexTextFilters.IsTargetFlavorLanguage(lang)) continue;

            var version = ft.GetProperty("version").GetProperty("name").GetString() ?? "";
            var text = PokedexTextFilters.CleanFlavorText(ft.GetProperty("flavor_text").GetString());
            if (!PokedexTextFilters.IsDisplayableFlavorText(text)) continue;

            var key = $"{lang}|{version}";
            if (!existingKeys.Add(key)) continue;

            _context.PokedexFlavorEntries.Add(new PokedexFlavorEntry
            {
                SpeciesId = speciesId,
                Language = lang,
                GameVersion = version,
                Text = text,
                Source = CacheSource.PokeApi
            });
            added++;
        }

        return added;
    }

    /// <summary>
    /// Fetches encounter data from PokeAPI for a Pokemon and saves to PokedexLocations table.
    /// Only fetches for the default form to avoid duplicates.
    /// </summary>
    private async Task FetchAndSaveEncountersAsync(int speciesId, int pokemonId, bool isDefault)
    {
        if (!isDefault) return;

        var data = await FetchJsonAsync($"{POKEAPI_BASE}/pokemon/{pokemonId}/encounters");
        if (data == null) return;

        foreach (var encounter in data.Value.EnumerateArray())
        {
            var locationName = encounter.GetProperty("location_area").GetProperty("name").GetString() ?? "";
            if (string.IsNullOrEmpty(locationName)) continue;

            if (encounter.TryGetProperty("version_details", out var versionDetails))
            {
                foreach (var vd in versionDetails.EnumerateArray())
                {
                    var game = vd.GetProperty("version").GetProperty("name").GetString() ?? "";
                    var method = "";
                    if (vd.TryGetProperty("encounter_details", out var ed))
                    {
                        var methods = ed.EnumerateArray()
                            .Select(e => e.GetProperty("method").GetProperty("name").GetString() ?? "")
                            .Where(m => !string.IsNullOrEmpty(m))
                            .Distinct()
                            .ToList();
                        method = string.Join(", ", methods);
                    }

                    _context.PokedexLocations.Add(new PokedexLocation
                    {
                        SpeciesId = speciesId,
                        Game = game,
                        Location = locationName.Replace("-", " "),
                        Method = string.IsNullOrEmpty(method) ? null : method,
                        Source = CacheSource.PokeApi
                    });
                }
            }
        }
    }

    /// <summary>
    /// Backfills flavor entries and encounter data for already-cached species that don't have them yet.
    /// </summary>
    public async Task<(int flavorsFilled, int locationsFilled, int errors)> BackfillEntriesAndLocationsAsync(
        int startId = 1, int endId = 1025)
    {
        int flavorsFilled = 0, locationsFilled = 0, errors = 0;

        for (int speciesId = startId; speciesId <= endId; speciesId++)
        {
            try
            {
                // Check if species exists in cache
                var exists = await _context.PokedexEntries.AnyAsync(e => e.SpeciesId == speciesId);
                if (!exists) continue;

                var existingFlavorRows = await _context.PokedexFlavorEntries
                    .AsNoTracking()
                    .Where(f => f.SpeciesId == speciesId)
                    .Select(f => new { f.Language, f.Text })
                    .ToListAsync();
                var existingFlavorLanguages = existingFlavorRows
                    .Where(f => PokedexTextFilters.IsDisplayableFlavorText(f.Text))
                    .Select(f => PokedexTextFilters.NormalizeFlavorLanguage(f.Language))
                    .ToHashSet(StringComparer.OrdinalIgnoreCase);
                var needsFlavors = PokedexTextFilters.TargetFlavorLanguages.Any(lang => !existingFlavorLanguages.Contains(lang));

                if (needsFlavors)
                {
                    var speciesData = await FetchJsonAsync($"{POKEAPI_BASE}/pokemon-species/{speciesId}");
                    if (speciesData != null)
                    {
                        var saved = SaveFlavorEntries(speciesId, speciesData.Value);
                        if (saved > 0) flavorsFilled++;
                    }
                    await Task.Delay(100);
                }

                // Check if locations already exist
                var hasLocations = await _context.PokedexLocations.AnyAsync(l => l.SpeciesId == speciesId);
                if (!hasLocations)
                {
                    // Get default pokemon ID for this species
                    var defaultPokemon = await _context.PokedexPokemon
                        .Where(p => p.SpeciesId == speciesId && p.IsDefault)
                        .Select(p => p.PokemonId)
                        .FirstOrDefaultAsync();
                    if (defaultPokemon > 0)
                    {
                        await FetchAndSaveEncountersAsync(speciesId, defaultPokemon, true);
                        locationsFilled++;
                        await Task.Delay(100);
                    }
                }

                if (needsFlavors || !hasLocations)
                    await _context.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error backfilling species {speciesId}: {ex.Message}");
                errors++;
                _context.ChangeTracker.Clear();
            }
        }

        return (flavorsFilled, locationsFilled, errors);
    }

    private static string SafeGetName(JsonElement data, string property)
    {
        if (!data.TryGetProperty(property, out var prop) || prop.ValueKind == JsonValueKind.Null)
            return "";
        return prop.GetProperty("name").GetString() ?? "";
    }

    public async Task<PokedexItem?> GetItemAsync(int itemId)
    {
        return await _context.PokedexItems.FindAsync(itemId);
    }

    public async Task<PokedexItem?> GetOrFetchItemAsync(int itemId)
    {
        var existing = await _context.PokedexItems.FindAsync(itemId);
        if (existing != null) return existing;

        try
        {
            var data = await FetchJsonAsync($"{POKEAPI_BASE}/item/{itemId}");
            if (data == null) return null;

            var item = ParseItem(itemId, data.Value);
            _context.PokedexItems.Add(item);
            await _context.SaveChangesAsync();
            return item;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error fetching item {itemId} on demand: {ex.Message}");
            return null;
        }
    }

    public async Task<int> PopulateItemsAsync(int startId, int endId)
    {
        lock (_populateItemsLock)
        {
            if (_isPopulatingItems) return 0;
            _isPopulatingItems = true;
            _populatingItemsCurrent = 0;
            _populatingItemsTotal = endId - startId + 1;
        }

        int populated = 0;

        try
        {
            for (int itemId = startId; itemId <= endId; itemId++)
            {
                _populatingItemsCurrent = itemId - startId + 1;

                try
                {
                    if (await _context.PokedexItems.FindAsync(itemId) != null)
                    {
                        populated++;
                        continue;
                    }

                    var data = await FetchJsonAsync($"{POKEAPI_BASE}/item/{itemId}");
                    if (data == null) continue;

                    var item = ParseItem(itemId, data.Value);
                    _context.PokedexItems.Add(item);
                    await _context.SaveChangesAsync();
                    populated++;

                    await Task.Delay(200);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error populating item {itemId}: {ex.Message}");
                    _context.ChangeTracker.Clear();
                }
            }
        }
        finally
        {
            _isPopulatingItems = false;
            _populatingItemsCurrent = 0;
            _populatingItemsTotal = 0;
        }

        return populated;
    }

    private static PokedexItem ParseItem(int itemId, JsonElement data)
    {
        var name = data.GetProperty("name").GetString() ?? "";

        // Get English display name
        var displayName = name;
        if (data.TryGetProperty("names", out var names))
        {
            foreach (var n in names.EnumerateArray())
            {
                if (n.GetProperty("language").GetProperty("name").GetString() == "en")
                {
                    displayName = n.GetProperty("name").GetString() ?? name;
                    break;
                }
            }
        }

        // Get category
        var category = "";
        if (data.TryGetProperty("category", out var cat) && cat.ValueKind != JsonValueKind.Null)
            category = cat.GetProperty("name").GetString() ?? "";

        // Get sprite URL
        var spriteUrl = "";
        if (data.TryGetProperty("sprites", out var sprites) && sprites.TryGetProperty("default", out var defSprite) && defSprite.ValueKind == JsonValueKind.String)
            spriteUrl = defSprite.GetString() ?? "";

        // Get effect (short English)
        var effect = "";
        if (data.TryGetProperty("effect_entries", out var effects))
        {
            foreach (var e in effects.EnumerateArray())
            {
                if (e.GetProperty("language").GetProperty("name").GetString() == "en")
                {
                    effect = e.TryGetProperty("short_effect", out var se)
                        ? se.GetString() ?? ""
                        : e.GetProperty("effect").GetString() ?? "";
                    break;
                }
            }
        }

        // Get flavor text (English)
        var flavorText = "";
        if (data.TryGetProperty("flavor_text_entries", out var ftes))
        {
            foreach (var ft in ftes.EnumerateArray())
            {
                if (ft.GetProperty("language").GetProperty("name").GetString() == "en")
                {
                    flavorText = ft.GetProperty("text").GetString() ?? "";
                    break;
                }
            }
        }

        var flingPower = data.TryGetProperty("fling_power", out var fp) && fp.ValueKind == JsonValueKind.Number
            ? fp.GetInt32() : (int?)null;

        return new PokedexItem
        {
            ItemId = itemId,
            Name = name,
            DisplayName = displayName,
            Category = category,
            SpriteUrl = spriteUrl,
            Effect = effect,
            FlavorText = flavorText.Replace("\f", " ").Replace("\n", " ").Trim(),
            FlingPower = flingPower,
            CachedAt = DateTime.UtcNow
        };
    }

    public async Task<int> PopulateMovesAsync(int startId, int endId)
    {
        lock (_populateMovesLock)
        {
            if (_isPopulatingMoves) return 0;
            _isPopulatingMoves = true;
            _populatingMovesCurrent = 0;
            _populatingMovesTotal = endId - startId + 1;
        }

        int populated = 0;

        try
        {
            for (int moveId = startId; moveId <= endId; moveId++)
            {
                _populatingMovesCurrent = moveId - startId + 1;

                try
                {
                    // INSERT OR IGNORE handles the duplicate-skip case atomically —
                    // no separate FindAsync needed (avoids stale context reads).
                    var data = await FetchJsonAsync($"{POKEAPI_BASE}/move/{moveId}");
                    if (data == null) continue;

                    var move = ParseMove(moveId, data.Value);

                    // Use direct SQL INSERT OR IGNORE to completely bypass EF change-tracking.
                    // EF Add+SaveChanges on a long-lived context can silently fail after any
                    // prior exception even with ChangeTracker.Clear().
                    object? powerParam = move.Power.HasValue ? move.Power.Value : (object?)null;
                    object? accuracyParam = move.Accuracy.HasValue ? move.Accuracy.Value : (object?)null;
                    await _context.Database.ExecuteSqlRawAsync(
                        "INSERT OR IGNORE INTO \"PokedexMoves\" " +
                        "(\"MoveId\",\"Name\",\"DisplayName\",\"Type\",\"DamageClass\",\"Power\",\"Accuracy\",\"PP\",\"Priority\",\"Effect\",\"FlavorText\",\"CachedAt\") " +
                        "VALUES ({0},{1},{2},{3},{4},{5},{6},{7},{8},{9},{10},{11})",
                        move.MoveId, move.Name, move.DisplayName, move.Type, move.DamageClass,
                        powerParam, accuracyParam,
                        move.PP, move.Priority, move.Effect, move.FlavorText,
                        move.CachedAt.ToString("O"));
                    populated++;

                    await Task.Delay(200);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error populating move {moveId}: {ex.Message}");
                    _context.ChangeTracker.Clear(); // Reset EF state after failed save
                }
            }
        }
        finally
        {
            _isPopulatingMoves = false;
            _populatingMovesCurrent = 0;
            _populatingMovesTotal = 0;
        }

        return populated;
    }

    private static PokedexMove ParseMove(int moveId, JsonElement data)
    {
        var name = data.TryGetProperty("name", out var nm) ? nm.GetString() ?? "" : "";

        var displayName = name;
        if (data.TryGetProperty("names", out var names))
        {
            foreach (var n in names.EnumerateArray())
            {
                if (!n.TryGetProperty("language", out var lang)) continue;
                if (!lang.TryGetProperty("name", out var langName)) continue;
                if (langName.GetString() != "en") continue;
                if (n.TryGetProperty("name", out var nName))
                    displayName = nName.GetString() ?? name;
                break;
            }
        }

        var type = "";
        if (data.TryGetProperty("type", out var t) && t.ValueKind != JsonValueKind.Null)
        {
            if (t.TryGetProperty("name", out var tName))
                type = tName.GetString() ?? "";
        }

        var damageClass = "";
        if (data.TryGetProperty("damage_class", out var dc) && dc.ValueKind != JsonValueKind.Null)
        {
            if (dc.TryGetProperty("name", out var dcName))
                damageClass = dcName.GetString() ?? "";
        }

        var power = data.TryGetProperty("power", out var pw) && pw.ValueKind == JsonValueKind.Number
            ? pw.GetInt32() : (int?)null;

        var accuracy = data.TryGetProperty("accuracy", out var acc) && acc.ValueKind == JsonValueKind.Number
            ? acc.GetInt32() : (int?)null;

        var pp = data.TryGetProperty("pp", out var ppEl) && ppEl.ValueKind == JsonValueKind.Number
            ? ppEl.GetInt32() : 0;

        var priority = data.TryGetProperty("priority", out var pri) && pri.ValueKind == JsonValueKind.Number
            ? pri.GetInt32() : 0;

        var effect = "";
        if (data.TryGetProperty("effect_entries", out var effects))
        {
            foreach (var e in effects.EnumerateArray())
            {
                if (!e.TryGetProperty("language", out var lang)) continue;
                if (!lang.TryGetProperty("name", out var langName)) continue;
                if (langName.GetString() != "en") continue;
                effect = e.TryGetProperty("short_effect", out var se)
                    ? se.GetString() ?? ""
                    : e.TryGetProperty("effect", out var eff) ? eff.GetString() ?? "" : "";
                break;
            }
        }

        var flavorText = "";
        if (data.TryGetProperty("flavor_text_entries", out var ftes))
        {
            foreach (var ft in ftes.EnumerateArray())
            {
                if (!ft.TryGetProperty("language", out var lang)) continue;
                if (!lang.TryGetProperty("name", out var langName)) continue;
                if (langName.GetString() != "en") continue;
                if (ft.TryGetProperty("text", out var txt))
                    flavorText = txt.GetString() ?? "";
            }
        }

        return new PokedexMove
        {
            MoveId = moveId,
            Name = name,
            DisplayName = displayName,
            Type = type,
            DamageClass = damageClass,
            Power = power,
            Accuracy = accuracy,
            PP = pp,
            Priority = priority,
            Effect = effect,
            FlavorText = flavorText.Replace("\f", " ").Replace("\n", " ").Trim(),
            CachedAt = DateTime.UtcNow
        };
    }

    // ── Abilities ──────────────────────────────────────────────────────────

    public async Task<PokedexAbility?> GetAbilityAsync(int abilityId)
        => await _context.PokedexAbilities.FindAsync(abilityId);

    public async Task<int> PopulateAbilitiesAsync(int startId, int endId)
    {
        lock (_populateAbilitiesLock)
        {
            if (_isPopulatingAbilities) return 0;
            _isPopulatingAbilities = true;
            _populatingAbilitiesCurrent = 0;
            _populatingAbilitiesTotal = endId - startId + 1;
        }

        int populated = 0;

        try
        {
            for (int abilityId = startId; abilityId <= endId; abilityId++)
            {
                _populatingAbilitiesCurrent = abilityId - startId + 1;

                try
                {
                    if (await _context.PokedexAbilities.FindAsync(abilityId) != null)
                    {
                        populated++;
                        continue;
                    }

                    var data = await FetchJsonAsync($"{POKEAPI_BASE}/ability/{abilityId}");
                    if (data == null) continue;

                    var ability = ParseAbility(abilityId, data.Value);
                    _context.PokedexAbilities.Add(ability);
                    await _context.SaveChangesAsync();
                    populated++;

                    await Task.Delay(200);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error populating ability {abilityId}: {ex.Message}");
                    _context.ChangeTracker.Clear();
                }
            }
        }
        finally
        {
            _isPopulatingAbilities = false;
            _populatingAbilitiesCurrent = 0;
            _populatingAbilitiesTotal = 0;
        }

        return populated;
    }

    private static PokedexAbility ParseAbility(int abilityId, JsonElement data)
    {
        var name = data.GetProperty("name").GetString() ?? "";

        var displayName = name;
        if (data.TryGetProperty("names", out var names))
        {
            foreach (var n in names.EnumerateArray())
            {
                if (n.GetProperty("language").GetProperty("name").GetString() == "en")
                {
                    displayName = n.GetProperty("name").GetString() ?? name;
                    break;
                }
            }
        }

        var effect = "";
        var shortEffect = "";
        if (data.TryGetProperty("effect_entries", out var effects))
        {
            foreach (var e in effects.EnumerateArray())
            {
                if (e.GetProperty("language").GetProperty("name").GetString() == "en")
                {
                    effect = e.TryGetProperty("effect", out var ef) ? ef.GetString() ?? "" : "";
                    shortEffect = e.TryGetProperty("short_effect", out var se) ? se.GetString() ?? "" : "";
                    break;
                }
            }
        }

        var flavorText = "";
        if (data.TryGetProperty("flavor_text_entries", out var ftes))
        {
            foreach (var ft in ftes.EnumerateArray())
            {
                if (ft.GetProperty("language").GetProperty("name").GetString() == "en")
                    flavorText = ft.GetProperty("flavor_text").GetString() ?? "";
            }
        }

        var genUrl = data.TryGetProperty("generation", out var gen) && gen.ValueKind != JsonValueKind.Null
            ? gen.GetProperty("url").GetString() ?? "" : "";
        var genStr = genUrl.TrimEnd('/').Split('/').Last();
        int.TryParse(genStr.Replace("generation-", "").Replace("i", "1").Replace("ii", "2"), out var generation);

        return new PokedexAbility
        {
            AbilityId = abilityId,
            Name = name,
            DisplayName = displayName,
            Effect = effect.Replace("\f", " ").Replace("\n", " ").Trim(),
            ShortEffect = shortEffect.Replace("\f", " ").Replace("\n", " ").Trim(),
            FlavorText = flavorText.Replace("\f", " ").Replace("\n", " ").Trim(),
            Generation = generation,
            IsMainSeries = data.TryGetProperty("is_main_series", out var ims) && ims.GetBoolean(),
            CachedAt = DateTime.UtcNow
        };
    }

    // ── Types ──────────────────────────────────────────────────────────────

    public async Task<PokedexType?> GetTypeAsync(int typeId)
        => await _context.PokedexTypes.FindAsync(typeId);

    public async Task<List<PokedexType>> GetAllTypesAsync()
        => await _context.PokedexTypes.OrderBy(t => t.TypeId).ToListAsync();

    public async Task<int> PopulateTypesAsync()
    {
        lock (_populateTypesLock)
        {
            if (_isPopulatingTypes) return 0;
            _isPopulatingTypes = true;
        }

        int populated = 0;

        try
        {
            // PokeAPI has 18 standard types (1-18) + 2 shadow/unknown (10001, 10002) — skip those
            for (int typeId = 1; typeId <= 18; typeId++)
            {
                try
                {
                    if (await _context.PokedexTypes.FindAsync(typeId) != null)
                    {
                        populated++;
                        continue;
                    }

                    var data = await FetchJsonAsync($"{POKEAPI_BASE}/type/{typeId}");
                    if (data == null) continue;

                    var type = ParseType(typeId, data.Value);
                    _context.PokedexTypes.Add(type);
                    await _context.SaveChangesAsync();
                    populated++;

                    await Task.Delay(200);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error populating type {typeId}: {ex.Message}");
                    _context.ChangeTracker.Clear();
                }
            }
        }
        finally
        {
            _isPopulatingTypes = false;
        }

        return populated;
    }

    private static PokedexType ParseType(int typeId, JsonElement data)
    {
        var name = data.GetProperty("name").GetString() ?? "";

        var dr = data.GetProperty("damage_relations");
        var damageRelations = new
        {
            doubleDamageTo = dr.GetProperty("double_damage_to").EnumerateArray()
                .Select(t => t.GetProperty("name").GetString()).ToList(),
            halfDamageTo = dr.GetProperty("half_damage_to").EnumerateArray()
                .Select(t => t.GetProperty("name").GetString()).ToList(),
            noDamageTo = dr.GetProperty("no_damage_to").EnumerateArray()
                .Select(t => t.GetProperty("name").GetString()).ToList(),
            doubleDamageFrom = dr.GetProperty("double_damage_from").EnumerateArray()
                .Select(t => t.GetProperty("name").GetString()).ToList(),
            halfDamageFrom = dr.GetProperty("half_damage_from").EnumerateArray()
                .Select(t => t.GetProperty("name").GetString()).ToList(),
            noDamageFrom = dr.GetProperty("no_damage_from").EnumerateArray()
                .Select(t => t.GetProperty("name").GetString()).ToList(),
        };

        var genUrl = data.TryGetProperty("generation", out var gen) && gen.ValueKind != JsonValueKind.Null
            ? gen.GetProperty("url").GetString() ?? "" : "";
        var genStr = genUrl.TrimEnd('/').Split('/').Last();
        int.TryParse(genStr.Replace("generation-", "").Replace("i", "1"), out var generation);

        return new PokedexType
        {
            TypeId = typeId,
            Name = name,
            DamageRelations = System.Text.Json.JsonSerializer.Serialize(damageRelations),
            Generation = generation,
            CachedAt = DateTime.UtcNow
        };
    }

    // ── Evolution Chains ──────────────────────────────────────────────────

    public async Task<PokedexEvolutionChain?> GetEvolutionChainAsync(int chainId)
        => await _context.PokedexEvolutionChains.FindAsync(chainId);

    public async Task<PokedexEvolutionChain?> GetEvolutionChainBySpeciesAsync(int speciesId)
    {
        var entry = await _context.PokedexEntries.FindAsync(speciesId);
        if (entry?.EvolutionChainId == null) return null;
        return await _context.PokedexEvolutionChains.FindAsync(entry.EvolutionChainId.Value);
    }

    public async Task<int> PopulateEvolutionChainsAsync(int startId, int endId)
    {
        lock (_populateChainsLock)
        {
            if (_isPopulatingChains) return 0;
            _isPopulatingChains = true;
            _populatingChainsCurrent = 0;
            _populatingChainsTotal = endId - startId + 1;
        }

        int populated = 0;

        try
        {
            for (int chainId = startId; chainId <= endId; chainId++)
            {
                _populatingChainsCurrent = chainId - startId + 1;

                try
                {
                    if (await _context.PokedexEvolutionChains.FindAsync(chainId) != null)
                    {
                        populated++;
                        continue;
                    }

                    var data = await FetchJsonAsync($"{POKEAPI_BASE}/evolution-chain/{chainId}");
                    if (data == null) continue;

                    // Store the full chain node JSON
                    var chainJson = data.Value.TryGetProperty("chain", out var chain)
                        ? chain.GetRawText() : "{}";

                    _context.PokedexEvolutionChains.Add(new PokedexEvolutionChain
                    {
                        ChainId = chainId,
                        ChainJson = chainJson,
                        CachedAt = DateTime.UtcNow
                    });
                    await _context.SaveChangesAsync();
                    populated++;

                    await Task.Delay(200);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error populating evolution chain {chainId}: {ex.Message}");
                    _context.ChangeTracker.Clear();
                }
            }
        }
        finally
        {
            _isPopulatingChains = false;
            _populatingChainsCurrent = 0;
            _populatingChainsTotal = 0;
        }

        return populated;
    }
}
