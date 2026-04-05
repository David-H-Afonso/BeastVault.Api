using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using BeastVault.Api.Domain.Entities;
using BeastVault.Api.Infrastructure;

namespace BeastVault.Api.Infrastructure.Services
{
    public class FileWatcherService
    {
        private readonly AppDbContext _context;
        private readonly PkhexCoreParser _parser;
        private readonly FileStorageService _storage;

        public FileWatcherService(AppDbContext context, PkhexCoreParser parser, FileStorageService storage)
        {
            _context = context;
            _parser = parser;
            _storage = storage;
        }

        public async Task<ImportScanResult> ScanAndImportNewFilesAsync(int userId)
        {
            var result = new ImportScanResult();
            var watchPath = _storage.GetUserDirectory(userId);
            var backupPath = _storage.GetUserBackupDirectory(userId);

            _storage.EnsureUserVault(userId);

            try
            {
                await CleanupDeletedFilesAsync(userId, result, watchPath, backupPath);

                var pokemonFiles = Directory.GetFiles(watchPath, "*.*", SearchOption.AllDirectories)
                    .Where(file => IsPokemonFile(file) && !IsInIgnoredDirectory(file, backupPath))
                    .ToList();

                foreach (var filePath in pokemonFiles)
                {
                    try
                    {
                        await ProcessFileAsync(filePath, userId, result);
                    }
                    catch (Exception ex)
                    {
                        result.Errors.Add($"{Path.GetFileName(filePath)}: {ex.Message}");
                    }
                }
            }
            catch (Exception ex)
            {
                result.Errors.Add($"Directory scan error: {ex.Message}");
            }

            return result;
        }

        private async Task CleanupDeletedFilesAsync(int userId, ImportScanResult result,
            string watchPath, string backupPath)
        {
            var currentUserFiles = Directory.GetFiles(watchPath, "*.*", SearchOption.AllDirectories)
                .Where(file => IsPokemonFile(file) && !IsInIgnoredDirectory(file, backupPath))
                .ToList();

            var currentFileHashes = new HashSet<string>();
            foreach (var filePath in currentUserFiles)
            {
                try
                {
                    var fileBytes = await File.ReadAllBytesAsync(filePath);
                    currentFileHashes.Add(FileStorageService.ComputeSha256(fileBytes));
                }
                catch { /* skip unreadable files */ }
            }

            var userDbFiles = await _context.Files
                .Where(f => f.UserId == userId)
                .ToListAsync();

            foreach (var dbFile in userDbFiles)
            {
                if (currentFileHashes.Contains(dbFile.Sha256))
                    continue;

                var pokemon = await _context.Pokemon.FirstOrDefaultAsync(p => p.FileId == dbFile.Id);
                if (pokemon != null)
                {
                    var stats = await _context.Stats.FirstOrDefaultAsync(s => s.PokemonId == pokemon.Id);
                    if (stats != null) _context.Stats.Remove(stats);

                    _context.Moves.RemoveRange(
                        await _context.Moves.Where(m => m.PokemonId == pokemon.Id).ToListAsync());
                    _context.RelearnMoves.RemoveRange(
                        await _context.RelearnMoves.Where(r => r.PokemonId == pokemon.Id).ToListAsync());

                    _context.Pokemon.Remove(pokemon);
                }

                _context.Files.Remove(dbFile);
                result.Deleted.Add(dbFile.FileName);
            }

            if (result.Deleted.Any())
                await _context.SaveChangesAsync();
        }
        private async Task ProcessFileAsync(string filePath, int userId, ImportScanResult result)
        {
            var fileName = Path.GetFileName(filePath);
            var fileBytes = await File.ReadAllBytesAsync(filePath);
            var sha256 = FileStorageService.ComputeSha256(fileBytes);

            var existingFile = await _context.Files
                .FirstOrDefaultAsync(f => f.UserId == userId && f.Sha256 == sha256);

            try
            {
                var ext = Path.GetExtension(fileName);
                var creationTime = File.GetCreationTime(filePath);
                var backupPath = _storage.GetBackupPath(userId, fileName, ext, creationTime);

                if (!File.Exists(backupPath))
                    _storage.SaveBackup(userId, fileName, ext, fileBytes, creationTime);
            }
            catch { /* non-critical */ }

            if (existingFile != null)
            {
                result.AlreadyImported.Add(fileName);
                return;
            }

            var parseResult = await _parser.ParseAsync(fileBytes, fileName, null);
            if (parseResult == null)
            {
                result.Errors.Add($"{fileName}: Failed to parse Pokemon file");
                return;
            }

            parseResult.File.UserId = userId;
            parseResult.File.StoredPath = filePath;
            parseResult.File.RawBlob = fileBytes;

            _context.Files.Add(parseResult.File);
            await _context.SaveChangesAsync();

            parseResult.Pokemon.UserId = userId;
            parseResult.Pokemon.FileId = parseResult.File.Id;
            _context.Pokemon.Add(parseResult.Pokemon);
            await _context.SaveChangesAsync();

            if (parseResult.Stats != null)
            {
                parseResult.Stats.PokemonId = parseResult.Pokemon.Id;
                _context.Stats.Add(parseResult.Stats);
            }

            if (parseResult.Moves.Any())
            {
                foreach (var move in parseResult.Moves)
                    move.PokemonId = parseResult.Pokemon.Id;
                _context.Moves.AddRange(parseResult.Moves);
            }

            if (parseResult.RelearnMoves.Any())
            {
                foreach (var rm in parseResult.RelearnMoves)
                    rm.PokemonId = parseResult.Pokemon.Id;
                _context.RelearnMoves.AddRange(parseResult.RelearnMoves);
            }

            await _context.SaveChangesAsync();
            result.NewlyImported.Add(fileName);
        }

        private static bool IsInIgnoredDirectory(string filePath, string backupPath)
        {
            var normalizedFilePath = Path.GetFullPath(filePath);
            var normalizedBackupPath = Path.GetFullPath(backupPath);

            if (normalizedFilePath.StartsWith(normalizedBackupPath, StringComparison.OrdinalIgnoreCase))
                return true;

            var directoryPath = Path.GetDirectoryName(normalizedFilePath);
            if (directoryPath != null)
            {
                foreach (var part in directoryPath.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar))
                {
                    if (part.StartsWith('.') && part.Length > 1)
                        return true;
                }
            }

            return false;
        }

        private static bool IsPokemonFile(string filePath)
        {
            var extension = Path.GetExtension(filePath).ToLowerInvariant();
            return extension switch
            {
                ".pk1" or ".pk2" or ".pk3" or ".pk4" or ".pk5" or ".pk6" or ".pk7" or ".pk8" or ".pk9" => true,
                ".pb7" or ".pb8" or ".pb9" => true,
                ".pa8" or ".pa9" => true,
                ".ek1" or ".ek2" or ".ek3" or ".ek4" or ".ek5" or ".ek6" or ".ek7" or ".ek8" or ".ek9" => true,
                ".ekx" => true,
                _ => false
            };
        }
    }

    public class ImportScanResult
    {
        public List<string> NewlyImported { get; } = new();
        public List<string> AlreadyImported { get; } = new();
        public List<string> Deleted { get; } = new();
        public List<string> Errors { get; } = new();

        public int TotalProcessed => NewlyImported.Count + AlreadyImported.Count + Deleted.Count + Errors.Count;
    }
}
