using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using BeastVault.Api.Contracts;
using BeastVault.Api.Domain.Entities;
using BeastVault.Api.Infrastructure;
using BeastVault.Api.Infrastructure.Helpers;
using BeastVault.Api.Infrastructure.Services;

namespace BeastVault.Api.Endpoints
{
    public static class UserEndpoints
    {
        public static IEndpointRouteBuilder MapUserEndpoints(this IEndpointRouteBuilder app)
        {
            var group = app.MapGroup("/users").WithTags("Users");

            group.MapPost("/login", Login).AllowAnonymous();
            group.MapGet("/me", GetCurrentUser).RequireAuthorization();
            group.MapGet("/", ListUsers).RequireAuthorization("AdminOnly");
            group.MapPost("/", Register).RequireAuthorization("AdminOnly");
            group.MapPut("/{id:int}", UpdateUser).RequireAuthorization();
            group.MapDelete("/{id:int}", DeleteUser).RequireAuthorization("AdminOnly");

            return app;
        }

        private static async Task<IResult> Login(LoginRequest request, AuthService auth)
        {
            var result = await auth.AuthenticateAsync(request.Username, request.Password);
            if (result == null)
                return Results.Unauthorized();

            var (user, token) = result.Value;

            return Results.Ok(new LoginResponse
            {
                Token = token,
                User = new UserDto
                {
                    Id = user.Id,
                    Username = user.Username,
                    Role = user.Role.ToString(),
                    IsDefault = user.IsDefault,
                    HasPassword = user.PasswordHash != null
                }
            });
        }

        private static async Task<IResult> GetCurrentUser(HttpContext context, AppDbContext db)
        {
            var userId = context.GetUserId();
            if (userId == null)
                return Results.Unauthorized();

            var user = await db.Users.FindAsync(userId.Value);
            if (user == null)
                return Results.NotFound();

            return Results.Ok(new UserDto
            {
                Id = user.Id,
                Username = user.Username,
                Role = user.Role.ToString(),
                IsDefault = user.IsDefault,
                HasPassword = user.PasswordHash != null
            });
        }

        private static async Task<IResult> ListUsers(AppDbContext db)
        {
            var users = await db.Users
                .OrderBy(u => u.Id)
                .Select(u => new UserDto
                {
                    Id = u.Id,
                    Username = u.Username,
                    Role = u.Role.ToString(),
                    IsDefault = u.IsDefault,
                    HasPassword = u.PasswordHash != null
                })
                .ToListAsync();

            return Results.Ok(users);
        }

        private static async Task<IResult> Register(
            RegisterRequest request, AppDbContext db, AuthService auth)
        {
            var exists = await db.Users
                .AnyAsync(u => u.Username.ToLower() == request.Username.ToLower());

            if (exists)
                return Results.Conflict(new { message = $"Username '{request.Username}' already exists" });

            var user = new UserEntity
            {
                Username = request.Username,
                PasswordHash = string.IsNullOrEmpty(request.Password)
                    ? null
                    : auth.HashPassword(request.Password),
                Role = Enum.TryParse<UserRole>(request.Role, true, out var parsedRole)
                    ? parsedRole
                    : UserRole.Standard,
                IsDefault = false
            };

            db.Users.Add(user);
            await db.SaveChangesAsync();

            return Results.Created($"/users/{user.Id}", new UserDto
            {
                Id = user.Id,
                Username = user.Username,
                Role = user.Role.ToString(),
                IsDefault = user.IsDefault,
                HasPassword = user.PasswordHash != null
            });
        }

        private static async Task<IResult> UpdateUser(
            int id, UpdateUserRequest request, HttpContext context, AppDbContext db, AuthService auth)
        {
            var currentUserId = context.GetUserId();
            var isAdmin = context.User.IsInRole("Admin");

            if (currentUserId != id && !isAdmin)
                return Results.Forbid();

            var user = await db.Users.FindAsync(id);
            if (user == null)
                return Results.NotFound();

            if (request.Username != null)
            {
                var exists = await db.Users
                    .AnyAsync(u => u.Username.ToLower() == request.Username.ToLower() && u.Id != id);
                if (exists)
                    return Results.Conflict(new { message = $"Username '{request.Username}' already exists" });
                user.Username = request.Username;
            }

            if (request.RemovePassword == true)
            {
                user.PasswordHash = null;
            }
            else if (!string.IsNullOrEmpty(request.Password))
            {
                user.PasswordHash = auth.HashPassword(request.Password);
            }

            if (request.Role != null && isAdmin)
            {
                if (Enum.TryParse<UserRole>(request.Role, true, out var newRole))
                    user.Role = newRole;
            }

            await db.SaveChangesAsync();

            return Results.Ok(new UserDto
            {
                Id = user.Id,
                Username = user.Username,
                Role = user.Role.ToString(),
                IsDefault = user.IsDefault,
                HasPassword = user.PasswordHash != null
            });
        }

        private static async Task<IResult> DeleteUser(int id, AppDbContext db)
        {
            var user = await db.Users.FindAsync(id);
            if (user == null)
                return Results.NotFound();

            if (user.IsDefault)
                return Results.BadRequest(new { message = "Cannot delete the default admin user" });

            db.Users.Remove(user);
            await db.SaveChangesAsync();

            return Results.NoContent();
        }
    }
}
