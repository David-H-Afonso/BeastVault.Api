# Backend Implementation Guide — BeastVault API

This document explains how the BeastVault backend works and how to implement future changes. It is written for a developer who is stronger on frontend and wants to understand the backend well enough to add features, fix bugs, and maintain the project confidently.

---

## 1. Backend Overview

BeastVault is a Pokémon save file reader and tracker. The backend is a REST API that:

- **Imports Pokémon files** — Accepts `.pk1` through `.pk9` (and `.pb*`, `.pa*`, `.ek*`) files via multipart upload
- **Parses Pokémon data** — Uses PKHeX.Core to extract species, stats, moves, abilities, trainer info, and 60+ other fields
- **Stores parsed data** — SQLite database with full Pokémon details, stats, moves, relearn moves
- **Manages file backups** — Keeps original files on disk with organized backup structure by format and year
- **Auto-scans directories** — Watches `~/Documents/BeastVault` for new files and imports them on startup
- **Advanced querying** — Specification pattern with ~20 filter types, multi-field sorting, pagination
- **Tag system** — User-defined tags with optional PNG images, many-to-many with Pokémon and files
- **Showdown export** — Generates Pokémon Showdown format text for competitive use
- **Cross-platform storage** — Dynamic path resolution for Windows, macOS, Linux, Docker, and Electron
- **Pokémon comparison** — Compare two Pokémon to detect differences (useful after trades)
- **Maintenance tools** — Database sync, orphan detection, duplicate finding

The main consumers are the Vue frontend (`BeastVault.Front/`) and the Electron desktop app (`BeastVault.Desktop/`).

---

## 2. Technology Stack

| Technology            | Version                                          | Purpose                                     |
| --------------------- | ------------------------------------------------ | ------------------------------------------- |
| .NET                  | 9.0                                              | Runtime                                     |
| ASP.NET Core          | 9.0                                              | Web API framework (Minimal API)             |
| Entity Framework Core | 9.0.8                                            | ORM / database access                       |
| SQLite                | via `Microsoft.EntityFrameworkCore.Sqlite` 9.0.8 | Database                                    |
| PKHeX.Core            | 25.12.21                                         | Pokémon save file parsing (all generations) |
| Swashbuckle           | 9.0.3                                            | Swagger / OpenAPI documentation             |
| Docker                | Dockerfile included                              | Containerized deployment                    |

**Not present:** No authentication (JWT/BCrypt), no test framework, no FluentValidation, no AutoMapper, no MediatR, no repository pattern, no service interfaces, no controllers (uses Minimal API endpoints).

---

## 3. Project Structure Explained

```
BeastVault.Api/
  Contracts/                ← DTOs and query parameter records (2 files)
  Domain/
    Entities/               ← All entities in one file (index.cs)
    Services/               ← Static query building and sorting services (3 files, 1 empty)
    Specifications/         ← Specification pattern for Pokemon filtering (2 files)
    ValueObjects/           ← Enums, sort options, Showdown export (2 files)
  Endpoints/                ← 8 Minimal API endpoint group files
  Extensions/               ← Empty file (WebApplicationExtension.cs)
  Infrastructure/
    Configuration/          ← Cross-platform storage path management (1 file)
    Mappings/               ← Empty placeholder (PkhexMappings.cs)
    Services/               ← 7 service files (3 injectable, 4 static)
    AppDbContext.cs          ← EF Core database context
    EnvironmentUtils.cs      ← Duplicate of StorageConfiguration logic
  Migrations/               ← 2 EF Core migrations
  Services/                 ← Empty folder (unused)
  Program.cs                ← Startup + DI + 2 inline sprite endpoints + ServiceCollectionExtensions
  appsettings.json          ← Storage paths and connection string
  assets/                   ← Custom sprite images
```

### Key Files

| File                                                   | What it does                                                                                                                                                                   |
| ------------------------------------------------------ | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------ |
| `Program.cs`                                           | Registers all services, configures database, CORS, Swagger. Contains two inline sprite endpoints (~150 lines), `ServiceCollectionExtensions` class, and startup file scanning. |
| `Infrastructure/AppDbContext.cs`                       | Entity Framework DbContext with 8 DbSets and Fluent API configuration for all entity relationships and indexes.                                                                |
| `Infrastructure/Configuration/StorageConfiguration.cs` | Cross-platform path resolution for database, Pokémon files, and backups. Supports env vars, appsettings, and platform defaults.                                                |
| `Infrastructure/Services/PkhexCoreParser.cs`           | Core PKHeX integration: reads raw `.pk*` bytes and produces all domain entities (File, Pokemon, Stats, Moves, RelearnMoves).                                                   |
| `Infrastructure/Services/FileWatcherService.cs`        | Scans `~/Documents/BeastVault` for new Pokémon files, imports them, and cleans up deleted files from the database.                                                             |
| `Infrastructure/Services/FileStorageService.cs`        | File I/O: save, backup, read, delete. Manages main files and organized backups (by format and year).                                                                           |
| `Domain/Services/PokemonQueryService.cs`               | Static service that builds Specification-based queries from `AdvancedPokemonQuery` parameters.                                                                                 |
| `Domain/Specifications/PokemonSpecifications.cs`       | ~20 Specification pattern classes for filtering Pokémon (text search, shiny, type, generation, level, tags, etc.).                                                             |
| `Contracts/AdvancedPokemonQuery.cs`                    | Complex query parameter record with ~30 fields for advanced filtering, sorting, and pagination.                                                                                |
| `Contracts/Dtos.cs`                                    | All DTOs in one file (~400 lines): ImportResult, PokemonQuery, PokemonListItem, PokemonDetail, Stats, Moves, Tags, etc.                                                        |

---

## 4. Startup Flow

When the app starts, `Program.cs` executes this sequence:

### 1. Register Services

```csharp
builder.Services.AddSingleton<StorageConfiguration>();  // Cross-platform path resolution
builder.Services.AddAppDbContext(configuration);         // SQLite via StorageConfiguration
builder.Services.AddBeastVaultServices(configuration);   // FileStorageService, PkhexCoreParser, FileWatcherService
builder.Services.AddHealthChecks();
```

**Service registrations (in `ServiceCollectionExtensions`, defined at bottom of Program.cs):**

| Service                | Lifetime  | Purpose                                                   |
| ---------------------- | --------- | --------------------------------------------------------- |
| `StorageConfiguration` | Singleton | Detects platform, resolves paths for DB + files + backups |
| `AppDbContext`         | Scoped    | EF Core database context (SQLite)                         |
| `FileStorageService`   | Scoped    | File save, backup, read, delete operations                |
| `PkhexCoreParser`      | Scoped    | Parse `.pk*` bytes into domain entities via PKHeX.Core    |
| `FileWatcherService`   | Scoped    | Scan directories and auto-import Pokémon files            |

**Not registered (static classes used directly):**

- `PkHexStringService` — Species, move, ability, nature name lookups
- `PokemonComparisonService` — Compare two Pokémon entities
- `PokemonFormService` — Mega Evolution and Gigantamax form resolution
- `PokemonGameInfoService` — Game/generation/type mappings
- `PokemonQueryService` — Build Specification-based queries
- `PokemonSortingService` — Multi-field sorting with LINQ expressions

### 2. Configure CORS

CORS is configured from:

1. `CORS_ALLOWED_ORIGINS` environment variable (comma-separated)
2. `CorsSettings:AllowedOrigins` in appsettings.json
3. Fallback: allow localhost, 127.0.0.1, and private network ranges (192.168._, 10._, 172.\*)

The fallback is designed for local development and Electron desktop app access on the same machine or LAN.

### 3. Build and Initialize

```
app.UseSwagger() + app.UseSwaggerUI()
app.UseCors("AllowLocalhost")
app.UseHttpsRedirection()
Map all endpoint groups (8 groups + 2 inline sprite endpoints)

Startup block:
  → db.Database.MigrateAsync()                          // Apply pending migrations
  → storage.EnsureVault()                                // Create directories
  → fileWatcher.ScanAndImportNewFilesAsync()              // Auto-import new files
```

### 4. Database Resolution

The SQLite database path is resolved in this priority order:

| Priority | Source                                           | Example                                                  |
| -------- | ------------------------------------------------ | -------------------------------------------------------- |
| 1        | `BEASTVAULT_DB_PATH` env var                     | `/app/data/beastvault.db`                                |
| 2        | `BeastVault:Storage:DatabasePath` in appsettings | `C:\data\beastvault.db`                                  |
| 3        | `ConnectionStrings:Default`                      | `Data Source=path/beastvault.db`                         |
| 4        | Platform default (Windows)                       | `%APPDATA%\BeastVault\beastvault.db`                     |
| 4        | Platform default (macOS)                         | `~/Library/Application Support/BeastVault/beastvault.db` |
| 4        | Platform default (Linux)                         | `~/.beastvault/beastvault.db`                            |
| 4        | Platform default (Docker)                        | `/app/data/beastvault.db`                                |

---

## 5. Database Flow

### Entity Relationship Diagram

```
FileEntity (1) ──────→ (1) PokemonEntity
  │ Id (PK)                  │ Id (PK)
  │ Sha256 (unique)          │ FileId (FK → FileEntity)
  │ FileName                 │ SpeciesId
  │ Format (.pk9, etc.)      │ ~60 data fields (stats, trainer, etc.)
  │ StoredPath               │ Favorite, Notes
  │ RawBlob (nullable)       │
  │ ImportedAt               ├── (1) StatsEntity
  │                          │       PokemonId (PK, FK)
  │                          │       IVs, EVs, HyperTrained, Calculated
  │                          │
  │                          ├── (4) MoveEntity
  │                          │       (PokemonId, Slot) (composite PK)
  │                          │       MoveId, PpUps, CurrentPp
  │                          │
  │                          ├── (4) RelearnMoveEntity
  │                          │       (PokemonId, Slot) (composite PK)
  │                          │       MoveId
  │                          │
  │                          └──(*) PokemonTagEntity ──→ TagEntity
  │                                  (PokemonId, TagId)      │ Id (PK)
  │                                                          │ Name (unique)
  └──(*) FileTagEntity ──→ TagEntity                         │ ImagePath
          (FileId, TagId)
```

### Important DbSets

| DbSet          | Entity              | Key                  | Purpose                                            |
| -------------- | ------------------- | -------------------- | -------------------------------------------------- |
| `Files`        | `FileEntity`        | `Id` (auto)          | Stored Pokémon file metadata and optional raw blob |
| `Pokemon`      | `PokemonEntity`     | `Id` (auto)          | Parsed Pokémon data — the central entity           |
| `Stats`        | `StatsEntity`       | `PokemonId` (1:1 FK) | IVs, EVs, hyper training flags, calculated stats   |
| `Moves`        | `MoveEntity`        | `(PokemonId, Slot)`  | 4 current move slots                               |
| `RelearnMoves` | `RelearnMoveEntity` | `(PokemonId, Slot)`  | 4 relearn move slots                               |
| `Tags`         | `TagEntity`         | `Id` (auto)          | User-defined tags with optional PNG image          |
| `PokemonTags`  | `PokemonTagEntity`  | `(PokemonId, TagId)` | Many-to-many: Pokémon ↔ Tags                       |
| `FileTags`     | `FileTagEntity`     | `(FileId, TagId)`    | Many-to-many: Files ↔ Tags                         |

### How Migrations Work

The project has 2 migrations:

1. `InitialCreate` — All base tables
2. `EnsureTagsTableExists` — Tags, PokemonTags, FileTags

On startup, `db.Database.MigrateAsync()` applies any pending migrations automatically.

---

## 6. Request Lifecycle

### Example: `GET /pokemon?IsShiny=true&SortBy=Level&Take=20`

1. **Request arrives** → ASP.NET routes to `PokemonEndpoints.MapPokemonEndpoints`
2. **Parameter binding** → `[AsParameters] AdvancedPokemonQuery q` binds all query string values
3. **Base query** → `db.Pokemon.AsNoTracking().AsQueryable()`
4. **Build specifications** → `PokemonQueryService.BuildQuery(baseQuery, q)`:
   - Creates `ShinySpecification(true)` from `IsShiny=true`
   - Creates `CompositeSpecification` wrapping all specs
   - Each spec adds `.Where()` clause to the query
5. **Apply sorting** → `PokemonSortingService.ApplyMultipleSort()`:
   - Maps `SortBy=Level` → `query.OrderBy(p => p.Level)`
6. **Count total** → `await query.CountAsync()`
7. **Paginate** → `.Skip(0).Take(20)`
8. **Join with Files** → `query.Join(db.Files, ...)` to get file format for form resolution
9. **Select and map** → Inline projection to anonymous type with:
   - `PokemonFormService.GetDisplayForm()` — resolve Mega/Gigantamax forms
   - `PokemonGameInfoService.GetSpeciesOriginGeneration()` — calculate generation
   - `PkHexStringService.GetSpeciesName()` — resolve species name from ID
10. **Load tags** → Separate query for `PokemonTags` grouped by PokemonId
11. **Build DTOs** → Map to `PokemonListItemDto` with tags
12. **Return** → `Results.Ok(new { Items, Total, Stats })`

---

## 7. Endpoints Explained

### PokemonEndpoints (530+ lines)

**Responsibility**: Pokémon CRUD, admin operations, metadata, comparison, debug.

**Endpoints**:

- `POST /admin/wipe-database` — ⚠️ Deletes entire database (no auth)
- `DELETE /pokemon/{id}/database` — Delete Pokémon + main file (preserve backup)
- `DELETE /pokemon/{id}/backup` — Delete Pokémon + all files (irreversible)
- `GET /pokemon` — Advanced query with specs, sorting, pagination, tags
- `GET /pokemon/metadata` — Filter/sort options for frontend dropdowns
- `GET /pokemon/{id}` — Full Pokémon detail with stats, moves, relearn moves
- `GET /pokemon/{id}/showdown` — Pokémon Showdown text export
- `PATCH /pokemon/{id}` — Update favorite/notes
- `GET /pokemon/compare/{id1}/{id2}` — Compare two Pokémon
- `GET /debug/origin-games` — Debug origin game values

**Dependencies**: `AppDbContext`, `FileStorageService`, `PokemonQueryService` (static), `PkHexStringService` (static), `PokemonFormService` (static), `PokemonGameInfoService` (static)

**Key behavior**: All database queries are performed inline in the endpoint handler. The `GET /pokemon` endpoint is ~100 lines of inline query building, joining, selecting, and tag loading.

---

### ImportEndpoints

**Responsibility**: Upload and parse Pokémon files.

**Endpoints**:

- `POST /import` — Accept multipart file upload, parse via PKHeX, save to DB

**Key behavior**: Accepts `IFormFileCollection`. For each file: reads bytes → `PkhexCoreParser.ParseAsync()` → deduplication check by SHA256 → saves file + Pokémon + stats + moves. Stores `RawBlob` in the Files table for database-level backup.

---

### FilesEndpoints

**Responsibility**: Download original Pokémon files.

**Endpoints**:

- `GET /files/{id}` — Download stored file by internal file ID
- `GET /export/{pokemonId}` — Download original from DB blob (RawBlob)
- `GET /export/database/{pokemonId}` — Download from disk
- `GET /export/backup/{pokemonId}` — Download backup file

---

### ScanEndpoints

**Responsibility**: Directory scanning and auto-import.

**Endpoints**:

- `POST /scan/directory` — Scan `~/Documents/BeastVault` for new files and import
- `GET /scan/status` — Directory info and file counts by extension

**Key behavior**: `FileWatcherService.ScanAndImportNewFilesAsync()` runs the full scan cycle:

1. Cleanup deleted files (remove DB entries for files no longer on disk)
2. Find all `.pk*` files in watch directory (excluding backup and hidden folders)
3. For each file: compute SHA256, check DB for existing, parse and import if new
4. Always ensure backup exists for every scanned file

---

### TagEndpoints

**Responsibility**: Tag CRUD and image management.

**Endpoints**:

- `GET /tags` — List all tags with Pokémon count
- `GET /tags/{id}` — Get tag by ID
- `POST /tags` — Create tag (unique name, case-sensitive)
- `PUT /tags/{id}` — Update tag name
- `DELETE /tags/{id}` — Delete tag + all associations
- `POST /tags/{id}/image` — Upload PNG image for tag
- `DELETE /tags/{id}/image` — Delete tag image

---

### MaintenanceEndpoints

**Responsibility**: Database health and filesystem sync.

**Endpoints**:

- `POST /maintenance/sync` — Remove orphaned DB entries (files that no longer exist on disk)
- `GET /maintenance/status` — DB counts, backup directory info, orphaned file detection
- `GET /maintenance/pokemon/{id}/duplicates` — Find duplicate files by SHA256

---

### ConfigurationEndpoints

**Responsibility**: Runtime path configuration.

**Endpoints**:

- `GET /config` — Show current platform, database path, Pokémon files path, backup path
- `POST /config/database` — Change database path at runtime (with optional data migration)
- `POST /config/pokemon` — Change Pokémon files path at runtime (with optional data migration)

**Key behavior**: `PathUpdateRequest` includes a `MigrateData` flag. When true, existing files are copied to the new location.

---

### HealthEndpoints

**Responsibility**: Simple health check.

**Endpoints**:

- `GET /health` — Returns `{ status: "ok" }`

---

### Sprite Endpoints (inline in Program.cs)

**Responsibility**: Custom sprite image serving.

**Endpoints**:

- `GET /custom-sprites/search/{pattern}` — Find sprite by filename pattern
- `GET /custom-sprites/{fileName}` — Serve sprite image file

**Key behavior**: Searches multiple possible asset locations (current dir, app base, parent dir for Electron, env var). Includes path traversal protection (`Path.GetFileName` + `Path.GetFullPath` validation).

---

## 8. Services Explained

### Injectable Services (registered in DI)

| Service                | Lifetime  | Interface | Purpose                                                               |
| ---------------------- | --------- | --------- | --------------------------------------------------------------------- |
| `StorageConfiguration` | Singleton | None      | Platform detection, path resolution for DB/files/backups              |
| `FileStorageService`   | Scoped    | None      | File save (main + backup), read, delete, SHA256 hashing               |
| `PkhexCoreParser`      | Scoped    | None      | Parse `.pk*` bytes → FileEntity + PokemonEntity + StatsEntity + Moves |
| `FileWatcherService`   | Scoped    | None      | Scan directories, auto-import, cleanup deleted files                  |

### Static Services (called directly, not in DI)

| Service                    | Purpose                                                                                         |
| -------------------------- | ----------------------------------------------------------------------------------------------- |
| `PkHexStringService`       | Name lookups via PKHeX.Core: species, moves, abilities, natures, items, balls, types, languages |
| `PokemonComparisonService` | Compare two PokemonEntity instances field-by-field, report differences                          |
| `PokemonFormService`       | Determine display form considering Mega Stones, Gigantamax flags, held items                    |
| `PokemonGameInfoService`   | Game-to-generation mapping, species generation ranges, type data                                |
| `PokemonQueryService`      | Build IQueryable from AdvancedPokemonQuery using Specification pattern                          |
| `PokemonSortingService`    | Apply multi-field sorting with LINQ OrderBy/ThenBy expressions                                  |

### Why Some Services Are Static

The static services are **pure functions** — they don't need database access, configuration, or state. They take input values and return computed results using PKHeX.Core's static data or hardcoded lookup tables. This is acceptable for:

- `PkHexStringService` — wraps `GameInfo.Strings` (PKHeX static data)
- `PokemonFormService` — hardcoded Mega Stone → form mappings
- `PokemonGameInfoService` — hardcoded game → generation mappings
- `PokemonComparisonService` — pure comparison logic

However, `PokemonQueryService` and `PokemonSortingService` should become injectable because they are consumed by services that need testability.

---

## 9. Entities and Tables Explained

### FileEntity — `Files` table

Represents a stored Pokémon save file on disk.

| Field              | Type            | Purpose                                       |
| ------------------ | --------------- | --------------------------------------------- |
| `Id`               | int (PK)        | Auto-increment identifier                     |
| `Sha256`           | string (unique) | SHA-256 hash for deduplication                |
| `FileName`         | string          | Display name of the file                      |
| `OriginalFileName` | string?         | Original filename for backup reference        |
| `Format`           | string          | File format: `pk1`, `pk9`, `pb7`, `pa8`, etc. |
| `Size`             | long            | File size in bytes                            |
| `StoredPath`       | string          | Absolute path to file on disk                 |
| `ImportedAt`       | DateTime        | UTC timestamp of import                       |
| `RawBlob`          | byte[]?         | Optional raw file bytes stored in DB          |

**Relationships**: One FileEntity → One PokemonEntity (via `PokemonEntity.FileId`). Many-to-many with Tags via `FileTagEntity`.

---

### PokemonEntity — `Pokemon` table

The central entity with ~60 fields parsed from PKHeX.Core.

**Core identity**: SpeciesId, Form, Nickname, EncryptionConstant, PersonalityId
**Trainer info**: OtName, Tid, Sid, OTGender, OTLanguage
**Battle data**: Level, Nature, AbilityId, BallId, TeraType, HeldItemId
**Physical**: HeightScalar, WeightScalar, Scale
**Special flags**: IsShiny, Favorite, IsEgg, FatefulEncounter, CanGigantamax, DynamaxLevel
**Memory system**: OriginalTrainerMemory/Intensity/Feeling/Variable, HandlingTrainerMemory/...
**Handler info**: CurrentHandler, HandlingTrainerName/Gender/Language/Friendship
**Contest stats**: ContestCool/Beauty/Cute/Smart/Tough/Sheen
**Pokérus**: PokerusState/Days/Strain
**User data**: Favorite (bool), Notes (string?)

**Relationships**: FK to FileEntity. One-to-one with StatsEntity. One-to-many with MoveEntity (4 slots). One-to-many with RelearnMoveEntity (4 slots). Many-to-many with Tags.

---

### StatsEntity — `Stats` table

One-to-one with PokemonEntity (PK = PokemonId).

Contains: 6 IVs, 6 EVs, 6 Hyper Training flags, 7 calculated stats (HP current + 6 base).

---

### MoveEntity — `Moves` table

Composite PK: `(PokemonId, Slot)`. Slot is 1-4.

Contains: MoveId, PpUps, CurrentPp.

---

### RelearnMoveEntity — `RelearnMoves` table

Composite PK: `(PokemonId, Slot)`. Slot is 1-4.

Contains: MoveId.

---

### TagEntity — `Tags` table

User-defined labels for organizing Pokémon.

| Field       | Type            | Purpose                    |
| ----------- | --------------- | -------------------------- |
| `Id`        | int (PK)        | Auto-increment             |
| `Name`      | string (unique) | Case-sensitive tag name    |
| `ImagePath` | string?         | Path to optional PNG image |

**Relationships**: Many-to-many with PokemonEntity and FileEntity via join tables.

---

## 10. DTOs and API Contracts Explained

### Current DTO Files

| File                                | DTOs                                                                                                                                                                                                                               | Purpose                                   |
| ----------------------------------- | ---------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- | ----------------------------------------- |
| `Contracts/Dtos.cs`                 | `ImportResultDto`, `PokemonQuery`, `PagedResult<T>`, `PokemonListItemDto`, `PokemonDetailDto`, `StatsDto`, `MoveDto`, `RelearnMoveDto`, `UpdatePokemonDto`, `TagDto`, `CreateTagRequest`, `UpdateTagRequest`, `PokemonTagsRequest` | All DTOs in one file                      |
| `Contracts/AdvancedPokemonQuery.cs` | `AdvancedPokemonQuery`                                                                                                                                                                                                             | Advanced query parameters with ~30 fields |

### Current Approach

DTOs are C# `record` types. `PokemonDetailDto` has a constructor that accepts entity parameters and maps fields inline — this is a layer violation (DTO knows about entities). Other DTOs use `init`-only properties and are mapped manually in endpoints.

### Frontend Type Mapping

The file `frontend-api-types.ts` contains TypeScript types that mirror the backend DTOs. This file should be regenerated when DTO structure changes.

---

## 11. Migrations Guide

### How to Add a New Property

1. Add the property to the entity class (e.g., `PokemonEntity`)
2. If it needs configuration, add it in `AppDbContext.OnModelCreating`
3. Run: `dotnet ef migrations add <MigrationName>`
4. Run: `dotnet ef database update` (or let startup `MigrateAsync` handle it)

```csharp
// Step 1: Add to entity
public class PokemonEntity
{
    // ... existing fields
    public int UserId { get; set; }  // NEW
}

// Step 2: Configure in DbContext
b.Entity<PokemonEntity>()
    .HasOne<User>()
    .WithMany()
    .HasForeignKey(p => p.UserId);
```

### How to Add a New Table

1. Create the entity class in `Domain/Entities/`
2. Add a `DbSet<T>` to `AppDbContext`
3. Configure relationships in `OnModelCreating`
4. Run: `dotnet ef migrations add Add<EntityName>Table`

### How to Handle Data Migration

For adding columns with default values on existing data:

```csharp
// In the migration Up() method
migrationBuilder.AddColumn<int>(
    name: "UserId",
    table: "Pokemon",
    nullable: false,
    defaultValue: 1);  // Assign to admin user
```

---

## 12. How to Add a New Feature

### Generic Workflow

1. **Define the entity** in `Domain/Entities/` (or modify existing)
2. **Add DbSet** to `AppDbContext` if new entity
3. **Create DTOs** in `Contracts/` for request/response
4. **Create service** in `Infrastructure/Services/` (interface + implementation)
5. **Create endpoint** in `Endpoints/` as an extension method on `IEndpointRouteBuilder`
6. **Register service** in `ServiceCollectionExtensions`
7. **Map endpoint** in `Program.cs`
8. **Create migration** if schema changed

### Adding an Endpoint to an Existing Group

```csharp
// In PokemonEndpoints.cs
app.MapGet("/pokemon/{id:int}/tags", async (int id, AppDbContext db) =>
{
    var tags = await db.PokemonTags
        .Where(pt => pt.PokemonId == id)
        .Include(pt => pt.Tag)
        .Select(pt => new TagDto { Id = pt.Tag.Id, Name = pt.Tag.Name })
        .ToListAsync();

    return Results.Ok(tags);
})
.WithTags("Pokemon")
.Produces<List<TagDto>>(200);
```

---

## 13. How to Modify an Existing Feature

### Where to Look First

| What you want to change | Where to look                                                                                    |
| ----------------------- | ------------------------------------------------------------------------------------------------ |
| API response shape      | `Contracts/Dtos.cs` (the DTO) and the endpoint that returns it                                   |
| Database schema         | `Domain/Entities/index.cs` → `Infrastructure/AppDbContext.cs` → create migration                 |
| Query filtering         | `Domain/Specifications/PokemonSpecifications.cs` → `Domain/Services/PokemonQueryService.cs`      |
| Sorting options         | `Domain/ValueObjects/PokemonQueryOptions.cs` (enum) → `Domain/Services/PokemonSortingService.cs` |
| Form/Mega resolution    | `Infrastructure/Services/PokemonFormService.cs`                                                  |
| Species/move names      | `Infrastructure/Services/PkHexStringService.cs`                                                  |
| File parsing            | `Infrastructure/Services/PkhexCoreParser.cs`                                                     |
| File storage paths      | `Infrastructure/Configuration/StorageConfiguration.cs`                                           |
| Auto-import behavior    | `Infrastructure/Services/FileWatcherService.cs`                                                  |
| Sprite serving          | Two inline endpoints in `Program.cs`                                                             |
| Pokémon Showdown export | `Domain/ValueObjects/ShowdownExport.cs`                                                          |

### Traced Example: Adding a new Pokémon filter

1. Add query parameter to `AdvancedPokemonQuery.cs`:
   ```csharp
   public bool? IsFavorite { get; init; }
   ```
2. Create specification in `PokemonSpecifications.cs`:
   ```csharp
   public class FavoriteSpecification : IPokemonSpecification
   {
       private readonly bool _isFavorite;
       public FavoriteSpecification(bool isFavorite) => _isFavorite = isFavorite;
       public IQueryable<PokemonEntity> Apply(IQueryable<PokemonEntity> query)
           => query.Where(p => p.Favorite == _isFavorite);
   }
   ```
3. Register in `PokemonQueryService.BuildSpecifications()`:
   ```csharp
   if (query.IsFavorite.HasValue)
       specifications.Add(new FavoriteSpecification(query.IsFavorite.Value));
   ```

No endpoint changes needed — `[AsParameters]` automatically binds the new query parameter.

---

## 14. How to Debug Common Problems

### API won't start

- Check `StorageConfiguration` logs — it prints all resolved paths
- Verify `BEASTVAULT_DB_PATH` env var if set
- Check if port 5000/5001 is already in use

### PKHeX fails to parse a file

- Check the file extension matches the actual format
- PKHeX.Core `EntityFormat.GetFromBytes()` returns null for invalid data
- The parser uses reflection for some fields (`GetProp<T>`) — check the PKM type has the expected property

### File not found on disk

- `StoredPath` in the Files table may reference a moved/deleted file
- Use `POST /maintenance/sync` to clean up orphaned entries
- Check if the file was deleted from `~/Documents/BeastVault` (scan will remove DB entry)

### SQLite database locked

- Only one writer at a time; scoped `AppDbContext` should prevent this
- Check for concurrent scan + import operations
- In Docker: ensure volume is not mounted read-only

### CORS errors

- Check `CORS_ALLOWED_ORIGINS` env var format (comma-separated, no trailing slash)
- For Electron: fallback allows localhost and private networks automatically

### Swagger not loading

- Swagger is always enabled (even in production) — check `/swagger` URL
- Verify `app.UseSwagger()` and `app.UseSwaggerUI()` are called in Program.cs

---

## 15. The PKHeX.Core Integration

PKHeX.Core is the heart of the application. Understanding it is key:

### What PKHeX.Core Does

- Reads raw `.pk*` binary files (from all Pokémon game generations)
- Exposes a `PKM` base class with properties for every Pokémon data field
- Provides `GameInfo.Strings` for localized name lookups (species, moves, abilities, items, natures, etc.)
- Provides `FormConverter` for form name resolution

### How BeastVault Uses PKHeX.Core

1. **Parsing** (`PkhexCoreParser.cs`): `EntityFormat.GetFromBytes(bytes)` → `PKM` object
2. **Name Resolution** (`PkHexStringService.cs`): `GameInfo.Strings.Species[speciesId]` → species name
3. **Form Resolution** (`PokemonFormService.cs`): Maps held items (Mega Stones) and flags (Gigantamax) to form IDs
4. **Export** (`ShowdownExport.cs`): Uses PKHeX string services to build Showdown-format text

### PKM Property Access

The parser uses reflection to safely access properties that may not exist on all PKM subclasses:

```csharp
T GetProp<T>(string propName, T defaultValue)
{
    var pi = pk.GetType().GetProperty(propName);
    if (pi != null && pi.PropertyType == typeof(T))
    {
        var val = pi.GetValue(pk);
        if (val is T t) return t;
    }
    return defaultValue;
}
```

This is necessary because a `.pk1` (Gen 1) file doesn't have the same properties as a `.pk9` (Gen 9) file.

---

## 16. Cross-Platform Storage Architecture

BeastVault supports multiple deployment targets. The `StorageConfiguration` class handles path resolution:

### Path Resolution Priority

| Component     | Env Var                   | Appsettings Key                       | Windows Default                      | macOS Default                                            | Linux Default                 | Docker Default            |
| ------------- | ------------------------- | ------------------------------------- | ------------------------------------ | -------------------------------------------------------- | ----------------------------- | ------------------------- |
| Database      | `BEASTVAULT_DB_PATH`      | `BeastVault:Storage:DatabasePath`     | `%APPDATA%\BeastVault\beastvault.db` | `~/Library/Application Support/BeastVault/beastvault.db` | `~/.beastvault/beastvault.db` | `/app/data/beastvault.db` |
| Pokémon files | `BEASTVAULT_POKEMON_PATH` | `BeastVault:Storage:PokemonFilesPath` | `Documents\BeastVault`               | `~/Documents/BeastVault`                                 | `~/BeastVault`                | `/app/pokemon`            |
| Backups       | (derived)                 | (derived)                             | `Documents\BeastVault\backup`        | `~/Documents/BeastVault/backup`                          | `~/BeastVault/backup`         | `/app/pokemon/backup`     |
| Sprites       | `BEASTVAULT_ASSETS_PATH`  | (none)                                | `./assets`                           | `./assets`                                               | `./assets`                    | `./assets`                |

### Backup Organization

```
backup/
├── pk9/
│   ├── 2024/
│   │   ├── Pikachu.pk9
│   │   └── Charizard.pk9
│   └── 2025/
│       └── Eevee.pk9
├── pk8/
│   └── 2024/
│       └── Snorlax.pk8
└── pa8/
    └── 2024/
        └── Arceus.pa8
```

---

## 17. How to Explain This Backend in an Interview

> "BeastVault is a cross-platform Pokémon save file tracker. The backend is a .NET 9 Minimal API that integrates PKHeX.Core to parse binary Pokémon files from all game generations. It uses Entity Framework Core with SQLite and a Specification pattern for advanced filtering. The app runs on Windows, macOS, Linux, Docker, and inside an Electron desktop shell. Storage paths are resolved dynamically per platform. The frontend is Vue 3, and there's also an Electron wrapper. Currently it's single-user, and my next step is adding JWT authentication with per-user data isolation."

---

## 18. Glossary

| Term                   | Explanation                                                                               |
| ---------------------- | ----------------------------------------------------------------------------------------- |
| PKHeX.Core             | Open-source .NET library for reading/writing Pokémon save file data                       |
| `.pk9`                 | Binary file format for a single Pokémon from Generation 9 (Scarlet/Violet)                |
| `.pa8`                 | Binary file for Pokémon Legends: Arceus                                                   |
| `.pb7`                 | Binary file for Let's Go Pikachu/Eevee                                                    |
| Specification pattern  | Design pattern where each filter is a separate class implementing `IPokemonSpecification` |
| CompositeSpecification | A specification that combines multiple specifications with AND logic                      |
| Showdown format        | Text format used by Pokémon Showdown battle simulator                                     |
| Mega Evolution         | Temporary power-up form triggered by held Mega Stones (Gen 6-7)                           |
| Gigantamax             | Special large forms in Sword/Shield (Gen 8)                                               |
| SpriteKey              | String combining species+form+shiny to identify the correct sprite image                  |
| SHA256                 | Hash used for file deduplication — same hash = same Pokémon file                          |
| RawBlob                | Raw file bytes stored in the database as an additional backup                             |
| FileWatcher            | Misnomer for `FileWatcherService` — actually does batch scanning, not filesystem watching |
| Minimal API            | ASP.NET Core pattern using `app.MapGet/MapPost` instead of MVC controllers                |

---

## 19. Safe Change Checklist

Before making any change to the backend, verify:

- [ ] `dotnet build` succeeds
- [ ] The change does not modify existing API response shapes (check `frontend-api-types.ts`)
- [ ] New entities have proper configuration in `AppDbContext.OnModelCreating`
- [ ] New services are registered in `ServiceCollectionExtensions`
- [ ] New endpoints are mapped in `Program.cs`
- [ ] File path operations use `StorageConfiguration` (never hardcode paths)
- [ ] PKHeX property access uses safe reflection or null checks
- [ ] New specifications follow the `IPokemonSpecification` interface
- [ ] Database changes have a corresponding migration
- [ ] CORS settings allow the frontend origin
- [ ] Swagger shows the new endpoint with correct tags
