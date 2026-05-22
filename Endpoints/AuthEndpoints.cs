using System.Security.Claims;
using BeastVault.Api.Contracts;
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
    }
}
