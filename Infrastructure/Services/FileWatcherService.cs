using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using BeastVault.Api.Domain.Entities;
using BeastVault.Api.Infrastructure;

namespace BeastVault.Api.Infrastructure.Services
{
    /// <summary>
    /// Service to monitor and automatically import Pokemon files from the Documents/BeastVault directory (excluding backup folder)
    /// </summary>
    public class FileWatcherService
    {
        private readonly AppDbContext _context;
        private readonly PkhexCoreParser _parser;
        private readonly FileStorageService _storage;
        private readonly string _watchPath;
        private readonly string _backupPath;

        public FileWatcherService(AppDbContext context, PkhexCoreParser parser, FileStorageService storage)
        {
            _context = context;
            _parser = parser;
            _storage = storage;

            // Usar las rutas del servicio FileStorageService que ya tiene configuración aplicada
            _watchPath = storage.BasePath;
            _backupPath = storage.BackupPath;

            Console.WriteLine($"Watching directory for Pokemon files: {_watchPath}");
            Console.WriteLine($"Using backup directory: {_backupPath}");

            // Estos directorios ya deberían existir por la configuración inicial,
            // pero verificamos por seguridad
            if (!Directory.Exists(_watchPath))
            {
                Directory.CreateDirectory(_watchPath);
            }

            if (!Directory.Exists(_backupPath))
            {
                Directory.CreateDirectory(_backupPath);
            }
        }

        /// <summary>
        /// Scans the watch directory for new Pokemon files and imports them
        /// Also removes database entries for files that no longer exist in the user area (NOT backup)
        /// </summary>
        public async Task<ImportScanResult> ScanAndImportNewFilesAsync()
        {
            var result = new ImportScanResult();

            try
            {
                // Always check for deleted files first, regardless of whether there are new files
                await CleanupDeletedFilesAsync(result);

                // Get all Pokemon files in the directory and subdirectories, EXCLUDING backup folder
                var pokemonFiles = Directory.GetFiles(_watchPath, "*.*", SearchOption.AllDirectories)
                    .Where(file => IsPokemonFile(file) && !IsInBackupDirectory(file))
                    .ToList();

                Console.WriteLine($"Found {pokemonFiles.Count} Pokemon files in watch directory (excluding backup)");

                foreach (var filePath in pokemonFiles)
                {
                    try
                    {
                        await ProcessFileAsync(filePath, result);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Error processing file {filePath}: {ex.Message}");
                        result.Errors.Add($"{Path.GetFileName(filePath)}: {ex.Message}");
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error scanning directory {_watchPath}: {ex.Message}");
                result.Errors.Add($"Directory scan error: {ex.Message}");
            }

            return result;
        }

        private async Task CleanupDeletedFilesAsync(ImportScanResult result)
        {
            try
            {
                // Get all files currently in the user area (excluding backup)
                var currentUserFiles = Directory.GetFiles(_watchPath, "*.*", SearchOption.AllDirectories)
                    .Where(file => IsPokemonFile(file) && !IsInBackupDirectory(file))
                    .ToList();

                // Calculate hashes for current files
                var currentFileHashes = new HashSet<string>();
                foreach (var filePath in currentUserFiles)
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

                // Get all files in database
                var allDbFiles = await _context.Files.ToListAsync();

                foreach (var dbFile in allDbFiles)
                {
                    // If the file hash is not found in current user files, it means the user deleted it
                    // Remove from database but keep backup file intact
                    if (!currentFileHashes.Contains(dbFile.Sha256))
                    {
                        var pokemon = await _context.Pokemon.FirstOrDefaultAsync(p => p.FileId == dbFile.Id);
                        if (pokemon != null)
                        {
                            // Remove related data
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
        private async Task ProcessFileAsync(string filePath, ImportScanResult result)
        {
            var fileName = Path.GetFileName(filePath);
            var fileBytes = await File.ReadAllBytesAsync(filePath);
            var sha256 = FileStorageService.ComputeSha256(fileBytes);

            // Check if file is already imported
            var existingFile = await _context.Files
                .FirstOrDefaultAsync(f => f.Sha256 == sha256);

            if (existingFile != null)
            {
                result.AlreadyImported.Add(fileName);
                return;
            }

            // Parse the Pokemon file WITHOUT creating a duplicate
            // Since this is a file scan, we want to register the existing file location
            var parseResult = await _parser.ParseAsync(fileBytes, fileName, null);
            if (parseResult == null)
            {
                result.Errors.Add($"{fileName}: Failed to parse Pokemon file");
                return;
            }

            // Set the original file path as the stored path
            parseResult.File.StoredPath = filePath;
            parseResult.File.RawBlob = fileBytes;

            // Save to database
            _context.Files.Add(parseResult.File);
            await _context.SaveChangesAsync(); // Save File first to get ID

            parseResult.Pokemon.FileId = parseResult.File.Id;
            _context.Pokemon.Add(parseResult.Pokemon);
            await _context.SaveChangesAsync(); // Save Pokemon to get ID

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
            Console.WriteLine($"Successfully imported: {fileName}");
        }

        /// <summary>
        /// Checks if a file path is within the backup directory
        /// </summary>
        private bool IsInBackupDirectory(string filePath)
        {
            var normalizedFilePath = Path.GetFullPath(filePath);
            var normalizedBackupPath = Path.GetFullPath(_backupPath);
            return normalizedFilePath.StartsWith(normalizedBackupPath, StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsPokemonFile(string filePath)
        {
            var extension = Path.GetExtension(filePath).ToLowerInvariant();
            return extension switch
            {
                ".pk1" or ".pk2" or ".pk3" or ".pk4" or ".pk5" or ".pk6" or ".pk7" or ".pk8" or ".pk9" => true,
                ".pb7" or ".pb8" => true, // Pokemon Box files
                ".ek1" or ".ek2" or ".ek3" or ".ek4" or ".ek5" or ".ek6" or ".ek7" or ".ek8" or ".ek9" => true, // Encrypted
                ".ekx" => true, // Encrypted batch
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
