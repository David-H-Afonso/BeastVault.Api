# JWT Authentication & Multi-User Migration Plan — BeastVault API

This document is the complete implementation plan for adding JWT authentication, user management, and per-user data isolation to BeastVault. It covers the current state, target architecture, data migration strategy, and step-by-step implementation with code examples.

---

## 1. Current State Analysis

### What Exists Today

| Aspect              | Current State                                                                |
| ------------------- | ---------------------------------------------------------------------------- |
| Authentication      | **None** — zero auth on any endpoint                                         |
| User model          | **None** — no User entity, no user concept                                   |
| Data ownership      | **Global** — all Pokémon, files, and tags belong to everyone                 |
| Protected endpoints | **Zero** — including `POST /admin/wipe-database` and `POST /config/database` |
| File storage        | **Single shared folder** — `~/Documents/BeastVault` for all data             |
| Desktop app         | **No auth** — Electron connects directly to API without credentials          |
| Frontend            | **No auth flow** — no login page, no token management                        |

### What the Other Projects Have (Reference)

Games Database and WarcraftArchive both implement:

- JWT Bearer authentication with BCrypt password hashing
- User entity with `UserRole` enum (Admin/Standard)
- `UserId` FK on all data entities
- `UserContextMiddleware` extracting UserId from JWT claims
- `BaseApiController` / endpoint-level `RequireAuthorization()`
- Default admin user seeded on startup (passwordless login)
- Per-user data filtering on every query

### What Makes BeastVault Different

| Concern             | Games Database / WarcraftArchive   | BeastVault                                            |
| ------------------- | ---------------------------------- | ----------------------------------------------------- |
| Data source         | User creates data manually (forms) | Data is parsed from binary files (PKHeX.Core)         |
| File storage        | No physical files                  | `.pk*` files on disk + DB blob backup                 |
| Auto-import         | No                                 | Scans `~/Documents/BeastVault` on startup             |
| Directory structure | Flat DB-only                       | Per-format backup folders (`backup/pk9/2024/`)        |
| Desktop app         | No                                 | Electron wrapper with same API                        |
| Tag model needed    | Per-user only                      | Hybrid: system tags + per-user custom tags            |
| Data volume         | Hundreds of rows                   | Potentially thousands of Pokémon with 60+ fields each |

---

## 2. Target Architecture

### User Model

```csharp
public enum UserRole
{
    Standard = 0,
    Admin = 1
}

public class User
{
    public int Id { get; set; }
    public string Username { get; set; } = string.Empty;
    public string? PasswordHash { get; set; }  // nullable → passwordless login allowed
    public UserRole Role { get; set; } = UserRole.Standard;
    public bool IsDefault { get; set; } = false;  // true → cannot be deleted
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Navigation properties
    public ICollection<FileEntity> Files { get; set; } = [];
    public ICollection<PokemonEntity> Pokemon { get; set; } = [];
    public ICollection<TagEntity> Tags { get; set; } = [];
}
```

**Key decisions:**

- `PasswordHash` is **nullable** — the default Admin user starts without a password (login with username only)
- `IsDefault = true` on the seeded admin — prevents accidental deletion
- `int Id` (not Guid) — matches all other portfolio projects
- No email field — BeastVault is a local/self-hosted app, email is unnecessary
- BCrypt with `workFactor=12` for password hashing

### JWT Configuration

```json
// appsettings.json
{
  "JwtSettings": {
    "SecretKey": "BeastVault-Dev-Secret-Key-Change-In-Production-Min32Chars!!",
    "Issuer": "BeastVault.Api",
    "Audience": "BeastVault.Client",
    "AccessTokenMinutes": 10080,
    "RefreshTokenDays": 30
  }
}
```

**Environment variable overrides:**

- `JWT_SECRET_KEY` → `JwtSettings:SecretKey`
- `JWT_ACCESS_TOKEN_MINUTES` → `JwtSettings:AccessTokenMinutes`
- `JWT_REFRESH_TOKEN_DAYS` → `JwtSettings:RefreshTokenDays`

### Data Ownership Model

**Fully isolated**: each user only sees their own Pokémon, files, and tags.

| Entity              | Ownership         | FK                                    |
| ------------------- | ----------------- | ------------------------------------- |
| `FileEntity`        | Per-user          | `int UserId` (required)               |
| `PokemonEntity`     | Per-user          | `int UserId` (required)               |
| `TagEntity`         | Hybrid            | `int? UserId` (nullable = system tag) |
| `StatsEntity`       | Via PokemonEntity | No direct UserId needed               |
| `MoveEntity`        | Via PokemonEntity | No direct UserId needed               |
| `RelearnMoveEntity` | Via PokemonEntity | No direct UserId needed               |
| `PokemonTagEntity`  | Via both parents  | No direct UserId needed               |
| `FileTagEntity`     | Via both parents  | No direct UserId needed               |

### File Storage — Per-User Directories

```
~/Documents/BeastVault/           ← Base directory
├── users/
│   ├── admin/                    ← Default admin user's directory
│   │   ├── Pikachu_a1b2c3d4.pk9
│   │   ├── Charizard_e5f6g7h8.pk9
│   │   └── backup/
│   │       ├── pk9/
│   │       │   └── 2025/
│   │       │       ├── Pikachu.pk9
│   │       │       └── Charizard.pk9
│   │       └── pk8/
│   │           └── 2024/
│   │               └── Snorlax.pk8
│   ├── player2/                  ← Second user's directory
│   │   ├── Eevee_i9j0k1l2.pk9
│   │   └── backup/
│   │       └── pk9/
│   │           └── 2025/
│   │               └── Eevee.pk9
│   └── ...
└── assets/                       ← Shared sprites (not user-scoped)
```

**Migration of existing files**: All files currently in `~/Documents/BeastVault/` (flat structure) are moved to `users/admin/` during the data migration.

---

## 3. Authentication Flow

### Login Flow

```
Client                          API
  │                              │
  ├── POST /auth/login ─────────→│
  │   { username, password }     │
  │                              ├── Find user by username (case-insensitive)
  │                              ├── If user.PasswordHash == null → allow passwordless
  │                              ├── If user.PasswordHash != null → BCrypt.Verify()
  │                              ├── Generate JWT with claims:
  │                              │   - ClaimTypes.NameIdentifier = user.Id
  │                              │   - ClaimTypes.Name = user.Username
  │                              │   - ClaimTypes.Role = user.Role
  │                              │
  │←── { userId, username, ─────┤
  │      role, token }           │
  │                              │
  ├── Store token (Pinia/Redux) ─┤
  │                              │
  ├── GET /pokemon ──────────────→│ Authorization: Bearer <token>
  │                              ├── JWT middleware validates token
  │                              ├── UserContextMiddleware extracts UserId
  │                              ├── Endpoint filters: WHERE UserId = extractedId
  │←── { Items: [...] } ────────┤
```

### Token Validation Pipeline

```csharp
// Program.cs — order matters
app.UseAuthentication();     // 1. Validates JWT, populates context.User
app.UseAuthorization();      // 2. Enforces [Authorize] / RequireAuthorization()
app.UseUserContext();        // 3. Extracts UserId from claims to HttpContext.Items["UserId"]
```

### Endpoint Protection Strategy

| Endpoint Group              | Auth Required | Role Required | Notes                                                |
| --------------------------- | ------------- | ------------- | ---------------------------------------------------- |
| `GET /health`               | No            | —             | Always public                                        |
| `POST /auth/login`          | No            | —             | Must be public                                       |
| `POST /auth/register`       | No            | —             | Public for first-time setup; can be admin-only later |
| `GET /auth/users`           | Yes           | Admin         | List all users                                       |
| `DELETE /auth/users/{id}`   | Yes           | Admin         | Cannot delete IsDefault user                         |
| All `/pokemon/*`            | Yes           | Any           | Filtered by UserId                                   |
| All `/import`               | Yes           | Any           | Files assigned to authenticated user                 |
| All `/tags/*`               | Yes           | Any           | User sees own tags + system tags                     |
| All `/files/*`, `/export/*` | Yes           | Any           | Filtered by UserId                                   |
| All `/scan/*`               | Yes           | Any           | Scans user's directory only                          |
| All `/maintenance/*`        | Yes           | Admin         | System-level operations                              |
| All `/config/*`             | Yes           | Admin         | System-level operations                              |
| `POST /admin/wipe-database` | Yes           | Admin         | Destructive operation                                |
| All `/custom-sprites/*`     | No            | —             | Shared assets, no user data                          |

---

## 4. Data Migration Strategy

### Overview

The migration adds multi-user support to an existing single-user database. All existing data is assigned to the default admin user.

### Step 1: Create Users Table

```sql
CREATE TABLE "Users" (
    "Id" INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT,
    "Username" TEXT NOT NULL,
    "PasswordHash" TEXT,
    "Role" INTEGER NOT NULL DEFAULT 0,
    "IsDefault" INTEGER NOT NULL DEFAULT 0,
    "CreatedAt" TEXT NOT NULL DEFAULT '0001-01-01T00:00:00'
);

CREATE UNIQUE INDEX "IX_Users_Username" ON "Users" ("Username");
```

### Step 2: Seed Default Admin

```sql
INSERT INTO "Users" ("Username", "PasswordHash", "Role", "IsDefault", "CreatedAt")
VALUES ('Admin', NULL, 1, 1, datetime('now'));
```

### Step 3: Add UserId to FileEntity

```sql
-- SQLite requires table rebuild for adding non-nullable FK with existing data
-- EF Core handles this automatically, but the migration SQL looks like:

ALTER TABLE "Files" ADD COLUMN "UserId" INTEGER NOT NULL DEFAULT 1;

-- Add FK index
CREATE INDEX "IX_Files_UserId" ON "Files" ("UserId");

-- Change unique constraint from Sha256-only to (UserId, Sha256)
-- (same file can be imported by different users)
DROP INDEX "IX_Files_Sha256";
CREATE UNIQUE INDEX "IX_Files_UserId_Sha256" ON "Files" ("UserId", "Sha256");
```

### Step 4: Add UserId to PokemonEntity

```sql
ALTER TABLE "Pokemon" ADD COLUMN "UserId" INTEGER NOT NULL DEFAULT 1;

CREATE INDEX "IX_Pokemon_UserId" ON "Pokemon" ("UserId");
```

### Step 5: Add UserId to TagEntity (nullable)

```sql
ALTER TABLE "Tags" ADD COLUMN "UserId" INTEGER;

CREATE INDEX "IX_Tags_UserId" ON "Tags" ("UserId");

-- Change unique name constraint to per-user
DROP INDEX "IX_Tags_Name";
CREATE UNIQUE INDEX "IX_Tags_UserId_Name" ON "Tags" ("UserId", "Name");
```

### Step 6: Seed System Tags

```sql
-- System tags have NULL UserId
INSERT INTO "Tags" ("Name", "UserId") VALUES ('Shiny', NULL);
INSERT INTO "Tags" ("Name", "UserId") VALUES ('Legendary', NULL);
INSERT INTO "Tags" ("Name", "UserId") VALUES ('Mythical', NULL);
INSERT INTO "Tags" ("Name", "UserId") VALUES ('Event', NULL);
INSERT INTO "Tags" ("Name", "UserId") VALUES ('Competitive', NULL);
INSERT INTO "Tags" ("Name", "UserId") VALUES ('Favorite', NULL);
```

### Step 7: Migrate Existing Tags

Existing user-created tags (before migration) get assigned to admin user:

```sql
UPDATE "Tags" SET "UserId" = 1 WHERE "UserId" IS NULL AND "Name" NOT IN ('Shiny', 'Legendary', 'Mythical', 'Event', 'Competitive', 'Favorite');
```

Wait — this conflicts with system tags. Better approach:

```sql
-- First, set all existing tags to admin user
UPDATE "Tags" SET "UserId" = 1;

-- Then insert system tags (they'll have NULL UserId)
INSERT OR IGNORE INTO "Tags" ("Name", "UserId") VALUES ('Shiny', NULL);
INSERT OR IGNORE INTO "Tags" ("Name", "UserId") VALUES ('Legendary', NULL);
-- ... etc.
```

### EF Core Migration Code

```csharp
// In the migration file
protected override void Up(MigrationBuilder migrationBuilder)
{
    // 1. Create Users table
    migrationBuilder.CreateTable(
        name: "Users",
        columns: table => new
        {
            Id = table.Column<int>(nullable: false)
                .Annotation("Sqlite:Autoincrement", true),
            Username = table.Column<string>(nullable: false),
            PasswordHash = table.Column<string>(nullable: true),
            Role = table.Column<int>(nullable: false, defaultValue: 0),
            IsDefault = table.Column<bool>(nullable: false, defaultValue: false),
            CreatedAt = table.Column<DateTime>(nullable: false)
        },
        constraints: table => table.PrimaryKey("PK_Users", x => x.Id));

    migrationBuilder.CreateIndex("IX_Users_Username", "Users", "Username", unique: true);

    // 2. Seed admin user
    migrationBuilder.InsertData(
        table: "Users",
        columns: new[] { "Username", "PasswordHash", "Role", "IsDefault", "CreatedAt" },
        values: new object[] { "Admin", null!, 1, true, DateTime.UtcNow });

    // 3. Add UserId to Files (default = 1 for existing data)
    migrationBuilder.AddColumn<int>("UserId", "Files", nullable: false, defaultValue: 1);
    migrationBuilder.CreateIndex("IX_Files_UserId", "Files", "UserId");

    // 4. Add UserId to Pokemon (default = 1)
    migrationBuilder.AddColumn<int>("UserId", "Pokemon", nullable: false, defaultValue: 1);
    migrationBuilder.CreateIndex("IX_Pokemon_UserId", "Pokemon", "UserId");

    // 5. Add UserId to Tags (nullable for system tags)
    migrationBuilder.AddColumn<int?>("UserId", "Tags", nullable: true);
    migrationBuilder.CreateIndex("IX_Tags_UserId", "Tags", "UserId");

    // 6. Assign existing tags to admin
    migrationBuilder.Sql("UPDATE Tags SET UserId = 1;");

    // 7. Update unique indexes
    migrationBuilder.DropIndex("IX_Files_Sha256", "Files");
    migrationBuilder.CreateIndex("IX_Files_UserId_Sha256", "Files",
        new[] { "UserId", "Sha256" }, unique: true);

    migrationBuilder.DropIndex("IX_Tags_Name", "Tags");
    migrationBuilder.CreateIndex("IX_Tags_UserId_Name", "Tags",
        new[] { "UserId", "Name" }, unique: true);
}
```

### File Migration Script

Existing files need to be moved from the flat structure to user directories:

```csharp
// Run once during upgrade (in startup or as a one-time migration)
public static async Task MigrateFileStructureAsync(
    AppDbContext db,
    StorageConfiguration storage,
    string adminUsername = "admin")
{
    var basePath = storage.PokemonFilesDirectory;
    var adminDir = Path.Combine(basePath, "users", adminUsername);
    var adminBackupDir = Path.Combine(adminDir, "backup");

    Directory.CreateDirectory(adminDir);
    Directory.CreateDirectory(adminBackupDir);

    // Move main files
    var mainFiles = Directory.GetFiles(basePath, "*.*")
        .Where(f => !Path.GetFileName(f).StartsWith("."))
        .Where(f => IsPokemonFile(f));

    foreach (var file in mainFiles)
    {
        var destPath = Path.Combine(adminDir, Path.GetFileName(file));
        if (!File.Exists(destPath))
        {
            File.Move(file, destPath);
            Console.WriteLine($"Migrated: {file} → {destPath}");
        }
    }

    // Move backup directory contents
    var oldBackupDir = Path.Combine(basePath, "backup");
    if (Directory.Exists(oldBackupDir))
    {
        foreach (var dir in Directory.GetDirectories(oldBackupDir, "*", SearchOption.AllDirectories))
        {
            var relative = Path.GetRelativePath(oldBackupDir, dir);
            var newDir = Path.Combine(adminBackupDir, relative);
            Directory.CreateDirectory(newDir);
        }

        foreach (var file in Directory.GetFiles(oldBackupDir, "*.*", SearchOption.AllDirectories))
        {
            var relative = Path.GetRelativePath(oldBackupDir, file);
            var destPath = Path.Combine(adminBackupDir, relative);
            if (!File.Exists(destPath))
            {
                File.Move(file, destPath);
            }
        }

        // Remove old backup directory after migration
        // Directory.Delete(oldBackupDir, true); // Uncomment after verification
    }

    // Update StoredPath in database
    var allFiles = await db.Files.ToListAsync();
    foreach (var dbFile in allFiles)
    {
        if (!string.IsNullOrEmpty(dbFile.StoredPath))
        {
            var fileName = Path.GetFileName(dbFile.StoredPath);
            dbFile.StoredPath = Path.Combine(adminDir, fileName);
        }
    }
    await db.SaveChangesAsync();
}
```

---

## 5. New Files to Create

### Configuration/JwtSettings.cs

```csharp
namespace BeastVault.Api.Configuration;

public class JwtSettings
{
    public const string SectionName = "JwtSettings";

    public string SecretKey { get; set; } = string.Empty;
    public string Issuer { get; set; } = "BeastVault.Api";
    public string Audience { get; set; } = "BeastVault.Client";
    public int AccessTokenMinutes { get; set; } = 10080; // 7 days
    public int RefreshTokenDays { get; set; } = 30;
}
```

### Domain/Entities/User.cs

```csharp
using System.Text.Json.Serialization;

namespace BeastVault.Api.Domain.Entities;

public enum UserRole
{
    Standard = 0,
    Admin = 1
}

public class User
{
    public int Id { get; set; }
    public string Username { get; set; } = string.Empty;

    [JsonIgnore]
    public string? PasswordHash { get; set; }

    public UserRole Role { get; set; } = UserRole.Standard;
    public bool IsDefault { get; set; } = false;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Navigation properties
    [JsonIgnore] public ICollection<FileEntity> Files { get; set; } = [];
    [JsonIgnore] public ICollection<PokemonEntity> Pokemon { get; set; } = [];
    [JsonIgnore] public ICollection<TagEntity> Tags { get; set; } = [];
}
```

### Infrastructure/Services/IAuthService.cs

```csharp
namespace BeastVault.Api.Infrastructure.Services;

public interface IAuthService
{
    Task<LoginResponse?> AuthenticateAsync(string username, string? password);
    Task<User?> RegisterAsync(string username, string? password, UserRole role = UserRole.Standard);
    Task<User?> GetByIdAsync(int userId);
    Task<IReadOnlyList<UserSummaryDto>> GetAllUsersAsync();
    Task<bool> DeleteUserAsync(int userId);
    Task<bool> ChangePasswordAsync(int userId, string? newPassword);
    string GenerateToken(User user);
}
```

### Infrastructure/Services/AuthService.cs

```csharp
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using BeastVault.Api.Configuration;
using BeastVault.Api.Domain.Entities;

namespace BeastVault.Api.Infrastructure.Services;

public class AuthService : IAuthService
{
    private readonly AppDbContext _context;
    private readonly JwtSettings _jwtSettings;

    public AuthService(AppDbContext context, IOptions<JwtSettings> jwtSettings)
    {
        _context = context;
        _jwtSettings = jwtSettings.Value;
    }

    public async Task<LoginResponse?> AuthenticateAsync(string username, string? password)
    {
        var user = await _context.Users
            .FirstOrDefaultAsync(u => u.Username.ToLower() == username.ToLower());

        if (user == null) return null;

        if (user.PasswordHash != null)
        {
            if (string.IsNullOrEmpty(password) ||
                !BCrypt.Net.BCrypt.Verify(password, user.PasswordHash))
                return null;
        }
        // PasswordHash == null → passwordless login allowed

        return new LoginResponse
        {
            UserId = user.Id,
            Username = user.Username,
            Role = user.Role.ToString(),
            Token = GenerateToken(user)
        };
    }

    public string GenerateToken(User user)
    {
        var key = Encoding.UTF8.GetBytes(_jwtSettings.SecretKey);
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new(ClaimTypes.Name, user.Username),
            new(ClaimTypes.Role, user.Role.ToString())
        };

        var tokenDescriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(claims),
            Expires = DateTime.UtcNow.AddMinutes(_jwtSettings.AccessTokenMinutes),
            Issuer = _jwtSettings.Issuer,
            Audience = _jwtSettings.Audience,
            SigningCredentials = new SigningCredentials(
                new SymmetricSecurityKey(key),
                SecurityAlgorithms.HmacSha256Signature)
        };

        var handler = new JwtSecurityTokenHandler();
        return handler.WriteToken(handler.CreateToken(tokenDescriptor));
    }

    public async Task<User?> RegisterAsync(string username, string? password, UserRole role = UserRole.Standard)
    {
        var existing = await _context.Users
            .AnyAsync(u => u.Username.ToLower() == username.ToLower());

        if (existing) return null;

        var user = new User
        {
            Username = username,
            PasswordHash = string.IsNullOrEmpty(password)
                ? null
                : BCrypt.Net.BCrypt.HashPassword(password, workFactor: 12),
            Role = role
        };

        _context.Users.Add(user);
        await _context.SaveChangesAsync();
        return user;
    }

    public async Task<User?> GetByIdAsync(int userId)
        => await _context.Users.FindAsync(userId);

    public async Task<IReadOnlyList<UserSummaryDto>> GetAllUsersAsync()
        => await _context.Users
            .Select(u => new UserSummaryDto
            {
                Id = u.Id,
                Username = u.Username,
                Role = u.Role.ToString(),
                IsDefault = u.IsDefault,
                HasPassword = u.PasswordHash != null,
                CreatedAt = u.CreatedAt,
                PokemonCount = _context.Pokemon.Count(p => p.UserId == u.Id),
                FileCount = _context.Files.Count(f => f.UserId == u.Id)
            })
            .ToListAsync();

    public async Task<bool> DeleteUserAsync(int userId)
    {
        var user = await _context.Users.FindAsync(userId);
        if (user == null || user.IsDefault) return false;

        _context.Users.Remove(user);
        await _context.SaveChangesAsync();
        return true;
    }

    public async Task<bool> ChangePasswordAsync(int userId, string? newPassword)
    {
        var user = await _context.Users.FindAsync(userId);
        if (user == null) return false;

        user.PasswordHash = string.IsNullOrEmpty(newPassword)
            ? null
            : BCrypt.Net.BCrypt.HashPassword(newPassword, workFactor: 12);

        await _context.SaveChangesAsync();
        return true;
    }
}
```

### Infrastructure/Middleware/UserContextMiddleware.cs

```csharp
using System.Security.Claims;

namespace BeastVault.Api.Infrastructure.Middleware;

public class UserContextMiddleware
{
    private readonly RequestDelegate _next;

    public UserContextMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        int? userId = null;

        if (context.User.Identity?.IsAuthenticated == true)
        {
            var claim = context.User.FindFirst(ClaimTypes.NameIdentifier);
            if (claim != null && int.TryParse(claim.Value, out var parsedId))
                userId = parsedId;
        }

        if (userId.HasValue)
            context.Items["UserId"] = userId.Value;

        await _next(context);
    }
}

public static class UserContextMiddlewareExtensions
{
    public static IApplicationBuilder UseUserContext(this IApplicationBuilder builder)
        => builder.UseMiddleware<UserContextMiddleware>();
}
```

### Extensions/ClaimsPrincipalExtensions.cs

```csharp
namespace BeastVault.Api.Extensions;

public static class ClaimsPrincipalExtensions
{
    public static int? GetUserId(this HttpContext context)
    {
        if (context.Items.TryGetValue("UserId", out var userId) && userId is int id)
            return id;
        return null;
    }

    public static int GetUserIdOrDefault(this HttpContext context, int defaultUserId = 1)
        => context.GetUserId() ?? defaultUserId;

    public static bool IsAdmin(this HttpContext context)
        => context.User.IsInRole("Admin");
}
```

### Endpoints/AuthEndpoints.cs

```csharp
namespace BeastVault.Api.Endpoints;

public static class AuthEndpoints
{
    public static IEndpointRouteBuilder MapAuthEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/auth").WithTags("Authentication");

        group.MapPost("/login", async (LoginRequest request, IAuthService authService) =>
        {
            var response = await authService.AuthenticateAsync(request.Username, request.Password);
            return response is null
                ? Results.Unauthorized()
                : Results.Ok(response);
        }).AllowAnonymous();

        group.MapPost("/register", async (RegisterRequest request, IAuthService authService) =>
        {
            var user = await authService.RegisterAsync(request.Username, request.Password);
            return user is null
                ? Results.Conflict("Username already exists")
                : Results.Created($"/auth/users/{user.Id}", new { user.Id, user.Username, Role = user.Role.ToString() });
        }).AllowAnonymous();

        group.MapGet("/users", async (IAuthService authService) =>
        {
            var users = await authService.GetAllUsersAsync();
            return Results.Ok(users);
        }).RequireAuthorization(policy => policy.RequireRole("Admin"));

        group.MapDelete("/users/{id:int}", async (int id, IAuthService authService) =>
        {
            var deleted = await authService.DeleteUserAsync(id);
            return deleted ? Results.NoContent() : Results.NotFound();
        }).RequireAuthorization(policy => policy.RequireRole("Admin"));

        group.MapPost("/users/{id:int}/password", async (int id, ChangePasswordRequest request, IAuthService authService, HttpContext context) =>
        {
            // Users can only change their own password, admins can change anyone's
            var currentUserId = context.GetUserId();
            if (currentUserId != id && !context.IsAdmin())
                return Results.Forbid();

            var changed = await authService.ChangePasswordAsync(id, request.NewPassword);
            return changed ? Results.NoContent() : Results.NotFound();
        }).RequireAuthorization();

        return app;
    }
}
```

### Contracts/Auth/AuthDtos.cs

```csharp
namespace BeastVault.Api.Contracts.Auth;

public record LoginRequest
{
    public required string Username { get; init; }
    public string? Password { get; init; }
}

public record LoginResponse
{
    public int UserId { get; init; }
    public required string Username { get; init; }
    public required string Role { get; init; }
    public required string Token { get; init; }
}

public record RegisterRequest
{
    public required string Username { get; init; }
    public string? Password { get; init; }
}

public record ChangePasswordRequest
{
    public string? NewPassword { get; init; }
}

public record UserSummaryDto
{
    public int Id { get; init; }
    public required string Username { get; init; }
    public required string Role { get; init; }
    public bool IsDefault { get; init; }
    public bool HasPassword { get; init; }
    public DateTime CreatedAt { get; init; }
    public int PokemonCount { get; init; }
    public int FileCount { get; init; }
}
```

---

## 6. Entity Changes

### FileEntity — Add UserId

```csharp
public class FileEntity
{
    // ... existing fields ...

    // NEW
    public int UserId { get; set; }
    public User User { get; set; } = null!;
}
```

### PokemonEntity — Add UserId

```csharp
public class PokemonEntity
{
    // ... existing fields ...

    // NEW
    public int UserId { get; set; }
    public User User { get; set; } = null!;
}
```

### TagEntity — Add UserId (nullable)

```csharp
public class TagEntity
{
    // ... existing fields ...

    // NEW — null means system tag (visible to all)
    public int? UserId { get; set; }
    public User? User { get; set; }
}
```

### AppDbContext Changes

```csharp
public class AppDbContext : DbContext
{
    // ... existing DbSets ...

    // NEW
    public DbSet<User> Users => Set<User>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        // ... existing configuration ...

        // NEW: User entity
        b.Entity<User>().HasKey(x => x.Id);
        b.Entity<User>().HasIndex(x => x.Username).IsUnique();

        // NEW: File → User relationship
        b.Entity<FileEntity>()
            .HasOne(f => f.User)
            .WithMany(u => u.Files)
            .HasForeignKey(f => f.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        // CHANGED: File SHA256 unique per user (not globally)
        b.Entity<FileEntity>().HasIndex(x => new { x.UserId, x.Sha256 }).IsUnique();

        // NEW: Pokemon → User relationship
        b.Entity<PokemonEntity>()
            .HasOne(p => p.User)
            .WithMany(u => u.Pokemon)
            .HasForeignKey(p => p.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        // NEW: Tag → User relationship (optional = system tag)
        b.Entity<TagEntity>()
            .HasOne(t => t.User)
            .WithMany(u => u.Tags)
            .HasForeignKey(t => t.UserId)
            .OnDelete(DeleteBehavior.SetNull);

        // CHANGED: Tag name unique per user
        b.Entity<TagEntity>().HasIndex(x => new { x.UserId, x.Name }).IsUnique();
    }
}
```

---

## 7. Endpoint Modification Patterns

### Pattern: GET with User Filtering

```csharp
// BEFORE
app.MapGet("/pokemon", async (AppDbContext db, [AsParameters] AdvancedPokemonQuery q) =>
{
    var baseQuery = db.Pokemon.AsNoTracking().AsQueryable();
    // ...
});

// AFTER
app.MapGet("/pokemon", async (AppDbContext db, HttpContext context, [AsParameters] AdvancedPokemonQuery q) =>
{
    var userId = context.GetUserIdOrDefault();
    var baseQuery = db.Pokemon.AsNoTracking()
        .Where(p => p.UserId == userId);  // ← ADD THIS
    // ...
}).RequireAuthorization();
```

### Pattern: POST with User Assignment

```csharp
// BEFORE (in ImportEndpoints)
parse.File.RawBlob = bytes;
db.Files.Add(parse.File);

// AFTER
var userId = context.GetUserIdOrDefault();
parse.File.UserId = userId;          // ← ADD THIS
parse.Pokemon.UserId = userId;       // ← ADD THIS
parse.File.RawBlob = bytes;
db.Files.Add(parse.File);
```

### Pattern: DELETE with Ownership Check

```csharp
// BEFORE
var poke = await db.Pokemon.FirstOrDefaultAsync(x => x.Id == pokemonId);
if (poke == null) return Results.NotFound();

// AFTER
var userId = context.GetUserIdOrDefault();
var poke = await db.Pokemon.FirstOrDefaultAsync(x => x.Id == pokemonId && x.UserId == userId);
if (poke == null) return Results.NotFound();  // Returns 404 even if it exists for another user
```

### Pattern: Tags with Hybrid Model

```csharp
// GET /tags — user sees system tags + own tags
var userId = context.GetUserIdOrDefault();
var tags = await db.Tags
    .Where(t => t.UserId == null || t.UserId == userId)  // System tags + user's tags
    .OrderBy(t => t.Name)
    .Select(t => new TagDto
    {
        Id = t.Id,
        Name = t.Name,
        ImagePath = t.ImagePath,
        IsSystemTag = t.UserId == null,  // NEW field in DTO
        PokemonCount = db.PokemonTags.Count(pt => pt.TagId == t.Id && pt.Pokemon.UserId == userId)
    })
    .ToListAsync();

// POST /tags — new tags belong to user
var tag = new TagEntity { Name = request.Name, UserId = userId };

// PUT/DELETE /tags — only own tags (not system tags unless admin)
var tag = await db.Tags.FindAsync(id);
if (tag == null) return Results.NotFound();
if (tag.UserId == null && !context.IsAdmin()) return Results.Forbid();  // System tag protection
if (tag.UserId != null && tag.UserId != userId) return Results.NotFound();  // Ownership check
```

### Pattern: Scan with User Directory

```csharp
// BEFORE
_watchPath = storage.BasePath;

// AFTER — in FileScanService constructor
public FileScanService(AppDbContext context, PkhexCoreParser parser, FileStorageService storage, int userId, string username)
{
    _watchPath = Path.Combine(storage.BasePath, "users", username);
    _backupPath = Path.Combine(_watchPath, "backup");
    _userId = userId;
    // ... ensure directories exist
}

// In scan endpoint
app.MapPost("/scan/directory", async (HttpContext context, AppDbContext db, ...) =>
{
    var userId = context.GetUserIdOrDefault();
    var user = await db.Users.FindAsync(userId);
    var scanner = new FileScanService(db, parser, storage, userId, user.Username);
    var result = await scanner.ScanAndImportNewFilesAsync();
    // ...
}).RequireAuthorization();
```

---

## 8. Program.cs Changes

### NuGet Packages to Add

```xml
<PackageReference Include="BCrypt.Net-Next" Version="4.0.3" />
<PackageReference Include="Microsoft.AspNetCore.Authentication.JwtBearer" Version="9.0.0" />
<PackageReference Include="System.IdentityModel.Tokens.Jwt" Version="8.0.0" />
```

### Service Registration

```csharp
// JWT Settings
var jwtSettings = builder.Configuration
    .GetSection(JwtSettings.SectionName)
    .Get<JwtSettings>() ?? new JwtSettings();

// Override from env vars
var envSecretKey = Environment.GetEnvironmentVariable("JWT_SECRET_KEY");
if (!string.IsNullOrEmpty(envSecretKey))
    builder.Configuration["JwtSettings:SecretKey"] = envSecretKey;

// Auth services
builder.Services.Configure<JwtSettings>(builder.Configuration.GetSection(JwtSettings.SectionName));
builder.Services.AddScoped<IAuthService, AuthService>();

// JWT authentication
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = jwtSettings.Issuer,
            ValidAudience = jwtSettings.Audience,
            IssuerSigningKey = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(jwtSettings.SecretKey))
        };
    });

builder.Services.AddAuthorization();
```

### Middleware Pipeline

```csharp
app.UseCors("AllowLocalhost");
app.UseAuthentication();    // NEW — must be before UseAuthorization
app.UseAuthorization();     // NEW
app.UseUserContext();       // NEW — must be after UseAuthorization
app.UseHttpsRedirection();
```

### Admin Seeding

```csharp
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    await db.Database.MigrateAsync();

    // Seed default admin if no users exist
    if (!await db.Users.AnyAsync())
    {
        db.Users.Add(new User
        {
            Username = "Admin",
            PasswordHash = null,  // Passwordless login
            Role = UserRole.Admin,
            IsDefault = true
        });
        await db.SaveChangesAsync();
        Console.WriteLine("✅ Default admin user created (passwordless login).");
    }

    // Seed system tags if they don't exist
    var systemTags = new[] { "Shiny", "Legendary", "Mythical", "Event", "Competitive", "Favorite" };
    foreach (var tagName in systemTags)
    {
        if (!await db.Tags.AnyAsync(t => t.Name == tagName && t.UserId == null))
        {
            db.Tags.Add(new TagEntity { Name = tagName, UserId = null });
        }
    }
    await db.SaveChangesAsync();

    // ... existing storage setup and scan
}
```

---

## 9. Desktop App (Electron) Integration

### What Changes for Electron

The Electron app wraps the Vue frontend and starts the .NET backend process. With JWT auth:

1. **Backend starts** as before (same process spawning)
2. **Frontend shows login screen** before loading the main app
3. **Token stored in Electron's renderer process** (Pinia store with localStorage)
4. **All API calls** include `Authorization: Bearer <token>` header
5. **No special Electron-only bypass** — same auth flow as web

### Electron-Specific Considerations

- **First run**: No users exist → admin is auto-seeded → user logs in as "Admin" with no password
- **Single-user Electron**: For users who don't want multi-user, they just never create additional users
- **Token expiry**: With 7-day access tokens, Electron users rarely need to re-login
- **CORS**: Electron requests come from `file://` or a localhost origin — already handled by the CORS fallback

---

## 10. Frontend Changes Required

### New Components Needed

1. **LoginPage** — Username + password form, handles passwordless login
2. **UserMenu** — Shows current user, logout button, admin options
3. **UserManagement** (admin) — Create/delete users, change passwords

### Auth Service (Frontend)

```typescript
// services/AuthService.ts
export interface LoginRequest {
  username: string;
  password?: string;
}

export interface LoginResponse {
  userId: number;
  username: string;
  role: "Admin" | "Standard";
  token: string;
}

export async function login(credentials: LoginRequest): Promise<LoginResponse> {
  const response = await fetch(`${API_BASE}/auth/login`, {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify(credentials),
  });

  if (!response.ok) throw new Error("Login failed");
  return response.json();
}
```

### Token Injection (Frontend)

All existing API calls must include the token:

```typescript
// utils/customFetch.ts or axios interceptor
function customFetch(
  url: string,
  options: RequestInit = {},
): Promise<Response> {
  const token = store.getters["auth/token"]; // or Pinia equivalent
  return fetch(url, {
    ...options,
    headers: {
      ...options.headers,
      Authorization: token ? `Bearer ${token}` : "",
    },
  });
}
```

### Router Guard (Frontend)

```typescript
router.beforeEach((to, from, next) => {
  const isAuthenticated = store.getters["auth/isAuthenticated"];
  if (to.meta.requiresAuth && !isAuthenticated) {
    next("/login");
  } else {
    next();
  }
});
```

---

## 11. Implementation Order

| Step | What                                      | Depends On | Estimated Effort |
| ---- | ----------------------------------------- | ---------- | ---------------- |
| 1    | Add NuGet packages                        | —          | Trivial          |
| 2    | Create User entity + JwtSettings          | —          | Small            |
| 3    | Create AuthService + middleware           | Step 2     | Medium           |
| 4    | Create AuthEndpoints                      | Step 3     | Small            |
| 5    | Register auth in Program.cs               | Steps 2-4  | Small            |
| 6    | Add UserId to entities                    | Step 2     | Small            |
| 7    | Create migration + data migration         | Steps 5-6  | Medium           |
| 8    | Add RequireAuthorization to all endpoints | Step 5     | Medium           |
| 9    | Add user filtering to all queries         | Step 6     | Medium           |
| 10   | Implement per-user file directories       | Step 6     | Medium           |
| 11   | Implement hybrid tag system               | Step 6     | Small            |
| 12   | File migration script (flat → per-user)   | Step 10    | Medium           |
| 13   | Frontend auth flow (login, token, guards) | Step 8     | Large            |
| 14   | Electron auth integration                 | Step 13    | Small            |
| 15   | Test full flow + data isolation           | Steps 1-14 | Medium           |

---

## 12. Risks and Mitigations

| Risk                                         | Impact                     | Mitigation                                                     |
| -------------------------------------------- | -------------------------- | -------------------------------------------------------------- |
| Migration fails on existing DB               | Data loss                  | Always backup `beastvault.db` before migration                 |
| File migration breaks StoredPath             | Pokémon files inaccessible | Update DB paths in same transaction as file moves              |
| Frontend breaks without auth                 | App unusable               | Implement auth endpoints first, then frontend auth flow        |
| Electron CORS issues with JWT                | Desktop app fails          | CORS fallback already handles localhost origins                |
| PKHeX parsing unaffected                     | —                          | No changes to PkhexCoreParser — it only receives bytes         |
| Static services don't need UserId            | —                          | Only injectable services + endpoints change                    |
| System tags conflict with existing user tags | Duplicate names            | Assign existing tags to admin first, then seed system tags     |
| Large DB migration on thousands of Pokémon   | Slow startup               | SQLite UPDATE with WHERE is fast; file migration is sequential |

---

## 13. Testing Checklist

### Authentication

- [ ] Admin can login without password
- [ ] User with password requires correct password
- [ ] Invalid credentials return 401
- [ ] Token contains correct claims (UserId, Username, Role)
- [ ] Expired token returns 401
- [ ] Protected endpoint without token returns 401

### Data Isolation

- [ ] User A cannot see User B's Pokémon
- [ ] User A cannot delete User B's files
- [ ] User A cannot modify User B's tags
- [ ] Import assigns Pokémon to authenticated user
- [ ] Scan only reads from user's directory
- [ ] Both users see system tags
- [ ] User tags are isolated per user

### Migration

- [ ] Existing Pokémon assigned to admin user after migration
- [ ] Existing files moved to `users/admin/` directory
- [ ] Existing tags assigned to admin user
- [ ] System tags created with NULL UserId
- [ ] StoredPath updated in database after file move
- [ ] App starts successfully after migration

### Admin Operations

- [ ] Only admin can access `/maintenance/*`
- [ ] Only admin can access `/config/*`
- [ ] Only admin can wipe database
- [ ] Admin can see all users
- [ ] Admin cannot delete the default user
- [ ] Standard user cannot access admin endpoints

### Desktop (Electron)

- [ ] Login screen appears on first launch
- [ ] Token persists across app restarts
- [ ] All API calls include Authorization header
- [ ] File scan works with user-specific directory
