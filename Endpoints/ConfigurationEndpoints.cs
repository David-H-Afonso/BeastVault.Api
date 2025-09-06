using BeastVault.Api.Infrastructure.Configuration;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System;
using System.IO;

namespace BeastVault.Api.Endpoints
{
    public static class ConfigurationEndpoints
    {
        public static void MapConfigurationEndpoints(this WebApplication app)
        {
            app.MapGet("/config", (StorageConfiguration config) =>
            {
                return Results.Ok(new
                {
                    Platform = new
                    {
                        IsDocker = config.IsDocker,
                        IsWindows = config.IsWindows,
                        IsMacOS = config.IsMacOS,
                        IsLinux = config.IsLinux,
                        PlatformName = config.PlatformName
                    },
                    Paths = new
                    {
                        DatabasePath = config.DatabasePath,
                        PokemonFilesDirectory = config.PokemonFilesDirectory,
                        BackupDirectory = config.BackupDirectory
                    }
                });
            })
            .WithName("GetConfiguration")
            .WithDescription("Returns the current system configuration")
            .WithTags("Configuration")
            .Produces<object>(StatusCodes.Status200OK);

            app.MapPost("/config/database", ([FromBody] PathUpdateRequest request, StorageConfiguration config) =>
            {
                try
                {
                    if (string.IsNullOrWhiteSpace(request.Path))
                    {
                        return Results.BadRequest("Path cannot be empty");
                    }

                    string oldDbPath = config.DatabasePath;
                    var newPath = config.UpdateDatabasePath(request.Path);

                    // Migrar datos si se solicita
                    if (request.MigrateData && File.Exists(oldDbPath))
                    {
                        try
                        {
                            // Solo copiar si las rutas son diferentes
                            if (oldDbPath != newPath)
                            {
                                File.Copy(oldDbPath, newPath, true);
                                return Results.Ok(new
                                {
                                    Message = "Database path updated and data migrated",
                                    Path = newPath,
                                    DataMigrated = true,
                                    OldPath = oldDbPath
                                });
                            }
                        }
                        catch (Exception migrateEx)
                        {
                            return Results.BadRequest(new
                            {
                                Error = "Path updated but migration failed",
                                Details = migrateEx.Message,
                                Path = newPath
                            });
                        }
                    }

                    return Results.Ok(new
                    {
                        Message = "Database path updated",
                        Path = newPath,
                        DataMigrated = false
                    });
                }
                catch (Exception ex)
                {
                    return Results.BadRequest(new { Error = ex.Message });
                }
            })
            .WithName("UpdateDatabasePath")
            .WithDescription("Updates the database path configuration")
            .WithTags("Configuration")
            .Produces<object>(StatusCodes.Status200OK)
            .Produces<object>(StatusCodes.Status400BadRequest);

            app.MapPost("/config/pokemon", ([FromBody] PathUpdateRequest request, StorageConfiguration config) =>
            {
                try
                {
                    if (string.IsNullOrWhiteSpace(request.Path))
                    {
                        return Results.BadRequest("Path cannot be empty");
                    }

                    string oldPokemonPath = config.PokemonFilesDirectory;
                    string oldBackupPath = config.BackupDirectory;

                    var newPath = config.UpdatePokemonFilesPath(request.Path);
                    string newBackupPath = Path.Combine(newPath, "backup");

                    // Migrar archivos si se solicita
                    if (request.MigrateData && Directory.Exists(oldPokemonPath))
                    {
                        try
                        {
                            // Solo migrar si las rutas son diferentes
                            if (oldPokemonPath != newPath)
                            {
                                // Migrar archivos principales de Pokémon
                                var migratedFiles = MigratePokemonFiles(oldPokemonPath, newPath);

                                // Migrar archivos de backup si existen
                                int migratedBackupFiles = 0;
                                if (Directory.Exists(oldBackupPath))
                                {
                                    migratedBackupFiles = MigratePokemonFiles(oldBackupPath, newBackupPath);
                                }

                                return Results.Ok(new
                                {
                                    Message = "Pokemon files path updated and data migrated",
                                    Path = newPath,
                                    BackupPath = newBackupPath,
                                    DataMigrated = true,
                                    MigratedMainFiles = migratedFiles,
                                    MigratedBackupFiles = migratedBackupFiles,
                                    OldPath = oldPokemonPath
                                });
                            }
                        }
                        catch (Exception migrateEx)
                        {
                            return Results.BadRequest(new
                            {
                                Error = "Path updated but migration failed",
                                Details = migrateEx.Message,
                                Path = newPath
                            });
                        }
                    }

                    return Results.Ok(new
                    {
                        Message = "Pokemon files path updated",
                        Path = newPath,
                        BackupPath = newBackupPath,
                        DataMigrated = false
                    });
                }
                catch (Exception ex)
                {
                    return Results.BadRequest(new { Error = ex.Message });
                }
            })
            .WithName("UpdatePokemonFilesPath")
            .WithDescription("Updates the Pokemon files directory path")
            .WithTags("Configuration")
            .Produces<object>(StatusCodes.Status200OK)
            .Produces<object>(StatusCodes.Status400BadRequest);
        }

        /// <summary>
        /// Request model for path update operations
        /// </summary>
        public class PathUpdateRequest
        {
            /// <summary>
            /// The new file path to set
            /// </summary>
            public required string Path { get; set; }

            /// <summary>
            /// Whether to migrate existing data to the new path
            /// </summary>
            public bool MigrateData { get; set; }
        }

        /// <summary>
        /// Migra todos los archivos y carpetas (excepto la carpeta de backup) de una ruta a otra
        /// </summary>
        /// <param name="sourceDir">Directorio fuente de los archivos</param>
        /// <param name="targetDir">Directorio destino para los archivos</param>
        /// <returns>Número de archivos migrados</returns>
        private static int MigratePokemonFiles(string sourceDir, string targetDir)
        {
            if (!Directory.Exists(targetDir))
            {
                Directory.CreateDirectory(targetDir);
            }

            int count = 0;

            // Primero copiar todos los archivos del directorio raíz
            foreach (string file in Directory.GetFiles(sourceDir))
            {
                string fileName = Path.GetFileName(file);
                string targetFile = Path.Combine(targetDir, fileName);
                File.Copy(file, targetFile, true);
                count++;
            }

            // Luego copiar todos los subdirectorios excepto 'backup'
            foreach (string directory in Directory.GetDirectories(sourceDir))
            {
                string dirName = Path.GetFileName(directory);

                // Saltarse el directorio de backup ya que se maneja por separado
                if (dirName.Equals("backup", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                string targetSubDir = Path.Combine(targetDir, dirName);
                count += CopyDirectoryRecursively(directory, targetSubDir);
            }

            return count;
        }

        /// <summary>
        /// Copia un directorio y todo su contenido recursivamente
        /// </summary>
        private static int CopyDirectoryRecursively(string sourceDir, string targetDir)
        {
            int count = 0;

            // Crear el directorio destino si no existe
            if (!Directory.Exists(targetDir))
            {
                Directory.CreateDirectory(targetDir);
            }

            // Copiar todos los archivos
            foreach (string file in Directory.GetFiles(sourceDir))
            {
                string fileName = Path.GetFileName(file);
                string targetFile = Path.Combine(targetDir, fileName);
                File.Copy(file, targetFile, true);
                count++;
            }

            // Copiar recursivamente todos los subdirectorios
            foreach (string directory in Directory.GetDirectories(sourceDir))
            {
                string dirName = Path.GetFileName(directory);
                string targetSubDir = Path.Combine(targetDir, dirName);
                count += CopyDirectoryRecursively(directory, targetSubDir);
            }

            return count;
        }
    }
}
