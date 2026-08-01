using BeastVault.Api.Application.Interfaces;
using BeastVault.Api.Application.Services;
using BeastVault.Api.Contracts;
using BeastVault.Api.Helpers;

namespace BeastVault.Api.Endpoints;

public static class TcgCollectionEndpoints
{
    public static IEndpointRouteBuilder MapTcgCollectionEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/tcg")
            .WithTags("TCG collection")
            .RequireAuthorization("NormalUserOnly");

        group.MapGet("/sets", async (
                string? search,
                TcgCollectionService service,
                HttpContext context,
                CancellationToken cancellationToken) =>
            {
                var userId = context.GetUserId();
                if (userId is null) return Results.Unauthorized();
                if (search?.Length > 100) return Results.BadRequest("Set search cannot exceed 100 characters.");
                try
                {
                    return Results.Ok(await service.GetSetsAsync(userId.Value, search, cancellationToken));
                }
                catch (HttpRequestException)
                {
                    return Results.Problem("The TCG catalog provider is temporarily unavailable and no local cache exists.", statusCode: 503);
                }
            })
            .WithName("GetTcgSets")
            .RequireRateLimiting("tcg-provider")
            .Produces<IReadOnlyList<TcgSetDto>>()
            .Produces(503);

        group.MapGet("/sets/{setProviderId}/cards", async (
                string setProviderId,
                int? page,
                int? pageSize,
                TcgCollectionService service,
                HttpContext context,
                CancellationToken cancellationToken) =>
            {
                var userId = context.GetUserId();
                if (userId is null) return Results.Unauthorized();
                if (setProviderId.Length > 100) return Results.BadRequest("Invalid set identifier.");
                try
                {
                    var result = await service.GetSetCardsAsync(userId.Value, setProviderId, page ?? 1, pageSize ?? 60, cancellationToken);
                    return result is null ? Results.NotFound() : Results.Ok(result);
                }
                catch (HttpRequestException)
                {
                    return Results.Problem("The cards for this set are not cached and the provider is unavailable.", statusCode: 503);
                }
            })
            .WithName("GetTcgSetCards")
            .RequireRateLimiting("tcg-provider")
            .Produces<TcgCardPageDto>()
            .Produces(404)
            .Produces(503);

        group.MapGet("/cards/search", async (
                string? query,
                int? setId,
                string? number,
                int? speciesId,
                int? page,
                int? pageSize,
                TcgCollectionService service,
                HttpContext context,
                CancellationToken cancellationToken) =>
            {
                var userId = context.GetUserId();
                if (userId is null) return Results.Unauthorized();
                if (query?.Length > 100 || number?.Length > 30)
                    return Results.BadRequest("Card search values are too long.");
                try
                {
                    return Results.Ok(await service.SearchCardsAsync(
                        userId.Value, query, setId, number, speciesId, page ?? 1, pageSize ?? 30, cancellationToken));
                }
                catch (HttpRequestException)
                {
                    return Results.Problem("Card search is temporarily unavailable.", statusCode: 503);
                }
            })
            .WithName("SearchTcgCards")
            .RequireRateLimiting("tcg-provider")
            .Produces<TcgCardPageDto>()
            .Produces(503);

        group.MapGet("/cards/{id:int}", async (
                int id,
                TcgCollectionService service,
                HttpContext context,
                CancellationToken cancellationToken) =>
            {
                var userId = context.GetUserId();
                if (userId is null) return Results.Unauthorized();
                var result = await service.GetCardAsync(userId.Value, id, false, cancellationToken);
                return result is null ? Results.NotFound() : Results.Ok(result);
            })
            .WithName("GetTcgCard")
            .RequireRateLimiting("tcg-provider")
            .Produces<TcgCardDto>()
            .Produces(404);

        group.MapPost("/cards/{id:int}/refresh", async (
                int id,
                TcgCollectionService service,
                HttpContext context,
                CancellationToken cancellationToken) =>
            {
                var userId = context.GetUserId();
                if (userId is null) return Results.Unauthorized();
                var result = await service.GetCardAsync(userId.Value, id, true, cancellationToken);
                return result is null ? Results.NotFound() : Results.Ok(result);
            })
            .WithName("RefreshTcgCard")
            .RequireRateLimiting("tcg-refresh")
            .Produces<TcgCardDto>()
            .Produces(404);

        group.MapGet("/species/{speciesId:int}/cards", async (
                int speciesId,
                int? page,
                int? pageSize,
                TcgCollectionService service,
                HttpContext context,
                CancellationToken cancellationToken) =>
            {
                var userId = context.GetUserId();
                if (userId is null) return Results.Unauthorized();
                if (speciesId is < 1 or > 1025) return Results.BadRequest("Invalid National Pokédex number.");
                try
                {
                    return Results.Ok(await service.GetSpeciesCardsAsync(userId.Value, speciesId, page ?? 1, pageSize ?? 60, cancellationToken));
                }
                catch (HttpRequestException)
                {
                    return Results.Problem("Species card search is temporarily unavailable.", statusCode: 503);
                }
            })
            .WithName("GetTcgSpeciesCards")
            .RequireRateLimiting("tcg-provider")
            .Produces<TcgCardPageDto>()
            .Produces(400)
            .Produces(503);

        group.MapGet("/collection", async (
                string? query,
                int? setId,
                string? language,
                string? condition,
                int? page,
                int? pageSize,
                TcgCollectionService service,
                HttpContext context,
                CancellationToken cancellationToken) =>
            {
                var userId = context.GetUserId();
                if (userId is null) return Results.Unauthorized();
                if (query?.Length > 100) return Results.BadRequest("Collection search cannot exceed 100 characters.");
                return Results.Ok(await service.GetCollectionAsync(
                    userId.Value, query, setId, language, condition, page ?? 1, pageSize ?? 60, cancellationToken));
            })
            .WithName("GetTcgCollection")
            .Produces<TcgCollectionPageDto>();

        group.MapGet("/collection/stats", async (
                TcgCollectionService service,
                HttpContext context,
                CancellationToken cancellationToken) =>
            {
                var userId = context.GetUserId();
                if (userId is null) return Results.Unauthorized();
                return Results.Ok(await service.GetStatsAsync(userId.Value, cancellationToken));
            })
            .WithName("GetTcgCollectionStats")
            .Produces<TcgCollectionStatsDto>();

        group.MapPost("/collection", async (
                AddTcgCollectionEntryRequest request,
                TcgCollectionService service,
                HttpContext context,
                CancellationToken cancellationToken) =>
            {
                var userId = context.GetUserId();
                if (userId is null) return Results.Unauthorized();
                try
                {
                    return Results.Ok(await service.AddAsync(userId.Value, request, cancellationToken));
                }
                catch (KeyNotFoundException) { return Results.NotFound(); }
                catch (ArgumentException exception) { return Results.BadRequest(exception.Message); }
            })
            .WithName("AddTcgCollectionEntry")
            .RequireRateLimiting("tcg-provider")
            .Produces<UserCardDto>()
            .Produces(400)
            .Produces(404);

        group.MapPatch("/collection/{id:int}", async (
                int id,
                UpdateTcgCollectionEntryRequest request,
                TcgCollectionService service,
                HttpContext context,
                CancellationToken cancellationToken) =>
            {
                var userId = context.GetUserId();
                if (userId is null) return Results.Unauthorized();
                try
                {
                    var result = await service.UpdateAsync(userId.Value, id, request, cancellationToken);
                    return result is null ? Results.NotFound() : Results.Ok(result);
                }
                catch (ArgumentException exception) { return Results.BadRequest(exception.Message); }
            })
            .WithName("UpdateTcgCollectionEntry")
            .Produces<UserCardDto>()
            .Produces(400)
            .Produces(404);

        group.MapDelete("/collection/{id:int}", async (
                int id,
                TcgCollectionService service,
                HttpContext context,
                CancellationToken cancellationToken) =>
            {
                var userId = context.GetUserId();
                if (userId is null) return Results.Unauthorized();
                return await service.DeleteAsync(userId.Value, id, cancellationToken)
                    ? Results.NoContent()
                    : Results.NotFound();
            })
            .WithName("DeleteTcgCollectionEntry")
            .Produces(204)
            .Produces(404);

        var preferences = app.MapGroup("/auth/preferences")
            .WithTags("Auth")
            .RequireAuthorization("NormalUserOnly");

        preferences.MapGet("/tcg-api-key", async (
                IUserApiCredentialService service,
                HttpContext context,
                CancellationToken cancellationToken) =>
            {
                var userId = context.GetUserId();
                if (userId is null) return Results.Unauthorized();
                return Results.Ok(await service.GetTcgApiKeyStatusAsync(userId.Value, cancellationToken));
            })
            .WithName("GetTcgApiKeyStatus")
            .Produces<TcgApiKeyStatusDto>();

        preferences.MapPatch("/tcg-api-key", async (
                UpdateTcgApiKeyRequest request,
                IUserApiCredentialService service,
                HttpContext context,
                CancellationToken cancellationToken) =>
            {
                var userId = context.GetUserId();
                if (userId is null) return Results.Unauthorized();
                try
                {
                    return Results.Ok(await service.SetTcgApiKeyAsync(userId.Value, request.ApiKey, cancellationToken));
                }
                catch (ArgumentException exception) { return Results.BadRequest(exception.Message); }
            })
            .WithName("UpdateTcgApiKey")
            .Produces<TcgApiKeyStatusDto>()
            .Produces(400);

        app.MapPost("/tcg/sync", async (
                bool? includeCards,
                TcgCollectionService service,
                CancellationToken cancellationToken) =>
            {
                try
                {
                    return Results.Ok(await service.SyncCatalogAsync(includeCards ?? false, cancellationToken));
                }
                catch (HttpRequestException)
                {
                    return Results.Problem("The TCG provider is temporarily unavailable.", statusCode: 503);
                }
            })
            .WithName("SyncTcgCatalog")
            .WithTags("TCG collection")
            .RequireAuthorization("AdminPolicy")
            .RequireRateLimiting("tcg-refresh")
            .Produces<TcgSyncResultDto>()
            .Produces(503);

        return app;
    }
}
