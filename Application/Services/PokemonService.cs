using Microsoft.EntityFrameworkCore;
using BeastVault.Api.Contracts;
using BeastVault.Api.Domain.Entities;
using BeastVault.Api.Domain.Services;
using BeastVault.Api.Domain.ValueObjects;
using BeastVault.Api.Infrastructure;
using BeastVault.Api.Infrastructure.Services;
using BeastVault.Api.Application.Interfaces;

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

    public async Task<object> GetPokemonListAsync(int userId, AdvancedPokemonQuery q)
    {
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
                BallId = pf.Pokemon.BallId,
                TeraType = pf.Pokemon.TeraType,
                HeldItemId = pf.Pokemon.HeldItemId,
                Gender = pf.Pokemon.Gender,
                SpriteKey = pf.Pokemon.SpriteKey,
                OriginGeneration = PokemonGameInfoService.GetSpeciesOriginGeneration(pf.Pokemon.SpeciesId),
                CapturedGeneration = PokemonGameInfoService.GetCapturedGeneration(pf.Pokemon.OriginGame, pf.File.Format),
                CanGigantamax = pf.Pokemon.CanGigantamax,
                HasMegaStone = PokemonFormService.CheckHasMegaStone(pf.Pokemon)
            })
            .ToListAsync();

        var pokemonIds = items.Select(i => i.Id).ToList();
        var pokemonTags = await _db.PokemonTags
            .Where(pt => pokemonIds.Contains(pt.PokemonId))
            .Include(pt => pt.Tag)
            .GroupBy(pt => pt.PokemonId)
            .ToDictionaryAsync(
                g => g.Key,
                g => g.Select(pt => new TagDto
                {
                    Id = pt.Tag.Id,
                    Name = pt.Tag.Name,
                    ImagePath = pt.Tag.ImagePath,
                    PokemonCount = 0
                })
                .OrderBy(t => t.Name)
                .ToList()
            );

        var resultItems = items.Select(item =>
        {
            string formName = PkHexStringService.GetFormName(item.SpeciesId, item.Form);

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
                BallId = item.BallId,
                TeraType = item.TeraType,
                HeldItemId = item.HeldItemId,
                Gender = item.Gender,
                SpriteKey = item.SpriteKey,
                OriginGeneration = item.OriginGeneration,
                CapturedGeneration = item.CapturedGeneration,
                CanGigantamax = item.CanGigantamax,
                HasMegaStone = item.HasMegaStone,
                Tags = pokemonTags.GetValueOrDefault(item.Id, new List<TagDto>())
            };
        }).ToList();

        var stats = PokemonQueryService.GetQueryStats(q);

        return new { Items = resultItems, Total = total, Stats = stats };
    }

    public async Task<PokemonDetailDto?> GetPokemonByIdAsync(int userId, int pokemonId)
    {
        var p = await _db.Pokemon.AsNoTracking().FirstOrDefaultAsync(x => x.Id == pokemonId && x.UserId == userId);
        if (p == null) return null;

        var stats = await _db.Stats.AsNoTracking().FirstOrDefaultAsync(x => x.PokemonId == p.Id);
        var moves = await _db.Moves.AsNoTracking().Where(x => x.PokemonId == p.Id).OrderBy(x => x.Slot).ToListAsync();
        var relearnMoves = await _db.RelearnMoves.AsNoTracking().Where(x => x.PokemonId == p.Id).OrderBy(x => x.Slot).ToListAsync();

        return new PokemonDetailDto(p, stats, moves, relearnMoves);
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
                    _storage.DeleteBackup(file.OriginalFileName, ext);
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
