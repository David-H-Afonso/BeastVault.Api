
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

        public void EnsureVault()
        {
            Directory.CreateDirectory(_basePath);
            Directory.CreateDirectory(_backupPath);
        }

        public static string ComputeSha256(byte[] bytes)
        {
            using var sha = SHA256.Create();
            var hash = sha.ComputeHash(bytes);
            return Convert.ToHexString(hash).ToLowerInvariant();
        }


        public string Save(string sha256, string ext, byte[] bytes, string? pokemonName = null, DateTime? importDate = null, string? originalFileName = null)
        {
            // Guardar archivo principal directamente en la raíz de BeastVault para consistencia
            ext = ext.TrimStart('.').ToLowerInvariant();
            var safeName = string.IsNullOrWhiteSpace(pokemonName) ? "pokemon" : SanitizeFileName(pokemonName);
            var shortHash = sha256.Length > 8 ? sha256[..8] : sha256;

            // Guardar directamente en la raíz de BeastVault
            var filePath = Path.Combine(_basePath, $"{safeName}_{shortHash}.{ext}");
            File.WriteAllBytes(filePath, bytes);

            // NUEVO: Guardar backup original si se proporciona el nombre original
            if (!string.IsNullOrWhiteSpace(originalFileName))
            {
                SaveBackup(originalFileName, ext, bytes, importDate);
            }

            return filePath;
        }

        public string SaveBackup(string originalFileName, string ext, byte[] bytes, DateTime? importDate = null)
        {
            // Normalize extension handling for all PKHeX formats
            ext = ext.TrimStart('.').ToLowerInvariant();

            // Recognize all PKHeX formats including pa8 (Legends Arceus), pa9 (Legends Z-A) and pb9 (Pokemon Box Gen 9)
            var validExtensions = new[] { "pk1", "pk2", "pk3", "pk4", "pk5", "pk6", "pk7", "pk8", "pk9", "pb7", "pb8", "pb9", "pa8", "pa9" };
            if (!validExtensions.Contains(ext))
            {
                Console.WriteLine($"Warning: Unrecognized PKM format: {ext}");
            }

            // Create backup structure: backup/{format}/{year}/
            var year = (importDate ?? DateTime.Now).Year.ToString();
            var formatFolder = ext;
            var backupDir = Path.Combine(_backupPath, formatFolder, year);

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
                    // Continue checking other files
                }
            }

            // No duplicate backup exists, create new one
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

        public void DeleteBackup(string originalFileName, string ext, DateTime? importDate = null)
        {
            try
            {
                var year = (importDate ?? DateTime.Now).Year.ToString();
                var formatFolder = ext.TrimStart('.').ToLowerInvariant();
                var backupFilePath = Path.Combine(_backupPath, formatFolder, year, originalFileName);

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

        /// <summary>
        /// Construye la ruta donde debería estar el archivo de backup
        /// </summary>
        public string GetBackupPath(string originalFileName, string ext, DateTime? importDate = null)
        {
            var year = (importDate ?? DateTime.Now).Year.ToString();
            var formatFolder = ext.TrimStart('.').ToLowerInvariant();
            var backupDir = Path.Combine(_backupPath, formatFolder, year);
            return Path.Combine(backupDir, originalFileName);
        }
    }
}
