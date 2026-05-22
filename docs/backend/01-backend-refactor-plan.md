# Backend Refactor Plan — BeastVault API

## 1. Executive Summary

**BeastVault** is a Pokémon save file reader and tracker that imports `.pk*` files (from all generations via PKHeX.Core), stores parsed Pokémon data in SQLite, and serves it through a REST API consumed by a Vue frontend and an Electron desktop app.

| Attribute    | Value                                                                                                                                     |
| ------------ | ----------------------------------------------------------------------------------------------------------------------------------------- |
| Framework    | ASP.NET Core 9 — Minimal API                                                                                                              |
| ORM          | Entity Framework Core 9 (SQLite)                                                                                                          |
| Auth         | **JWT Bearer** — BCrypt password hashing, role-based (Standard/Admin), multi-user with data isolation                                     |
| External lib | PKHeX.Core 25.12.21 (Pokémon file parser)                                                                                                 |
| Architecture | DDD-influenced layered (Domain/Entities, Domain/Services, Domain/Specifications, Domain/ValueObjects, Infrastructure/Services, Endpoints) |
| Deployment   | Docker, Windows, macOS, Linux, Electron Desktop                                                                                           |

### Architectural Quality

**Rating: 8/10 — Refactored with auth, services, and proper architecture**

The project now has JWT authentication, multi-user data isolation, extracted service layer (IPokemonService, ITagService), split entity/DTO files, and proper endpoint authorization. Static domain services remain (by design — they are pure functions with no DI needs).

### Completed Refactor

- ✅ JWT authentication with BCrypt password hashing
- ✅ User entity with role-based authorization (Standard/Admin)
- ✅ UserId on all data entities (FileEntity, PokemonEntity, TagEntity)
- ✅ All endpoints protected with RequireAuthorization
- ✅ Admin-only endpoints use AdminPolicy
- ✅ Service layer extracted (IPokemonService/PokemonService, ITagService/TagService)
- ✅ Entity files split from index.cs into individual files
- ✅ DTO files split from Dtos.cs into domain-specific files
- ✅ Sprite endpoints extracted from Program.cs
- ✅ ServiceCollectionExtensions extracted from Program.cs
- ✅ Dead code and empty files removed
- ✅ Global error handling middleware
- ✅ EF migration with admin user seeding

### Remaining Opportunities

- Static services (PkHexStringService, PokemonComparisonService, etc.) could be made injectable if testing requires it
- TagEndpoints still has inline DB logic (image upload, SHA256 duplicate cascading)
- ImportEndpoints has inline DB logic (could extract IImportService)
- Validation layer could be formalized

---

## 2. Current Backend Structure

```
BeastVault.Api/
├── Program.cs                          ← Entry point + DI + 2 inline sprite endpoints + ServiceCollectionExtensions
├── BeastVault.Api.csproj               ← .NET 9, PKHeX.Core, EF Core 9, Swagger
├── appsettings.json                    ← Storage paths (empty defaults), connection string
├── appsettings.Development.json        ← Dev overrides
├── frontend-api-types.ts               ← Generated TypeScript types
├── Dockerfile / docker-compose.yml     ← Container config
│
├── Contracts/
│   ├── Dtos.cs                         ← All DTOs in one file (~400 lines)
│   └── AdvancedPokemonQuery.cs         ← Complex query parameter record
│
├── Domain/
│   ├── Entities/
│   │   └── index.cs                    ← All entities in one file (FileEntity, PokemonEntity, StatsEntity, MoveEntity, RelearnMoveEntity, TagEntity, join tables)
│   ├── Services/
│   │   ├── PkheXMappingService.cs      ← EMPTY file
│   │   ├── PokemonQueryService.cs      ← Static: builds Specification-based queries
│   │   └── PokemonSortingService.cs    ← Static: multi-field sorting with LINQ expressions
│   ├── Specifications/
│   │   ├── IPokemonSpecification.cs    ← Interface + CompositeSpecification
│   │   └── PokemonSpecifications.cs    ← ~20 specification classes (filters)
│   └── ValueObjects/
│       ├── PokemonQueryOptions.cs      ← Enums (TypeFilterMode, PokemonSortField, SortDirection) + records
│       └── ShowdownExport.cs           ← Pokémon Showdown text export
│
├── Endpoints/
│   ├── PokemonEndpoints.cs             ← ~530 lines: CRUD, admin wipe, compare, metadata, debug
│   ├── ImportEndpoints.cs              ← File upload and parse
│   ├── FilesEndpoints.cs               ← Download original/backup/disk files
│   ├── ScanEndpoints.cs                ← Directory scan and auto-import
│   ├── TagEndpoints.cs                 ← CRUD + image upload for tags
│   ├── MaintenanceEndpoints.cs         ← Sync DB with filesystem, orphan detection
│   ├── ConfigurationEndpoints.cs       ← Runtime path configuration, data migration
│   └── HealthEndpoints.cs              ← Simple /health check
│
├── Extensions/
│   └── WebApplicationExtension.cs      ← EMPTY file
│
├── Infrastructure/
│   ├── AppDbContext.cs                 ← DbContext: 8 DbSets, Fluent API config
│   ├── EnvironmentUtils.cs             ← DUPLICATE: Docker detection + path logic (also in StorageConfiguration)
│   ├── Configuration/
│   │   └── StorageConfiguration.cs     ← Cross-platform storage paths (DB, Pokémon files, backups)
│   ├── Mappings/
│   │   └── PkhexMappings.cs            ← EMPTY file (placeholder)
│   └── Services/
│       ├── FileStorageService.cs       ← File CRUD (save, backup, delete, read)
│       ├── FileWatcherService.cs       ← Auto-scan ~/Documents/BeastVault + import new files
│       ├── PkhexCoreParser.cs          ← PKHeX.Core integration: parse .pk* → entities
│       ├── PkHexStringService.cs       ← Static: species/move/ability name lookups via PKHeX
│       ├── PokemonComparisonService.cs ← Static: compare two Pokémon entities
│       ├── PokemonFormService.cs       ← Static: Mega/Gigantamax form resolution
│       └── PokemonGameInfoService.cs   ← Static: game/generation/type mappings
│
├── Migrations/                         ← 2 migrations (InitialCreate, EnsureTagsTableExists)
├── Properties/
├── Services/                           ← EMPTY folder
└── assets/                             ← Custom sprite images
```

| Folder                          | Purpose                                 | Concern                                                                              |
| ------------------------------- | --------------------------------------- | ------------------------------------------------------------------------------------ |
| `Contracts/`                    | DTOs and query parameter records        | All DTOs in one file; some DTOs have constructors with entity logic (mapping in DTO) |
| `Domain/Entities/`              | All entities in `index.cs`              | Single file for 8 entities; file named `index.cs` (JS convention)                    |
| `Domain/Services/`              | Query building and sorting              | All static — cannot be injected or tested with mocks                                 |
| `Domain/Specifications/`        | Specification pattern for filtering     | Well designed, good pattern                                                          |
| `Domain/ValueObjects/`          | Enums, records, Showdown export         | ShowdownExport has infrastructure dependency (PkHexStringService)                    |
| `Endpoints/`                    | Minimal API route groups                | DB logic inline; PokemonEndpoints.cs is 530+ lines                                   |
| `Extensions/`                   | Pipeline extensions                     | Empty file only                                                                      |
| `Infrastructure/Configuration/` | Storage path management                 | Well designed, cross-platform                                                        |
| `Infrastructure/Services/`      | File I/O, PKHeX parsing, static helpers | Mix of injectable (3) and static (4) services                                        |
| `Infrastructure/Mappings/`      | PKHeX-to-domain mappings                | Empty placeholder                                                                    |
| `Services/`                     | (top-level)                             | Empty folder, misleading — real services are elsewhere                               |

---

## 3. Current Request Flow

### Import flow (with services)

```
POST /import → ImportEndpoints
    → PkhexCoreParser.ParseAsync(bytes, fileName, storage)
        → PKHeX.Core.EntityFormat.GetFromBytes(bytes)
        → FileStorageService.Save(sha256, ext, bytes, ...)
    → AppDbContext.Files.Add(file)
    → AppDbContext.Pokemon.Add(pokemon)
    → AppDbContext.Stats.Add(stats)
    → AppDbContext.Moves.AddRange(moves)
    → SaveChangesAsync()
```

### Query flow (specification pattern)

```
GET /pokemon?[filters] → PokemonEndpoints
    → PokemonQueryService.BuildQuery(baseQuery, queryParams)
        → BuildSpecifications(queryParams)
            → TextSearchSpecification, ShinySpecification, etc.
        → ApplySpecifications(query, specs)
            → CompositeSpecification.Apply()
        → ApplySorting(query, queryParams)
            → PokemonSortingService.ApplyMultipleSort()
    → db.Pokemon.Join(db.Files) [inline in endpoint]
    → PokemonFormService.GetDisplayForm() [static, in Select()]
    → PokemonGameInfoService.GetSpeciesOriginGeneration() [static, in Select()]
    → db.PokemonTags.Where().Include() [inline tag loading]
    → PkHexStringService.GetFormName/GetSpeciesName [inline mapping]
    → Results.Ok(new { Items, Total, Stats })
```

### Auto-scan flow (startup)

```
app.RunAsync() startup
    → FileWatcherService.ScanAndImportNewFilesAsync()
        → CleanupDeletedFilesAsync() [removes DB entries for deleted files]
        → Directory.GetFiles(watchPath, "*.*")
        → ProcessFileAsync(filePath)
            → PkhexCoreParser.ParseAsync()
            → AppDbContext.Files.Add() + SaveChangesAsync()
```

---

## 4. Current Database and EF Core Setup

### DbContext

`AppDbContext` has 8 `DbSet` properties:

| DbSet          | Entity              | Purpose                                        |
| -------------- | ------------------- | ---------------------------------------------- |
| `Files`        | `FileEntity`        | Stored .pk\* file metadata + optional raw blob |
| `Pokemon`      | `PokemonEntity`     | Parsed Pokémon data (~60 fields)               |
| `Stats`        | `StatsEntity`       | IVs, EVs, hyper training, calculated stats     |
| `Moves`        | `MoveEntity`        | 4 move slots per Pokémon                       |
| `RelearnMoves` | `RelearnMoveEntity` | 4 relearn move slots                           |
| `Tags`         | `TagEntity`         | User-defined tags with optional image          |
| `PokemonTags`  | `PokemonTagEntity`  | Many-to-many: Pokémon ↔ Tags                   |
| `FileTags`     | `FileTagEntity`     | Many-to-many: Files ↔ Tags                     |

### Entity Relationships

```
FileEntity (1) ──→ (1) PokemonEntity
                        ├── (1) StatsEntity
                        ├── (4) MoveEntity [composite key: PokemonId+Slot]
                        ├── (4) RelearnMoveEntity [composite key: PokemonId+Slot]
                        └── (*) PokemonTagEntity ──→ TagEntity
FileEntity (*) ──→ FileTagEntity ──→ TagEntity
```

### Indexes

- `FileEntity.Sha256` — unique (deduplication)
- `PokemonEntity.(SpeciesId, IsShiny)` — composite (common query)
- `PokemonEntity.OriginGame` — individual
- `TagEntity.Name` — unique (case-sensitive)

### SQLite Connection

Resolved dynamically via `StorageConfiguration`:

1. `BEASTVAULT_DB_PATH` env var
2. `BeastVault:Storage:DatabasePath` in appsettings
3. `ConnectionStrings:Default`
4. Platform-default: `%APPDATA%/BeastVault/beastvault.db` (Windows), `~/.beastvault/beastvault.db` (Linux), etc.

### Database Initialization

Uses `db.Database.MigrateAsync()` on startup — correct approach.

### Risks

- **No `UserId` FK on any entity** — cannot isolate data by user
- Composite keys for Moves/RelearnMoves mean pokemonId deletion requires manual cascade
- `RawBlob` (byte[]) stored inline — could cause DB bloat for large collections
- No soft delete — data is permanently removed

---

## 5. Current API Surface

### Pokemon Domain

| Route                                  | Method | Responsibility                         | Concern                                                           |
| -------------------------------------- | ------ | -------------------------------------- | ----------------------------------------------------------------- |
| `GET /pokemon`                         | GET    | Advanced query with specs + pagination | ~100 lines inline with Join, Select, tag loading, form resolution |
| `GET /pokemon/{id}`                    | GET    | Detail with stats/moves                | Direct DB access                                                  |
| `GET /pokemon/{id}/showdown`           | GET    | Showdown text export                   | Direct DB access                                                  |
| `PATCH /pokemon/{id}`                  | PATCH  | Update favorite/notes                  | Direct DB access                                                  |
| `GET /pokemon/compare/{id1}/{id2}`     | GET    | Compare two Pokémon                    | Uses static ComparisonService                                     |
| `GET /pokemon/metadata`                | GET    | Filter/sort options for frontend       | Hardcoded arrays with disabled items                              |
| `DELETE /pokemon/{pokemonId}/database` | DELETE | Delete + preserve backup               | ~50 lines inline cleanup                                          |
| `DELETE /pokemon/{pokemonId}/backup`   | DELETE | Delete completely                      | ~60 lines inline cleanup (duplicated pattern)                     |
| `POST /admin/wipe-database`            | POST   | **Nuke entire database**               | ⚠️ No auth protection whatsoever                                  |
| `GET /debug/origin-games`              | GET    | Debug data                             | Should be dev-only                                                |

### Import / File Management

| Route                              | Method | Responsibility                      | Concern                       |
| ---------------------------------- | ------ | ----------------------------------- | ----------------------------- |
| `POST /import`                     | POST   | Upload and parse .pk\* files        | No auth; complex inline logic |
| `GET /files/{id}`                  | GET    | Download stored file                | Direct DB + filesystem        |
| `GET /export/{pokemonId}`          | GET    | Download original file from DB blob | Direct DB access              |
| `GET /export/database/{pokemonId}` | GET    | Download from disk                  | Direct DB + filesystem        |
| `GET /export/backup/{pokemonId}`   | GET    | Download backup                     | Direct DB + filesystem        |

### File Scanning

| Route                  | Method | Responsibility                   | Concern                         |
| ---------------------- | ------ | -------------------------------- | ------------------------------- |
| `POST /scan/directory` | POST   | Scan and auto-import from folder | No auth; scans system directory |
| `GET /scan/status`     | GET    | Directory info and file counts   | No auth                         |

### Tags

| Route                     | Method | Responsibility            | Concern                        |
| ------------------------- | ------ | ------------------------- | ------------------------------ |
| `GET /tags`               | GET    | List all tags             | No auth                        |
| `GET /tags/{id}`          | GET    | Get tag by ID             | No auth                        |
| `POST /tags`              | POST   | Create tag                | No auth                        |
| `PUT /tags/{id}`          | PUT    | Update tag                | No auth                        |
| `DELETE /tags/{id}`       | DELETE | Delete tag + associations | No auth                        |
| `POST /tags/{id}/image`   | POST   | Upload tag image          | No auth; saves to wwwroot/tags |
| `DELETE /tags/{id}/image` | DELETE | Delete tag image          | No auth                        |

### Maintenance / Configuration

| Route                                      | Method | Responsibility              | Concern                        |
| ------------------------------------------ | ------ | --------------------------- | ------------------------------ |
| `POST /maintenance/sync`                   | POST   | Remove orphaned DB entries  | No auth; modifies data         |
| `GET /maintenance/status`                  | GET    | DB/filesystem health info   | No auth                        |
| `GET /maintenance/pokemon/{id}/duplicates` | GET    | Find duplicate files        | No auth                        |
| `GET /config`                              | GET    | Show current paths          | Exposes system paths           |
| `POST /config/database`                    | POST   | Change DB path at runtime   | ⚠️ No auth; can relocate DB    |
| `POST /config/pokemon`                     | POST   | Change file path at runtime | ⚠️ No auth; can relocate files |

### Sprites (inline in Program.cs)

| Route                                  | Method | Responsibility           | Concern                                  |
| -------------------------------------- | ------ | ------------------------ | ---------------------------------------- |
| `GET /custom-sprites/search/{pattern}` | GET    | Search sprite by pattern | Defined inline in Program.cs (~60 lines) |
| `GET /custom-sprites/{fileName}`       | GET    | Serve sprite file        | Defined inline in Program.cs (~90 lines) |

### Health

| Route         | Method | Responsibility | Concern     |
| ------------- | ------ | -------------- | ----------- |
| `GET /health` | GET    | Service status | No concerns |

**Total: ~30 endpoints, 0 with auth protection**

---

## 6. Current Problems and Risks

| Area             | Problem                                                                     | Why It Matters                                          | Severity    | Recommended Action                             |
| ---------------- | --------------------------------------------------------------------------- | ------------------------------------------------------- | ----------- | ---------------------------------------------- |
| **Security**     | No authentication on any endpoint                                           | Anyone can wipe DB, change paths, delete data           | 🔴 Critical | Add JWT auth + User entity                     |
| **Security**     | `POST /admin/wipe-database` unprotected                                     | Complete data destruction by any caller                 | 🔴 Critical | Add admin-only auth                            |
| **Security**     | `POST /config/database` and `/config/pokemon` unprotected                   | Can relocate DB/files to arbitrary paths                | 🔴 Critical | Add admin-only auth                            |
| **Security**     | `POST /scan/directory` unprotected                                          | Can trigger filesystem scan as any user                 | 🟠 High     | Add auth                                       |
| **Architecture** | No user model or data ownership                                             | Multi-user impossible; all data is global               | 🔴 Critical | Add User entity + UserId FK                    |
| **Architecture** | All business logic inline in endpoints                                      | 530+ line endpoint files; untestable                    | 🟠 High     | Extract to services with interfaces            |
| **Architecture** | Static domain services                                                      | Cannot inject dependencies, mock, or test               | 🟠 High     | Convert to injectable + interface              |
| **Architecture** | ServiceCollectionExtensions in Program.cs namespace                         | Breaks single-responsibility, confusing location        | 🟡 Medium   | Move to Extensions/ folder                     |
| **Code quality** | PokemonDetailDto has entity-mapping constructor                             | DTO knows about entity internals                        | 🟡 Medium   | Use mapping method in service                  |
| **Code quality** | All entities in `index.cs`                                                  | JS naming convention; hard to navigate                  | 🟡 Medium   | Split into one file per entity                 |
| **Code quality** | All DTOs in `Dtos.cs`                                                       | 400+ lines, hard to find specific DTO                   | 🟡 Medium   | Split by domain area                           |
| **Code quality** | Empty files: PkheXMappingService, WebApplicationExtension, PkhexMappings    | Dead code, misleading                                   | 🟢 Low      | Delete                                         |
| **Code quality** | EnvironmentUtils duplicates StorageConfiguration                            | Redundant logic                                         | 🟢 Low      | Delete EnvironmentUtils                        |
| **Code quality** | Duplicate delete patterns in PokemonEndpoints                               | Two delete endpoints with nearly identical cleanup code | 🟡 Medium   | Extract shared cleanup method                  |
| **Code quality** | Debug endpoint in production code                                           | `/debug/origin-games` should be dev-only                | 🟡 Medium   | Move behind `#if DEBUG` or admin auth          |
| **Code quality** | Empty `Services/` folder at root                                            | Misleading structure                                    | 🟢 Low      | Delete                                         |
| **Performance**  | `RawBlob` stored in Files table                                             | DB bloat; 232-byte .pk9 files × thousands               | 🟡 Medium   | Evaluate if needed alongside disk storage      |
| **DDD**          | ShowdownExport (ValueObject) depends on PkHexStringService (Infrastructure) | Layer violation: Domain depends on Infrastructure       | 🟡 Medium   | Inject name resolver or move to Infrastructure |

---

## 7. Code Style and Comment Cleanup

### Overall Assessment

Comments are primarily in Spanish (project preference — keep). Code uses a mix of:

- `Console.WriteLine` with emoji (📂 ✅ ❌ ⚠️) for logging — acceptable for desktop app context
- XML doc comments on DTOs and service classes — good, keep
- Some commented-out code in metadata endpoint (disabled filters) — document why or remove

### Comments to Remove

| File                         | Line/Area                                                | Reason                                  |
| ---------------------------- | -------------------------------------------------------- | --------------------------------------- |
| `PokemonEndpoints.cs`        | Disabled gender/form/held-item filters (commented block) | Either re-enable or remove with a TODO  |
| `PokemonEndpoints.cs`        | Disabled sort fields comments                            | Document in metadata response or remove |
| `PkhexMappings.cs`           | Entire file is one comment                               | Delete the empty file                   |
| `WebApplicationExtension.cs` | Single comment placeholder                               | Delete the empty file                   |

### Comments to Keep

- XML `<summary>` docs on DTO properties — good for Swagger
- Spanish inline comments explaining business logic — project convention
- PKHeX format explanations (`.pk1` to `.pk9`, `.pa8`, `.pb7`, etc.)

### Naming Observations

| Current                                   | Issue                                                                   | Suggestion                                                   |
| ----------------------------------------- | ----------------------------------------------------------------------- | ------------------------------------------------------------ |
| `index.cs`                                | JS naming convention for entities file                                  | Split into individual entity files                           |
| `Dtos.cs`                                 | Too generic                                                             | Split: `PokemonDtos.cs`, `TagDtos.cs`, `ImportDtos.cs`, etc. |
| `PkhexCoreParser` vs `PkHexStringService` | Inconsistent casing (Pkhex vs PkHex)                                    | Standardize to `PkHex`                                       |
| `FileWatcherService`                      | Name implies filesystem watching (events); actually does batch scanning | Rename to `FileScanner` or `FileScanService`                 |

### Files That Would Benefit from Splitting

| File                            | Lines | Recommendation                                                                                                                               |
| ------------------------------- | ----- | -------------------------------------------------------------------------------------------------------------------------------------------- |
| `Domain/Entities/index.cs`      | ~250  | Split into `FileEntity.cs`, `PokemonEntity.cs`, `StatsEntity.cs`, `MoveEntity.cs`, `TagEntity.cs`, `PokemonTagEntity.cs`, `FileTagEntity.cs` |
| `Contracts/Dtos.cs`             | ~400  | Split into `PokemonDtos.cs`, `TagDtos.cs`, `ImportDtos.cs`, `PaginationDtos.cs`                                                              |
| `Endpoints/PokemonEndpoints.cs` | ~530  | Extract admin endpoints to `AdminEndpoints.cs`, debug endpoints to `DebugEndpoints.cs`                                                       |
| `Program.cs`                    | ~300+ | Move sprite endpoints to `SpriteEndpoints.cs`, move `ServiceCollectionExtensions` to `Extensions/`                                           |

---

## 8. Proposed Target Architecture for This Project

```
BeastVault.Api/
├── Program.cs                              ← Clean: DI + pipeline + app.Run()
├── appsettings.json                        ← + JwtSettings section
│
├── Configuration/
│   ├── JwtSettings.cs                      ← NEW: JWT config record
│   └── StorageConfiguration.cs             ← MOVED from Infrastructure/Configuration/
│
├── Contracts/
│   ├── Auth/
│   │   ├── LoginRequest.cs                 ← NEW
│   │   ├── LoginResponse.cs                ← NEW
│   │   └── UserDtos.cs                     ← NEW
│   ├── Pokemon/
│   │   ├── PokemonDtos.cs                  ← SPLIT from Dtos.cs
│   │   ├── PokemonQuery.cs                 ← RENAMED from AdvancedPokemonQuery.cs
│   │   └── ImportDtos.cs                   ← SPLIT from Dtos.cs
│   ├── Tags/
│   │   └── TagDtos.cs                      ← SPLIT from Dtos.cs
│   └── Common/
│       └── PagedResult.cs                  ← SPLIT from Dtos.cs
│
├── Domain/
│   ├── Entities/
│   │   ├── User.cs                         ← NEW: User entity with UserRole enum
│   │   ├── FileEntity.cs                   ← SPLIT + add UserId FK
│   │   ├── PokemonEntity.cs                ← SPLIT + add UserId FK
│   │   ├── StatsEntity.cs                  ← SPLIT from index.cs
│   │   ├── MoveEntity.cs                   ← SPLIT from index.cs
│   │   ├── RelearnMoveEntity.cs            ← SPLIT from index.cs
│   │   ├── TagEntity.cs                    ← SPLIT + add UserId FK (nullable for system tags)
│   │   ├── PokemonTagEntity.cs             ← SPLIT from index.cs
│   │   └── FileTagEntity.cs               ← SPLIT from index.cs
│   ├── Services/
│   │   ├── IPokemonQueryService.cs         ← NEW: interface
│   │   ├── PokemonQueryService.cs          ← CONVERT from static to injectable
│   │   ├── IPokemonSortingService.cs       ← NEW: interface
│   │   └── PokemonSortingService.cs        ← CONVERT from static to injectable
│   ├── Specifications/                     ← KEEP as-is (well designed)
│   │   ├── IPokemonSpecification.cs
│   │   └── PokemonSpecifications.cs
│   └── ValueObjects/                       ← KEEP
│       ├── PokemonQueryOptions.cs
│       └── ShowdownExport.cs
│
├── Endpoints/
│   ├── AuthEndpoints.cs                    ← NEW: login, register, user management
│   ├── PokemonEndpoints.cs                 ← REFACTORED: use service, add auth
│   ├── ImportEndpoints.cs                  ← ADD auth
│   ├── FilesEndpoints.cs                   ← ADD auth
│   ├── ScanEndpoints.cs                    ← ADD auth + user-scoped paths
│   ├── TagEndpoints.cs                     ← ADD auth
│   ├── SpriteEndpoints.cs                  ← NEW: moved from Program.cs
│   ├── MaintenanceEndpoints.cs             ← ADD admin-only auth
│   ├── ConfigurationEndpoints.cs           ← ADD admin-only auth
│   └── HealthEndpoints.cs                  ← KEEP
│
├── Extensions/
│   ├── ServiceCollectionExtensions.cs      ← MOVED from Program.cs
│   └── ClaimsPrincipalExtensions.cs        ← NEW: GetUserId() helper
│
├── Infrastructure/
│   ├── AppDbContext.cs                     ← + User DbSet, + UserId FK configs
│   ├── Middleware/
│   │   └── UserContextMiddleware.cs        ← NEW: extract UserId from JWT
│   ├── Services/
│   │   ├── IAuthService.cs                 ← NEW: interface
│   │   ├── AuthService.cs                  ← NEW: JWT generation + BCrypt verify
│   │   ├── IFileStorageService.cs          ← NEW: interface
│   │   ├── FileStorageService.cs           ← ADD interface, user-scoped paths
│   │   ├── IFileScanService.cs             ← NEW: interface (renamed from FileWatcher)
│   │   ├── FileScanService.cs              ← RENAMED + user-scoped scanning
│   │   ├── IPkHexParserService.cs          ← NEW: interface
│   │   ├── PkHexParserService.cs           ← RENAMED from PkhexCoreParser
│   │   ├── PkHexStringService.cs           ← KEEP static (pure lookups, no state)
│   │   ├── PokemonComparisonService.cs     ← KEEP static (pure logic)
│   │   ├── PokemonFormService.cs           ← KEEP static (pure lookups)
│   │   └── PokemonGameInfoService.cs       ← KEEP static (pure lookups)
│   └── Mappings/
│       └── PokemonMapper.cs                ← NEW: entity-to-DTO mapping (replace DTO constructors)
│
├── Migrations/
└── Properties/

DELETED:
- Domain/Services/PkheXMappingService.cs     (empty)
- Extensions/WebApplicationExtension.cs      (empty)
- Infrastructure/EnvironmentUtils.cs          (duplicate of StorageConfiguration)
- Infrastructure/Mappings/PkhexMappings.cs    (empty)
- Services/                                   (empty folder)
```

---

## 9. Refactor Phases

### Phase 0 — Cleanup Dead Code

**Goal**: Remove empty files, duplicate utilities, and the empty Services folder.

**Files affected**: `PkheXMappingService.cs`, `WebApplicationExtension.cs`, `PkhexMappings.cs`, `EnvironmentUtils.cs`, `Services/` folder

**Actions**:

- [ ] Delete `Domain/Services/PkheXMappingService.cs` (empty)
- [ ] Delete `Extensions/WebApplicationExtension.cs` (empty)
- [ ] Delete `Infrastructure/Mappings/PkhexMappings.cs` (empty)
- [ ] Delete `Infrastructure/EnvironmentUtils.cs` (duplicates StorageConfiguration)
- [ ] Delete empty `Services/` folder at project root

**Risks**: None — all targets are unused.
**Verification**: Build succeeds; `dotnet build` produces no errors.
**Frontend impact**: None.

---

### Phase 1 — Split Entity and DTO Files

**Goal**: One entity per file, DTOs grouped by domain area.

**Files affected**: `Domain/Entities/index.cs`, `Contracts/Dtos.cs`

**Actions**:

- [ ] Split `index.cs` into: `FileEntity.cs`, `PokemonEntity.cs`, `StatsEntity.cs`, `MoveEntity.cs`, `RelearnMoveEntity.cs`, `TagEntity.cs`, `PokemonTagEntity.cs`, `FileTagEntity.cs`
- [ ] Split `Dtos.cs` into: `Pokemon/PokemonDtos.cs`, `Tags/TagDtos.cs`, `Pokemon/ImportDtos.cs`, `Common/PagedResult.cs`
- [ ] Update all `using` statements across the project
- [ ] Delete `index.cs` and `Dtos.cs`

**Risks**: Missing using statements. Run `dotnet build` after each split.
**Verification**: All existing endpoints still compile and return same responses.
**Frontend impact**: None — no API contract changes.

---

### Phase 2 — Move Code Out of Program.cs

**Goal**: Clean Program.cs down to DI registration + pipeline configuration only.

**Files affected**: `Program.cs`, new `Endpoints/SpriteEndpoints.cs`, new `Extensions/ServiceCollectionExtensions.cs`

**Actions**:

- [ ] Create `Endpoints/SpriteEndpoints.cs` with `MapSpriteEndpoints()` extension method
- [ ] Move the two inline sprite endpoints from Program.cs to SpriteEndpoints.cs
- [ ] Move `ServiceCollectionExtensions` class from bottom of Program.cs to `Extensions/ServiceCollectionExtensions.cs`
- [ ] Update Program.cs to call `app.MapSpriteEndpoints()` and use the moved extension methods
- [ ] Move `StorageConfiguration` from `Infrastructure/Configuration/` to `Configuration/`

**Risks**: Path resolution differences if `Directory.GetCurrentDirectory()` changes. Test sprite endpoints.
**Verification**: Sprite search and serve endpoints still work. Build succeeds.
**Frontend impact**: None — same routes.

---

### Phase 3 — Add User Entity and JWT Authentication

**Goal**: Introduce User model, JWT auth, BCrypt, and protect all endpoints.

**Files affected**: NEW files (`User.cs`, `JwtSettings.cs`, `AuthService.cs`, `AuthEndpoints.cs`, `UserContextMiddleware.cs`, `ClaimsPrincipalExtensions.cs`), `Program.cs`, `appsettings.json`, `BeastVault.Api.csproj`

**Actions**:

- [ ] Add NuGet packages: `BCrypt.Net-Next`, `Microsoft.AspNetCore.Authentication.JwtBearer`, `System.IdentityModel.Tokens.Jwt`
- [ ] Create `Domain/Entities/User.cs` with: Id (int), Username, PasswordHash (nullable), Role (UserRole enum), IsDefault
- [ ] Create `Configuration/JwtSettings.cs` record
- [ ] Create `Infrastructure/Services/IAuthService.cs` + `AuthService.cs` (JWT generation, BCrypt verification, login, register)
- [ ] Create `Infrastructure/Middleware/UserContextMiddleware.cs` (extract UserId from JWT claims)
- [ ] Create `Extensions/ClaimsPrincipalExtensions.cs` (GetUserId helper)
- [ ] Create `Endpoints/AuthEndpoints.cs` (POST /auth/login, POST /auth/register, GET /auth/users, etc.)
- [ ] Add `JwtSettings` section to `appsettings.json`
- [ ] Register JWT auth in Program.cs: `AddAuthentication`, `AddJwtBearer`, `UseAuthentication`, `UseAuthorization`
- [ ] Register `UseUserContext()` middleware after auth
- [ ] Seed default Admin user on startup (IsDefault=true, no password)
- [ ] Add `User` DbSet to AppDbContext

**Risks**: Token validation errors if SecretKey is too short. Test with Swagger "Authorize" button.
**Verification**: `/auth/login` returns JWT; protected endpoints return 401 without token.
**Frontend impact**: Frontend must add auth flow (login page, token storage, `Authorization` header).

---

### Phase 4 — Add UserId to All Data Entities

**Goal**: Associate every piece of data with a user for complete data isolation.

**Files affected**: `FileEntity.cs`, `PokemonEntity.cs`, `TagEntity.cs`, `AppDbContext.cs`, migration files

**Actions**:

- [ ] Add `int UserId` + `User User` navigation to `FileEntity`
- [ ] Add `int UserId` + `User User` navigation to `PokemonEntity`
- [ ] Add `int? UserId` + `User? User` navigation to `TagEntity` (nullable = system tag)
- [ ] Configure FK relationships in `AppDbContext.OnModelCreating`:
  - `FileEntity.UserId` → `User` (required, cascade delete)
  - `PokemonEntity.UserId` → `User` (required, cascade delete)
  - `TagEntity.UserId` → `User` (optional, set null on delete)
- [ ] Add unique index: `(UserId, Sha256)` on FileEntity (same file can belong to different users)
- [ ] Create migration: `dotnet ef migrations add AddUserOwnership`
- [ ] Write data migration SQL: assign all existing data to admin user (UserId=1)

**Risks**: Migration on existing databases with data. Test with backup of current DB.
**Verification**: All existing data is assigned to user 1. New imports get the authenticated user's ID.
**Frontend impact**: None visible — filtering happens server-side.

---

### Phase 5 — Protect Endpoints with User Filtering

**Goal**: Every data endpoint filters by authenticated user's ID.

**Files affected**: All endpoint files

**Actions**:

- [ ] Add `.RequireAuthorization()` to all endpoint groups (except auth + health)
- [ ] In every GET endpoint: add `.Where(x => x.UserId == userId)` filter
- [ ] In every POST endpoint: set `entity.UserId = userId` before saving
- [ ] In every PUT/DELETE endpoint: verify `x.UserId == userId` before modifying
- [ ] In admin endpoints (wipe, config, maintenance): require admin role
- [ ] In scan endpoint: scope to user's folder
- [ ] Mark health and auth endpoints as `[AllowAnonymous]`

**Risks**: Missing a filter somewhere = data leak. Audit every endpoint.
**Verification**: Create two users; user A cannot see user B's Pokémon.
**Frontend impact**: Frontend must send `Authorization` header on all requests.

---

### Phase 6 — Extract Business Logic to Services

**Goal**: Move DB access out of endpoints into injectable services with interfaces.

**Files affected**: All endpoint files, new service files

**Actions**:

- [ ] Create `Infrastructure/Services/IPokemonService.cs` + `PokemonService.cs`
  - Methods: `GetPagedAsync(userId, query)`, `GetByIdAsync(userId, id)`, `UpdateAsync(userId, id, dto)`, `DeleteAsync(userId, id, preserveBackup)`, `CompareAsync(userId, id1, id2)`, `ExportShowdownAsync(userId, id)`, `WipeDatabaseAsync(userId)` (admin)
- [ ] Create `Infrastructure/Services/ITagService.cs` + `TagService.cs`
  - Methods: `GetAllAsync(userId)`, `GetByIdAsync(userId, id)`, `CreateAsync(userId, dto)`, `UpdateAsync(userId, id, dto)`, `DeleteAsync(userId, id)`, `UploadImageAsync(userId, id, file)`, `DeleteImageAsync(userId, id)`
- [ ] Create `Infrastructure/Services/IImportService.cs` + `ImportService.cs`
  - Methods: `ImportFilesAsync(userId, files)`, `ScanDirectoryAsync(userId)`
- [ ] Refactor endpoints to call services instead of using AppDbContext directly
- [ ] Register all new services in DI

**Risks**: Behavioral changes during extraction. Compare responses before/after.
**Verification**: All endpoints return identical responses. Service methods are unit-testable.
**Frontend impact**: None — same API contracts.

---

### Phase 7 — Convert Static Domain Services to Injectable

**Goal**: Make PokemonQueryService and PokemonSortingService injectable for testability.

**Files affected**: `PokemonQueryService.cs`, `PokemonSortingService.cs`, new interfaces

**Actions**:

- [ ] Create `Domain/Services/IPokemonQueryService.cs`
- [ ] Convert `PokemonQueryService` from static to instance class implementing interface
- [ ] Create `Domain/Services/IPokemonSortingService.cs`
- [ ] Convert `PokemonSortingService` from static to instance class implementing interface
- [ ] Register in DI as scoped services
- [ ] Update PokemonService to inject these instead of calling static methods

**Risks**: Static methods called in LINQ expressions may need adjustment.
**Verification**: Query and sort behavior unchanged.
**Frontend impact**: None.

---

### Phase 8 — User-Scoped File Storage

**Goal**: Each user gets their own Pokémon file directory.

**Files affected**: `FileStorageService.cs`, `FileScanService.cs`, `StorageConfiguration.cs`

**Actions**:

- [ ] Modify `StorageConfiguration` to support user-scoped paths: `{base}/users/{userId}/pokemon/` and `{base}/users/{userId}/backup/`
- [ ] Update `FileStorageService` to accept userId and use user-scoped directories
- [ ] Rename `FileWatcherService` to `FileScanService`
- [ ] Update `FileScanService` to scan user-specific directories
- [ ] Create interface `IFileScanService`
- [ ] Write migration script to move existing files from flat structure to `users/1/` (admin user)
- [ ] Update startup scan to scan the authenticated user's directory (or all users for background service)

**Risks**: File path changes break existing stored paths in DB. Update `StoredPath` in migration.
**Verification**: Files are saved/read from correct user directories. Existing files still accessible after migration.
**Frontend impact**: None — file access is through API endpoints.

---

### Phase 9 — Create Mapping Layer

**Goal**: Remove entity-to-DTO mapping logic from DTOs and endpoints.

**Files affected**: `PokemonDetailDto`, new `PokemonMapper.cs`

**Actions**:

- [ ] Create `Infrastructure/Mappings/PokemonMapper.cs` with static mapping methods
- [ ] Move `PokemonDetailDto` constructor logic to mapper
- [ ] Move inline `PokemonListItemDto` mapping from PokemonEndpoints to mapper
- [ ] Remove entity parameters from DTO constructors
- [ ] Update services to use mapper

**Risks**: Missing field mapping. Compare DTO output before/after.
**Verification**: All DTO responses match previous format exactly.
**Frontend impact**: None.

---

### Phase 10 — Hybrid Tag System

**Goal**: Implement global system tags + per-user custom tags.

**Files affected**: `TagEntity.cs`, `TagEndpoints.cs` / `TagService.cs`, seed data

**Actions**:

- [ ] `TagEntity.UserId` is already nullable from Phase 4 (null = system tag)
- [ ] Create system tags seeded on startup (e.g., "Shiny", "Legendary", "Mythical", "Event", "Competitive")
- [ ] Update tag queries: user sees system tags (UserId=null) + their own tags (UserId=currentUser)
- [ ] Update tag creation: new tags get current user's ID
- [ ] System tags cannot be edited/deleted by non-admin users
- [ ] User tags can only be managed by their owner

**Risks**: Tag name uniqueness scope changes (unique per user, not globally). Update index.
**Verification**: Both users see system tags; each user's custom tags are isolated.
**Frontend impact**: Frontend may want to visually distinguish system vs. user tags.

---

### Phase 11 — Polish and Hardening

**Goal**: Final cleanup and security hardening.

**Files affected**: Various

**Actions**:

- [ ] Add `[Authorize(Roles = "Admin")]` to admin endpoints
- [ ] Remove or guard debug endpoints (`/debug/origin-games`)
- [ ] Standardize PkHex casing across all file names
- [ ] Add request validation (FluentValidation or manual) for file uploads and config changes
- [ ] Add rate limiting for auth endpoints
- [ ] Review CORS configuration for production
- [ ] Update `frontend-api-types.ts` to include auth types
- [ ] Update README.md with auth setup instructions

**Risks**: Over-restricting can break Electron app. Test both web and desktop flows.
**Verification**: Full end-to-end test: register, login, import, query, export, logout.
**Frontend impact**: Frontend needs updated type definitions.

---

## 10. Detailed Implementation Checklist

### Phase 0 — Cleanup

- [ ] Delete `Domain/Services/PkheXMappingService.cs`
- [ ] Delete `Extensions/WebApplicationExtension.cs`
- [ ] Delete `Infrastructure/Mappings/PkhexMappings.cs`
- [ ] Delete `Infrastructure/EnvironmentUtils.cs`
- [ ] Delete empty `Services/` folder
- [ ] `dotnet build` — verify no errors

### Phase 1 — Split Files

- [ ] Create individual entity files in `Domain/Entities/`
- [ ] Create DTO subfolders in `Contracts/`
- [ ] Update all namespaces and using statements
- [ ] Delete `index.cs` and `Dtos.cs`
- [ ] `dotnet build` — verify no errors

### Phase 2 — Clean Program.cs

- [ ] Create `Endpoints/SpriteEndpoints.cs`
- [ ] Move sprite endpoints from Program.cs
- [ ] Move `ServiceCollectionExtensions` to `Extensions/`
- [ ] Move `StorageConfiguration` to `Configuration/`
- [ ] `dotnet build` + test sprite endpoints

### Phase 3 — JWT Auth

- [ ] Add NuGet packages (BCrypt.Net-Next, JwtBearer)
- [ ] Create User entity
- [ ] Create JwtSettings
- [ ] Create AuthService
- [ ] Create UserContextMiddleware
- [ ] Create AuthEndpoints
- [ ] Configure auth in Program.cs
- [ ] Seed admin user
- [ ] Test login flow

### Phase 4 — UserId on Entities

- [ ] Add UserId to FileEntity, PokemonEntity, TagEntity
- [ ] Configure FK relationships in DbContext
- [ ] Create migration
- [ ] Write data migration (assign existing to admin)
- [ ] Apply migration on test DB

### Phase 5 — Endpoint Protection

- [ ] Add RequireAuthorization to all groups
- [ ] Add user filtering to all queries
- [ ] Add user assignment to all creates
- [ ] Add ownership check to all updates/deletes
- [ ] Test cross-user data isolation

### Phase 6 — Service Extraction

- [ ] Create PokemonService + interface
- [ ] Create TagService + interface
- [ ] Create ImportService + interface
- [ ] Refactor all endpoints to use services
- [ ] Register services in DI
- [ ] Compare responses before/after

### Phase 7 — Injectable Domain Services

- [ ] Create interfaces for query and sorting services
- [ ] Convert from static to instance
- [ ] Register in DI
- [ ] Update consumers

### Phase 8 — User-Scoped Storage

- [ ] Implement user directories in StorageConfiguration
- [ ] Update FileStorageService for user scope
- [ ] Rename FileWatcherService → FileScanService
- [ ] Create IFileScanService interface
- [ ] Migrate existing files to user directories
- [ ] Test scan per user

### Phase 9 — Mapping Layer

- [ ] Create PokemonMapper
- [ ] Extract mapping from DTOs
- [ ] Extract mapping from endpoints
- [ ] Verify DTO output unchanged

### Phase 10 — Hybrid Tags

- [ ] Seed system tags
- [ ] Update tag queries for hybrid model
- [ ] Update tag uniqueness index (per user)
- [ ] Protect system tags from non-admin edits
- [ ] Test both user types

### Phase 11 — Polish

- [ ] Admin role enforcement
- [ ] Debug endpoint protection
- [ ] File name standardization
- [ ] Input validation
- [ ] Rate limiting
- [ ] Type definitions update
- [ ] README update

---

## 11. Migration Strategy

### Adding Migrations

```bash
# From BeastVault.Api/ directory
dotnet ef migrations add <MigrationName>
dotnet ef database update
```

### Critical Migration: AddUserOwnership

This migration adds `UserId` to FileEntity, PokemonEntity, and TagEntity. It must:

1. Create the `Users` table first
2. Seed the default admin user (Id=1)
3. Add `UserId` column to Files, Pokemon, Tags
4. Set all existing records to `UserId = 1`
5. Add FK constraints
6. Update indexes (add UserId to unique constraints where needed)

```sql
-- Step 1: Create Users table
CREATE TABLE Users (
    Id INTEGER PRIMARY KEY AUTOINCREMENT,
    Username TEXT NOT NULL,
    PasswordHash TEXT,
    Role INTEGER NOT NULL DEFAULT 0,
    IsDefault INTEGER NOT NULL DEFAULT 0
);

-- Step 2: Seed admin user
INSERT INTO Users (Username, PasswordHash, Role, IsDefault) VALUES ('Admin', NULL, 1, 1);

-- Step 3-4: Add UserId columns with default value
ALTER TABLE Files ADD COLUMN UserId INTEGER NOT NULL DEFAULT 1;
ALTER TABLE Pokemon ADD COLUMN UserId INTEGER NOT NULL DEFAULT 1;
ALTER TABLE Tags ADD COLUMN UserId INTEGER;

-- Step 5: SQLite doesn't support ADD CONSTRAINT, handled by EF Core table rebuild
```

### SQLite Limitations

- No `ALTER TABLE ... ADD CONSTRAINT` — EF Core handles via table rebuild
- No concurrent writes — single-writer, multiple-reader
- `PRAGMA foreign_keys = ON` must be set per connection

### Backup Before Migration

Always backup `beastvault.db` before running migrations that modify existing tables:

```bash
cp beastvault.db beastvault.db.backup
```

---

## 12. Frontend Compatibility Notes

### Frontend Architecture

- **Framework**: Vue 3 with TypeScript
- **State**: Likely Pinia or Vue reactive stores
- **HTTP**: customFetch or axios with interceptors
- **Desktop**: Electron wrapping the Vue app

### Key API Contracts

| Frontend Expects                                 | Backend Provides     | Breaking? |
| ------------------------------------------------ | -------------------- | --------- |
| `GET /pokemon` returns `{ Items, Total, Stats }` | Same                 | No        |
| `GET /pokemon/{id}` returns PokemonDetailDto     | Same                 | No        |
| `POST /import` accepts multipart files           | Same                 | No        |
| `GET /tags` returns TagDto[]                     | Same                 | No        |
| All endpoints are unauthenticated                | **Will require JWT** | **Yes**   |

### Breaking Changes (Phase 3)

When JWT auth is added, all existing frontend calls will receive 401. The frontend must:

1. Add login page/component
2. Store JWT token (Pinia persist or localStorage)
3. Add `Authorization: Bearer <token>` header to all API calls
4. Handle 401 responses (redirect to login)
5. For Electron: same flow, token stored in app state

### Naming Conventions

Keep all API routes as-is. Do not add `/api/` prefix unless frontend already expects it.

---

## 13. Testing Strategy

### Priority Tests

| Area           | What to Test                                    | Why                        |
| -------------- | ----------------------------------------------- | -------------------------- |
| Auth           | Login with valid/invalid credentials            | Core security              |
| Auth           | Token generation and validation                 | JWT integrity              |
| Auth           | Protected endpoint without token returns 401    | Access control             |
| Data isolation | User A cannot see User B's Pokémon              | Multi-user security        |
| Data isolation | User A cannot delete User B's data              | Ownership enforcement      |
| Import         | File import assigns correct UserId              | Data ownership             |
| Import         | Duplicate detection per user                    | Same file, different users |
| Scan           | Scan only reads from user's folder              | Directory isolation        |
| Tags           | System tags visible to all users                | Hybrid tag model           |
| Tags           | User tags isolated per user                     | Tag ownership              |
| Migration      | Existing data assigned to admin after migration | Data integrity             |

### Where Tests Should Live

```
BeastVault.Api.Tests/
├── Unit/
│   ├── Services/
│   │   ├── AuthServiceTests.cs
│   │   ├── PokemonServiceTests.cs
│   │   └── TagServiceTests.cs
│   └── Specifications/
│       └── PokemonSpecificationTests.cs
└── Integration/
    ├── AuthEndpointTests.cs
    ├── PokemonEndpointTests.cs
    └── DataIsolationTests.cs
```

### How to Test EF Core + SQLite

Use `Microsoft.EntityFrameworkCore.InMemory` or SQLite in-memory for unit tests:

```csharp
var options = new DbContextOptionsBuilder<AppDbContext>()
    .UseSqlite("DataSource=:memory:")
    .Options;
```

---

## 14. Summary Assessment

| Dimension           | Score  | Notes                                                                            |
| ------------------- | ------ | -------------------------------------------------------------------------------- |
| **Security**        | 2/10   | No auth, no user model, destructive endpoints unprotected                        |
| **Architecture**    | 7/10   | DDD patterns (Specifications, ValueObjects) are excellent; missing service layer |
| **Code Quality**    | 5/10   | Empty files, duplicate code, JS naming, 530-line endpoints                       |
| **Database Design** | 6/10   | Clean schema but missing user FK; RawBlob concern                                |
| **API Design**      | 7/10   | Good REST patterns, Swagger docs, meaningful error responses                     |
| **Domain Modeling** | 8/10   | Best in portfolio: Specification pattern, composite specs, value objects         |
| **Testability**     | 3/10   | Static services + inline DB logic = untestable                                   |
| **Overall**         | 5.5/10 | Strong domain foundation, critical security and isolation gaps                   |

### Do First

1. Add JWT auth + User entity (Phases 3-4)
2. Protect all endpoints (Phase 5)
3. Delete dead code (Phase 0)

### Do Not Do

- Don't remove PKHeX.Core or change the parsing logic
- Don't change API routes (frontend compatibility)
- Don't add complexity to Specifications (they're well designed)
- Don't convert pure static helpers to injectable (PkHexStringService, PokemonFormService, PokemonGameInfoService, PokemonComparisonService)

### Can Wait

- Phase 9 (mapping layer) — nice-to-have, not blocking
- Phase 11 (polish) — incremental improvement

### What Would Make It Look Professional

- Authenticated API with proper user isolation
- Per-user file directories
- Service layer with interfaces between endpoints and database
- Clean Program.cs with no inline endpoints
- One entity per file, grouped DTOs
