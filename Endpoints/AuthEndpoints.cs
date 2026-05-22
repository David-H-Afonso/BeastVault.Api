using System.Security.Claims;
using BeastVault.Api.Contracts;
using BeastVault.Api.Domain.Entities;
using BeastVault.Api.Helpers;
using BeastVault.Api.Application.Interfaces;

namespace BeastVault.Api.Endpoints;

public static class AuthEndpoints
{
    public static void MapAuthEndpoints(this WebApplication app)
    {
        var anon = app.MapGroup("/auth").WithTags("Auth").AllowAnonymous();

        anon.MapPost("/login", async (LoginRequest request, IAuthService authService) =>
        {
            var result = await authService.LoginAsync(request.Username, request.Password);
            return result == null ? Results.Unauthorized() : Results.Ok(result);
        }).WithName("Login");

        anon.MapPost("/register", async (RegisterRequest request, IAuthService authService) =>
        {
            var user = await authService.CreateUserAsync(request.Username, request.Password);
            if (user == null)
                return Results.Conflict(new { message = "Username already exists" });

            var loginResponse = await authService.LoginAsync(request.Username, request.Password);
            return loginResponse == null
                ? Results.StatusCode(500)
                : Results.Ok(loginResponse);
        }).WithName("Register");

        var authed = app.MapGroup("/auth").WithTags("Auth").RequireAuthorization();

        authed.MapGet("/me", (HttpContext ctx) =>
        {
            var userId = ctx.GetUserId();
            if (userId == null) return Results.Unauthorized();
            var username = ctx.User.FindFirst(ClaimTypes.Name)?.Value ?? string.Empty;
            var role = ctx.User.FindFirst(ClaimTypes.Role)?.Value ?? "Standard";
            return Results.Ok(new MeResponse(userId.Value, username, role));
        }).WithName("Me");

        authed.MapPut("/password", async (UpdatePasswordRequest request, HttpContext ctx, IAuthService authService) =>
        {
            var userId = ctx.GetUserId();
            if (userId == null) return Results.Unauthorized();
            var success = await authService.UpdatePasswordAsync(userId.Value, request.CurrentPassword, request.NewPassword);
            return success ? Results.Ok(new { message = "Password updated" }) : Results.BadRequest(new { message = "Invalid current password" });
        }).WithName("UpdatePassword");

        // Rename own account
        authed.MapPut("/username", async (RenameUserRequest request, HttpContext ctx, IAuthService authService) =>
        {
            var userId = ctx.GetUserId();
            if (userId == null) return Results.Unauthorized();
            var success = await authService.RenameUserAsync(userId.Value, request.NewUsername);
            return success
                ? Results.Ok(new { message = "Username updated" })
                : Results.BadRequest(new { message = "Username already taken or invalid" });
        }).WithName("UpdateOwnUsername");

        // User preferences
        authed.MapGet("/preferences", async (HttpContext ctx, IAuthService authService) =>
        {
            var userId = ctx.GetUserId();
            if (userId == null) return Results.Unauthorized();
            var prefs = await authService.GetPreferencesAsync(userId.Value);
            return Results.Ok(prefs);
        }).WithName("GetPreferences");

        authed.MapPut("/preferences", async (UpdatePreferencesRequest request, HttpContext ctx, IAuthService authService) =>
        {
            var userId = ctx.GetUserId();
            if (userId == null) return Results.Unauthorized();
            var prefs = await authService.UpdatePreferencesAsync(userId.Value, request);
            return Results.Ok(prefs);
        }).WithName("UpdatePreferences");

        var admin = app.MapGroup("/auth/admin").WithTags("Auth").RequireAuthorization("AdminPolicy");

        admin.MapGet("/users", async (IAuthService authService) =>
        {
            var users = await authService.GetAllUsersAsync();
            return Results.Ok(users);
        }).WithName("GetAllUsers");

        admin.MapDelete("/users/{id:int}", async (int id, IAuthService authService) =>
        {
            var success = await authService.DeleteUserAsync(id);
            return success ? Results.NoContent() : Results.NotFound();
        }).WithName("DeleteUser");

        admin.MapPut("/users/{id:int}/password", async (int id, AdminResetPasswordRequest request, IAuthService authService) =>
        {
            var success = await authService.AdminResetPasswordAsync(id, request.NewPassword);
            return success ? Results.Ok(new { message = "Password reset" }) : Results.NotFound();
        }).WithName("AdminResetPassword");

        // Admin rename any user
        admin.MapPut("/users/{id:int}/username", async (int id, RenameUserRequest request, IAuthService authService) =>
        {
            var success = await authService.RenameUserAsync(id, request.NewUsername);
            return success
                ? Results.Ok(new { message = "Username updated" })
                : Results.BadRequest(new { message = "Username already taken or invalid" });
        }).WithName("AdminRenameUser");

        // Admin toggle role
        admin.MapPut("/users/{id:int}/role", async (int id, UpdateRoleRequest request, HttpContext ctx, IAuthService authService) =>
        {
            var requestingUserId = ctx.GetUserId();
            if (requestingUserId == null) return Results.Unauthorized();

            if (!Enum.TryParse<UserRole>(request.Role, true, out var role))
                return Results.BadRequest(new { message = "Invalid role. Use 'Admin' or 'Standard'" });

            var success = await authService.UpdateRoleAsync(requestingUserId.Value, id, role);
            return success
                ? Results.Ok(new { message = "Role updated" })
                : Results.BadRequest(new { message = "Cannot remove last admin" });
        }).WithName("AdminUpdateRole");
    }
}
