# Configuración de Almacenamiento en BeastVault

## Rutas de Almacenamiento

BeastVault utiliza dos rutas principales para almacenar datos:

1. **Ruta de la Base de Datos**: Donde se almacena el archivo SQLite de la base de datos
2. **Ruta de Archivos Pokémon**: Donde se almacenan los archivos Pokémon (.pk\*) y sus backups

## Rutas predeterminadas según plataforma

### Windows

- **Base de Datos**: `%AppData%\BeastVault\beastvault.db`
- **Archivos Pokémon**: `%UserProfile%\Documents\BeastVault\`
- **Archivos de Backup**: `%UserProfile%\Documents\BeastVault\backup\`

### macOS

- **Base de Datos**: `~/Library/Application Support/BeastVault/beastvault.db`
- **Archivos Pokémon**: `~/Documents/BeastVault/`
- **Archivos de Backup**: `~/Documents/BeastVault/backup/`

### Linux

- **Base de Datos**: `~/.beastvault/beastvault.db`
- **Archivos Pokémon**: `~/BeastVault/`
- **Archivos de Backup**: `~/BeastVault/backup/`

### Docker

- **Base de Datos**: `/app/data/beastvault.db`
- **Archivos Pokémon**: `/app/pokemon/`
- **Archivos de Backup**: `/app/pokemon/backup/`

## Personalización de rutas

Existen tres formas de personalizar las rutas de almacenamiento:

### 1. Variables de Entorno

```bash
# Para la base de datos
BEASTVAULT_DB_PATH=/ruta/personalizada/beastvault.db

# Para los archivos Pokémon
BEASTVAULT_POKEMON_PATH=/ruta/personalizada/pokemon
```

### 2. Configuración en appsettings.json

```json
{
  "BeastVault": {
    "Storage": {
      "DatabasePath": "/ruta/personalizada/beastvault.db",
      "PokemonFilesPath": "/ruta/personalizada/pokemon"
    }
  }
}
```

### 3. API REST (en tiempo de ejecución)

Puedes cambiar las rutas de almacenamiento en tiempo de ejecución a través de la API:

**Ver configuración actual:**

```
GET /config
```

**Actualizar ruta de base de datos:**

```
POST /config/database
Content-Type: application/json

{
  "path": "/ruta/personalizada/beastvault.db",
  "migrateData": true  // Opcional: migrar los datos existentes a la nueva ubicación
}
```

**Actualizar ruta de archivos Pokémon:**

```
POST /config/pokemon
Content-Type: application/json

{
  "path": "/ruta/personalizada/pokemon",
  "migrateData": true  // Opcional: migrar los archivos Pokémon existentes a la nueva ubicación
}
```

## Migración de Datos

Al cambiar las rutas de almacenamiento, BeastVault ofrece la opción de migrar automáticamente los datos existentes a la nueva ubicación:

- **Migración de Base de Datos**: Al establecer `migrateData: true` en la solicitud de cambio de ruta de base de datos, el sistema copiará automáticamente el archivo de base de datos existente a la nueva ubicación.

- **Migración de Archivos Pokémon**: Al establecer `migrateData: true` en la solicitud de cambio de ruta de archivos Pokémon, el sistema copiará automáticamente todos los archivos .pk\* (archivos Pokémon) y sus backups a la nueva ubicación.

Ejemplos de respuesta con migración:

```json
// Migración de base de datos exitosa
{
  "message": "Database path updated and data migrated",
  "path": "/nueva/ruta/beastvault.db",
  "dataMigrated": true,
  "oldPath": "/ruta/anterior/beastvault.db"
}

// Migración de archivos Pokémon exitosa
{
  "message": "Pokemon files path updated and data migrated",
  "path": "/nueva/ruta/pokemon",
  "backupPath": "/nueva/ruta/pokemon/backup",
  "dataMigrated": true,
  "migratedMainFiles": 42,
  "migratedBackupFiles": 15,
  "oldPath": "/ruta/anterior/pokemon"
}
```

## En Docker

Cuando se ejecuta en Docker, la configuración se realiza principalmente a través de variables de entorno y volúmenes:

```yaml
services:
  beastvault-api:
    # ... otras configuraciones ...
    volumes:
      - beastvault-data:/app/data
      - beastvault-pokemon:/app/pokemon
    environment:
      - BEASTVAULT_DB_PATH=/app/data/beastvault.db
      - BEASTVAULT_POKEMON_PATH=/app/pokemon
```

Los volúmenes garantizan la persistencia de datos incluso si se elimina o recrea el contenedor.
