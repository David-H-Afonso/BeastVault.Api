# Beast Vault API

A modern, local-first Pokémon collection management system that imports, stores, analyzes, and organizes Pokémon data from `.pk*` files across all game generations. Built with .NET 9 and designed for personal use with legitimately obtained Pokémon files.

## Tech Stack

- **.NET 9** - Modern C# runtime and framework
- **ASP.NET Core** - Web API with minimal endpoints
- **Entity Framework Core** - ORM with SQLite database
- **PKHeX.Core** - Official Pokémon file parsing and validation
- **SQLite** - Local database for fast, reliable storage
- **Swagger/OpenAPI** - Interactive API documentation
- **Docker** - Containerized deployment support

## Legal Disclaimer

**Beast Vault** is an independent, non-commercial, open-source project for personal use. It is **NOT** affiliated, associated, endorsed, sponsored, or approved by Nintendo, The Pokémon Company, Game Freak, Creatures Inc., or any of their subsidiaries, affiliates, or partners. All trademarks, service marks, trade names, product names, and trade dress mentioned or referenced within this project are the property of their respective owners.

This software is **not an official Pokémon product** and does not attempt to simulate, emulate, reproduce, replace, or provide any product, service, or functionality of official Pokémon games, services, or hardware. Any similarity to proprietary formats, terminology, or concepts is purely for descriptive purposes and does not imply endorsement or association.

**Beast Vault** is intended solely for lawful, personal-use management and storage of legitimately obtained Pokémon data files (e.g., `.pk*` formats) that belong to the user. The project does **NOT**:

- Provide or facilitate the creation, modification, or acquisition of Pokémon.
- Distribute or include copyrighted game assets, code, or data belonging to Nintendo or The Pokémon Company.
- Encourage, promote, or support any activity that violates applicable laws, the Pokémon games' End User License Agreements (EULAs), or the terms of service of official products or platforms.

Use of this software is entirely at the user's own risk. The authors and contributors disclaim any and all responsibility and liability for misuse, infringement, or violation of third-party rights. By using this software, the user agrees to comply with all applicable laws, regulations, and contractual obligations.

## TL;DR

- **Import & Store**: Import `.pk*` files from all generations (Gen 1-9) while preserving original files
- **Rich Metadata**: Store complete Pokémon data - stats, moves, abilities, ribbons, marks, forms, and more
- **Advanced Search**: Query and filter your collection with sophisticated search capabilities
- **Tagging System**: Organize Pokémon with custom tags and categories
- **Showdown Export**: Generate competitive-ready Pokémon Showdown sets
- **Comparison Tools**: Compare any two Pokémon to see differences after trades or modifications
- **REST API**: Full-featured API with Swagger documentation
- **Docker Ready**: Easy deployment with Docker and Docker Compose
- **Local-First**: All data stays on your machine - no external dependencies

## Local Development

### Prerequisites

- [.NET 9 SDK](https://dotnet.microsoft.com/download/dotnet/9.0)
- [Git](https://git-scm.com/downloads)
- (Optional) [Visual Studio Code](https://code.visualstudio.com/) or any IDE

### Installation

1. **Clone the repository**

   ```bash
   git clone https://github.com/David-H-Afonso/BeastVault.Api.git
   cd BeastVault.Api
   ```

2. **Restore dependencies**

   ```bash
   dotnet restore
   ```

3. **Install Entity Framework tools**

   ```bash
   dotnet tool install --global dotnet-ef
   ```

4. **Setup database**
   ```bash
   dotnet ef database update
   ```

### Running Locally

#### Option 1: HTTPS (Recommended)

1. **Trust development certificate** (one-time setup)

   ```bash
   dotnet dev-certs https --trust
   ```

2. **Run the application**

   ```bash
   dotnet run
   ```

3. **Access the API**
   - Swagger UI: https://localhost:7178/swagger
   - API Base: https://localhost:7178

#### Option 2: HTTP Only

```bash
dotnet run --launch-profile http
```

- Swagger UI: http://localhost:5111/swagger
- API Base: http://localhost:5111

### Building for Production

```bash
# Build optimized release
dotnet build -c Release

# Publish self-contained application
dotnet publish -c Release -o ./publish
```

## Project Structure

```
BeastVault.Api/
├── Contracts/                    # Data Transfer Objects (DTOs)
│   ├── AdvancedPokemonQuery.cs  # Advanced query parameters
│   └── Dtos.cs                  # API response models
├── Domain/                      # Core business logic
│   ├── Entities/                # Database entities
│   │   └── index.cs            # Pokemon, Stats, Moves, Tags entities
│   ├── Services/               # Domain services
│   │   ├── PkheXMappingService.cs
│   │   ├── PokemonQueryService.cs
│   │   └── PokemonSortingService.cs
│   ├── Specifications/         # Query specifications
│   │   ├── IPokemonSpecification.cs
│   │   └── PokemonSpecifications.cs
│   └── ValueObjects/           # Value objects
│       ├── PokemonQueryOptions.cs
│       └── ShowdownExport.cs
├── Endpoints/                  # API endpoints (Minimal APIs)
│   ├── ConfigurationEndpoints.cs
│   ├── FilesEndpoints.cs
│   ├── HealthEndpoints.cs
│   ├── ImportEndpoints.cs
│   ├── MaintenanceEndpoints.cs
│   ├── PokemonEndpoints.cs
│   ├── ScanEndpoints.cs
│   └── TagEndpoints.cs
├── Extensions/                 # Extension methods
│   └── WebApplicationExtension.cs
├── Infrastructure/             # Data access and external services
│   ├── Configuration/
│   │   └── StorageConfiguration.cs
│   ├── Mappings/
│   │   └── PkhexMappings.cs
│   ├── Services/
│   │   ├── FileStorageService.cs
│   │   ├── FileWatcherService.cs
│   │   ├── PkhexCoreParser.cs
│   │   ├── PkHexStringService.cs
│   │   ├── PokemonComparisonService.cs
│   │   ├── PokemonFormService.cs
│   │   └── PokemonGameInfoService.cs
│   ├── AppDbContext.cs         # Entity Framework context
│   └── EnvironmentUtils.cs
├── Migrations/                 # EF Core database migrations
├── Properties/
│   └── launchSettings.json     # Development profiles
├── appsettings.json           # Configuration
├── docker-compose.yml         # Docker Compose setup
├── Dockerfile                 # Docker build instructions
├── Program.cs                 # Application entry point
└── BeastVault.Api.csproj     # Project file
```

## Acknowledgments

This project wouldn't be possible without these amazing open-source projects and communities:

- **[PKHeX](https://github.com/kwsch/PKHeX)** - The gold standard for Pokémon file parsing and validation
- **[PokéAPI](https://github.com/PokeAPI/pokeapi)** - Comprehensive Pokémon data and API
- **[pokemon-sprites (bamq)](https://github.com/bamq/pokemon-sprites)** - High-quality Pokémon sprite collections
- **[pokesprite (msikma)](https://github.com/msikma/pokesprite)** - Pokémon icon sprites and tools

Special thanks to the maintainers and contributors of these projects for their dedication to the Pokémon development community.

## API Integration

### Key Endpoints

- **Health Check**: `GET /health` - API status and system health
- **Import Pokémon**: `POST /import` - Import .pk\* files
- **Get Pokémon**: `GET /pokemon` - Retrieve Pokémon with filtering and pagination
- **Pokémon Details**: `GET /pokemon/{id}` - Get detailed Pokémon information
- **Compare Pokémon**: `GET /pokemon/compare/{id1}/{id2}` - Compare two Pokémon
- **Showdown Export**: `GET /pokemon/{id}/showdown` - Generate Showdown format
- **Tags Management**: `GET/POST/PUT/DELETE /tags` - Manage custom tags
- **File Operations**: `GET /files` - Browse and manage Pokémon files
- **Auto Scan**: `POST /scan` - Automatically scan for new files

### Supported File Formats

- **Core formats**: `.pk1`, `.pk2`, `.pk3`, `.pk4`, `.pk5`, `.pk6`, `.pk7`, `.pk8`, `.pk9`

#### Should work - still in testing

- **Box formats**: `.pb7`, `.pb8`
- **Encrypted**: `.ek1` through `.ek9`, `.ekx`

### Data Storage

**Local Development:**

- **Database**: `%LocalAppData%\BeastVault\storage\beastvault.db`
- **Pokémon Files**: `%UserProfile%\Documents\BeastVault\`

**Docker Deployment:**

- **Database**: `/app/data/beastvault.db` (persisted volume)
- **Pokémon Files**: `/app/pokemon/` (persisted volume)

## Docker Deployment

### Quick Start with Docker Compose

```bash
# Clone and navigate to project
git clone https://github.com/David-H-Afonso/BeastVault.Api.git
cd BeastVault.Api

# Start services
docker-compose up -d

# Access the application
# API: http://localhost:8080
# Swagger: http://localhost:8080/swagger
```

### Docker Compose Configuration

```yaml
# docker-compose.yml
version: "3.8"
services:
  beastvault-api:
    build: .
    ports:
      - "8080:8080"
    volumes:
      - beastvault-data:/app/data # Database persistence
      - beastvault-pokemon:/app/pokemon # Pokémon files persistence
    environment:
      - ASPNETCORE_ENVIRONMENT=Production
      - ASPNETCORE_URLS=http://+:8080
      - BEASTVAULT_DB_PATH=/app/data/beastvault.db
      - BEASTVAULT_POKEMON_PATH=/app/pokemon

volumes:
  beastvault-data:
  beastvault-pokemon:
```

### Manual Docker Build

```bash
# Build image
docker build -t beastvault-api .

# Run container
docker run -d \
  --name beastvault \
  -p 8080:8080 \
  -v beastvault-data:/app/data \
  -v beastvault-pokemon:/app/pokemon \
  beastvault-api
```

## Features

### Core Functionality

- **Multi-Generation Support**: Full compatibility with Gen 1-9 Pokémon files
- **Original File Preservation**: Keep unmodified `.pk*` files for full fidelity
- **Comprehensive Metadata**: Store all Pokémon data including hidden properties
- **Advanced Querying**: Search by species, stats, moves, abilities, and more
- **Tagging System**: Organize your collection with custom categories
- **Automatic File Detection**: Monitor folders for new Pokémon files
- **Showdown Integration**: Export competitive-ready sets
- **Comparison Tools**: Detailed diff analysis between Pokémon

### API Features

- **RESTful Design**: Clean, intuitive API endpoints
- **Interactive Documentation**: Built-in Swagger UI
- **Rich Query Support**: Advanced filtering and pagination
- **CORS Enabled**: Ready for web frontend integration
- **Health Monitoring**: System status and diagnostics
- **Local-First**: No external API dependencies

### Technical Features

- **High Performance**: SQLite with optimized queries
- **Container Ready**: Docker and Docker Compose support
- **Database Migrations**: Automatic schema updates
- **Comprehensive Logging**: Detailed application logs
- **Error Handling**: Robust error responses and validation
- **Type Safety**: Full .NET type system benefits

## 🤝 Contributing

We welcome contributions to Beast Vault! Please feel free to:

1. **Fork the repository**
2. **Create a feature branch** (`git checkout -b feature/amazing-feature`)
3. **Commit your changes** (`git commit -m 'Add amazing feature'`)
4. **Push to the branch** (`git push origin feature/amazing-feature`)
5. **Open a Pull Request**

### Development Setup

1. Fork the repository
2. Create a feature branch: `git checkout -b feature/amazing-feature`
3. Commit changes: `git commit -m 'Add amazing feature'`
4. Push to branch: `git push origin feature/amazing-feature`
5. Open a Pull Request

### Reporting Issues

Please use the [GitHub Issues](https://github.com/David-H-Afonso/BeastVault.Api/issues) page to report bugs or request features.

## License

This project is licensed under the **GNU General Public License v3.0 (GPL-3.0)**.

### Third-Party Licenses

- **PKHeX.Core**: [MIT License](https://github.com/kwsch/PKHeX/blob/master/LICENSE)
- **PokéAPI**: [BSD License](https://github.com/PokeAPI/pokeapi/blob/master/LICENSE.rst)
- **pokemon-sprites (bamq)**: [MIT License](https://github.com/bamq/pokemon-sprites/blob/main/LICENSE)
- **pokesprite (msikma)**: [MIT License](https://github.com/msikma/pokesprite/blob/master/LICENSE)
- **SQLite**: [Public Domain](https://www.sqlite.org/copyright.html)
- **Swashbuckle/Swagger UI**: [MIT License](https://github.com/domaindrivendev/Swashbuckle.AspNetCore/blob/master/LICENSE)
- **.NET 9 SDK & ASP.NET Core**: [MIT License](https://github.com/dotnet/runtime/blob/main/LICENSE.TXT)

See [LICENSE.md](LICENSE.md) for complete license information.

---

**Built with ❤️ for Pokémon collectors worldwide**
