using System;
using System.IO;

namespace BeastVault.Api.Infrastructure
{
    /// <summary>
    /// Utilidades para determinar el entorno en el que se está ejecutando la aplicación
    /// </summary>
    public static class EnvironmentUtils
    {
        /// <summary>
        /// Determina si la aplicación se está ejecutando dentro de un contenedor Docker
        /// </summary>
        public static bool IsRunningInDocker()
        {
            // Verificar si existe el archivo /.dockerenv que Docker crea
            if (File.Exists("/.dockerenv"))
                return true;

            // Otra forma de verificar es buscando "docker" en cgroup
            try
            {
                return File.ReadAllText("/proc/1/cgroup").Contains("docker");
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Obtiene la ruta para la base de datos según el entorno
        /// </summary>
        public static string GetDatabasePath()
        {
            // Priorizar variable de entorno BEASTVAULT_DB_PATH si existe
            var envDbPath = Environment.GetEnvironmentVariable("BEASTVAULT_DB_PATH");
            if (!string.IsNullOrEmpty(envDbPath))
            {
                return envDbPath;
            }

            if (IsRunningInDocker())
            {
                // En Docker, usar un directorio dentro del contenedor
                return "/app/data/beastvault.db";
            }
            else
            {
                // En escritorio, usar la ruta de AppData del usuario
                var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
                return Path.Combine(appDataPath, "BeastVault", "beastvault.db");
            }
        }

        /// <summary>
        /// Obtiene la ruta para los archivos Pokémon según el entorno
        /// </summary>
        public static string GetPokemonFilesPath()
        {
            // Priorizar variable de entorno BEASTVAULT_POKEMON_PATH si existe
            var envStoragePath = Environment.GetEnvironmentVariable("BEASTVAULT_POKEMON_PATH");
            if (!string.IsNullOrEmpty(envStoragePath))
            {
                return envStoragePath;
            }

            if (IsRunningInDocker())
            {
                // En Docker, usar un directorio dentro del contenedor
                return "/app/pokemon";
            }
            else
            {
                // En escritorio, usar la carpeta de Documentos del usuario
                var documentsPath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
                return Path.Combine(documentsPath, "BeastVault");
            }
        }
    }
}
