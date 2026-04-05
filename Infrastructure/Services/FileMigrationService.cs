using Microsoft.EntityFrameworkCore;
using BeastVault.Api.Infrastructure.Configuration;

namespace BeastVault.Api.Infrastructure.Services
{
    public static class FileMigrationService
    {
        public static async Task MigrateRootFilesToUserDirectoryAsync(
            AppDbContext db, StorageConfiguration storageConfig)
        {
            var basePath = storageConfig.PokemonFilesDirectory;
            var defaultUserId = 1;
            var userDir = storageConfig.GetUserDirectory(defaultUserId);

            if (!Directory.Exists(basePath))
                return;

            // Skip if user directory already has files (already migrated)
            if (Directory.Exists(userDir) && Directory.GetFiles(userDir, "*.*").Any())
                return;

            var rootFiles = Directory.GetFiles(basePath, "*.*", SearchOption.TopDirectoryOnly)
                .Where(f => IsPokemonFile(f))
                .ToList();

            if (!rootFiles.Any())
                return;

            storageConfig.EnsureUserDirectoriesExist(defaultUserId);

            var movedCount = 0;
            foreach (var file in rootFiles)
            {
                var fileName = Path.GetFileName(file);
                var destination = Path.Combine(userDir, fileName);

                try
                {
                    File.Move(file, destination, overwrite: false);
                    movedCount++;
                }
                catch (IOException)
                {
                    // File already exists at destination, skip
                }
            }

            // Migrate backup directory if it exists at root level
            var rootBackup = Path.Combine(basePath, "backup");
            var userBackup = storageConfig.GetUserBackupDirectory(defaultUserId);

            if (Directory.Exists(rootBackup) && !Directory.Exists(userBackup))
            {
                try
                {
                    Directory.Move(rootBackup, userBackup);
                }
                catch (IOException)
                {
                    // Fallback: copy files individually
                    CopyDirectoryRecursive(rootBackup, userBackup);
                }
            }

            // Update StoredPath in database for migrated files
            var allFiles = await db.Files.Where(f => f.UserId == defaultUserId).ToListAsync();
            foreach (var dbFile in allFiles)
            {
                if (string.IsNullOrEmpty(dbFile.StoredPath))
                    continue;

                var oldFileName = Path.GetFileName(dbFile.StoredPath);
                var newPath = Path.Combine(userDir, oldFileName);

                if (File.Exists(newPath))
                    dbFile.StoredPath = newPath;
            }

            if (allFiles.Any())
                await db.SaveChangesAsync();

            if (movedCount > 0)
                Console.WriteLine($"Migrated {movedCount} root-level files to user {defaultUserId} directory.");
        }

        private static void CopyDirectoryRecursive(string source, string destination)
        {
            Directory.CreateDirectory(destination);

            foreach (var file in Directory.GetFiles(source))
            {
                var destFile = Path.Combine(destination, Path.GetFileName(file));
                File.Copy(file, destFile, overwrite: false);
            }

            foreach (var dir in Directory.GetDirectories(source))
            {
                var destDir = Path.Combine(destination, Path.GetFileName(dir));
                CopyDirectoryRecursive(dir, destDir);
            }
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
}
