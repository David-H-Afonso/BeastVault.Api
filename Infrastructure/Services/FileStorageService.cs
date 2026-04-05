
using System.Security.Cryptography;
using BeastVault.Api.Infrastructure.Configuration;

namespace BeastVault.Api.Infrastructure.Services
{
    public class FileStorageService
    {
        private readonly StorageConfiguration _storageConfig;

        public string BasePath => _storageConfig.PokemonFilesDirectory;

        public FileStorageService(StorageConfiguration storageConfig)
        {
            _storageConfig = storageConfig;
        }

        public void EnsureVault()
        {
            Directory.CreateDirectory(BasePath);
        }

        public void EnsureUserVault(int userId)
        {
            _storageConfig.EnsureUserDirectoriesExist(userId);
        }

        public static string ComputeSha256(byte[] bytes)
        {
            using var sha = SHA256.Create();
            var hash = sha.ComputeHash(bytes);
            return Convert.ToHexString(hash).ToLowerInvariant();
        }

        public string GetUserDirectory(int userId) => _storageConfig.GetUserDirectory(userId);
        public string GetUserBackupDirectory(int userId) => _storageConfig.GetUserBackupDirectory(userId);

        public string Save(int userId, string sha256, string ext, byte[] bytes,
            string? pokemonName = null, DateTime? importDate = null, string? originalFileName = null)
        {
            EnsureUserVault(userId);

            ext = ext.TrimStart('.').ToLowerInvariant();
            var safeName = string.IsNullOrWhiteSpace(pokemonName) ? "pokemon" : SanitizeFileName(pokemonName);
            var shortHash = sha256.Length > 8 ? sha256[..8] : sha256;

            var userDir = GetUserDirectory(userId);
            var filePath = Path.Combine(userDir, $"{safeName}_{shortHash}.{ext}");
            File.WriteAllBytes(filePath, bytes);

            if (!string.IsNullOrWhiteSpace(originalFileName))
                SaveBackup(userId, originalFileName, ext, bytes, importDate);

            return filePath;
        }

        public string SaveBackup(int userId, string originalFileName, string ext, byte[] bytes,
            DateTime? importDate = null)
        {
            EnsureUserVault(userId);

            ext = ext.TrimStart('.').ToLowerInvariant();

            var year = (importDate ?? DateTime.Now).Year.ToString();
            var backupDir = Path.Combine(GetUserBackupDirectory(userId), ext, year);
            Directory.CreateDirectory(backupDir);

            var incomingHash = ComputeSha256(bytes);
            var existingFiles = Directory.GetFiles(backupDir, $"*.{ext}");

            foreach (var existingFile in existingFiles)
            {
                try
                {
                    var existingBytes = File.ReadAllBytes(existingFile);
                    if (ComputeSha256(existingBytes) == incomingHash)
                        return existingFile;
                }
                catch { /* continue checking */ }
            }

            var backupFilePath = Path.Combine(backupDir, originalFileName);
            File.WriteAllBytes(backupFilePath, bytes);
            return backupFilePath;
        }

        public void Delete(string path)
        {
            if (File.Exists(path))
                File.Delete(path);
        }

        public void DeleteBackup(int userId, string originalFileName, string ext, DateTime? importDate = null)
        {
            var year = (importDate ?? DateTime.Now).Year.ToString();
            var formatFolder = ext.TrimStart('.').ToLowerInvariant();
            var backupFilePath = Path.Combine(GetUserBackupDirectory(userId), formatFolder, year, originalFileName);

            if (File.Exists(backupFilePath))
                File.Delete(backupFilePath);
        }

        public byte[] Read(string path) => File.ReadAllBytes(path);

        public string GetBackupPath(int userId, string originalFileName, string ext, DateTime? importDate = null)
        {
            var year = (importDate ?? DateTime.Now).Year.ToString();
            var formatFolder = ext.TrimStart('.').ToLowerInvariant();
            return Path.Combine(GetUserBackupDirectory(userId), formatFolder, year, originalFileName);
        }

        private static string SanitizeFileName(string name)
        {
            foreach (var c in Path.GetInvalidFileNameChars())
                name = name.Replace(c, '_');
            return name.Replace(" ", "_");
        }
    }
}
