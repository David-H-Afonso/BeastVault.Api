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
            parsed.SaveFile.RawBlob,
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
        var saves = await QuerySaveRows(userId)
            .OrderByDescending(x => x.ImportedAt)
            .ToListAsync(cancellationToken);

        return saves.Select(ToSummary).ToList();
    }

    public async Task<SaveFileDetailDto?> GetDetailAsync(
        int userId,
        int saveFileId,
        CancellationToken cancellationToken)
    {
        var save = await QuerySaveRows(userId)
            .SingleOrDefaultAsync(x => x.Id == saveFileId, cancellationToken);
        if (save is null)
            return null;

        var pokedex = await db.SavePokedexEntries
            .AsNoTracking()
            .Where(x => x.SaveFileId == saveFileId && x.SaveFile.UserId == userId)
            .OrderBy(x => x.SpeciesId)
            .Select(x => new SavePokedexEntryDto(
                x.SpeciesId,
                x.SpeciesName,
                x.Seen,
                x.Caught,
                SavePokedexRules.IsVersionExclusive(save.OriginGame, x.SpeciesId)))
            .ToListAsync(cancellationToken);

        // Re-read the raw save so older imports also get the correct grouped game rules and revision.
        var rawPokedex = await TryReadPokedexFromRawAsync(
            userId,
            saveFileId,
            save.OriginalFileName,
            save.OriginGame,
            cancellationToken);
        if (rawPokedex is not null)
        {
            pokedex = rawPokedex;
        }

        var pokemonRows = await db.SavePokemonPreviews
            .AsNoTracking()
            .Where(x => x.SaveFileId == saveFileId && x.SaveFile.UserId == userId)
            .OrderBy(x => x.Location)
            .ThenBy(x => x.BoxIndex)
            .ThenBy(x => x.SlotIndex)
            .Select(x => new
            {
                x.Id,
                x.Location,
                x.BoxIndex,
                x.SlotIndex,
                x.SpeciesId,
                x.SpeciesName,
                x.Nickname,
                x.Level,
                x.IsShiny,
                x.IsEgg,
                x.Form,
                x.Gender,
                x.Nature,
                x.NatureName,
                x.AbilityName,
                x.HeldItemName,
                x.MovesJson,
                x.PokemonHash,
                x.PokemonStoredHash
            })
            .ToListAsync(cancellationToken);
        var existing = await FindExistingPokemonAsync(
            userId,
            pokemonRows.Select(x => (x.Id, x.PokemonHash, x.PokemonStoredHash)),
            cancellationToken);

        var pokedexDtos = pokedex.Select(x => new SavePokedexEntryDto(
            x.SpeciesId,
            x.SpeciesName,
            x.Seen,
            x.Caught,
            x.IsVersionExclusive)).ToList();
        var regionalIds = SavePokedexRules.RegionalSpecies(save.OriginGame, save.Generation, save.GameName);
        var regional = pokedexDtos.Where(x => regionalIds.Contains(x.SpeciesId)).ToList();
        var national = pokedexDtos;

        return new SaveFileDetailDto(
            ToSummary(save),
            ToTrainer(save),
            national,
            pokemonRows.Select(x =>
            {
                var existingPokemonId = existing.GetValueOrDefault(x.Id);
                return new SavePokemonPreviewDto(
                    x.Id,
                    x.Location == SavePokemonLocation.Party ? "party" : "box",
                    x.BoxIndex.HasValue ? x.BoxIndex.Value + 1 : null,
                    x.SlotIndex + 1,
                    x.SpeciesId,
                    x.SpeciesName,
                    x.Nickname,
                    x.Level,
                    x.IsShiny,
                    x.IsEgg,
                    x.Form,
                    x.Gender,
                    x.Nature,
                    x.NatureName,
                    x.AbilityName,
                    x.HeldItemName,
                    DeserializeMoves(x.MovesJson),
                    x.PokemonHash,
                    existingPokemonId > 0 ? existingPokemonId : null);
            }).ToList(),
            ToPokedexProgress(regional),
            ToPokedexProgress(national));
    }

    public async Task<bool> UpdateMetadataAsync(
        int userId,
        int saveFileId,
        string? title,
        string? notes,
        CancellationToken cancellationToken)
    {
        var normalizedTitle = NormalizeOptionalText(title);
        var normalizedNotes = NormalizeOptionalText(notes);
        if (normalizedTitle?.Length > 120)
            throw new ArgumentOutOfRangeException(nameof(title), "Save titles cannot exceed 120 characters.");
        if (normalizedNotes?.Length > 4000)
            throw new ArgumentOutOfRangeException(nameof(notes), "Save notes cannot exceed 4000 characters.");

        var updated = await db.SaveFiles
            .Where(x => x.UserId == userId && x.Id == saveFileId)
            .ExecuteUpdateAsync(update => update
                .SetProperty(x => x.Title, normalizedTitle)
                .SetProperty(x => x.Notes, normalizedNotes), cancellationToken);
        return updated == 1;
    }

    public async Task<IReadOnlyList<SavePokemonImportResultDto>?> ImportPokemonAsync(
        int userId,
        int saveFileId,
        IReadOnlyCollection<int> previewIds,
        CancellationToken cancellationToken)
    {
        var save = await db.SaveFiles
            .AsNoTracking()
            .Where(x => x.UserId == userId && x.Id == saveFileId)
            .Select(x => new { x.OriginalFileName, x.StoredPath, x.RawBlob })
            .SingleOrDefaultAsync(cancellationToken);
        if (save is null)
            return null;

        var requestedIds = previewIds.Distinct().ToList();
        var previews = await db.SavePokemonPreviews
            .AsNoTracking()
            .Where(x => x.SaveFileId == saveFileId &&
                x.SaveFile.UserId == userId &&
                requestedIds.Contains(x.Id))
            .OrderBy(x => x.Id)
            .ToListAsync(cancellationToken);
        var results = requestedIds
            .Except(previews.Select(x => x.Id))
            .Select(id => new SavePokemonImportResultDto(
                id,
                "error",
                Message: "This Pokémon slot does not belong to the save."))
            .ToList();

        if (!TryReadSaveBytes(userId, save.RawBlob, save.StoredPath, out var saveBytes))
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

        var existing = await FindExistingPokemonAsync(
            userId,
            previews.Select(x => (x.Id, x.PokemonHash, x.PokemonStoredHash)),
            cancellationToken);
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
        var save = await db.SaveFiles
            .AsNoTracking()
            .Where(x => x.UserId == userId && x.Id == saveFileId)
            .Select(x => new { x.OriginalFileName, x.StoredPath, x.RawBlob })
            .SingleOrDefaultAsync(cancellationToken);
        if (save is null || !TryReadSaveBytes(userId, save.RawBlob, save.StoredPath, out var bytes))
            return null;
        return (bytes, save.OriginalFileName);
    }

    public async Task<bool> DeleteAsync(
        int userId,
        int saveFileId,
        CancellationToken cancellationToken)
    {
        var save = await db.SaveFiles
            .AsNoTracking()
            .Where(x => x.UserId == userId && x.Id == saveFileId)
            .Select(x => new { x.StoredPath })
            .SingleOrDefaultAsync(cancellationToken);
        if (save is null)
            return false;

        var deleted = await db.SaveFiles
            .Where(x => x.UserId == userId && x.Id == saveFileId)
            .ExecuteDeleteAsync(cancellationToken);
        if (deleted != 1)
            return false;

        storage.DeleteUserFile(userId, save.StoredPath);
        return true;
    }

    private bool TryReadSaveBytes(int userId, byte[] rawBlob, string storedPath, out byte[] bytes)
    {
        if (rawBlob.Length > 0)
        {
            bytes = rawBlob;
            return true;
        }
        return storage.TryReadUserFile(userId, storedPath, out bytes);
    }

    private async Task<Dictionary<int, int>> FindExistingPokemonAsync(
        int userId,
        IEnumerable<(int Id, string PokemonHash, string PokemonStoredHash)> previews,
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

    private IQueryable<SaveFileRow> QuerySaveRows(int userId)
    {
        return db.SaveFiles
            .AsNoTracking()
            .Where(x => x.UserId == userId)
            .Select(x => new SaveFileRow
            {
                Id = x.Id,
                Title = x.Title,
                OriginalFileName = x.OriginalFileName,
                Format = x.Format,
                Size = x.Size,
                Generation = x.Generation,
                OriginGame = x.OriginGame,
                GameName = x.GameName,
                SaveType = x.SaveType,
                ImportedAt = x.ImportedAt,
                Notes = x.Notes,
                ChecksumsValid = x.ChecksumsValid,
                TrainerName = x.Trainer.TrainerName,
                TrainerId = x.Trainer.TrainerId,
                SecretId = x.Trainer.SecretId,
                TrainerGender = x.Trainer.Gender,
                Language = x.Trainer.Language,
                Money = x.Trainer.Money,
                PlayTimeHours = x.Trainer.PlayTimeHours,
                PlayTimeMinutes = x.Trainer.PlayTimeMinutes,
                PlayTimeSeconds = x.Trainer.PlayTimeSeconds,
                BadgeCount = x.Trainer.BadgeCount,
                DexSeen = x.Trainer.DexSeen,
                DexCaught = x.Trainer.DexCaught,
                PartyCount = x.PokemonPreviews.Count(p => p.Location == SavePokemonLocation.Party),
                StoredPokemonCount = x.PokemonPreviews.Count(p => p.Location == SavePokemonLocation.Box)
            });
    }

    private static SaveFileSummaryDto ToSummary(SaveFileRow save)
    {
        var title = NormalizeOptionalText(save.Title);
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
            save.TrainerName,
            save.TrainerId,
            save.SecretId,
            FormatPlayTime(save.PlayTimeHours, save.PlayTimeMinutes, save.PlayTimeSeconds),
            save.BadgeCount,
            save.DexSeen,
            save.DexCaught,
            save.PartyCount,
            save.StoredPokemonCount,
            save.ChecksumsValid,
            title,
            title ?? save.GameName,
            save.TrainerGender,
            GetBadgeTotal(save));
    }

    private static SaveTrainerDto ToTrainer(SaveFileRow save) => new(
        save.TrainerName,
        save.TrainerId,
        save.SecretId,
        save.TrainerGender,
        save.Language,
        save.Money,
        save.PlayTimeHours,
        save.PlayTimeMinutes,
        save.PlayTimeSeconds,
        FormatPlayTime(save.PlayTimeHours, save.PlayTimeMinutes, save.PlayTimeSeconds),
        save.BadgeCount,
        save.DexSeen,
        save.DexCaught);

    private static SavePokedexProgressDto ToPokedexProgress(IReadOnlyList<SavePokedexEntryDto> entries) =>
        new(entries, entries.Count(x => x.Seen), entries.Count(x => x.Caught), entries.Count);

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

    private static int? GetBadgeTotal(SaveFileRow save)
    {
        if (save.SaveType.Contains("HGSS", StringComparison.OrdinalIgnoreCase))
            return 16;
        if (save.Generation == 9 &&
            (save.SaveType.Contains("SV", StringComparison.OrdinalIgnoreCase) ||
             save.GameName.Equals("Scarlet", StringComparison.OrdinalIgnoreCase) ||
             save.GameName.Equals("Violet", StringComparison.OrdinalIgnoreCase)))
        {
            return 18;
        }
        if (save.Generation is >= 1 and <= 6)
            return 8;
        if (save.Generation == 7 &&
            (save.SaveType.Equals("7b", StringComparison.OrdinalIgnoreCase) ||
             save.GameName.StartsWith("Let's Go", StringComparison.OrdinalIgnoreCase)))
        {
            return 8;
        }
        if (save.Generation == 8 &&
            (save.SaveType.Contains("SWSH", StringComparison.OrdinalIgnoreCase) ||
             save.SaveType.Equals("8BS", StringComparison.OrdinalIgnoreCase) ||
             save.GameName is "Sword" or "Shield" or "Brilliant Diamond" or "Shining Pearl"))
        {
            return 8;
        }
        return null;
    }

    private static string? NormalizeOptionalText(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static string FormatPlayTime(int hours, int minutes, int seconds) =>
        $"{hours}:{minutes:00}:{seconds:00}";

    private async Task<List<SavePokedexEntryDto>?> TryReadPokedexFromRawAsync(
        int userId,
        int saveFileId,
        string fileName,
        int originGame,
        CancellationToken cancellationToken)
    {
        var save = await db.SaveFiles
            .AsNoTracking()
            .Where(x => x.Id == saveFileId && x.UserId == userId)
            .Select(x => new { x.RawBlob, x.StoredPath })
            .SingleOrDefaultAsync(cancellationToken);
        if (save is null || !TryReadSaveBytes(userId, save.RawBlob, save.StoredPath, out var bytes)) return null;

        var parsed = saveParser.Load(bytes, fileName);
        if (parsed is null) return null;

        return PkhexSaveParser.ReadPokedex(parsed)
            .Select(entry => new SavePokedexEntryDto(
                entry.SpeciesId,
                entry.SpeciesName,
                entry.Seen,
                entry.Caught,
                SavePokedexRules.IsVersionExclusive(originGame, entry.SpeciesId)))
            .ToList();
    }

    private sealed class SaveFileRow
    {
        public int Id { get; init; }
        public string? Title { get; init; }
        public string OriginalFileName { get; init; } = string.Empty;
        public string Format { get; init; } = string.Empty;
        public long Size { get; init; }
        public int Generation { get; init; }
        public int OriginGame { get; init; }
        public string GameName { get; init; } = string.Empty;
        public string SaveType { get; init; } = string.Empty;
        public DateTime ImportedAt { get; init; }
        public string? Notes { get; init; }
        public bool ChecksumsValid { get; init; }
        public string TrainerName { get; init; } = string.Empty;
        public uint TrainerId { get; init; }
        public uint SecretId { get; init; }
        public int TrainerGender { get; init; }
        public string Language { get; init; } = string.Empty;
        public uint Money { get; init; }
        public int PlayTimeHours { get; init; }
        public int PlayTimeMinutes { get; init; }
        public int PlayTimeSeconds { get; init; }
        public int? BadgeCount { get; init; }
        public int DexSeen { get; init; }
        public int DexCaught { get; init; }
        public int PartyCount { get; init; }
        public int StoredPokemonCount { get; init; }
    }
}
