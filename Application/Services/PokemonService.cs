using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using BeastVault.Api.Contracts;
using BeastVault.Api.Domain.Entities;
using BeastVault.Api.Domain.Services;
using BeastVault.Api.Domain.ValueObjects;
using BeastVault.Api.Infrastructure;
using BeastVault.Api.Infrastructure.Services;
using BeastVault.Api.Application.Interfaces;
using BeastVault.Api.Application.Mapping;

namespace BeastVault.Api.Application.Services;

public class PokemonService : IPokemonService
{
    private readonly AppDbContext _db;
    private readonly FileStorageService _storage;

    public PokemonService(AppDbContext db, FileStorageService storage)
    {
        _db = db;
        _storage = storage;
    }

    public async Task<PokemonListResponseDto> GetPokemonListAsync(int userId, AdvancedPokemonQuery q)
    {
        q = await ResolveSearchHelpersAsync(q);

        var baseQuery = _db.Pokemon.AsNoTracking()
            .Where(p => p.UserId == userId)
            .AsQueryable();

        var query = PokemonQueryService.BuildQuery(baseQuery, q);
        var total = await query.CountAsync();

        var items = await query
            .Skip(q.Skip)
            .Take(q.Take)
            .Join(_db.Files, p => p.FileId, f => f.Id, (p, f) => new { Pokemon = p, File = f })
            .Select(pf => new
            {
                Id = pf.Pokemon.Id,
                SpeciesId = pf.Pokemon.SpeciesId,
                Form = PokemonFormService.GetDisplayForm(pf.Pokemon, pf.File.Format),
                Nickname = pf.Pokemon.Nickname,
                Level = pf.Pokemon.Level,
                IsShiny = pf.Pokemon.IsShiny,
                Favorite = pf.Pokemon.Favorite,
                IsEgg = pf.Pokemon.IsEgg,
                BallId = pf.Pokemon.BallId,
                TeraType = pf.Pokemon.TeraType,
                HeldItemId = pf.Pokemon.HeldItemId,
                Gender = pf.Pokemon.Gender,
                SpriteKey = pf.Pokemon.SpriteKey,
                OriginGeneration = PokemonGameInfoService.GetSpeciesOriginGeneration(pf.Pokemon.SpeciesId),
                CapturedGeneration = PokemonGameInfoService.GetCapturedGeneration(pf.Pokemon.OriginGame, pf.File.Format),
                CanGigantamax = pf.Pokemon.CanGigantamax,
                HasMegaStone = PokemonFormService.CheckHasMegaStone(pf.Pokemon),
                pf.File.ImportedAt
            })
            .ToListAsync();

        // Batch-load tags
        var pokemonIds = items.Select(i => i.Id).ToList();
        var pokemonTags = await _db.PokemonTags
            .Where(pt => pokemonIds.Contains(pt.PokemonId) && (pt.Tag.UserId == null || pt.Tag.UserId == userId))
            .Include(pt => pt.Tag)
            .GroupBy(pt => pt.PokemonId)
            .ToDictionaryAsync(
                g => g.Key,
                g => g.Select(pt => new TagDto
                {
                    Id = pt.Tag.Id,
                    Name = pt.Tag.Name,
                    ImagePath = pt.Tag.ImagePath,
                    PokemonCount = 0,
                    Category = pt.Tag.Category.ToString(),
                    ColorHex = pt.Tag.ColorHex,
                    SortOrder = pt.Tag.SortOrder,
                    Description = pt.Tag.Description
                })
                .OrderBy(t => t.Name)
                .ToList()
            );

        // Batch-load box membership for the result set
        var boxedPokemonIds = await _db.PokemonBoxSlots
            .Where(s => pokemonIds.Contains(s.PokemonId))
            .Select(s => s.PokemonId)
            .ToHashSetAsync();

        // Batch-load Pokédex data for enrichment (types + sprites)
        var uniqueSpeciesIds = items.Select(i => i.SpeciesId).Distinct().ToList();
        var pokedexEntries = await _db.PokedexEntries
            .Where(e => uniqueSpeciesIds.Contains(e.SpeciesId))
            .ToDictionaryAsync(e => e.SpeciesId);

        // Build mapping: (speciesId, form) -> PokeAPI pokemonId using Varieties
        var neededPokemonIds = new HashSet<int>();
        var formToPokemonId = new Dictionary<string, int>();

        foreach (var item in items)
        {
            var pokeApiPokemonId = ResolvePokeApiPokemonId(item.SpeciesId, item.Form, item.CanGigantamax, item.HasMegaStone, pokedexEntries);
            var key = $"{item.SpeciesId}-{item.Form}-{item.CanGigantamax}-{item.HasMegaStone}";
            formToPokemonId[key] = pokeApiPokemonId;
            neededPokemonIds.Add(pokeApiPokemonId);
        }

        var pokedexPokemon = await _db.PokedexPokemon
            .Where(p => neededPokemonIds.Contains(p.PokemonId))
            .ToDictionaryAsync(p => p.PokemonId);

        // Also load all variants per species for accurate form-name matching
        // (variety-index lookup can fail for forms like Pikachu caps where PKHeX index != PokeAPI index)
        var allSpeciesVariants = await _db.PokedexPokemon
            .AsNoTracking()
            .Where(p => uniqueSpeciesIds.Contains(p.SpeciesId))
            .ToListAsync();
        var pokedexPokemonBySpecies = allSpeciesVariants
            .GroupBy(p => p.SpeciesId)
            .ToDictionary(g => g.Key, g => g.ToList());

        var resultItems = items.Select(item =>
        {
            string rawFormName = PkHexStringService.GetFormName(item.SpeciesId, item.Form);
            string formName = rawFormName;

            if (item.HasMegaStone && item.Form > 0)
            {
                formName = (item.SpeciesId, item.Form) switch
                {
                    (6, 1) => "Mega X",
                    (6, _) => "Mega Y",
                    (26, 1) => "Mega X",
                    (26, _) => "Mega Y",
                    (150, 1) => "Mega X",
                    (150, _) => "Mega Y",
                    (359, 2) => "Mega Z",
                    (445, 2) => "Mega Z",
                    (448, 2) => "Mega Z",
                    (678, 2) => "Mega (Female)",
                    _ => "Mega"
                };
            }

            // Resolve enrichment data from Pokédex cache
            // For non-default forms, prefer name-based lookup over variety-index-based lookup
            PokedexPokemon? cachedPokemon = null;
            if (item.Form != 0 && !string.IsNullOrEmpty(rawFormName)
                && pokedexPokemonBySpecies.TryGetValue(item.SpeciesId, out var speciesVariants))
            {
                var formNameLower = rawFormName.ToLowerInvariant();
                cachedPokemon = speciesVariants.FirstOrDefault(p => p.Name.Contains(formNameLower))
                    ?? speciesVariants.FirstOrDefault(p => p.IsDefault)
                    ?? speciesVariants.OrderBy(p => p.PokemonId).FirstOrDefault();
            }

            if (cachedPokemon == null)
            {
                var key = $"{item.SpeciesId}-{item.Form}-{item.CanGigantamax}-{item.HasMegaStone}";
                var pokeApiId = formToPokemonId.GetValueOrDefault(key, item.SpeciesId);
                if (!pokedexPokemon.TryGetValue(pokeApiId, out cachedPokemon)
                    && pokedexPokemonBySpecies.TryGetValue(item.SpeciesId, out var fallbackVariants))
                {
                    cachedPokemon = fallbackVariants.FirstOrDefault(p => p.IsDefault)
                        ?? fallbackVariants.OrderBy(p => p.PokemonId).FirstOrDefault();
                }
            }

            pokedexEntries.TryGetValue(item.SpeciesId, out var cachedSpecies);

            var (type1, type2) = ExtractTypes(cachedPokemon);
            var sprites = BuildSpritesDto(cachedPokemon, cachedSpecies);
            var ballName = PkHexStringService.GetBallName(item.BallId);
            var ballSpriteUrl = BuildBallSpriteUrl(item.BallId, ballName);

            return new PokemonListItemDto
            {
                Id = item.Id,
                SpeciesId = item.SpeciesId,
                SpeciesName = PkHexStringService.GetSpeciesName(item.SpeciesId),
                Form = item.Form,
                FormName = formName,
                Nickname = item.Nickname,
                Level = item.Level,
                IsShiny = item.IsShiny,
                Favorite = item.Favorite,
                IsEgg = item.IsEgg,
                BallId = item.BallId,
                TeraType = item.TeraType,
                HeldItemId = item.HeldItemId,
                Gender = item.Gender,
                SpriteKey = item.SpriteKey,
                OriginGeneration = item.OriginGeneration,
                CapturedGeneration = item.CapturedGeneration,
                CanGigantamax = item.CanGigantamax,
                HasMegaStone = item.HasMegaStone,
                ImportedAt = item.ImportedAt,
                Tags = pokemonTags.GetValueOrDefault(item.Id, new List<TagDto>()),
                Type1 = type1,
                Type2 = type2,
                BallName = ballName,
                BallSpriteUrl = ballSpriteUrl,
                Sprites = sprites,
                IsBoxed = boxedPokemonIds.Contains(item.Id)
            };
        }).ToList();

        var stats = PokemonQueryService.GetQueryStats(q);

        return new PokemonListResponseDto(resultItems, total, stats);
    }

    public async Task<PokemonSummaryDto> GetPokemonSummaryAsync(int userId)
    {
        var ownedPokemon = _db.Pokemon.AsNoTracking().Where(p => p.UserId == userId);

        var counts = new PokemonSummaryCountsDto(
            Total: await ownedPokemon.CountAsync(),
            Favorites: await ownedPokemon.CountAsync(p => p.Favorite),
            Shiny: await ownedPokemon.CountAsync(p => p.IsShiny),
            Eggs: await ownedPokemon.CountAsync(p => p.IsEgg));

        var recentImportRows = await ownedPokemon
            .Join(
                _db.Files.AsNoTracking().Where(f => f.UserId == userId),
                pokemon => pokemon.FileId,
                file => file.Id,
                (pokemon, file) => new
                {
                    pokemon.Id,
                    pokemon.SpeciesId,
                    pokemon.Nickname,
                    file.ImportedAt,
                    FileName = file.OriginalFileName ?? file.FileName
                })
            .OrderByDescending(import => import.ImportedAt)
            .ThenByDescending(import => import.Id)
            .Take(10)
            .ToListAsync();

        var recentImports = recentImportRows
            .Select(import => new PokemonRecentImportDto(
                import.Id,
                import.SpeciesId,
                PkHexStringService.GetSpeciesName(import.SpeciesId),
                import.Nickname,
                import.ImportedAt,
                import.FileName))
            .ToList();

        var tags = await _db.PokemonTags
            .AsNoTracking()
            .Where(pt => pt.Pokemon.UserId == userId &&
                (pt.Tag.UserId == null || pt.Tag.UserId == userId))
            .GroupBy(pt => new { pt.TagId, pt.Tag.Name })
            .Select(group => new PokemonSummaryTagDto(group.Key.TagId, group.Key.Name, group.Count()))
            .OrderByDescending(tag => tag.PokemonCount)
            .ThenBy(tag => tag.Name)
            .ToListAsync();

        return new PokemonSummaryDto(counts, recentImports, tags);
    }

    public async Task<TagFacetCountsDto> GetTagFacetCountsAsync(int userId, AdvancedPokemonQuery q)
    {
        // Strip tag include/exclude filters and pagination so the counts reflect the
        // current search + non-tag filters only. This answers "if I picked tag X,
        // how many of my current matches would remain?" for every tag.
        var facetQuery = q with
        {
            TagIds = null,
            TagNames = null,
            AnyTagIds = null,
            AnyTagNames = null,
            ExcludedTagIds = null,
            HasNoTags = null,
            Skip = 0,
            Take = int.MaxValue
        };

        facetQuery = await ResolveSearchHelpersAsync(facetQuery);

        var baseQuery = _db.Pokemon.AsNoTracking().Where(p => p.UserId == userId);
        var filtered = PokemonQueryService.BuildQuery(baseQuery, facetQuery);

        var matchingIds = await filtered.Select(p => p.Id).ToListAsync();

        var counts = await _db.PokemonTags
            .Where(pt => matchingIds.Contains(pt.PokemonId))
            .GroupBy(pt => pt.TagId)
            .Select(g => new { TagId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(g => g.TagId, g => g.Count);

        return new TagFacetCountsDto
        {
            Total = matchingIds.Count,
            Counts = counts
        };
    }

    private async Task<AdvancedPokemonQuery> ResolveSearchHelpersAsync(AdvancedPokemonQuery q)
    {
        if (string.IsNullOrWhiteSpace(q.Search))
            return q;

        var search = q.Search.Trim();
        var speciesIds = PokemonGameInfoService.GetSpeciesIdsByName(search).Distinct().ToArray();

        var itemIds = await _db.PokedexItems
            .AsNoTracking()
            .Where(i => i.Name.Contains(search) || i.DisplayName.Contains(search))
            .Select(i => i.ItemId)
            .Distinct()
            .ToArrayAsync();

        var moveIds = await _db.PokedexMoves
            .AsNoTracking()
            .Where(m => m.Name.Contains(search) || m.DisplayName.Contains(search))
            .Select(m => m.MoveId)
            .Distinct()
            .ToArrayAsync();

        return q with
        {
            SearchSpeciesIds = speciesIds,
            SearchHeldItemIds = itemIds,
            SearchMoveIds = moveIds
        };
    }

    /// <summary>
    /// Resolves the PokeAPI pokemon ID for a given species+form combination using cached Varieties data.
    /// </summary>
    private static int ResolvePokeApiPokemonId(int speciesId, int form, bool canGigantamax, bool hasMegaStone, Dictionary<int, PokedexEntry> entries)
    {
        if (!entries.TryGetValue(speciesId, out var entry))
            return speciesId; // fallback to speciesId as pokemonId (works for form 0)

        try
        {
            var varieties = JsonSerializer.Deserialize<List<JsonElement>>(entry.Varieties);
            if (varieties == null || varieties.Count == 0)
                return speciesId;

            // For Gigantamax, look for a gmax variety
            if (canGigantamax)
            {
                var gmaxVariety = varieties.FirstOrDefault(v =>
                    v.TryGetProperty("name", out var n) && (n.GetString()?.Contains("-gmax") ?? false));
                if (gmaxVariety.ValueKind != JsonValueKind.Undefined && gmaxVariety.TryGetProperty("id", out var gmaxId))
                    return gmaxId.GetInt32();
            }

            // For Mega evolutions, look for mega variety matching the form
            if (hasMegaStone && form > 0)
            {
                var megaVarieties = varieties.Where(v =>
                    v.TryGetProperty("name", out var n) && (n.GetString()?.Contains("-mega") ?? false)).ToList();

                // Special cases: Charizard, Mewtwo have mega-x and mega-y
                if (speciesId is 6 or 150)
                {
                    var suffix = form == 1 ? "-mega-x" : "-mega-y";
                    var megaMatch = megaVarieties.FirstOrDefault(v =>
                        v.TryGetProperty("name", out var n) && (n.GetString()?.EndsWith(suffix) ?? false));
                    if (megaMatch.ValueKind != JsonValueKind.Undefined && megaMatch.TryGetProperty("id", out var megaId))
                        return megaId.GetInt32();
                }
                else if (megaVarieties.Count > 0)
                {
                    if (megaVarieties[0].TryGetProperty("id", out var megaId))
                        return megaId.GetInt32();
                }
            }

            // Standard form lookup
            if (form < varieties.Count && varieties[form].TryGetProperty("id", out var varId))
                return varId.GetInt32();
        }
        catch { /* fallback */ }

        return speciesId;
    }

    /// <summary>
    /// Extracts type1 and type2 from cached PokedexPokemon Types JSON.
    /// </summary>
    private static (string? type1, string? type2) ExtractTypes(PokedexPokemon? cached)
    {
        if (cached == null) return (null, null);

        try
        {
            var types = JsonSerializer.Deserialize<List<JsonElement>>(cached.Types);
            if (types == null || types.Count == 0) return (null, null);

            var sorted = types
                .OrderBy(t => t.TryGetProperty("slot", out var s) ? s.GetInt32() : 99)
                .ToList();

            var type1 = sorted.ElementAtOrDefault(0).TryGetProperty("name", out var n1) ? n1.GetString() : null;
            var type2 = sorted.Count > 1 && sorted[1].TryGetProperty("name", out var n2) ? n2.GetString() : null;

            return (type1, type2);
        }
        catch { return (null, null); }
    }

    /// <summary>
    /// Builds sprite URLs from cached PokedexPokemon Sprites JSON.
    /// </summary>
    private static PokemonSpritesDto BuildSpritesDto(PokedexPokemon? cached, PokedexEntry? species)
    {
        if (cached == null) return new PokemonSpritesDto();
        return PokemonSpritesDto.ForPokemonId(cached.PokemonId, cached.Name);
    }

    /// <summary>
    /// Builds a Pokéball sprite URL from the ball ID and name.
    /// PKHeX Ball enum values 27-36 are Legends Arceus balls which use "la-" prefix in PokeAPI.
    /// </summary>
    private static readonly Dictionary<int, string> _ballSpriteOverrides = new()
    {
        { 27, "la-poke-ball" },
        { 28, "la-great-ball" },
        { 29, "la-ultra-ball" },
        { 30, "la-feather-ball" },
        { 31, "la-wing-ball" },
        { 32, "la-jet-ball" },
        { 33, "la-heavy-ball" },
        { 34, "la-leaden-ball" },
        { 35, "la-gigaton-ball" },
        { 36, "la-origin-ball" },
    };

    private static string BuildBallSpriteUrl(int ballId, string ballName)
    {
        if (string.IsNullOrEmpty(ballName) || ballName == "Unknown") return "";
        return PokemonDisplayMapper.ResolveBallSpriteUrl(ballId, ballName);
    }

    public async Task<PokemonDetailDto?> GetPokemonByIdAsync(int userId, int pokemonId)
    {
        var p = await _db.Pokemon.AsNoTracking().FirstOrDefaultAsync(x => x.Id == pokemonId && x.UserId == userId);
        if (p == null) return null;

        var file = await _db.Files.AsNoTracking().FirstOrDefaultAsync(f => f.Id == p.FileId);
        var stats = await _db.Stats.AsNoTracking().FirstOrDefaultAsync(x => x.PokemonId == p.Id);
        var moves = await _db.Moves.AsNoTracking().Where(x => x.PokemonId == p.Id).OrderBy(x => x.Slot).ToListAsync();
        var relearnMoves = await _db.RelearnMoves.AsNoTracking().Where(x => x.PokemonId == p.Id).OrderBy(x => x.Slot).ToListAsync();

        return new PokemonDetailDto(p, stats, moves, relearnMoves, file?.Format ?? "");
    }

    public async Task<string?> GetShowdownExportAsync(int userId, int pokemonId)
    {
        var p = await _db.Pokemon.AsNoTracking().FirstOrDefaultAsync(x => x.Id == pokemonId && x.UserId == userId);
        if (p == null) return null;

        var stats = await _db.Stats.AsNoTracking().FirstOrDefaultAsync(x => x.PokemonId == p.Id);
        var moves = await _db.Moves.AsNoTracking().Where(x => x.PokemonId == p.Id).OrderBy(x => x.Slot).ToListAsync();

        return ShowdownExport.From(p, stats, moves);
    }

    public async Task<bool> UpdatePokemonAsync(int userId, int pokemonId, UpdatePokemonDto dto)
    {
        var p = await _db.Pokemon.FirstOrDefaultAsync(x => x.Id == pokemonId && x.UserId == userId);
        if (p == null) return false;

        if (dto.Favorite.HasValue) p.Favorite = dto.Favorite.Value;
        if (dto.Notes is not null) p.Notes = dto.Notes;
        await _db.SaveChangesAsync();

        return true;
    }

    public async Task<bool> UpdateFavoriteAsync(int userId, int pokemonId, bool favorite)
    {
        var updated = await _db.Pokemon
            .Where(pokemon => pokemon.Id == pokemonId && pokemon.UserId == userId)
            .ExecuteUpdateAsync(update => update.SetProperty(pokemon => pokemon.Favorite, favorite));
        return updated == 1;
    }

    public async Task<bool> UpdateNotesAsync(int userId, int pokemonId, string? notes)
    {
        var updated = await _db.Pokemon
            .Where(pokemon => pokemon.Id == pokemonId && pokemon.UserId == userId)
            .ExecuteUpdateAsync(update => update.SetProperty(pokemon => pokemon.Notes, notes));
        return updated == 1;
    }

    public async Task<object?> ComparePokemonAsync(int userId, int id1, int id2)
    {
        var p1 = await _db.Pokemon.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id1 && x.UserId == userId);
        var p2 = await _db.Pokemon.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id2 && x.UserId == userId);

        if (p1 == null || p2 == null) return null;

        var comparison = PokemonComparisonService.Compare(p1, p2);

        return new
        {
            Pokemon1 = new { Id = p1.Id, Species = PkHexStringService.GetSpeciesName(p1.SpeciesId), Nickname = p1.Nickname },
            Pokemon2 = new { Id = p2.Id, Species = PkHexStringService.GetSpeciesName(p2.SpeciesId), Nickname = p2.Nickname },
            AreIdentical = comparison.AreIdentical,
            Differences = comparison.Differences,
            Summary = comparison.AreIdentical ? "Pokemon are identical" : $"Found {comparison.Differences.Count} differences"
        };
    }

    public async Task<(bool Success, bool FileDeleted, bool BackupPreserved)> DeletePokemonDatabaseAsync(int userId, int pokemonId)
    {
        var poke = await _db.Pokemon.FirstOrDefaultAsync(x => x.Id == pokemonId && x.UserId == userId);
        if (poke == null) return (false, false, false);

        var file = await _db.Files.FirstOrDefaultAsync(f => f.Id == poke.FileId);

        await RemoveRelatedDataAsync(pokemonId);
        _db.Pokemon.Remove(poke);

        bool fileDeleted = false;
        if (file != null)
        {
            try { _storage.Delete(file.StoredPath); fileDeleted = true; }
            catch (Exception ex) { Console.WriteLine($"Could not delete main file {file.StoredPath}: {ex.Message}"); }
            _db.Files.Remove(file);
        }

        await _db.SaveChangesAsync();
        return (true, fileDeleted, true);
    }

    public async Task<(bool Success, bool FileDeleted, bool BackupDeleted, string? FileName)> DeletePokemonAndBackupAsync(int userId, int pokemonId)
    {
        var poke = await _db.Pokemon.FirstOrDefaultAsync(x => x.Id == pokemonId && x.UserId == userId);
        if (poke == null) return (false, false, false, null);

        var file = await _db.Files.FirstOrDefaultAsync(f => f.Id == poke.FileId);

        await RemoveRelatedDataAsync(pokemonId);
        _db.Pokemon.Remove(poke);

        bool fileDeleted = false;
        bool backupDeleted = false;
        if (file != null)
        {
            try { _storage.Delete(file.StoredPath); fileDeleted = true; }
            catch (Exception ex) { Console.WriteLine($"Could not delete physical file {file.StoredPath}: {ex.Message}"); }

            if (!string.IsNullOrEmpty(file.OriginalFileName))
            {
                try
                {
                    var ext = Path.GetExtension(file.OriginalFileName);
                    _storage.DeleteBackup(file.OriginalFileName, ext, userId);
                    backupDeleted = true;
                }
                catch (Exception ex) { Console.WriteLine($"Could not delete backup file {file.OriginalFileName}: {ex.Message}"); }
            }

            _db.Files.Remove(file);
        }

        await _db.SaveChangesAsync();
        return (true, fileDeleted, backupDeleted, file?.FileName);
    }

    private async Task RemoveRelatedDataAsync(int pokemonId)
    {
        var stats = await _db.Stats.Where(s => s.PokemonId == pokemonId).ToListAsync();
        _db.Stats.RemoveRange(stats);
        var moves = await _db.Moves.Where(m => m.PokemonId == pokemonId).ToListAsync();
        _db.Moves.RemoveRange(moves);
        var relearnMoves = await _db.RelearnMoves.Where(rm => rm.PokemonId == pokemonId).ToListAsync();
        _db.RelearnMoves.RemoveRange(relearnMoves);
        var pokemonTags = await _db.PokemonTags.Where(pt => pt.PokemonId == pokemonId).ToListAsync();
        _db.PokemonTags.RemoveRange(pokemonTags);
    }
}
