using BeastVault.Api.Application.Interfaces;
using BeastVault.Api.Contracts;
using BeastVault.Api.Helpers;
using BeastVault.Api.Security;

namespace BeastVault.Api.Endpoints;

public static class HouseholdIntegrationEndpoints
{
    public static IEndpointRouteBuilder MapHouseholdIntegrationEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/integrations/household/v1")
            .WithTags("Household Integration");

        group.MapPost("/authorize", async (
            HouseholdAuthorizeRequest request,
            HttpContext context,
            IHouseholdIntegrationService service,
            CancellationToken cancellationToken) =>
        {
            var userId = context.GetUserId();
            if (userId is null)
            {
                return Results.Unauthorized();
            }

            var result = await service.AuthorizeAsync(userId.Value, request, cancellationToken);
            return result.Succeeded
                ? Results.Ok(result.Value)
                : Results.BadRequest(new { error = result.Error, errorDescription = result.ErrorDescription });
        })
        .RequireAuthorization("NormalUserOnly")
        .RequireRateLimiting("household-authorize")
        .Produces<HouseholdAuthorizeResponse>()
        .Produces(StatusCodes.Status400BadRequest)
        .Produces(StatusCodes.Status401Unauthorized);

        group.MapPost("/token", async (
            HouseholdTokenRequest request,
            IHouseholdIntegrationService service,
            CancellationToken cancellationToken) =>
        {
            var result = await service.TokenAsync(request, cancellationToken);
            return result.Succeeded
                ? Results.Ok(result.Value)
                : Results.BadRequest(new { error = result.Error, errorDescription = result.ErrorDescription });
        })
        .AllowAnonymous()
        .RequireRateLimiting("household-token")
        .Produces<HouseholdTokenResponse>()
        .Produces(StatusCodes.Status400BadRequest);

        group.MapPost("/revoke", async (
            HouseholdRevokeRequest request,
            IHouseholdIntegrationService service,
            CancellationToken cancellationToken) =>
        {
            await service.RevokeAsync(request, cancellationToken);
            return Results.NoContent();
        })
        .AllowAnonymous()
        .RequireRateLimiting("household-token")
        .Produces(StatusCodes.Status204NoContent);

        group.MapGet("/me", async (
            HttpContext context,
            IHouseholdIntegrationService service,
            CancellationToken cancellationToken) =>
        {
            var connectionValue = context.User.FindFirst(HouseholdIntegrationDefaults.ConnectionIdClaim)?.Value;
            if (!Guid.TryParse(connectionValue, out var connectionId))
            {
                return Results.Unauthorized();
            }

            var response = await service.GetMeAsync(connectionId, cancellationToken);
            return response is null ? Results.Unauthorized() : Results.Ok(response);
        })
        .RequireAuthorization("HouseholdProfileReadPolicy")
        .Produces<HouseholdMeResponse>()
        .Produces(StatusCodes.Status401Unauthorized);

        group.MapGet("/pokemon/{pokemonId:int}/download", async (
            int pokemonId,
            HttpContext context,
            IHouseholdIntegrationService service,
            CancellationToken cancellationToken) =>
        {
            var userId = context.GetUserId();
            if (userId is null)
            {
                return Results.Unauthorized();
            }

            var download = await service.GetPokemonDownloadAsync(
                userId.Value,
                pokemonId,
                cancellationToken);
            return download is null
                ? Results.NotFound()
                : Results.File(download.Content, "application/octet-stream", download.FileName);
        })
        .RequireAuthorization("HouseholdPokemonDownloadPolicy")
        .WithName("DownloadHouseholdPokemon")
        .WithSummary("Download an owned Pokemon file through the Household integration")
        .Produces<byte[]>(StatusCodes.Status200OK, "application/octet-stream")
        .Produces(StatusCodes.Status401Unauthorized)
        .Produces(StatusCodes.Status403Forbidden)
        .Produces(StatusCodes.Status404NotFound);

        return app;
    }
}
