using BeastVault.Api.Contracts;
using BeastVault.Api.Domain.Entities;
using BeastVault.Api.Infrastructure;
using BeastVault.Api.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace BeastVault.Api.Application.Services;

public sealed class SaveFileService(
    AppDbContext db,
    FileStorageService storage,
    PkhexSaveParser saveParser,
    PkhexCoreParser pokemonParser)
{
    public async Task<SaveFileUploadResultDto> UploadAsync(
        int userId,
        string fileName,
        byte[] bytes,
        CancellationToken cancellationToken)
    {
        if (bytes.Length == 0)
            return new SaveFileUploadResultDto(fileName, "error", Message: "The save file is empty.");

        var hash = FileStorageService.ComputeSha256(bytes);
        var duplicateId = await db.SaveFiles
            .Where(x => x.UserId == userId && x.Sha256 == hash)
            .Select(x => (int?)x.Id)
            .SingleOrDefaultAsync(cancellationToken);
        if (duplicateId.HasValue)
            return new SaveFileUploadResultDto(fileName, "duplicate", duplicateId, "This save is already in your vault.");

        var parsed = await saveParser.ParseAsync(bytes, fileName);
        if (parsed is null)
            return new SaveFileUploadResultDto(fileName, "error", Message: "PKHeX could not recognize this save format.");

        parsed.SaveFile.UserId = userId;
        parsed.SaveFile.StoredPath = storage.SaveGameSave(
            parsed.SaveFile.Sha256,
            parsed.SaveFile.Format,
            bytes,
            userId,
            parsed.SaveFile.Generation,
            fileName);

        db.SaveFiles.Add(parsed.SaveFile);
        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            db.ChangeTracker.Clear();
            var existingId = await db.SaveFiles
                .Where(x => x.UserId == userId && x.Sha256 == hash)
                .Select(x => (int?)x.Id)
                .SingleOrDefaultAsync(cancellationToken);
            if (existingId.HasValue)
                return new SaveFileUploadResultDto(fileName, "duplicate", existingId, "This save is already in your vault.");
            storage.DeleteUserFile(userId, parsed.SaveFile.StoredPath);
            throw;
        }
        catch
        {
            storage.DeleteUserFile(userId, parsed.SaveFile.StoredPath);
            throw;
        }

        return new SaveFileUploadResultDto(fileName, "imported", parsed.SaveFile.Id);
    }

    public async Task<IReadOnlyList<SaveFileSummaryDto>> GetAllAsync(
        int userId,
        CancellationToken cancellationToken)
    {
        var saves = await db.SaveFiles
            .AsNoTracking()
            .Where(x => x.UserId == userId)
            .Include(x => x.Trainer)
            .Include(x => x.PokemonPreviews)
            .OrderByDescending(x => x.ImportedAt)
            .ToListAsync(cancellationToken);

        return saves.Select(ToSummary).ToList();
    }

    public async Task<SaveFileDetailDto?> GetDetailAsync(
        int userId,
        int saveFileId,
        CancellationToken cancellationToken)
    {
        var save = await db.SaveFiles
            .AsNoTracking()
            .Where(x => x.UserId == userId && x.Id == saveFileId)
            .Include(x => x.Trainer)
            .Include(x => x.PokedexEntries)
            .Include(x => x.PokemonPreviews)
            .SingleOrDefaultAsync(cancellationToken);
        if (save is null)
            return null;

        var existing = await FindExistingPokemonAsync(userId, save.PokemonPreviews, cancellationToken);
        return new SaveFileDetailDto(
            ToSummary(save),
            ToTrainer(save.Trainer),
            save.PokedexEntries
                .OrderBy(x => x.SpeciesId)
                .Select(x => new SavePokedexEntryDto(x.SpeciesId, x.SpeciesName, x.Seen, x.Caught))
                .ToList(),
            save.PokemonPreviews
                .OrderBy(x => x.Location)
                .ThenBy(x => x.BoxIndex)
                .ThenBy(x => x.SlotIndex)
                .Select(x => ToPreview(x, existing.GetValueOrDefault(x.Id)))
                .ToList());
    }

    public async Task<bool> UpdateNotesAsync(
        int userId,
        int saveFileId,
        string? notes,
        CancellationToken cancellationToken)
    {
        var save = await db.SaveFiles.SingleOrDefaultAsync(
            x => x.UserId == userId && x.Id == saveFileId,
            cancellationToken);
        if (save is null)
            return false;

        save.Notes = string.IsNullOrWhiteSpace(notes) ? null : notes.Trim();
        await db.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<IReadOnlyList<SavePokemonImportResultDto>?> ImportPokemonAsync(
        int userId,
        int saveFileId,
        IReadOnlyCollection<int> previewIds,
        CancellationToken cancellationToken)
    {
        var save = await db.SaveFiles
            .Where(x => x.UserId == userId && x.Id == saveFileId)
            .Include(x => x.PokemonPreviews)
            .SingleOrDefaultAsync(cancellationToken);
        if (save is null)
            return null;

        var requestedIds = previewIds.Distinct().ToHashSet();
        var previews = save.PokemonPreviews
            .Where(x => requestedIds.Contains(x.Id))
            .OrderBy(x => x.Id)
            .ToList();
        var results = requestedIds
            .Except(previews.Select(x => x.Id))
            .Select(id => new SavePokemonImportResultDto(
                id,
                "error",
                Message: "This Pokémon slot does not belong to the save."))
            .ToList();

        if (!TryReadSaveBytes(userId, save, out var saveBytes))
        {
            results.AddRange(previews.Select(x =>
                new SavePokemonImportResultDto(x.Id, "error", Message: "The original save data is unavailable.")));
            return results;
        }

        var parsedSave = saveParser.Load(saveBytes, save.OriginalFileName);
        if (parsedSave is null)
        {
            results.AddRange(previews.Select(x =>
                new SavePokemonImportResultDto(x.Id, "error", Message: "The original save can no longer be parsed.")));
            return results;
        }

        var existing = await FindExistingPokemonAsync(userId, previews, cancellationToken);
        foreach (var preview in previews)
        {
            if (existing.TryGetValue(preview.Id, out var existingPokemonId))
            {
                results.Add(new SavePokemonImportResultDto(preview.Id, "duplicate", existingPokemonId));
                continue;
            }

            var pokemon = PkhexSaveParser.GetPokemon(parsedSave, preview);
            if (pokemon is null)
            {
                results.Add(new SavePokemonImportResultDto(preview.Id, "error", Message: "The Pokémon slot could not be read."));
                continue;
            }

            var bytes = pokemon.DecryptedPartyData;
            var exportedHash = FileStorageService.ComputeSha256(bytes);
            if (!string.Equals(exportedHash, preview.PokemonHash, StringComparison.Ordinal))
            {
                results.Add(new SavePokemonImportResultDto(preview.Id, "error", Message: "The Pokémon slot no longer matches its preview."));
                continue;
            }

            var exportedFileName = $"{preview.SpeciesName}.{pokemon.Extension}";
            var parsedPokemon = await pokemonParser.ParseAsync(bytes, exportedFileName, storage, userId);
            if (parsedPokemon is null || string.IsNullOrWhiteSpace(parsedPokemon.File.StoredPath))
            {
                results.Add(new SavePokemonImportResultDto(preview.Id, "error", Message: "The Pokémon could not be imported."));
                continue;
            }

            parsedPokemon.File.RawBlob = bytes;
            parsedPokemon.File.UserId = userId;
            parsedPokemon.Pokemon.UserId = userId;

            await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
            try
            {
                db.Files.Add(parsedPokemon.File);
                await db.SaveChangesAsync(cancellationToken);

                parsedPokemon.Pokemon.FileId = parsedPokemon.File.Id;
                db.Pokemon.Add(parsedPokemon.Pokemon);
                await db.SaveChangesAsync(cancellationToken);

                if (parsedPokemon.Stats is not null)
                {
                    parsedPokemon.Stats.PokemonId = parsedPokemon.Pokemon.Id;
                    db.Stats.Add(parsedPokemon.Stats);
                }
                foreach (var move in parsedPokemon.Moves)
                {
                    move.PokemonId = parsedPokemon.Pokemon.Id;
                    db.Moves.Add(move);
                }
                foreach (var move in parsedPokemon.RelearnMoves)
                {
                    move.PokemonId = parsedPokemon.Pokemon.Id;
                    db.RelearnMoves.Add(move);
                }

                await db.SaveChangesAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);
                results.Add(new SavePokemonImportResultDto(preview.Id, "imported", parsedPokemon.Pokemon.Id));
                foreach (var matchingPreview in previews.Where(x =>
                    string.Equals(x.PokemonHash, preview.PokemonHash, StringComparison.Ordinal) ||
                    string.Equals(x.PokemonStoredHash, preview.PokemonStoredHash, StringComparison.Ordinal)))
                {
                    existing[matchingPreview.Id] = parsedPokemon.Pokemon.Id;
                }
            }
            catch (Exception exception)
            {
                await transaction.RollbackAsync(cancellationToken);
                db.ChangeTracker.Clear();
                var duplicateId = exception is DbUpdateException
                    ? await FindPokemonByHashesAsync(
                        userId,
                        [preview.PokemonHash, preview.PokemonStoredHash],
                        cancellationToken)
                    : null;
                if (!duplicateId.HasValue)
                    storage.DeleteUserFile(userId, parsedPokemon.File.StoredPath);
                results.Add(duplicateId.HasValue
                    ? new SavePokemonImportResultDto(preview.Id, "duplicate", duplicateId)
                    : new SavePokemonImportResultDto(preview.Id, "error", Message: "The Pokémon could not be persisted."));
            }
        }

        return results.OrderBy(x => x.PreviewId).ToList();
    }

    public async Task<(byte[] Content, string FileName)?> DownloadAsync(
        int userId,
        int saveFileId,
        CancellationToken cancellationToken)
    {
        var save = await db.SaveFiles.AsNoTracking().SingleOrDefaultAsync(
            x => x.UserId == userId && x.Id == saveFileId,
            cancellationToken);
        if (save is null || !TryReadSaveBytes(userId, save, out var bytes))
            return null;
        return (bytes, save.OriginalFileName);
    }

    public async Task<bool> DeleteAsync(
        int userId,
        int saveFileId,
        CancellationToken cancellationToken)
    {
        var save = await db.SaveFiles.SingleOrDefaultAsync(
            x => x.UserId == userId && x.Id == saveFileId,
            cancellationToken);
        if (save is null)
            return false;

        db.SaveFiles.Remove(save);
        await db.SaveChangesAsync(cancellationToken);
        storage.DeleteUserFile(userId, save.StoredPath);
        return true;
    }

    private bool TryReadSaveBytes(int userId, SaveFileEntity save, out byte[] bytes)
    {
        if (save.RawBlob.Length > 0)
        {
            bytes = save.RawBlob;
            return true;
        }
        return storage.TryReadUserFile(userId, save.StoredPath, out bytes);
    }

    private async Task<Dictionary<int, int>> FindExistingPokemonAsync(
        int userId,
        IEnumerable<SavePokemonPreviewEntity> previews,
        CancellationToken cancellationToken)
    {
        var previewList = previews.ToList();
        var hashes = previewList
            .SelectMany(x => new[] { x.PokemonHash, x.PokemonStoredHash })
            .Distinct()
            .ToList();
        if (hashes.Count == 0)
            return [];

        var matches = await db.Files
            .AsNoTracking()
            .Where(x => x.UserId == userId && hashes.Contains(x.Sha256))
            .Join(db.Pokemon.AsNoTracking(), file => file.Id, pokemon => pokemon.FileId,
                (file, pokemon) => new { file.Sha256, PokemonId = pokemon.Id })
            .ToListAsync(cancellationToken);
        var byHash = matches
            .GroupBy(x => x.Sha256)
            .ToDictionary(x => x.Key, x => x.First().PokemonId);

        return previewList
            .Select(x => new
            {
                x.Id,
                PokemonId = byHash.GetValueOrDefault(x.PokemonHash) is var partyId && partyId > 0
                    ? partyId
                    : byHash.GetValueOrDefault(x.PokemonStoredHash)
            })
            .Where(x => x.PokemonId > 0)
            .ToDictionary(x => x.Id, x => x.PokemonId);
    }

    private async Task<int?> FindPokemonByHashesAsync(
        int userId,
        IReadOnlyCollection<string> hashes,
        CancellationToken cancellationToken)
    {
        return await db.Files
            .AsNoTracking()
            .Where(x => x.UserId == userId && hashes.Contains(x.Sha256))
            .Join(db.Pokemon.AsNoTracking(), file => file.Id, pokemon => pokemon.FileId,
                (_, pokemon) => (int?)pokemon.Id)
            .FirstOrDefaultAsync(cancellationToken);
    }

    private static SaveFileSummaryDto ToSummary(SaveFileEntity save)
    {
        var trainer = save.Trainer;
        return new SaveFileSummaryDto(
            save.Id,
            save.OriginalFileName,
            save.Format,
            save.Size,
            save.Generation,
            save.OriginGame,
            save.GameName,
            save.SaveType,
            save.ImportedAt,
            save.Notes,
            trainer.TrainerName,
            trainer.TrainerId,
            trainer.SecretId,
            FormatPlayTime(trainer),
            trainer.BadgeCount,
            trainer.DexSeen,
            trainer.DexCaught,
            save.PokemonPreviews.Count(x => x.Location == SavePokemonLocation.Party),
            save.PokemonPreviews.Count(x => x.Location == SavePokemonLocation.Box),
            save.ChecksumsValid);
    }

    private static SaveTrainerDto ToTrainer(SaveTrainerEntity trainer) => new(
        trainer.TrainerName,
        trainer.TrainerId,
        trainer.SecretId,
        trainer.Gender,
        trainer.Language,
        trainer.Money,
        trainer.PlayTimeHours,
        trainer.PlayTimeMinutes,
        trainer.PlayTimeSeconds,
        FormatPlayTime(trainer),
        trainer.BadgeCount,
        trainer.DexSeen,
        trainer.DexCaught);

    private static SavePokemonPreviewDto ToPreview(SavePokemonPreviewEntity preview, int existingPokemonId) => new(
        preview.Id,
        preview.Location == SavePokemonLocation.Party ? "party" : "box",
        preview.BoxIndex.HasValue ? preview.BoxIndex.Value + 1 : null,
        preview.SlotIndex + 1,
        preview.SpeciesId,
        preview.SpeciesName,
        preview.Nickname,
        preview.Level,
        preview.IsShiny,
        preview.IsEgg,
        preview.Form,
        preview.Gender,
        preview.Nature,
        preview.NatureName,
        preview.AbilityName,
        preview.HeldItemName,
        DeserializeMoves(preview.MovesJson),
        preview.PokemonHash,
        existingPokemonId > 0 ? existingPokemonId : null);

    private static IReadOnlyList<string> DeserializeMoves(string value)
    {
        try
        {
            return JsonSerializer.Deserialize<List<string>>(value) ?? [];
        }
        catch (JsonException)
        {
            return [];
        }
    }

    private static string FormatPlayTime(SaveTrainerEntity trainer) =>
        $"{trainer.PlayTimeHours}:{trainer.PlayTimeMinutes:00}:{trainer.PlayTimeSeconds:00}";
}
