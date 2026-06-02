using System.Text.Json;
using BeastVault.Api.Application.Interfaces;
using BeastVault.Api.Application.Services;
using BeastVault.Api.Contracts;
using BeastVault.Api.Domain.Entities;
using BeastVault.Api.Helpers;
using BeastVault.Api.Infrastructure;
using BeastVault.Api.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
namespace BeastVault.Api.Endpoints;

public static class VaultPokedexEndpoints
{
    public static IEndpointRouteBuilder MapVaultPokedexEndpoints(this IEndpointRouteBuilder app)
    {
        var dex = app.MapGroup("/dex").WithTags("VaultPokedex").RequireAuthorization();

        // GET /dex/games — distinct origin games the user has Pokémon from
        dex.MapGet("games", async (AppDbContext db, HttpContext ctx) =>
        {
            var userId = ctx.GetUserId();
            if (userId == null) return Results.Unauthorized();

            var gameIds = await db.Pokemon
                .AsNoTracking()
                .Where(p => p.UserId == userId.Value)
                .Select(p => p.OriginGame)
                .Distinct()
                .OrderBy(id => id)
                .ToListAsync();

            var games = gameIds.Select(id => new { id, name = PkHexStringService.GetVersionName(id) }).ToList();
            return Results.Ok(games);
        })
        .WithName("GetDexGames")
        .WithSummary("Get distinct origin games present in the user's vault");

        // GET /dex?page=1&pageSize=30&generation=1&search=pika&unlockedOnly=false&originGame=52
        dex.MapGet("", async (
            AppDbContext db,
            HttpContext ctx,
            int page = 1,
            int pageSize = 30,
            int? generation = null,
            string? search = null,
            bool? unlockedOnly = null,
            int? originGame = null) =>
        {
            var userId = ctx.GetUserId();
            if (userId == null) return Results.Unauthorized();

            page = Math.Max(1, page);
            pageSize = Math.Clamp(pageSize, 1, 100);

            // All cached species + their default forms for types/sprites
            var speciesQuery = db.PokedexEntries.AsNoTracking();

            if (generation.HasValue)
                speciesQuery = speciesQuery.Where(s => s.Generation == generation.Value);

            if (!string.IsNullOrWhiteSpace(search))
                speciesQuery = speciesQuery.Where(s => s.Name.Contains(search.ToLower()));

            var allSpecies = await speciesQuery
                .OrderBy(s => s.SpeciesId)
                .ToListAsync();

            // Get all species IDs user owns — optionally scoped to a specific origin game
            var userPokemonQuery = db.Pokemon.Where(p => p.UserId == userId.Value);
            if (originGame.HasValue)
                userPokemonQuery = userPokemonQuery.Where(p => p.OriginGame == originGame.Value);

            var ownedSpeciesIds = await userPokemonQuery
                .Select(p => p.SpeciesId)
                .Distinct()
                .ToListAsync();
            var ownedSet = ownedSpeciesIds.ToHashSet();

            // Counts per species (always from all games so badge shows total)
            var ownedCounts = await db.Pokemon
                .Where(p => p.UserId == userId.Value && ownedSet.Contains(p.SpeciesId))
                .GroupBy(p => p.SpeciesId)
                .Select(g => new { SpeciesId = g.Key, Count = g.Count() })
                .ToDictionaryAsync(x => x.SpeciesId, x => x.Count);

            // Shiny species — where user has at least one shiny
            var shinySpeciesIds = await db.Pokemon
                .Where(p => p.UserId == userId.Value && p.IsShiny)
                .Select(p => p.SpeciesId)
                .Distinct()
                .ToHashSetAsync();

            // Default form per species for types + sprite
            var speciesIds = allSpecies.Select(s => s.SpeciesId).ToList();
            var defaultForms = await db.PokedexPokemon
                .AsNoTracking()
                .Where(p => speciesIds.Contains(p.SpeciesId) && p.IsDefault)
                .ToDictionaryAsync(p => p.SpeciesId);

            // Apply unlocked filter (when originGame is set, ownedSet is already game-scoped)
            var filtered = (unlockedOnly == true || originGame.HasValue)
                ? allSpecies.Where(s => ownedSet.Contains(s.SpeciesId)).ToList()
                : allSpecies;

            var total = filtered.Count;
            var paged = filtered
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToList();

            var items = paged.Select(s =>
            {
                defaultForms.TryGetValue(s.SpeciesId, out var form);

                var types = Array.Empty<string>();
                if (form != null)
                {
                    try
                    {
                        var typeList = JsonSerializer.Deserialize<List<JsonElement>>(form.Types);
                        types = typeList?
                            .Select(t => t.GetProperty("name").GetString() ?? "")
                            .Where(n => n != "")
                            .ToArray() ?? Array.Empty<string>();
                    }
                    catch { /* ignore bad JSON */ }
                }

                return new DexGridEntryDto(
                    s.SpeciesId,
                    s.Name,
                    s.Generation,
                    ownedSet.Contains(s.SpeciesId),
                    ownedCounts.GetValueOrDefault(s.SpeciesId, 0),
                    types,
                    form != null ? PokemonSpritesDto.ForPokemonId(form.PokemonId, form.Name) : null,
                    s.IsLegendary,
                    s.IsMythical,
                    shinySpeciesIds.Contains(s.SpeciesId)
                );
            }).ToList();

            return Results.Ok(new DexGridResponse(items, total, page, pageSize));
        })
        .WithName("GetVaultPokedex")
        .WithSummary("Get national Pokédex grid with user unlock status");

        // GET /dex/{speciesId}
        dex.MapGet("{speciesId:int}", async (
            int speciesId,
            AppDbContext db,
            IPokedexService pokedexService,
            IWikidexService wikidexService,
            IJaWikiService jaWikiService,
            HttpContext ctx) =>
        {
            var userId = ctx.GetUserId();
            if (userId == null) return Results.Unauthorized();

            var species = await db.PokedexEntries
                .AsNoTracking()
                .FirstOrDefaultAsync(s => s.SpeciesId == speciesId);

            if (species == null) return Results.NotFound();

            // Default form for this species
            var defaultForm = await db.PokedexPokemon
                .AsNoTracking()
                .Where(p => p.SpeciesId == speciesId)
                .OrderBy(p => p.PokemonId)
                .FirstOrDefaultAsync(p => p.IsDefault)
                ?? await db.PokedexPokemon
                    .AsNoTracking()
                    .Where(p => p.SpeciesId == speciesId)
                    .OrderBy(p => p.PokemonId)
                    .FirstOrDefaultAsync();

            // User's owned Pokémon of this species
            var ownedRaw = await db.Pokemon
                .AsNoTracking()
                .Where(p => p.UserId == userId.Value && p.SpeciesId == speciesId)
                .OrderBy(p => p.Id)
                .ToListAsync();

            // Build sprites
            PokemonSpritesDto? sprites = null;
            string[] types = Array.Empty<string>();
            object abilities = Array.Empty<object>();
            object baseStats = new { };
            object gameIndices = Array.Empty<object>();

            if (defaultForm != null)
            {
                sprites = PokemonSpritesDto.ForPokemonId(defaultForm.PokemonId, defaultForm.Name);

                types = ParseStringArray(defaultForm.Types, "name");
                try { abilities = JsonSerializer.Deserialize<object>(defaultForm.Abilities)!; } catch { }
                try { baseStats = JsonSerializer.Deserialize<object>(defaultForm.BaseStats)!; } catch { }
                try { gameIndices = JsonSerializer.Deserialize<object>(defaultForm.GameIndices)!; } catch { }
            }

            var eggGroups = Array.Empty<string>();
            try
            {
                eggGroups = JsonSerializer.Deserialize<string[]>(species.EggGroups) ?? Array.Empty<string>();
            }
            catch { }

            // Resolve form name and sprite for each owned Pokémon using PKHeX
            var owned = new List<DexOwnedPokemonDto>();
            foreach (var p in ownedRaw)
            {
                var formName = PkHexStringService.GetFormName(p.SpeciesId, p.Form);
                var formNameLower = formName?.ToLowerInvariant() ?? "";

                // Match PKHeX form name against PokeAPI pokemon name
                // e.g. "alola" matches "pikachu-alola-cap", "partner" matches "pikachu-partner-cap"
                BeastVault.Api.Domain.Entities.PokedexPokemon? formEntry = null;
                if (!string.IsNullOrEmpty(formNameLower) && p.Form != 0)
                {
                    formEntry = await db.PokedexPokemon.AsNoTracking()
                        .Where(dp => dp.SpeciesId == speciesId && dp.Name.Contains(formNameLower))
                        .OrderBy(dp => dp.PokemonId)
                        .FirstOrDefaultAsync();
                }
                if (formEntry == null)
                {
                    formEntry = await db.PokedexPokemon.AsNoTracking()
                        .Where(dp => dp.SpeciesId == speciesId && dp.IsDefault)
                        .FirstOrDefaultAsync()
                        ?? await db.PokedexPokemon.AsNoTracking()
                           .Where(dp => dp.SpeciesId == speciesId)
                           .OrderBy(dp => dp.PokemonId)
                           .FirstOrDefaultAsync();
                }

                string ownedSprite;
                if (p.IsShiny)
                    ownedSprite = $"/sprites/pokemon/shiny/{(formEntry ?? defaultForm)?.PokemonId ?? speciesId}.png";
                else
                    ownedSprite = $"/sprites/pokemon/{(formEntry ?? defaultForm)?.PokemonId ?? speciesId}.png";

                owned.Add(new DexOwnedPokemonDto(
                    p.Id,
                    p.Nickname,
                    p.IsShiny,
                    p.Level,
                    formName ?? "",
                    PkHexStringService.GetVersionName(p.OriginGame),
                    ownedSprite
                ));
            }

            // Fetch evolution chain if cached
            string? evolutionChainJson = null;
            if (species.EvolutionChainId.HasValue)
            {
                var chain = await db.PokedexEvolutionChains
                    .AsNoTracking()
                    .FirstOrDefaultAsync(c => c.ChainId == species.EvolutionChainId.Value);
                evolutionChainJson = chain?.ChainJson;
            }

            // --- Enriched data ---

            // Localized names from PokeAPI cache
            var localizedNames = new List<DexLocalizedNameDto>();
            string? japaneseName = null;
            string? japaneseRomanized = null;
            string? nameMeaning = null;

            if (!string.IsNullOrEmpty(species.LocalizedNames))
            {
                try
                {
                    using var namesDoc = JsonDocument.Parse(species.LocalizedNames);
                    if (namesDoc.RootElement.ValueKind == JsonValueKind.Object)
                    {
                        foreach (var property in namesDoc.RootElement.EnumerateObject())
                        {
                            var lang = PokedexTextFilters.NormalizeFlavorLanguage(property.Name);
                            var name = property.Value.GetString() ?? "";
                            if (!string.IsNullOrEmpty(lang) && !string.IsNullOrEmpty(name))
                                localizedNames.Add(new DexLocalizedNameDto(lang, name));
                        }
                    }
                    else if (namesDoc.RootElement.ValueKind == JsonValueKind.Array)
                    {
                        foreach (var n in namesDoc.RootElement.EnumerateArray())
                        {
                            var lang = n.TryGetProperty("language", out var lp) ? lp.GetString() ?? "" : "";
                            var name = n.TryGetProperty("name", out var np) ? np.GetString() ?? "" : "";
                            if (!string.IsNullOrEmpty(lang) && !string.IsNullOrEmpty(name))
                                localizedNames.Add(new DexLocalizedNameDto(PokedexTextFilters.NormalizeFlavorLanguage(lang), name));
                        }
                    }

                    var ja = localizedNames.FirstOrDefault(n => n.Language == "ja");
                    var jaRomaji = localizedNames.FirstOrDefault(n => n.Language == "roomaji");
                    japaneseName = ja?.Name;
                    japaneseRomanized = jaRomaji?.Name;
                }
                catch { }
            }

            // Flavor entries from enrichment table. Prefer Bulbapedia when both sources provide the same game.
            var flavorRows = await db.PokedexFlavorEntries
                .AsNoTracking()
                .Where(f => f.SpeciesId == speciesId)
                .ToListAsync();

            // Auto-backfill if Spanish or Japanese entries are missing (lazy per-species fetch)
            var existingLangs = flavorRows
                .Where(f => PokedexTextFilters.IsDisplayableFlavorText(f.Text))
                .Select(f => PokedexTextFilters.NormalizeFlavorLanguage(f.Language))
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            var needsReload = false;

            // PokeAPI backfill — covers English and Spanish/Japanese from Gen 6+
            if (PokedexTextFilters.TargetFlavorLanguages.Any(l => !existingLangs.Contains(l)))
            {
                await pokedexService.BackfillEntriesAndLocationsAsync(speciesId, speciesId);
                needsReload = true;
            }

            // WikiDex backfill — fills in Spanish for all generations (Gen 1–9).
            // Run once per species: if no WikiDex-sourced entry exists yet, fetch now.
            var hasWikiDexEs = flavorRows.Any(f => f.Source == CacheSource.WikiDex);
            if (!hasWikiDexEs)
            {
                await wikidexService.FetchEsFlavorEntriesAsync(speciesId);
                needsReload = true;
            }

            // JaWiki backfill — fills in Japanese for all generations (Gen 1–9).
            // The wiki is Cloudflare-protected; returns 0 if blocked, succeeds otherwise.
            var hasJaWiki = flavorRows.Any(f => f.Source == CacheSource.JaWiki);
            if (!hasJaWiki)
            {
                await jaWikiService.FetchJaFlavorEntriesAsync(speciesId);
                needsReload = true;
            }

            if (needsReload)
            {
                flavorRows = await db.PokedexFlavorEntries
                    .AsNoTracking()
                    .Where(f => f.SpeciesId == speciesId)
                    .ToListAsync();
            }
            var flavorEntries = flavorRows
                .Where(f => PokedexTextFilters.IsTargetFlavorLanguage(f.Language)
                    && PokedexTextFilters.IsDisplayableFlavorText(f.Text))
                .GroupBy(f => new { Language = PokedexTextFilters.NormalizeFlavorLanguage(f.Language), f.GameVersion })
                .Select(g => g.OrderByDescending(f => f.Source == CacheSource.Bulbapedia).ThenByDescending(f => f.CachedAt).First())
                .OrderBy(f => PokedexTextFilters.NormalizeFlavorLanguage(f.Language))
                .ThenBy(f => GameSortOrder(f.GameVersion))
                .ThenBy(f => f.GameVersion)
                .Select(f => new DexFlavorEntryDto(
                    PokedexTextFilters.NormalizeFlavorLanguage(f.Language),
                    f.GameVersion,
                    PokedexTextFilters.CleanFlavorText(f.Text),
                    f.Source.ToString()))
                .ToList();

            // Locations from enrichment table. Merge PokeAPI + Bulbapedia and dedupe exact normalized rows.
            var locationRows = await db.PokedexLocations
                .AsNoTracking()
                .Where(l => l.SpeciesId == speciesId)
                .ToListAsync();
            var locations = locationRows
                .Where(l => PokedexTextFilters.IsDisplayableLocation(l.Location, l.Method))
                .GroupBy(l => $"{l.Game}|{l.Location}|{l.Method}")
                .Select(g => g.OrderByDescending(l => l.Source == CacheSource.Bulbapedia).ThenByDescending(l => l.CachedAt).First())
                .OrderBy(l => GameSortOrder(l.Game))
                .ThenBy(l => l.Location)
                .Select(l => new DexLocationDto(l.Game, l.Location, l.Method, l.Source.ToString()))
                .ToList();

            // All forms for this species
            var allForms = await db.PokedexPokemon
                .AsNoTracking()
                .Where(p => p.SpeciesId == speciesId)
                .OrderBy(p => p.PokemonId)
                .ToListAsync();

            var formDtos = allForms.Select(f =>
            {
                var fTypes = ParseStringArray(f.Types, "name");
                object[] fAbilities;
                try { fAbilities = JsonSerializer.Deserialize<object[]>(f.Abilities) ?? Array.Empty<object>(); }
                catch { fAbilities = Array.Empty<object>(); }
                var fSprites = PokemonSpritesDto.ForPokemonId(f.PokemonId, f.Name);
                return new DexFormDto(f.PokemonId, f.Name, f.IsDefault, fTypes, fAbilities, fSprites);
            }).ToList();

            // Sprites by generation — prefer normalized Bulbapedia local sprites, then local PokeAPI fallback routes.
            var spritesByGen = new List<DexGenerationSpritesDto>();
            var normalizedSprites = await db.PokedexSpriteEntries
                .AsNoTracking()
                .Where(s => s.SpeciesId == speciesId)
                .OrderBy(s => s.SortOrder)
                .ToListAsync();

            if (normalizedSprites.Count > 0)
            {
                spritesByGen.AddRange(normalizedSprites.Select(s => new DexGenerationSpritesDto(
                    s.Generation,
                    s.DisplayLabel,
                    s.NormalLocalPath,
                    s.ShinyLocalPath,
                    s.BackLocalPath,
                    s.BackShinyLocalPath,
                    s.Source.ToString()
                )));
            }
            else if (defaultForm != null && !string.IsNullOrEmpty(defaultForm.Sprites))
            {
                try
                {
                    var doc = JsonDocument.Parse(defaultForm.Sprites);
                    if (doc.RootElement.TryGetProperty("versions", out var versions))
                    {
                        int genNum = 0;
                        foreach (var gen in versions.EnumerateObject())
                        {
                            genNum++;
                            foreach (var game in gen.Value.EnumerateObject())
                            {
                                string? normal = null, shiny = null, back = null, backShiny = null;
                                if (game.Value.TryGetProperty("front_default", out var fd) && fd.ValueKind == JsonValueKind.String)
                                    normal = fd.GetString();
                                if (game.Value.TryGetProperty("front_shiny", out var fs) && fs.ValueKind == JsonValueKind.String)
                                    shiny = fs.GetString();
                                if (game.Value.TryGetProperty("back_default", out var bd) && bd.ValueKind == JsonValueKind.String)
                                    back = bd.GetString();
                                if (game.Value.TryGetProperty("back_shiny", out var bs) && bs.ValueKind == JsonValueKind.String)
                                    backShiny = bs.GetString();

                                if (normal != null || shiny != null)
                                {
                                    var localNormal = BuildVersionSpriteRoute(defaultForm.PokemonId, game.Name, "front", normal);
                                    var localShiny = BuildVersionSpriteRoute(defaultForm.PokemonId, game.Name, "shiny", shiny);
                                    var localBack = BuildVersionSpriteRoute(defaultForm.PokemonId, game.Name, "back", back);
                                    var localBackShiny = BuildVersionSpriteRoute(defaultForm.PokemonId, game.Name, "back-shiny", backShiny);
                                    spritesByGen.Add(new DexGenerationSpritesDto(
                                        genNum, game.Name, localNormal, localShiny, localBack, localBackShiny, "PokeApi"));
                                }
                            }
                        }
                    }
                }
                catch { }
            }

            // Bulbapedia cache status
            var bulbCache = await db.BulbapediaCache
                .AsNoTracking()
                .FirstOrDefaultAsync(b => b.SpeciesId == speciesId);

            var cacheStatus = new DexCacheStatusDto(
                PokeApiCached: species != null,
                BulbapediaCached: bulbCache != null,
                BulbapediaStatus: bulbCache?.Status.ToString(),
                BulbapediaNormalized: bulbCache?.NormalizedStatus == ParseStatus.Success,
                BulbapediaNormalizedStatus: bulbCache?.NormalizedStatus.ToString(),
                BulbapediaEntriesCount: bulbCache?.EntriesCount ?? 0,
                BulbapediaLocationsCount: bulbCache?.LocationsCount ?? 0,
                BulbapediaSpritesCount: bulbCache?.SpritesCount ?? 0
            );

            nameMeaning = bulbCache?.NameMeaning;
            var speciesDetailSource = species!;
            var flavorText = PokedexTextFilters.IsDisplayableFlavorText(speciesDetailSource.FlavorText)
                ? PokedexTextFilters.CleanFlavorText(speciesDetailSource.FlavorText)
                : "";

            var detail = new DexSpeciesDetailDto(
                speciesDetailSource.SpeciesId,
                speciesDetailSource.Name,
                flavorText,
                speciesDetailSource.Genus,
                speciesDetailSource.Generation,
                speciesDetailSource.IsLegendary,
                speciesDetailSource.IsMythical,
                speciesDetailSource.IsBaby,
                speciesDetailSource.Color,
                types,
                abilities,
                baseStats,
                speciesDetailSource.CaptureRate,
                speciesDetailSource.BaseHappiness,
                speciesDetailSource.GenderRate,
                eggGroups,
                gameIndices,
                sprites,
                owned.Count > 0,
                owned,
                evolutionChainJson,
                localizedNames,
                japaneseName,
                japaneseRomanized,
                nameMeaning,
                flavorEntries,
                locations,
                spritesByGen,
                formDtos,
                cacheStatus
            );

            return Results.Ok(detail);
        })
        .WithName("GetVaultPokedexSpecies")
        .WithSummary("Get species details + user's owned Pokémon of that species");

        return app;
    }

    // ── Sprite URL helpers ────────────────────────────────────────────────────

    private static string? BuildVersionSpriteRoute(int pokemonId, string gameSlug, string kind, string? externalUrl)
    {
        if (string.IsNullOrWhiteSpace(externalUrl)) return null;
        var extension = externalUrl.Contains(".gif", StringComparison.OrdinalIgnoreCase) ? "gif" : "png";
        return $"/sprites/pokemon/version/{pokemonId}/{gameSlug}/{kind}.{extension}";
    }

    private static string? ExtractFrontDefault(string spritesJson)
    {
        try
        {
            var doc = JsonDocument.Parse(spritesJson);
            if (doc.RootElement.TryGetProperty("front_default", out var fd) && fd.ValueKind == JsonValueKind.String)
                return fd.GetString();
        }
        catch { }
        return null;
    }

    private static string? ExtractFrontShiny(string spritesJson)
    {
        try
        {
            var doc = JsonDocument.Parse(spritesJson);
            if (doc.RootElement.TryGetProperty("front_shiny", out var fs) && fs.ValueKind == JsonValueKind.String)
                return fs.GetString();
        }
        catch { }
        return null;
    }

    private static string? ExtractOfficialArtwork(string spritesJson)
    {
        try
        {
            var doc = JsonDocument.Parse(spritesJson);
            if (doc.RootElement.TryGetProperty("other", out var other) &&
                other.TryGetProperty("official-artwork", out var oa) &&
                oa.TryGetProperty("front_default", out var fd) &&
                fd.ValueKind == JsonValueKind.String)
                return fd.GetString();
        }
        catch { }
        return null;
    }

    private static int GameSortOrder(string game)
    {
        var normalized = game.ToLowerInvariant();
        return normalized switch
        {
            "red" => 10,
            "green" => 11,
            "blue" => 12,
            "yellow" => 13,
            "stadium" => 14,
            "gold" => 20,
            "silver" => 21,
            "crystal" => 22,
            "stadium-2" => 23,
            "ruby" => 30,
            "sapphire" => 31,
            "emerald" => 32,
            "firered" => 33,
            "leafgreen" => 34,
            "diamond" => 40,
            "pearl" => 41,
            "platinum" => 42,
            "heartgold" => 43,
            "soulsilver" => 44,
            "black" => 50,
            "white" => 51,
            "black-2" => 52,
            "white-2" => 53,
            "x" => 60,
            "y" => 61,
            "omega-ruby" => 62,
            "alpha-sapphire" => 63,
            "sun" => 70,
            "moon" => 71,
            "ultra-sun" => 72,
            "ultra-moon" => 73,
            "lets-go-pikachu" => 74,
            "lets-go-eevee" => 75,
            "sword" => 80,
            "shield" => 81,
            "brilliant-diamond" => 82,
            "shining-pearl" => 83,
            "legends-arceus" => 84,
            "scarlet" => 90,
            "violet" => 91,
            "legends-za" => 92,
            "pokopia" => 93,
            "mega-dimension" => 94,
            _ => 999
        };
    }

    private static string[] ParseStringArray(string json, string propertyName)
    {
        try
        {
            var list = JsonSerializer.Deserialize<List<JsonElement>>(json);
            return list?
                .Select(e => e.TryGetProperty(propertyName, out var p) ? p.GetString() ?? "" : "")
                .Where(s => s != "")
                .ToArray() ?? Array.Empty<string>();
        }
        catch { return Array.Empty<string>(); }
    }
}
