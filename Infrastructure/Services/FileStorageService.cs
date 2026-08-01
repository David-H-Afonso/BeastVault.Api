
using System.Security.Cryptography;
using BeastVault.Api.Infrastructure.Configuration;

namespace BeastVault.Api.Infrastructure.Services
{
    public class FileStorageService
    {
        private readonly string _basePath;
        private readonly string _backupPath;
        private readonly StorageConfiguration _storageConfig;

        // Propiedades públicas para acceder a las rutas
        public string BasePath => _basePath;
        public string BackupPath => _backupPath;

        public FileStorageService(StorageConfiguration storageConfig)
        {
            _storageConfig = storageConfig;
            _basePath = storageConfig.PokemonFilesDirectory;
            _backupPath = storageConfig.BackupDirectory;
        }

        /// <summary>
        /// Get the user-specific storage directory: {basePath}/{userId}/
        /// </summary>
        public string GetUserBasePath(int userId) => Path.Combine(_basePath, userId.ToString());

        /// <summary>
        /// Get the user-specific backup directory: {basePath}/{userId}/backup/
        /// </summary>
        public string GetUserBackupPath(int userId) => Path.Combine(GetUserBasePath(userId), "backup");

        public string GetUserSavesPath(int userId) => Path.Combine(GetUserBasePath(userId), "saves");

        public void EnsureVault()
        {
            Directory.CreateDirectory(_basePath);
            // Don't create global backup — backups are per-user now
        }

        /// <summary>
        /// Ensures the user-specific directories exist
        /// </summary>
        public void EnsureUserVault(int userId)
        {
            var userPath = GetUserBasePath(userId);
            var userBackupPath = GetUserBackupPath(userId);
            Directory.CreateDirectory(userPath);
            Directory.CreateDirectory(userBackupPath);
            Directory.CreateDirectory(GetUserSavesPath(userId));
            Console.WriteLine($"Ensured user vault: {userPath}");
        }

        /// <summary>
        /// Get all user IDs that have folders under the base path
        /// </summary>
        public List<int> GetExistingUserIds()
        {
            var userIds = new List<int>();
            if (!Directory.Exists(_basePath)) return userIds;

            foreach (var dir in Directory.GetDirectories(_basePath))
            {
                var dirName = Path.GetFileName(dir);
                if (int.TryParse(dirName, out var userId))
                    userIds.Add(userId);
            }
            return userIds;
        }

        public static string ComputeSha256(byte[] bytes)
        {
            using var sha = SHA256.Create();
            var hash = sha.ComputeHash(bytes);
            return Convert.ToHexString(hash).ToLowerInvariant();
        }

        public string Save(string sha256, string ext, byte[] bytes, int userId, string? pokemonName = null, DateTime? importDate = null, string? originalFileName = null)
        {
            ext = ext.TrimStart('.').ToLowerInvariant();
            var safeName = string.IsNullOrWhiteSpace(pokemonName) ? "pokemon" : SanitizeFileName(pokemonName);
            var shortHash = sha256.Length > 8 ? sha256[..8] : sha256;

            // Save to user-specific directory: {basePath}/{userId}/
            var userPath = GetUserBasePath(userId);
            Directory.CreateDirectory(userPath);

            var filePath = Path.Combine(userPath, $"{safeName}_{shortHash}.{ext}");
            File.WriteAllBytes(filePath, bytes);

            // Save backup in user-specific backup folder
            if (!string.IsNullOrWhiteSpace(originalFileName))
            {
                SaveBackup(originalFileName, ext, bytes, userId, importDate);
            }

            return filePath;
        }

        public string SaveBackup(string originalFileName, string ext, byte[] bytes, int userId, DateTime? importDate = null)
        {
            ext = ext.TrimStart('.').ToLowerInvariant();

            var validExtensions = new[] { "pk1", "pk2", "pk3", "pk4", "pk5", "pk6", "pk7", "pk8", "pk9", "pb7", "pb8", "pb9", "pa8", "pa9" };
            if (!validExtensions.Contains(ext))
            {
                Console.WriteLine($"Warning: Unrecognized PKM format: {ext}");
            }

            // Create backup structure: {basePath}/{userId}/backup/{format}/{year}/
            var year = (importDate ?? DateTime.Now).Year.ToString();
            var formatFolder = ext;
            var userBackupPath = GetUserBackupPath(userId);
            var backupDir = Path.Combine(userBackupPath, formatFolder, year);

            Directory.CreateDirectory(backupDir);

            // Check if a backup with the same content already exists (same SHA256)
            var incomingHash = ComputeSha256(bytes);
            var existingFiles = Directory.GetFiles(backupDir, $"*.{ext}");

            foreach (var existingFile in existingFiles)
            {
                try
                {
                    var existingBytes = File.ReadAllBytes(existingFile);
                    var existingHash = ComputeSha256(existingBytes);

                    if (existingHash == incomingHash)
                    {
                        Console.WriteLine($"Backup already exists with same content: {existingFile} (skipping duplicate)");
                        return existingFile;
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error checking existing backup file {existingFile}: {ex.Message}");
                }
            }

            var backupFilePath = Path.Combine(backupDir, originalFileName);
            File.WriteAllBytes(backupFilePath, bytes);

            Console.WriteLine($"Backup saved: {backupFilePath}");
            return backupFilePath;
        }

        public void Delete(string path)
        {
            try
            {
                if (File.Exists(path))
                {
                    File.Delete(path);
                    Console.WriteLine($"Deleted file: {path}");
                }
                else
                {
                    Console.WriteLine($"File not found for deletion: {path}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error deleting file {path}: {ex.Message}");
                throw;
            }
        }

        public bool DeleteUserFile(int userId, string storedPath)
        {
            if (!TryResolveUserPath(userId, storedPath, out var fullPath))
                return false;

            Delete(fullPath);
            return true;
        }

        public void DeleteBackup(string originalFileName, string ext, int userId, DateTime? importDate = null)
        {
            try
            {
                var year = (importDate ?? DateTime.Now).Year.ToString();
                var formatFolder = ext.TrimStart('.').ToLowerInvariant();
                var userBackupPath = GetUserBackupPath(userId);
                var backupFilePath = Path.Combine(userBackupPath, formatFolder, year, originalFileName);

                if (File.Exists(backupFilePath))
                {
                    File.Delete(backupFilePath);
                    Console.WriteLine($"Deleted backup file: {backupFilePath}");
                }
                else
                {
                    Console.WriteLine($"Backup file not found for deletion: {backupFilePath}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error deleting backup file: {ex.Message}");
                throw;
            }
        }

        private static string SanitizeFileName(string name)
        {
            foreach (var c in Path.GetInvalidFileNameChars())
                name = name.Replace(c, '_');
            return name.Replace(" ", "_");
        }

        public byte[] Read(string path) => File.ReadAllBytes(path);

        public bool TryReadUserFile(int userId, string storedPath, out byte[] content)
        {
            content = [];
            if (userId <= 0 || string.IsNullOrWhiteSpace(storedPath)) return false;

            try
            {
                if (!TryResolveUserPath(userId, storedPath, out var fullPath)) return false;
                content = File.ReadAllBytes(fullPath);
                return true;
            }
            catch (Exception exception) when (exception is
                ArgumentException or
                IOException or
                NotSupportedException or
                UnauthorizedAccessException)
            {
                return false;
            }
        }

        private bool TryResolveUserPath(int userId, string storedPath, out string fullPath)
        {
            fullPath = string.Empty;
            if (userId <= 0 || string.IsNullOrWhiteSpace(storedPath)) return false;

            try
            {
                var userRoot = Path.GetFullPath(GetUserBasePath(userId));
                var candidatePath = Path.IsPathFullyQualified(storedPath)
                    ? storedPath
                    : Path.Combine(userRoot, storedPath);
                fullPath = Path.GetFullPath(candidatePath);
                var relativePath = Path.GetRelativePath(userRoot, fullPath);
                return relativePath != "." &&
                    !Path.IsPathRooted(relativePath) &&
                    relativePath != ".." &&
                    !relativePath.StartsWith($"..{Path.DirectorySeparatorChar}", StringComparison.Ordinal) &&
                    !relativePath.StartsWith($"..{Path.AltDirectorySeparatorChar}", StringComparison.Ordinal);
            }
            catch (Exception exception) when (exception is
                ArgumentException or
                IOException or
                NotSupportedException or
                UnauthorizedAccessException)
            {
                return false;
            }
        }

        public string SaveGameSave(
            string sha256,
            string format,
            byte[] bytes,
            int userId,
            int generation,
            string originalFileName)
        {
            var safeFormat = string.IsNullOrWhiteSpace(format) ? "main" : SanitizeFileName(format.TrimStart('.').ToLowerInvariant());
            var safeOriginalName = SanitizeFileName(Path.GetFileName(originalFileName));
            if (string.IsNullOrWhiteSpace(safeOriginalName))
                safeOriginalName = "save";

            var shortHash = sha256.Length > 8 ? sha256[..8] : sha256;
            var saveDirectory = Path.Combine(GetUserSavesPath(userId), $"gen-{generation}");
            Directory.CreateDirectory(saveDirectory);

            var baseName = Path.GetFileNameWithoutExtension(safeOriginalName);
            if (string.IsNullOrWhiteSpace(baseName) || string.Equals(safeOriginalName, "main", StringComparison.OrdinalIgnoreCase))
                baseName = safeOriginalName;
            var filePath = Path.Combine(saveDirectory, $"{baseName}_{shortHash}.{safeFormat}");
            File.WriteAllBytes(filePath, bytes);
            return filePath;
        }

        /// <summary>
        /// Get the path where a user's backup file should be
        /// </summary>
        public string GetBackupPath(string originalFileName, string ext, int userId, DateTime? importDate = null)
        {
            var year = (importDate ?? DateTime.Now).Year.ToString();
            var formatFolder = ext.TrimStart('.').ToLowerInvariant();
            var userBackupPath = GetUserBackupPath(userId);
            var backupDir = Path.Combine(userBackupPath, formatFolder, year);
            return Path.Combine(backupDir, originalFileName);
        }
    }
}
