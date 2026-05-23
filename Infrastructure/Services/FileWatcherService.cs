using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using BeastVault.Api.Domain.Entities;
using BeastVault.Api.Infrastructure;

namespace BeastVault.Api.Infrastructure.Services
{
    /// <summary>
    /// Service to monitor and automatically import Pokemon files from user-specific subdirectories
    /// Each user's files are stored in {basePath}/{userId}/
    /// </summary>
    public class FileWatcherService
    {
        private readonly AppDbContext _context;
        private readonly PkhexCoreParser _parser;
        private readonly FileStorageService _storage;
        private readonly string _watchPath;

        public FileWatcherService(AppDbContext context, PkhexCoreParser parser, FileStorageService storage)
        {
            _context = context;
            _parser = parser;
            _storage = storage;

            _watchPath = storage.BasePath;

            Console.WriteLine($"Watching directory for Pokemon files: {_watchPath}");

            if (!Directory.Exists(_watchPath))
            {
                Directory.CreateDirectory(_watchPath);
            }
        }

        /// <summary>
        /// Scans all user-specific subdirectories for new Pokemon files and imports them.
        /// Also handles legacy files in the root directory (migrates to user 1).
        /// </summary>
        public async Task<ImportScanResult> ScanAndImportNewFilesAsync()
        {
            var result = new ImportScanResult();

            try
            {
                // Always check for deleted files first
                await CleanupDeletedFilesAsync(result);

                // Scan each user's subdirectory
                var userIds = _storage.GetExistingUserIds();
                foreach (var userId in userIds)
                {
                    await ScanUserDirectoryAsync(userId, result);
                }

                // Handle legacy files in root directory (not in any user subfolder)
                await ScanLegacyRootFilesAsync(result);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error scanning directory {_watchPath}: {ex.Message}");
                result.Errors.Add($"Directory scan error: {ex.Message}");
            }

            return result;
        }

        /// <summary>
        /// Scans a specific user's directory for new Pokemon files
        /// </summary>
        public async Task<ImportScanResult> ScanUserDirectoryAsync(int userId)
        {
            var result = new ImportScanResult();
            await ScanUserDirectoryAsync(userId, result);
            return result;
        }

        private async Task ScanUserDirectoryAsync(int userId, ImportScanResult result)
        {
            var userPath = _storage.GetUserBasePath(userId);
            if (!Directory.Exists(userPath)) return;

            var userBackupPath = _storage.GetUserBackupPath(userId);

            var pokemonFiles = Directory.GetFiles(userPath, "*.*", SearchOption.AllDirectories)
                .Where(file => IsPokemonFile(file) && !IsInIgnoredDirectory(file, userBackupPath))
                .ToList();

            Console.WriteLine($"Found {pokemonFiles.Count} Pokemon files in user {userId} directory");

            foreach (var filePath in pokemonFiles)
            {
                try
                {
                    await ProcessFileAsync(filePath, userId, result);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error processing file {filePath}: {ex.Message}");
                    result.Errors.Add($"{Path.GetFileName(filePath)}: {ex.Message}");
                }
            }
        }

        /// <summary>
        /// Handles legacy files sitting in the root directory (not in user subfolders).
        /// These are files from before per-user directory support.
        /// </summary>
        private async Task ScanLegacyRootFilesAsync(ImportScanResult result)
        {
            // Only scan immediate files in root, not subdirectories (those are user folders)
            if (!Directory.Exists(_watchPath)) return;

            var rootFiles = Directory.GetFiles(_watchPath, "*.*", SearchOption.TopDirectoryOnly)
                .Where(file => IsPokemonFile(file))
                .ToList();

            if (rootFiles.Count == 0) return;

            Console.WriteLine($"Found {rootFiles.Count} legacy Pokemon files in root directory");

            foreach (var filePath in rootFiles)
            {
                try
                {
                    // Legacy files are associated with userId 0 (no owner)
                    await ProcessFileAsync(filePath, 0, result);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error processing legacy file {filePath}: {ex.Message}");
                    result.Errors.Add($"{Path.GetFileName(filePath)}: {ex.Message}");
                }
            }
        }

        private async Task CleanupDeletedFilesAsync(ImportScanResult result)
        {
            try
            {
                // Build a set of all current file hashes across all user directories
                var currentFileHashes = new HashSet<string>();

                // Scan all user directories
                var userIds = _storage.GetExistingUserIds();
                foreach (var userId in userIds)
                {
                    var userPath = _storage.GetUserBasePath(userId);
                    if (!Directory.Exists(userPath)) continue;

                    var userBackupPath = _storage.GetUserBackupPath(userId);
                    var userFiles = Directory.GetFiles(userPath, "*.*", SearchOption.AllDirectories)
                        .Where(file => IsPokemonFile(file) && !IsInIgnoredDirectory(file, userBackupPath));

                    foreach (var filePath in userFiles)
                    {
                        try
                        {
                            var fileBytes = await File.ReadAllBytesAsync(filePath);
                            var hash = FileStorageService.ComputeSha256(fileBytes);
                            currentFileHashes.Add(hash);
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"Error reading file {filePath} for hash calculation: {ex.Message}");
                        }
                    }
                }

                // Also scan legacy root files
                var rootFiles = Directory.GetFiles(_watchPath, "*.*", SearchOption.TopDirectoryOnly)
                    .Where(IsPokemonFile);
                foreach (var filePath in rootFiles)
                {
                    try
                    {
                        var fileBytes = await File.ReadAllBytesAsync(filePath);
                        var hash = FileStorageService.ComputeSha256(fileBytes);
                        currentFileHashes.Add(hash);
                    }
                    catch { }
                }

                // Get all files in database
                var allDbFiles = await _context.Files.ToListAsync();

                foreach (var dbFile in allDbFiles)
                {
                    if (!currentFileHashes.Contains(dbFile.Sha256))
                    {
                        var pokemon = await _context.Pokemon.FirstOrDefaultAsync(p => p.FileId == dbFile.Id);
                        if (pokemon != null)
                        {
                            var stats = await _context.Stats.FirstOrDefaultAsync(s => s.PokemonId == pokemon.Id);
                            if (stats != null) _context.Stats.Remove(stats);

                            var moves = await _context.Moves.Where(m => m.PokemonId == pokemon.Id).ToListAsync();
                            _context.Moves.RemoveRange(moves);

                            var relearnMoves = await _context.RelearnMoves.Where(r => r.PokemonId == pokemon.Id).ToListAsync();
                            _context.RelearnMoves.RemoveRange(relearnMoves);

                            _context.Pokemon.Remove(pokemon);
                        }

                        _context.Files.Remove(dbFile);
                        result.Deleted.Add(dbFile.FileName);
                        Console.WriteLine($"Removed deleted file from database: {dbFile.FileName} (backup preserved)");
                    }
                }

                if (result.Deleted.Any())
                {
                    await _context.SaveChangesAsync();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error during cleanup: {ex.Message}");
                result.Errors.Add($"Cleanup error: {ex.Message}");
            }
        }

        private async Task ProcessFileAsync(string filePath, int userId, ImportScanResult result)
        {
            var fileName = Path.GetFileName(filePath);
            var fileBytes = await File.ReadAllBytesAsync(filePath);
            var sha256 = FileStorageService.ComputeSha256(fileBytes);

            // Check if file is already imported (per user)
            var existingFile = userId > 0
                ? await _context.Files.FirstOrDefaultAsync(f => f.Sha256 == sha256 && f.UserId == userId)
                : await _context.Files.FirstOrDefaultAsync(f => f.Sha256 == sha256);

            // Verify and create backup if not exists
            if (userId > 0)
            {
                try
                {
                    var ext = Path.GetExtension(fileName);
                    var creationTime = File.GetCreationTime(filePath);
                    var backupPath = _storage.GetBackupPath(fileName, ext, userId, creationTime);

                    if (!File.Exists(backupPath))
                    {
                        _storage.SaveBackup(fileName, ext, fileBytes, userId, creationTime);
                        Console.WriteLine($"✅ Backup created for directory file: {fileName} (user {userId})");
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"⚠️  Warning: Could not create/verify backup for {fileName}: {ex.Message}");
                }
            }

            if (existingFile != null)
            {
                result.AlreadyImported.Add(fileName);
                return;
            }

            // Parse the Pokemon file WITHOUT creating a duplicate on disk
            var parseResult = await _parser.ParseAsync(fileBytes, fileName, null);
            if (parseResult == null)
            {
                result.Errors.Add($"{fileName}: Failed to parse Pokemon file");
                return;
            }

            // Set the original file path as the stored path
            parseResult.File.StoredPath = filePath;
            parseResult.File.RawBlob = fileBytes;
            parseResult.File.UserId = userId;

            // Save to database
            _context.Files.Add(parseResult.File);
            await _context.SaveChangesAsync();

            parseResult.Pokemon.FileId = parseResult.File.Id;
            parseResult.Pokemon.UserId = userId;
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
                foreach (var relearnMove in parseResult.RelearnMoves)
                    relearnMove.PokemonId = parseResult.Pokemon.Id;
                _context.RelearnMoves.AddRange(parseResult.RelearnMoves);
            }

            await _context.SaveChangesAsync();

            result.NewlyImported.Add(fileName);
            Console.WriteLine($"Successfully imported: {fileName} (user {userId})");
        }

        /// <summary>
        /// Checks if a file path is within directories that should be ignored
        /// </summary>
        private bool IsInIgnoredDirectory(string filePath, string backupPath)
        {
            var normalizedFilePath = Path.GetFullPath(filePath);
            var normalizedBackupPath = Path.GetFullPath(backupPath);

            if (normalizedFilePath.StartsWith(normalizedBackupPath, StringComparison.OrdinalIgnoreCase))
                return true;

            var directoryPath = Path.GetDirectoryName(normalizedFilePath);
            if (directoryPath != null)
            {
                var pathParts = directoryPath.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                foreach (var part in pathParts)
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
                ".pa8" => true,
                ".pa9" => true,
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
