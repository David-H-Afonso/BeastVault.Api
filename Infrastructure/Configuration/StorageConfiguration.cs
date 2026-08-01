using System;
using System.IO;
using System.Runtime.InteropServices;
using Microsoft.Extensions.Configuration;

namespace BeastVault.Api.Infrastructure.Configuration
{
    /// <summary>
    /// Configuración centralizada para rutas de almacenamiento en BeastVault
    /// </summary>
    public class StorageConfiguration
    {
        private readonly IConfiguration _configuration;

        // Rutas configuradas
        public string DatabaseDirectory { get; private set; } = string.Empty;
        public string DatabasePath { get; private set; } = string.Empty;
        public string PokemonFilesDirectory { get; private set; } = string.Empty;
        public string BackupDirectory { get; private set; } = string.Empty;
        public string TagImagesDirectory { get; private set; } = string.Empty;
        public string SaveFilesDirectory { get; private set; } = string.Empty;
        public string DataProtectionKeysDirectory { get; private set; } = string.Empty;

        // Información de entorno
        public bool IsDocker { get; private set; }
        public bool IsWindows { get; private set; }
        public bool IsMacOS { get; private set; }
        public bool IsLinux { get; private set; }
        public string PlatformName { get; private set; } = string.Empty;

        public StorageConfiguration(IConfiguration configuration)
        {
            _configuration = configuration;

            // Detectar plataforma
            DetectPlatform();

            // Configurar rutas
            ConfigureDatabasePath();
            ConfigurePokemonFilesPath();
            DataProtectionKeysDirectory = Path.Combine(DatabaseDirectory, "data-protection-keys");

            // Asegurar que los directorios existan
            EnsureDirectoriesExist();
        }

        /// <summary>
        /// Detecta la plataforma en la que se está ejecutando la aplicación
        /// </summary>
        private void DetectPlatform()
        {
            // Detectar Docker
            IsDocker = CheckIsRunningInDocker();

            // Detectar sistema operativo
            IsWindows = RuntimeInformation.IsOSPlatform(OSPlatform.Windows);
            IsMacOS = RuntimeInformation.IsOSPlatform(OSPlatform.OSX);
            IsLinux = RuntimeInformation.IsOSPlatform(OSPlatform.Linux) && !IsDocker;

            // Establecer nombre de plataforma para mostrar en logs
            if (IsDocker) PlatformName = "Docker";
            else if (IsWindows) PlatformName = "Windows";
            else if (IsMacOS) PlatformName = "macOS";
            else if (IsLinux) PlatformName = "Linux";
            else PlatformName = "Unknown";
        }

        /// <summary>
        /// Configura la ruta de la base de datos según la plataforma y configuración
        /// </summary>
        private void ConfigureDatabasePath()
        {
            // 1. Verificar variable de entorno
            var envDbPath = Environment.GetEnvironmentVariable("BEASTVAULT_DB_PATH");

            // 2. Verificar configuración en appsettings.json
            var configDbPath = _configuration.GetSection("BeastVault:Storage:DatabasePath").Value;

            // 3. Obtener conexión de base de datos configurada
            var connectionString = _configuration.GetConnectionString("Default");

            if (!string.IsNullOrEmpty(envDbPath))
            {
                // Usar la ruta de variable de entorno
                DatabasePath = envDbPath;
                DatabaseDirectory = Path.GetDirectoryName(envDbPath) ?? string.Empty;
            }
            else if (!string.IsNullOrEmpty(configDbPath))
            {
                // Usar la ruta de appsettings.json
                DatabasePath = configDbPath;
                DatabaseDirectory = Path.GetDirectoryName(configDbPath) ?? string.Empty;
            }
            else if (!string.IsNullOrEmpty(connectionString) && connectionString.Contains("Data Source="))
            {
                // Extraer ruta de la conexión
                var dbPath = connectionString.Split("Data Source=")[1].Split(';')[0].Trim();
                DatabasePath = dbPath;
                DatabaseDirectory = Path.GetDirectoryName(dbPath) ?? string.Empty;
            }
            else
            {
                // Usar ruta predeterminada según plataforma
                if (IsDocker)
                {
                    DatabaseDirectory = "/app/data";
                    DatabasePath = Path.Combine(DatabaseDirectory, "beastvault.db");
                }
                else if (IsWindows)
                {
                    var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
                    DatabaseDirectory = Path.Combine(appDataPath, "BeastVault");
                    DatabasePath = Path.Combine(DatabaseDirectory, "beastvault.db");
                }
                else if (IsMacOS)
                {
                    var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
                    DatabaseDirectory = Path.Combine(home, "Library", "Application Support", "BeastVault");
                    DatabasePath = Path.Combine(DatabaseDirectory, "beastvault.db");
                }
                else // Linux u otros
                {
                    var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
                    DatabaseDirectory = Path.Combine(home, ".beastvault");
                    DatabasePath = Path.Combine(DatabaseDirectory, "beastvault.db");
                }
            }
        }

        /// <summary>
        /// Configura la ruta de los archivos Pokémon según la plataforma y configuración
        /// </summary>
        private void ConfigurePokemonFilesPath()
        {
            // 1. Verificar variable de entorno
            var envPokemonPath = Environment.GetEnvironmentVariable("BEASTVAULT_POKEMON_PATH");

            // 2. Verificar configuración en appsettings.json
            var configPokemonPath = _configuration.GetSection("BeastVault:Storage:PokemonFilesPath").Value;

            if (!string.IsNullOrEmpty(envPokemonPath))
            {
                // Usar la ruta de variable de entorno
                PokemonFilesDirectory = envPokemonPath;
            }
            else if (!string.IsNullOrEmpty(configPokemonPath))
            {
                // Usar la ruta de appsettings.json
                PokemonFilesDirectory = configPokemonPath;
            }
            else
            {
                // Usar ruta predeterminada según plataforma
                if (IsDocker)
                {
                    PokemonFilesDirectory = "/app/pokemon";
                }
                else if (IsWindows)
                {
                    var documentsPath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
                    PokemonFilesDirectory = Path.Combine(documentsPath, "BeastVault");
                }
                else if (IsMacOS)
                {
                    var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
                    PokemonFilesDirectory = Path.Combine(home, "Documents", "BeastVault");
                }
                else // Linux u otros
                {
                    var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
                    PokemonFilesDirectory = Path.Combine(home, "BeastVault");
                }
            }

            // Configurar directorio de backup
            BackupDirectory = Path.Combine(PokemonFilesDirectory, "backup");

            // Tag images live alongside the Pokémon files so they share the same
            // persistent storage volume (e.g. /app/pokemon in Docker).
            TagImagesDirectory = Path.Combine(PokemonFilesDirectory, "tag-images");
            SaveFilesDirectory = Path.Combine(PokemonFilesDirectory, "saves");
        }

        /// <summary>
        /// Asegura que todos los directorios configurados existan
        /// </summary>
        public void EnsureDirectoriesExist()
        {
            if (!Directory.Exists(DatabaseDirectory))
            {
                Directory.CreateDirectory(DatabaseDirectory);
                Console.WriteLine($"Created database directory: {DatabaseDirectory}");
            }

            if (!Directory.Exists(PokemonFilesDirectory))
            {
                Directory.CreateDirectory(PokemonFilesDirectory);
                Console.WriteLine($"Created Pokemon files directory: {PokemonFilesDirectory}");
            }

            if (!Directory.Exists(BackupDirectory))
            {
                Directory.CreateDirectory(BackupDirectory);
                Console.WriteLine($"Created backup directory: {BackupDirectory}");
            }

            if (!Directory.Exists(TagImagesDirectory))
            {
                Directory.CreateDirectory(TagImagesDirectory);
                Console.WriteLine($"Created tag images directory: {TagImagesDirectory}");
            }

            if (!Directory.Exists(SaveFilesDirectory))
            {
                Directory.CreateDirectory(SaveFilesDirectory);
                Console.WriteLine($"Created save files directory: {SaveFilesDirectory}");
            }

            if (!Directory.Exists(DataProtectionKeysDirectory))
            {
                Directory.CreateDirectory(DataProtectionKeysDirectory);
                Console.WriteLine($"Created data protection key directory: {DataProtectionKeysDirectory}");
            }
        }

        /// <summary>
        /// Muestra la configuración actual de rutas
        /// </summary>
        public void LogCurrentConfiguration()
        {
            Console.WriteLine("=== BeastVault Storage Configuration ===");
            Console.WriteLine($"Platform: {PlatformName}");
            Console.WriteLine($"Database Path: {DatabasePath}");
            Console.WriteLine($"Pokemon Files Directory: {PokemonFilesDirectory}");
            Console.WriteLine($"Backup Directory: {BackupDirectory}");
            Console.WriteLine($"Tag Images Directory: {TagImagesDirectory}");
            Console.WriteLine($"Save Files Directory: {SaveFilesDirectory}");
            Console.WriteLine($"Data Protection Keys Directory: {DataProtectionKeysDirectory}");
            Console.WriteLine("=======================================");
        }

        /// <summary>
        /// Determina si la aplicación se está ejecutando dentro de un contenedor Docker
        /// </summary>
        private bool CheckIsRunningInDocker()
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
        /// Actualiza la configuración de ruta de base de datos en tiempo de ejecución
        /// </summary>
        public string UpdateDatabasePath(string newPath)
        {
            if (string.IsNullOrWhiteSpace(newPath))
                throw new ArgumentException("Database path cannot be empty");

            // Asegurar que el directorio existe
            var directory = Path.GetDirectoryName(newPath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                Directory.CreateDirectory(directory);

            // Actualizar rutas
            DatabasePath = newPath;
            DatabaseDirectory = directory ?? string.Empty;
            DataProtectionKeysDirectory = Path.Combine(DatabaseDirectory, "data-protection-keys");
            Directory.CreateDirectory(DataProtectionKeysDirectory);

            Console.WriteLine($"Database path updated: {DatabasePath}");
            return DatabasePath;
        }

        /// <summary>
        /// Actualiza la configuración de ruta de archivos Pokémon en tiempo de ejecución
        /// </summary>
        public string UpdatePokemonFilesPath(string newPath)
        {
            if (string.IsNullOrWhiteSpace(newPath))
                throw new ArgumentException("Pokemon files path cannot be empty");

            // Asegurar que el directorio existe
            if (!Directory.Exists(newPath))
                Directory.CreateDirectory(newPath);

            // Actualizar rutas
            PokemonFilesDirectory = newPath;
            BackupDirectory = Path.Combine(newPath, "backup");
            TagImagesDirectory = Path.Combine(newPath, "tag-images");
            SaveFilesDirectory = Path.Combine(newPath, "saves");

            // Asegurar que el directorio de backup existe
            if (!Directory.Exists(BackupDirectory))
                Directory.CreateDirectory(BackupDirectory);

            if (!Directory.Exists(TagImagesDirectory))
                Directory.CreateDirectory(TagImagesDirectory);

            if (!Directory.Exists(SaveFilesDirectory))
                Directory.CreateDirectory(SaveFilesDirectory);

            Console.WriteLine($"Pokemon files path updated: {PokemonFilesDirectory}");
            Console.WriteLine($"Backup directory updated: {BackupDirectory}");

            return PokemonFilesDirectory;
        }

        /// <summary>
        /// Obtiene la cadena de conexión completa basada en la ruta de base de datos
        /// </summary>
        public string GetConnectionString()
        {
            return $"Data Source={DatabasePath}";
        }
    }
}
