# Beast Vault

A local-first **Beast Vault** to import, store, compare, and browse Pokémon data from `.pk*` files across all game generations (Gen 1 → Gen 9, plus supported spin-offs).

This tool works only with legitimately obtained backup files and is **not an official Pokémon product**. All Pokémon references are for descriptive purposes only and are trademarks of their respective owners.

Uses **PKHeX.Core** for parsing and name resolution, **SQLite** for storage, and exposes a clean **REST API**.

A companion React/TypeScript UI is available at [BeastVault.Front](https://github.com/David-H-Afonso/BeastVault.Front) to be used with an UI.

## Legal Disclaimer

**Beast Vault** is an independent, non-commercial, open-source project for personal use. It is **NOT** affiliated, associated, endorsed, sponsored, or approved by Nintendo, The Pokémon Company, Game Freak, Creatures Inc., or any of their subsidiaries, affiliates, or partners. All trademarks, service marks, trade names, product names, and trade dress mentioned or referenced within this project are the property of their respective owners.

This software is **not an official Pokémon product** and does not attempt to simulate, emulate, reproduce, replace, or provide any product, service, or functionality of official Pokémon games, services, or hardware. Any similarity to proprietary formats, terminology, or concepts is purely for descriptive purposes and does not imply endorsement or association.

**Beast Vault** is intended solely for lawful, personal-use management and storage of legitimately obtained Pokémon data files (e.g., `.pk*` formats) that belong to the user. The project does **NOT**:

- Provide or facilitate the creation, modification, or acquisition of Pokémon.
- Distribute or include copyrighted game assets, code, or data belonging to Nintendo or The Pokémon Company.
- Encourage, promote, or support any activity that violates applicable laws, the Pokémon games’ End User License Agreements (EULAs), or the terms of service of official products or platforms.

Use of this software is entirely at the user’s own risk. The authors and contributors disclaim any and all responsibility and liability for misuse, infringement, or violation of third-party rights. By using this software, the user agrees to comply with all applicable laws, regulations, and contractual obligations.

---

## TL;DR

- **Import** one or many `.pk*` files (e.g., `.pk9`) and keep the **original file** (unmodified, for full fidelity).
- **Store** full metadata locally (species, forms, OT/TID/SID, IV/EV, moves, ball, language, origin, Tera Type, ribbons, marks, etc.).
- **Compare** any two Pokémon and see all differences (e.g., after trading or editing).
- **Export** a **Pokémon Showdown** set (with names from PKHeX.Core).
- **Stack:** .NET 9 + ASP.NET Core + EF Core + SQLite + PKHeX.Core + Swagger.
- **Status:** Full PK9 support, PKHeX.Core GameStrings migration, original file preservation, Pokémon comparison endpoint, and clean codebase (no custom enums).

---

## Clone & run locally

### 1) Prerequisites

- [Git](https://git-scm.com/downloads)
- [.NET 9 SDK](https://dotnet.microsoft.com/en-us/download/dotnet/9.0)
- (Optional) [Visual Studio Code](https://code.visualstudio.com/) or any IDE

### 2) Clone

```bash
git clone https://github.com/username/beast-vault.git
cd beast-vault/BeastVault.Api
```

### 3) Restore & build

```bash
dotnet restore
dotnet build
```

### 4) Create the database

```bash
dotnet tool install --global dotnet-ef
dotnet ef database update
```

### 5) Run the API

**Option A — HTTPS (recommended)**

1. Trust the dev cert (one-time):

```bash
dotnet dev-certs https --trust
```

2. Use the `https` launch profile:

```bash
dotnet run --launch-profile https
```

3. Open Swagger:

```
https://localhost:7178/swagger
```

**Option B — HTTP only (quick start)**  
Comment out `app.UseHttpsRedirection();` in `Program.cs` and run:

```bash
dotnet run --launch-profile http
```

Swagger:

```
http://localhost:5111/swagger
```

---

## Project structure

```
BeastVault.Api/
├── Contracts/           # DTOs
├── Domain/              # Entities (PokemonEntity, StatsEntity, etc.)
├── Endpoints/           # Minimal API endpoints (Import, Health, Pokemon, Compare)
├── Extensions/          # WebApplication extensions
├── Infrastructure/      # DbContext, services, PKHeX integration, helpers
│   ├── AppDbContext.cs
│   ├── Services/
│   │   ├── FileStorageService.cs
│   │   ├── PkhexCoreParser.cs
│   │   ├── PkHexStringService.cs
│   │   └── PokemonComparisonService.cs
│   └── Mappings/
├── Migrations/          # EF Core migrations
├── Properties/          # Launch settings
├── ReferenceData/       # (Removed) Old enums/data, now replaced by PKHeX.Core
├── appsettings.json
├── Program.cs
├── BeastVault.Api.csproj
├── README.md
├── docker-compose.yml
├── dockerfile
```

## Docker Deployment

BeastVault can be run as a Docker container, making it easy to deploy on a server. The application automatically detects if it's running in a Docker environment and uses appropriate paths for storage.

### Running with Docker

1. Build and run using Docker Compose:

```bash
docker-compose up -d
```

2. The API will be accessible at `http://your-server:8080`

### Docker Storage Paths

When running in Docker, the application uses the following paths:

- **Database:** `/app/data/beastvault.db` (persisted as a Docker volume)
- **Pokemon Files:** `/app/pokemon/` (persisted as a Docker volume)
- **Backup Files:** `/app/pokemon/backup/` (included in the Pokemon volume)

### Docker Volumes

Docker Compose creates two named volumes:

- `beastvault-data`: For the database
- `beastvault-pokemon`: For Pokemon files

These volumes ensure your data persists even if the container is removed or recreated.

**Data Storage Location:**
The application uses two separate directories for optimal security and usability:

**Private Data (Hidden from user):**

- **Database:** `%LocalAppData%\BeastVault\storage\beastvault.db`

**Public Data (User accessible):**

- **Pokemon Files:** `%UserProfile%\Documents\BeastVault\`

**📁 Automatic File Detection:**
The application automatically monitors the Documents folder and:

- Scans for new Pokemon files on startup
- Supports drag-and-drop workflow (just copy files to Documents/BeastVault)
- Automatically imports new files and skips duplicates
- Works with subdirectories

**Supported file formats:**

- `.pk1`, `.pk2`, `.pk3`, `.pk4`, `.pk5`, `.pk6`, `.pk7`, `.pk8`, `.pk9`
- `.pb7`, `.pb8` (Pokemon Box files)
- `.ek1` - `.ek9` (Encrypted files)
- `.ekx` (Encrypted batch)

This ensures:

- Database is secure and hidden from casual user access
- Pokemon files are easily accessible for backup/sharing
- Clean separation between application data and user data
- Multiple users can run the application independently
- "Dump" folder functionality for easy file management

You can override these paths in `appsettings.json` if needed:

```json
{
  "Vault": {
    "BasePath": "C:\\CustomPath\\backup"
  },
  "ConnectionStrings": {
    "Default": "Data Source=C:\\CustomPath\\beastvault.db"
  }
}
```

- `Contracts/`: DTOs for API requests/responses.
- `Domain/`: Core entities for Pokémon, stats, moves, etc.
- `Endpoints/`: Minimal API endpoints (import, get, compare, health).
- `Infrastructure/Services/`: Business logic, file storage, PKHeX integration, helpers (including name resolution and comparison).
- `ReferenceData/`: **Removed**. All enums and static data now use PKHeX.Core GameStrings.

---

## Tech stack

- **Backend:**

  - .NET 9
  - ASP.NET Core Minimal API
  - Entity Framework Core
  - SQLite (local storage)
  - PKHeX.Core (Pokémon parsing, name resolution, GameStrings)
  - Swashbuckle / Swagger UI

- **Other:**
  - Dependency Injection
  - HealthChecks
  - Modular endpoint & service structure
  - Multi-generation compatibility
  - Original file preservation (vault)
  - Pokémon comparison tooling

---

## Goals

### General

Create a **personal Beast Vault** that:

- Imports Pokémon from `.pk*` files obtained legally.
- Displays detailed information in a user-friendly format.
- Keeps a secure local record of the collection (including original files).

### Technical

- Full compatibility with `.pk*` from Gen 1 to Gen 9 + supported spin-offs.
- Local storage of Pokémon data, metadata & original file (unmodified).
- Advanced parsing and name resolution with PKHeX.Core (no custom enums).
- Showdown export (with accurate names).
- Pokémon comparison endpoint for difference analysis.
- Modular architecture for future UI integration.
- REST API with Swagger documentation.

---

## Current progress

- Full PK9 support: All fields parsed and stored, including new Gen 9 data.
- PKHeX.Core GameStrings: All names (species, moves, items, etc.) resolved via PKHeX.Core, no custom enums.
- Original file preservation: Every import stores the unmodified file in the vault.
- Pokémon comparison: `/pokemon/compare/{id1}/{id2}` endpoint to analyze all differences between two Pokémon (e.g., after trading or editing).
- Showdown export: `/pokemon/{id}/showdown` (GET) returns a Showdown set with accurate names.
- Clean codebase: ReferenceData and all custom enums removed.
- Minimal API endpoints: Import, get, compare, health.
- Swagger UI for testing & docs.
- Launch profiles for HTTP + HTTPS.

---

## Next steps

- [ ] Support ribbons, marks, contest stats (in progress).
- [ ] Return sprites/images in API responses.
- [ ] Implement advanced filtering/search.
- [ ] Build React/TypeScript UI.

---

## License

This project is licensed under the terms of the [GNU General Public License v3.0 (GPL-3.0)](https://www.gnu.org/licenses/gpl-3.0.html).

### Third-party dependencies

- **PKHeX.Core**: [MIT License](https://github.com/kwsch/PKHeX/blob/master/LICENSE.txt)
- **SQLite**: [Public Domain](https://www.sqlite.org/copyright.html)
- **Swashbuckle/Swagger UI**: [MIT License](https://github.com/domaindrivendev/Swashbuckle.AspNetCore/blob/master/LICENSE)
- **.NET 9 SDK & ASP.NET Core**: [MIT License](https://github.com/dotnet/runtime/blob/main/LICENSE.TXT)

See the `LICENSE.md` file for full license texts and details.
