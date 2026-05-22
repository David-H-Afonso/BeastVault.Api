# Backend Architecture Standard

This document defines the reusable backend architecture standard for small-to-medium ASP.NET Core Web APIs using Entity Framework Core and SQLite. It can be copied into any similar project as context for maintaining architectural consistency.

---

## 1. Purpose

This standard provides a repeatable architecture for C# + SQLite backend APIs. It is designed to be applied consistently across multiple projects so that each one follows the same conventions, folder structure, naming patterns, and quality expectations.

It is not a theoretical exercise — it is grounded in real production experience building self-hosted media tracking applications.

---

## 2. Target Project Type

This standard applies to projects that share these characteristics:

- **Personal or professional portfolio applications** — built to demonstrate real engineering ability
- **Self-hosted applications** — deployed on Docker, CasaOS, or similar home-server platforms
- **React frontend + ASP.NET Core API backend** — SPA frontend consuming a REST API
- **SQLite database** — lightweight, file-based, portable
- **Long-term maintainable** — expected to evolve over months or years
- **Recruiter-friendly** — the codebase should look professional enough that a technical reviewer would be impressed
- **Docker-ready** — configurable through environment variables, no hardcoded paths

---

## 3. Architecture Style

### Pragmatic Layered Architecture

This is the preferred style. It provides clear separation of concerns without the overhead of full Clean Architecture.

**Why this style:**

- Cleaner than random folders with no structure
- Simpler than multi-project Clean Architecture with `Domain`, `Application`, `Infrastructure`, `Presentation` projects
- Good enough for projects with 10-50 entities and 50-150 endpoints
- Easy to apply consistently across projects
- Easy to explain in an interview: _"I use a pragmatic layered architecture where controllers are thin, services own business logic, and EF Core handles persistence."_
- Works well with EF Core and SQLite (no need for repository abstraction)
- Scales up to medium complexity without becoming restrictive

**What it is NOT:**

- Not CQRS or event sourcing
- Not hexagonal architecture
- Not a monolith that throws everything in one folder
- Not over-engineered "astronaut architecture" with abstractions for every dependency

---

## 4. Standard Folder Structure

```
ProjectName.Api/
  Controllers/
  Contracts/
    Requests/
    Responses/
    External/
  Domain/
    Entities/
    Enums/
  Application/
    Interfaces/
    Services/
    Mapping/
  Infrastructure/
    Persistence/
      AppDbContext.cs
      DatabaseStartupHelper.cs
      Configurations/
      Migrations/
    ExternalServices/
  Configuration/
  Common/
  Middleware/
  Program.cs
  appsettings.json
  appsettings.Development.json
```

### Folder Responsibilities

| Folder                             | What belongs here                                                           | What does NOT belong here                         | Example files                                           |
| ---------------------------------- | --------------------------------------------------------------------------- | ------------------------------------------------- | ------------------------------------------------------- |
| `Controllers/`                     | Thin API controllers that receive requests, call services, return responses | Business logic, complex EF queries, mapping logic | `GamesController.cs`, `AuthController.cs`               |
| `Contracts/Requests/`              | DTOs representing what the client sends                                     | Entity classes, internal models                   | `CreateGameRequest.cs`, `UpdateGameRequest.cs`          |
| `Contracts/Responses/`             | DTOs representing what the API returns                                      | EF entities, database implementation details      | `GameResponse.cs`, `GameDetailResponse.cs`              |
| `Contracts/External/`              | Response models for third-party APIs                                        | Internal DTOs, domain entities                    | `TmdbModels.cs`, `SteamModels.cs`                       |
| `Domain/Entities/`                 | EF Core entity classes representing database tables                         | DTOs, view models, frontend-specific fields       | `Game.cs`, `User.cs`, `Replay.cs`                       |
| `Domain/Enums/`                    | Enum types used across the application                                      | Constants that should be configuration            | `GameStatus.cs`, `ReplayType.cs`                        |
| `Application/Interfaces/`          | Service interfaces for dependency injection                                 | Implementation details                            | `IGameService.cs`, `IImportService.cs`                  |
| `Application/Services/`            | Service implementations containing business logic                           | HTTP concerns, controller attributes              | `GameService.cs`, `ImportService.cs`                    |
| `Application/Mapping/`             | Extension methods for entity↔DTO mapping                                    | Complex transformation logic (put in services)    | `GameMappingExtensions.cs`                              |
| `Infrastructure/Persistence/`      | DbContext, entity configurations, migrations, startup helpers               | Business logic, DTOs                              | `AppDbContext.cs`, `GameConfiguration.cs`               |
| `Infrastructure/ExternalServices/` | HTTP clients for external APIs                                              | Internal service logic                            | `SteamApiClient.cs`, `TmdbApiClient.cs`                 |
| `Configuration/`                   | Strongly-typed Options classes, DI extension methods                        | Business logic, entity definitions                | `DatabaseSettings.cs`, `ServiceCollectionExtensions.cs` |
| `Common/`                          | Shared types: pagination, result models, extensions                         | Business logic, domain-specific types             | `PagedResult.cs`, `QueryParameters.cs`                  |
| `Middleware/`                      | ASP.NET Core middleware                                                     | Business logic                                    | `ErrorHandlingMiddleware.cs`                            |

---

## 5. Dependency Direction

```
Controllers → Application (Services/Interfaces) → Infrastructure (Persistence/External) → SQLite
                                                                                        → External APIs

Contracts are used at the API boundary (Controllers ↔ Frontend).
Domain entities represent stored data (Services ↔ DbContext).
Program.cs only wires dependencies.
```

### What to Avoid

- **Controllers directly doing business logic** — controllers should be 10-50 lines per action
- **Frontend-specific logic in entities** — entities model the database, not the UI
- **Random helper classes everywhere** — use focused services or well-named extension methods
- **Hardcoded configuration** — use Options pattern for grouped settings, environment variables for deployment
- **Massive Program.cs** — extract registration logic into extension methods
- **Massive DbContext** — extract entity configuration into `IEntityTypeConfiguration<T>` classes
- **Exposing EF entities as API responses** — use DTOs to protect the contract and avoid leaking database structure

---

## 6. Controller Standards

Controllers should be thin HTTP boundary handlers.

### Good Controller Characteristics

- Receives HTTP requests with typed parameters
- Calls one or two service methods
- Returns typed responses with appropriate HTTP status codes
- Does not contain EF Core queries
- Does not contain business validation
- Does not know about SQLite
- Uses `[ApiController]` for automatic model validation
- Uses constructor injection for services

### Example

```csharp
[ApiController]
[Route("api/games")]
public sealed class GamesController(IGameService gameService) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<PagedResult<GameListResponse>>> GetGames(
        [FromQuery] GameQueryParameters query)
    {
        var result = await gameService.GetGamesAsync(query);
        return Ok(result);
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<GameDetailResponse>> GetById(int id)
    {
        var result = await gameService.GetByIdAsync(id);
        if (result is null) return NotFound();
        return Ok(result);
    }

    [HttpPost]
    public async Task<ActionResult<GameResponse>> Create(CreateGameRequest request)
    {
        var result = await gameService.CreateAsync(request);
        return CreatedAtAction(nameof(GetById), new { id = result.Id }, result);
    }
}
```

### Anti-Pattern

```csharp
[HttpPost]
public async Task<IActionResult> Create(CreateGameRequest request)
{
    if (string.IsNullOrEmpty(request.Name)) return BadRequest("Name required");

    var existing = await _context.Games.FirstOrDefaultAsync(g => g.Name == request.Name);
    if (existing != null) return Conflict("Game already exists");

    var entity = new Game { Name = request.Name, Status = GameStatus.Backlog };
    _context.Games.Add(entity);
    await _context.SaveChangesAsync();

    return Ok(new { entity.Id, entity.Name });
}
```

---

## 7. Service Standards

Application services represent use cases and own business logic.

### Service Responsibilities

- Contain business validation and rules
- Coordinate EF Core operations (queries, inserts, updates)
- Map entities to response DTOs
- Return predictable results (typed responses, not `IActionResult`)
- Use `CancellationToken` for async operations
- Do not know about HTTP (no `HttpContext`, no status codes, no `ActionResult`)

### Naming Convention

```
GameService              — Core game CRUD and queries
GameImportService        — Import games from external sources
GameExportService        — Export game data
UserSettingsService      — User preference management
BackupService            — Backup scheduling and execution
SyncService              — Synchronization with external systems
```

### Interface Pattern

Every service should have an interface for testability and DI:

```csharp
public interface IGameService
{
    Task<PagedResult<GameListResponse>> GetGamesAsync(GameQueryParameters query);
    Task<GameDetailResponse?> GetByIdAsync(int id);
    Task<GameResponse> CreateAsync(CreateGameRequest request);
    Task<bool> DeleteAsync(int id);
}
```

### Registration

```csharp
builder.Services.AddScoped<IGameService, GameService>();
```

Use `AddScoped` for services that use `DbContext` (which is scoped by default). Use `AddSingleton` only for stateless services or background workers that create their own scopes.

---

## 8. DTO / Contract Standards

### Naming Convention

```
CreateGameRequest        — What the client sends to create
UpdateGameRequest        — What the client sends to update
GameResponse             — Standard response for a game
GameDetailResponse       — Extended response with relationships
GameListResponse         — Compact response for list views
GameSummaryResponse      — Minimal response for dropdowns/autocomplete
```

### Rules

- **Requests model what the client sends.** They may have validation attributes.
- **Responses model what the API returns.** They should not expose database internals (auto-increment IDs are fine, but internal tracking fields are not).
- **Entities should not be the public API contract.** Even if they look similar today, separating them allows independent evolution.
- **Keep DTO naming consistent.** If series use `SeriesListResponse`, movies should use `MovieListResponse`, not `MovieSummaryDto`.
- **Avoid leaking database implementation details.** Do not expose foreign key IDs unless the client needs them.

### Records vs Classes

Prefer C# records for DTOs — they are immutable by default and generate equality/hash/toString automatically:

```csharp
public record GameResponse(int Id, string Name, string Status, DateOnly? ReleaseDate);

public record CreateGameRequest(string Name, int? Year, string? Platform);
```

For complex DTOs with many optional fields, use classes with init-only properties.

---

## 9. Entity and Domain Standards

### Entity Characteristics

- Match business concepts (one entity per table, unless using TPH/TPT inheritance)
- Use clear property names that reflect the domain
- Use navigation properties for relationships
- Use enums for stable business concepts (statuses, types, categories)
- Include `CreatedAt` and `UpdatedAt` timestamps on all entities
- Do not add frontend-only computed fields
- Do not add validation logic (that belongs in services or request DTOs)

### Naming

- Entity names are singular: `Game`, `User`, `Replay` (not `Games`, `Users`)
- Navigation collections use plural: `public List<Replay> Replays { get; set; }`
- Foreign keys follow the pattern: `UserId`, `GameId`

### Enums

Place enums in `Domain/Enums/` with one file per enum:

```
Domain/Enums/GameStatus.cs
Domain/Enums/ReplayType.cs
Domain/Enums/SyncSource.cs
```

---

## 10. EF Core and SQLite Standards

### DbContext Location

```
Infrastructure/Persistence/AppDbContext.cs
```

### DbSet Naming

Use plural names matching the entity:

```csharp
public DbSet<Game> Games => Set<Game>();
public DbSet<User> Users => Set<User>();
```

### Entity Configuration

Extract configuration into individual classes:

```
Infrastructure/Persistence/Configurations/GameConfiguration.cs
Infrastructure/Persistence/Configurations/UserConfiguration.cs
```

```csharp
public class GameConfiguration : IEntityTypeConfiguration<Game>
{
    public void Configure(EntityTypeBuilder<Game> builder)
    {
        builder.ToTable("game");
        builder.Property(x => x.Name).IsRequired().HasMaxLength(200);
        builder.HasIndex(x => x.Name);
        builder.HasMany(x => x.Replays).WithOne(x => x.Game).OnDelete(DeleteBehavior.Cascade);
    }
}
```

Register all configurations in DbContext:

```csharp
protected override void OnModelCreating(ModelBuilder modelBuilder)
{
    modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
}
```

### Migrations

```bash
# Add a migration
dotnet ef migrations add AddSomeFeature --project ./ProjectName.Api

# Apply migrations
dotnet ef database update --project ./ProjectName.Api

# Check for pending changes
dotnet ef migrations has-pending-model-changes --project ./ProjectName.Api
```

Exact project paths may change per repository.

### SQLite-Specific Rules

- **Connection string:** `Data Source=path/to/database.db`
- **Path configuration:** Use `DatabaseSettings.DatabasePath` with environment variable override
- **Column naming:** Prefer snake_case for SQLite columns (explicit in fluent config)
- **Date/time handling:** Store as TEXT in ISO 8601 format. Use UTC value converters.
- **Decimal precision:** SQLite has limited decimal support — use `HasPrecision()` in configuration
- **Table rebuilds:** SQLite cannot rename or drop columns directly. EF Core handles this with table rebuilds, but verify data preservation.
- **Concurrent writes:** SQLite does not support concurrent writes. Avoid long transactions.
- **Indexes:** Add indexes for frequently queried columns, especially foreign keys and unique constraints

### Seeding

For initial data, use `HasData` in entity configurations:

```csharp
builder.HasData(new GameStatus { Id = 1, Name = "Backlog" });
```

For runtime seed data, use a startup method that checks before inserting.

### Delete Behaviors

- `Cascade` for owned children (delete parent → delete children)
- `SetNull` for optional references (delete parent → null the FK)
- `Restrict` for required references that should prevent deletion

---

## 11. Program.cs Standard

Program.cs should be small and readable — ideally under 80 lines.

### Preferred Pattern

```csharp
var builder = WebApplication.CreateBuilder(args);

builder.Services
    .AddApiConfiguration(builder.Configuration)
    .AddPersistence(builder.Configuration)
    .AddApplicationServices()
    .AddExternalServices()
    .AddApiDocumentation();

var app = builder.Build();

await app.InitializeDatabaseAsync();

app.UseApiPipeline();
app.MapControllers();
app.MapHealthCheck();

app.Run();
```

### Extension Methods

Define extension methods in `Configuration/ServiceCollectionExtensions.cs`:

```csharp
public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddApiConfiguration(
        this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<DatabaseSettings>(configuration.GetSection(DatabaseSettings.SectionName));
        services.Configure<JwtSettings>(configuration.GetSection(JwtSettings.SectionName));
        // ...

        services.AddControllers().AddJsonOptions(options => { /* ... */ });
        services.AddCors(options => { /* ... */ });
        services.AddAuthentication(/* ... */);
        services.AddAuthorization();

        return services;
    }

    public static IServiceCollection AddPersistence(
        this IServiceCollection services, IConfiguration configuration)
    {
        var dbSettings = configuration.GetSection(DatabaseSettings.SectionName).Get<DatabaseSettings>();
        services.AddDbContext<AppDbContext>(options => options.UseSqlite(/* ... */));
        return services;
    }

    public static IServiceCollection AddApplicationServices(this IServiceCollection services)
    {
        services.AddScoped<IGameService, GameService>();
        // ...
        return services;
    }
}
```

### What Does NOT Belong in Program.cs

- Business logic
- Database schema repair SQL
- Long configuration blocks that could be in extension methods
- Static helper methods

---

## 12. Configuration Standard

### Options Pattern

Use strongly-typed Options classes for grouped settings:

```csharp
public class DatabaseSettings
{
    public const string SectionName = "DatabaseSettings";
    public string DatabasePath { get; set; } = "../app.db";
    public bool EnableSensitiveDataLogging { get; set; }
}
```

Register with:

```csharp
builder.Services.Configure<DatabaseSettings>(
    builder.Configuration.GetSection(DatabaseSettings.SectionName));
```

Inject with:

```csharp
public class MyService(IOptions<DatabaseSettings> options)
{
    private readonly DatabaseSettings _settings = options.Value;
}
```

### Environment Variable Overrides

ASP.NET Core natively supports environment variable overrides using `__` as a section separator:

```
DatabaseSettings__DatabasePath=/data/app.db
JwtSettings__SecretKey=my-production-key
```

For Docker/CasaOS, prefer explicit mapping in `Program.cs` or `docker-compose.yml`:

```csharp
builder.Configuration["JwtSettings:SecretKey"] =
    Environment.GetEnvironmentVariable("JWT_SECRET_KEY")
    ?? builder.Configuration["JwtSettings:SecretKey"];
```

### Rules

- Use `appsettings.json` for defaults
- Use `appsettings.Development.json` for development overrides
- Use environment variables for deployment-specific values (secrets, paths, URLs)
- Never commit real secrets to `appsettings.json`
- Keep CasaOS/Docker path compatibility in mind (e.g., `/data/`, `/config/`)
- Default database path should work both locally and in containers

---

## 13. Error Handling Standard

### Global Exception Handler

Use middleware for consistent error responses:

```csharp
public class ErrorHandlingMiddleware(RequestDelegate next, ILogger<ErrorHandlingMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await next(context);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Unhandled exception");
            context.Response.StatusCode = StatusCodes.Status500InternalServerError;
            await context.Response.WriteAsJsonAsync(new
            {
                statusCode = 500,
                message = "An unexpected error occurred."
            });
        }
    }
}
```

### Consistent Status Codes

| Situation      | Status Code    | When                                  |
| -------------- | -------------- | ------------------------------------- |
| Success        | 200 OK         | Returning data                        |
| Created        | 201 Created    | Resource created                      |
| No Content     | 204 No Content | Successful delete/update with no body |
| Bad Request    | 400            | Invalid input, validation failure     |
| Unauthorized   | 401            | Missing or invalid authentication     |
| Forbidden      | 403            | Authenticated but not authorized      |
| Not Found      | 404            | Resource does not exist               |
| Conflict       | 409            | Duplicate entry, constraint violation |
| Internal Error | 500            | Unhandled exceptions                  |

### Rules

- Never expose raw stack traces in production
- Log all unexpected exceptions with full details
- Map known exceptions (e.g., SQLite constraint violations) to appropriate status codes
- Return consistent error shapes (`{ statusCode, message, details? }`)

---

## 14. Validation Standard

### Three Levels

1. **Request DTOs** — Basic shape validation using data annotations or records:

   ```csharp
   public record CreateGameRequest(
       [Required] [MaxLength(200)] string Name,
       int? Year);
   ```

2. **Service methods** — Business validation (duplicates, state transitions, authorization):

   ```csharp
   var existing = await _context.Games.FirstOrDefaultAsync(g => g.Name == request.Name);
   if (existing is not null) return null; // or throw, or return Result<T>
   ```

3. **Database constraints** — Final safety net via EF Core configuration:
   ```csharp
   builder.HasIndex(x => x.Name).IsUnique();
   ```

### Rules

- Do not introduce FluentValidation unless there are complex cross-field rules that justify it
- Keep validation close to where it matters
- Validate at system boundaries (controller input), not deep in the call stack
- Let `[ApiController]` handle basic model state validation automatically

---

## 15. Mapping Standard

For small-to-medium apps, prefer static extension methods over mapping libraries:

```csharp
public static class GameMappings
{
    public static GameResponse ToResponse(this Game entity)
    {
        return new GameResponse(
            entity.Id,
            entity.Name,
            entity.Status.ToString(),
            entity.ReleaseDate);
    }

    public static GameDetailResponse ToDetailResponse(this Game entity)
    {
        return new GameDetailResponse
        {
            Id = entity.Id,
            Name = entity.Name,
            Replays = entity.Replays.Select(r => r.ToResponse()).ToList()
        };
    }
}
```

### Location

```
Application/Mapping/GameMappingExtensions.cs
Application/Mapping/UserMappingExtensions.cs
```

### Rules

- One mapping file per domain entity or domain group
- Use extension methods on the source type (`this Game entity`)
- Avoid AutoMapper unless the project is large enough to justify it
- Keep mappings simple — if a mapping requires business logic, put that logic in the service and pass the result to the mapper

---

## 16. Code Style Standard

### Core Principle

Prefer self-explanatory code over explanatory comments.

Good code explains itself through clear names, small methods, consistent structure, and explicit business concepts.

### Comments

**Useful comments explain WHY something exists:**

```csharp
// SQLite does not support dropping this column directly, so this migration rebuilds the table.
```

```csharp
// Keep this response shape stable because the frontend stores it in local cache.
```

```csharp
// This default value keeps old imported CSV files compatible with the new schema.
```

**Remove comments that repeat the code:**

```csharp
// Bad — the method name already says this
// Get all games
public async Task<List<Game>> GetAllGamesAsync()

// Bad — the code is self-explanatory
// Check if user is null
if (user == null)

// Bad — obvious from the method call
// Save changes to database
await dbContext.SaveChangesAsync();
```

### Naming

- Use specific business names: `GetActiveGamesAsync()` instead of `GetGamesAsync()` with a comment "only active"
- Avoid generic names: `ProcessData`, `HandleLogic`, `DoOperation`, `Manager`, `Helper`, `Processor`
- Use `Service` suffix for application services: `GameService`, not `GameManager` or `GameHelper`
- Use `Extensions` suffix for extension method classes: `HttpContextExtensions`
- Keep method names under ~40 characters — if longer, the method may be doing too much

### Methods

- Each method should do one thing
- Prefer short methods (10-30 lines) over long methods with section comments
- If a method has section divider comments (`// ── Step 1 ──`), it probably needs to be split
- If a method needs more than 3-4 parameters, consider a parameter object

### XML Documentation

- Do not add XML docs everywhere
- Use XML docs only for public library APIs, complex interfaces, and non-obvious service contracts
- For internal application code, readable method names are better than XML summaries

### Regions

- Do not use `#region` to organize code
- If a file needs regions, it needs to be split into smaller files

### General Rules

- Do not create abstractions before there is a real use case
- Do not add error handling for scenarios that cannot happen
- Do not add extra layers "just in case"
- Keep formatting consistent with the existing project style
- Prefer explicit types over `var` when the type is not obvious from the right side
- Use `sealed` on classes that are not designed for inheritance

---

## 17. Testing Standard

### Expected Tests

| Type                  | Purpose                                | Location             | Priority                |
| --------------------- | -------------------------------------- | -------------------- | ----------------------- |
| Service unit tests    | Verify business logic in isolation     | `Tests/Services/`    | High                    |
| API integration tests | Verify endpoint behavior end-to-end    | `Tests/Integration/` | Medium                  |
| EF Core tests         | Verify queries and configurations      | `Tests/Persistence/` | Medium                  |
| Import/export tests   | Verify data parsing and transformation | `Tests/Services/`    | High (if import exists) |

### Testing with SQLite

```csharp
var options = new DbContextOptionsBuilder<AppDbContext>()
    .UseSqlite("Data Source=:memory:")
    .Options;

using var context = new AppDbContext(options);
context.Database.OpenConnection();
context.Database.EnsureCreated();
```

For tests that need realistic behavior (triggers, constraints), use a temporary file-based database:

```csharp
var dbPath = Path.GetTempFileName();
var options = new DbContextOptionsBuilder<AppDbContext>()
    .UseSqlite($"Data Source={dbPath}")
    .Options;
```

### What to Test First

1. State transition logic (the core business rules)
2. Data import/export (error-prone I/O operations)
3. Complex aggregation/calculation services
4. Auth flows

### What NOT to Test

- Simple CRUD operations that just call `context.SaveChangesAsync()`
- Mapping extensions (unless they contain logic)
- Configuration registration

---

## 18. Frontend Compatibility Standard

When changing backend APIs consumed by a frontend:

1. **Inspect the frontend API client first** — check which endpoints and response shapes it uses
2. **Preserve response shapes when possible** — add new fields, do not remove or rename existing ones
3. **Avoid breaking route names** — route changes require frontend updates
4. **Document breaking changes** — if unavoidable, list them and update frontend types
5. **Prefer additive changes** — new fields with `null` default are non-breaking
6. **Update TypeScript types** — if frontend is in the same repo, update type definitions alongside backend changes
7. **Test the full flow** — after any API change, verify the frontend still renders correctly

### JSON Serialization

ASP.NET Core serializes PascalCase C# properties to camelCase JSON by default. The frontend expects camelCase. Do not change this convention.

---

## 19. Security Standard

### Checklist

- [ ] CORS origins are explicitly configured (no `AllowAnyOrigin` in production)
- [ ] JWT secret key is overridden via environment variable in production
- [ ] Default/development secrets are not usable in production
- [ ] Auth is applied globally (via base controller `[Authorize]`) with explicit `[AllowAnonymous]` exceptions
- [ ] Admin endpoints check role/claim explicitly
- [ ] Secrets are never committed to `appsettings.json`
- [ ] Input validation prevents injection
- [ ] File upload endpoints validate file type and size
- [ ] Internal file paths are not exposed in API responses
- [ ] Stack traces are not returned in production error responses
- [ ] `[AllowAnonymous]` endpoints are documented and minimal

---

## 20. Definition of Done for Backend Changes

```md
- [ ] Entity updated if needed
- [ ] DTOs updated (request and response)
- [ ] Service logic updated
- [ ] Controller endpoint updated
- [ ] DbContext/configuration updated if schema changed
- [ ] Migration created if schema changed
- [ ] Migration reviewed for data safety
- [ ] Tests added or updated
- [ ] Frontend API contract checked for compatibility
- [ ] App runs locally and serves requests correctly
- [ ] Existing data in SQLite is preserved
- [ ] Documentation updated if significant
- [ ] Unnecessary comments removed
- [ ] Code names are clear enough without comments
- [ ] No obvious AI-generated boilerplate remains
```

---

## 21. How to Apply This Standard to Another Project

When starting work on a new or existing project:

1. **Inspect the project first** — understand its current structure, stack, and state before proposing changes
2. **Compare with this standard** — identify gaps between the current structure and the target architecture
3. **Do not blindly rewrite** — a working application is more valuable than a perfectly structured one that does not work
4. **Create a project-specific plan** — use File 01 (`01-backend-refactor-plan.md`) as a template for a per-project refactor plan
5. **Preserve working behavior** — refactoring should not change what the app does, only how it is organized
6. **Migrate gradually** — follow the phased approach (extract services first, reorganize folders later)
7. **Document deviations** — if the project cannot follow this standard exactly, explain why and document the differences
8. **Keep the frontend working** — every refactor step should leave the frontend functional
9. **Test after each phase** — verify the app starts, serves requests, and handles edge cases

### Quick Start for a New AI Assistant

If you are an AI coding assistant and this document is in your context:

- This is the target architecture standard for this user's C# APIs
- The user is stronger on frontend than backend — explain backend concepts clearly
- Prefer practical examples from the project over generic advice
- Do not over-engineer — keep it pragmatic
- Do not add abstractions without clear benefit
- Do not add repositories when DbContext is sufficient
- Do not split into multiple projects unless the app is large
- Do not add XML documentation comments everywhere
- Do not add comments that repeat the code
- Follow the phased refactoring approach — never rewrite everything at once
