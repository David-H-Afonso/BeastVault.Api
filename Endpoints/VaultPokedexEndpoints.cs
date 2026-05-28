using System.Text.Json;
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

        // GET /dex?page=1&pageSize=30&generation=1&search=pika&unlockedOnly=false
        dex.MapGet("", async (
            AppDbContext db,
            HttpContext ctx,
            int page = 1,
            int pageSize = 30,
            int? generation = null,
            string? search = null,
            bool? unlockedOnly = null) =>
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

            // Get all species IDs user owns (just a set of ints — fast)
            var ownedSpeciesIds = await db.Pokemon
                .Where(p => p.UserId == userId.Value)
                .Select(p => p.SpeciesId)
                .Distinct()
                .ToListAsync();
            var ownedSet = ownedSpeciesIds.ToHashSet();

            // Counts per species
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

            // Apply unlocked filter after in-memory (simpler and dataset is bounded)
            var filtered = unlockedOnly == true
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

            var detail = new DexSpeciesDetailDto(
                species.SpeciesId,
                species.Name,
                species.FlavorText,
                species.Genus,
                species.Generation,
                species.IsLegendary,
                species.IsMythical,
                species.IsBaby,
                species.Color,
                types,
                abilities,
                baseStats,
                species.CaptureRate,
                species.BaseHappiness,
                species.GenderRate,
                eggGroups,
                gameIndices,
                sprites,
                owned.Count > 0,
                owned,
                evolutionChainJson
            );

            return Results.Ok(detail);
        })
        .WithName("GetVaultPokedexSpecies")
        .WithSummary("Get species details + user's owned Pokémon of that species");

        return app;
    }

    // ── Sprite URL helpers ────────────────────────────────────────────────────

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
