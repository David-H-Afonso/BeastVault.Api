using System.Text.Json;
using BeastVault.Api.Application.Services;
using BeastVault.Api.Contracts;
using BeastVault.Api.Helpers;

namespace BeastVault.Api.Endpoints;

public static class DexHuntEndpoints
{
    private static readonly JsonSerializerOptions ExportJsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };

    public static IEndpointRouteBuilder MapDexHuntEndpoints(this IEndpointRouteBuilder app)
    {
        var hunts = app.MapGroup("/dex-hunts")
            .WithTags("DexHunts")
            .RequireAuthorization("NormalUserOnly");

        hunts.MapGet("/games", () => Results.Ok(DexHuntService.GetGames()))
            .WithName("GetDexHuntGames");

        hunts.MapGet("", async (DexHuntService service, HttpContext context) =>
        {
            var userId = context.GetUserId();
            return userId is null ? Results.Unauthorized() : Results.Ok(await service.GetListsAsync(userId.Value));
        }).WithName("GetDexHunts");

        hunts.MapPost("", async (CreateDexHuntListRequest request, DexHuntService service, HttpContext context) =>
        {
            var userId = context.GetUserId();
            if (userId is null) return Results.Unauthorized();
            var created = await service.CreateListAsync(userId.Value, request);
            return Results.Created($"/dex-hunts/{created.Id}", created);
        }).WithName("CreateDexHunt");

        hunts.MapGet("/{id:int}", async (
            int id,
            DexHuntService service,
            HttpContext context,
            string? search = null,
            string status = "all",
            int? priority = null,
            int? generation = null,
            string? type = null,
            string sortBy = "manual",
            bool descending = false) =>
        {
            var userId = context.GetUserId();
            return userId is null
                ? Results.Unauthorized()
                : Results.Ok(await service.GetListAsync(userId.Value, id, search, status, priority, generation, type, sortBy, descending));
        }).WithName("GetDexHunt");

        hunts.MapPatch("/{id:int}", async (int id, UpdateDexHuntListRequest request, DexHuntService service, HttpContext context) =>
        {
            var userId = context.GetUserId();
            return userId is null
                ? Results.Unauthorized()
                : Results.Ok(await service.UpdateListAsync(userId.Value, id, request));
        }).WithName("UpdateDexHunt");

        hunts.MapDelete("/{id:int}", async (int id, DexHuntService service, HttpContext context) =>
        {
            var userId = context.GetUserId();
            if (userId is null) return Results.Unauthorized();
            await service.DeleteListAsync(userId.Value, id);
            return Results.NoContent();
        }).WithName("DeleteDexHunt");

        hunts.MapPut("/reorder", async (ReorderDexHuntListsRequest request, DexHuntService service, HttpContext context) =>
        {
            var userId = context.GetUserId();
            if (userId is null) return Results.Unauthorized();
            await service.ReorderListsAsync(userId.Value, request.ListIds);
            return Results.NoContent();
        }).WithName("ReorderDexHunts");

        hunts.MapPost("/{id:int}/items", async (int id, AddDexHuntItemRequest request, DexHuntService service, HttpContext context) =>
        {
            var userId = context.GetUserId();
            if (userId is null) return Results.Unauthorized();
            var created = await service.AddItemAsync(userId.Value, id, request);
            return Results.Created($"/dex-hunts/{id}/items/{created.Id}", created);
        }).WithName("AddDexHuntTarget");

        hunts.MapPatch("/{id:int}/items/{itemId:int}", async (int id, int itemId, UpdateDexHuntItemRequest request, DexHuntService service, HttpContext context) =>
        {
            var userId = context.GetUserId();
            if (userId is null) return Results.Unauthorized();
            await service.UpdateItemAsync(userId.Value, id, itemId, request);
            return Results.NoContent();
        }).WithName("UpdateDexHuntTarget");

        hunts.MapDelete("/{id:int}/items/{itemId:int}", async (int id, int itemId, DexHuntService service, HttpContext context) =>
        {
            var userId = context.GetUserId();
            if (userId is null) return Results.Unauthorized();
            await service.DeleteItemAsync(userId.Value, id, itemId);
            return Results.NoContent();
        }).WithName("DeleteDexHuntTarget");

        hunts.MapPut("/{id:int}/items/reorder", async (int id, ReorderDexHuntItemsRequest request, DexHuntService service, HttpContext context) =>
        {
            var userId = context.GetUserId();
            if (userId is null) return Results.Unauthorized();
            await service.ReorderItemsAsync(userId.Value, id, request.ItemIds);
            return Results.NoContent();
        }).WithName("ReorderDexHuntTargets");

        hunts.MapGet("/{id:int}/export", async (int id, DexHuntService service, HttpContext context) =>
        {
            var userId = context.GetUserId();
            if (userId is null) return Results.Unauthorized();
            var export = await service.ExportAsync(userId.Value, id);
            var filename = $"dex-hunt-{Slugify(export.List.Name)}.json";
            return Results.File(JsonSerializer.SerializeToUtf8Bytes(export, ExportJsonOptions), "application/json", filename);
        }).WithName("ExportDexHunt");

        hunts.MapPost("/import", async (DexHuntExportDto export, DexHuntService service, HttpContext context) =>
        {
            var userId = context.GetUserId();
            if (userId is null) return Results.Unauthorized();
            var created = await service.ImportAsync(userId.Value, export);
            return Results.Created($"/dex-hunts/{created.Id}", created);
        }).WithName("ImportDexHunt");

        return app;
    }

    private static string Slugify(string value)
    {
        var slug = new string(value.ToLowerInvariant().Select(character => char.IsLetterOrDigit(character) ? character : '-').ToArray());
        return string.Join('-', slug.Split('-', StringSplitOptions.RemoveEmptyEntries)).Trim('-');
    }
}
