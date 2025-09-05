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
            if (IsRunningInDocker())
            {
                // En Docker, usar un directorio dentro del contenedor
                return "/app/data/beastvault.db";
            }
            else
            {
                // En escritorio, usar la ruta de AppData del usuario
                var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
                var defaultDbPath = Path.Combine(appDataPath, "BeastVault", "beastvault.db");

                // Priorizar variable de entorno DB_PATH si existe
                var envDbPath = Environment.GetEnvironmentVariable("DB_PATH");
                return !string.IsNullOrEmpty(envDbPath) ? envDbPath : defaultDbPath;
            }
        }

        /// <summary>
        /// Obtiene la ruta para los archivos Pokémon según el entorno
        /// </summary>
        public static string GetPokemonFilesPath()
        {
            if (IsRunningInDocker())
            {
                // En Docker, usar un directorio dentro del contenedor
                return "/app/pokemon";
            }
            else
            {
                // En escritorio, usar la carpeta de Documentos del usuario
                var documentsPath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
                var defaultStoragePath = Path.Combine(documentsPath, "BeastVault");

                // Priorizar variable de entorno STORAGE_PATH si existe
                var envStoragePath = Environment.GetEnvironmentVariable("STORAGE_PATH");
                return !string.IsNullOrEmpty(envStoragePath) ? envStoragePath : defaultStoragePath;
            }
        }
    }
}
