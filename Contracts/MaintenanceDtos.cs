namespace BeastVault.Api.Contracts;

public class SyncResult
{
    public int TotalFilesInDatabase { get; set; }
    public List<string> RemovedFiles { get; set; } = new();
    public List<string> RemovedPokemon { get; set; } = new();
    public List<string> ValidFiles { get; set; } = new();
    public string Summary => $"Removed {RemovedFiles.Count} orphaned files and {RemovedPokemon.Count} Pokemon. {ValidFiles.Count} files remain valid.";
}

public class MaintenanceStatusResult
{
    public int TotalPokemonInDatabase { get; set; }
    public int TotalFilesInDatabase { get; set; }
    public int TotalFilesInBackupDirectory { get; set; }
    public string BackupDirectoryPath { get; set; } = string.Empty;
    public List<OrphanedFileInfo> OrphanedFiles { get; set; } = new();
    public bool IsInSync => OrphanedFiles.Count == 0;
    public string Summary => $"Database: {TotalPokemonInDatabase} Pokemon, {TotalFilesInDatabase} files. Directory: {TotalFilesInBackupDirectory} files. Orphaned: {OrphanedFiles.Count}";
}

public class OrphanedFileInfo
{
    public int Id { get; set; }
    public string FileName { get; set; } = string.Empty;
    public string StoredPath { get; set; } = string.Empty;
}

public class PokemonDuplicatesInfo
{
    public int PokemonId { get; set; }
    public string PokemonName { get; set; } = string.Empty;
    public string FileHash { get; set; } = string.Empty;
    public int DatabaseEntries { get; set; }
    public int PhysicalFiles { get; set; }
    public List<string> PhysicalFilePaths { get; set; } = new();
    public List<int> DatabaseFileIds { get; set; } = new();
    public bool IsInBackup { get; set; }
    public string Summary => $"{PokemonName}: {DatabaseEntries} DB entries, {PhysicalFiles} user files, backup: {IsInBackup}";
}

public class TotalDeletionResult
{
    public int DeletedFromDatabase { get; set; }
    public bool IncludedBackup { get; set; }
    public List<string> DeletedPokemonNames { get; set; } = new();
    public List<string> DeletedDatabaseFiles { get; set; } = new();
    public List<string> DeletedPhysicalFiles { get; set; } = new();
    public List<string> PreservedBackupFiles { get; set; } = new();
    public List<string> Errors { get; set; } = new();
    public string Summary => $"Deleted {DeletedFromDatabase} DB entries, {DeletedPhysicalFiles.Count} physical files. " +
                            $"Preserved {PreservedBackupFiles.Count} backup files. {Errors.Count} errors.";
}
