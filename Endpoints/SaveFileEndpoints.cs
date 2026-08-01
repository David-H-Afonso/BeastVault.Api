using BeastVault.Api.Application.Services;
using BeastVault.Api.Contracts;
using BeastVault.Api.Helpers;
using Microsoft.AspNetCore.Mvc;

namespace BeastVault.Api.Endpoints;

public static class SaveFileEndpoints
{
    private const long MaxSaveFileSize = 128 * 1024 * 1024;
    private const int MaxTitleLength = 120;
    private const int MaxNotesLength = 4000;

    public static IEndpointRouteBuilder MapSaveFileEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/saves")
            .WithTags("Save files")
            .RequireAuthorization("NormalUserOnly");

        group.MapPost("/",
            [Consumes("multipart/form-data")]
            async ([FromForm] IFormFileCollection files, SaveFileService service, HttpContext context, CancellationToken cancellationToken) =>
            {
                var userId = context.GetUserId();
                if (userId is null) return Results.Unauthorized();
                if (files.Count == 0) return Results.BadRequest("No save files provided.");

                var results = new List<SaveFileUploadResultDto>();
                foreach (var file in files)
                {
                    if (file.Length > MaxSaveFileSize)
                    {
                        results.Add(new SaveFileUploadResultDto(
                            file.FileName,
                            "error",
                            Message: "Save files cannot exceed 128 MB."));
                        continue;
                    }

                    await using var stream = new MemoryStream();
                    await file.CopyToAsync(stream, cancellationToken);
                    results.Add(await service.UploadAsync(
                        userId.Value,
                        Path.GetFileName(file.FileName),
                        stream.ToArray(),
                        cancellationToken));
                }

                return Results.Ok(results);
            })
            .WithName("UploadSaveFiles")
            .WithSummary("Upload and inspect Pokémon save files")
            .Accepts<IFormFileCollection>("multipart/form-data")
            .Produces<IReadOnlyList<SaveFileUploadResultDto>>()
            .DisableAntiforgery();

        group.MapGet("/", async (SaveFileService service, HttpContext context, CancellationToken cancellationToken) =>
            {
                var userId = context.GetUserId();
                if (userId is null) return Results.Unauthorized();
                return Results.Ok(await service.GetAllAsync(userId.Value, cancellationToken));
            })
            .WithName("GetSaveFiles")
            .Produces<IReadOnlyList<SaveFileSummaryDto>>();

        group.MapGet("/{id:int}", async (int id, SaveFileService service, HttpContext context, CancellationToken cancellationToken) =>
            {
                var userId = context.GetUserId();
                if (userId is null) return Results.Unauthorized();
                var save = await service.GetDetailAsync(userId.Value, id, cancellationToken);
                return save is null ? Results.NotFound() : Results.Ok(save);
            })
            .WithName("GetSaveFile")
            .Produces<SaveFileDetailDto>()
            .Produces(404);

        group.MapPatch("/{id:int}", async (
                int id,
                UpdateSaveFileRequest request,
                SaveFileService service,
                HttpContext context,
                CancellationToken cancellationToken) =>
            {
                var userId = context.GetUserId();
                if (userId is null) return Results.Unauthorized();
                var title = NormalizeOptionalText(request.Title);
                var notes = NormalizeOptionalText(request.Notes);
                if (title?.Length > MaxTitleLength)
                    return Results.BadRequest($"Save titles cannot exceed {MaxTitleLength} characters.");
                if (notes?.Length > MaxNotesLength)
                    return Results.BadRequest($"Save notes cannot exceed {MaxNotesLength} characters.");

                return await service.UpdateMetadataAsync(userId.Value, id, title, notes, cancellationToken)
                    ? Results.NoContent()
                    : Results.NotFound();
            })
            .WithName("UpdateSaveFile")
            .Produces(204)
            .Produces(400)
            .Produces(404);

        group.MapPost("/{id:int}/import", async (
                int id,
                ImportSavePokemonRequest request,
                SaveFileService service,
                HttpContext context,
                CancellationToken cancellationToken) =>
            {
                var userId = context.GetUserId();
                if (userId is null) return Results.Unauthorized();
                if (request.PreviewIds is null || request.PreviewIds.Count == 0)
                    return Results.BadRequest("Select at least one Pokémon.");

                var result = await service.ImportPokemonAsync(
                    userId.Value,
                    id,
                    request.PreviewIds,
                    cancellationToken);
                return result is null ? Results.NotFound() : Results.Ok(result);
            })
            .WithName("ImportPokemonFromSave")
            .Produces<IReadOnlyList<SavePokemonImportResultDto>>()
            .Produces(400)
            .Produces(404);

        group.MapGet("/{id:int}/download", async (
                int id,
                SaveFileService service,
                HttpContext context,
                CancellationToken cancellationToken) =>
            {
                var userId = context.GetUserId();
                if (userId is null) return Results.Unauthorized();
                var file = await service.DownloadAsync(userId.Value, id, cancellationToken);
                return file is null
                    ? Results.NotFound()
                    : Results.File(file.Value.Content, "application/octet-stream", file.Value.FileName);
            })
            .WithName("DownloadSaveFile")
            .Produces(200)
            .Produces(404);

        group.MapDelete("/{id:int}", async (
                int id,
                SaveFileService service,
                HttpContext context,
                CancellationToken cancellationToken) =>
            {
                var userId = context.GetUserId();
                if (userId is null) return Results.Unauthorized();
                return await service.DeleteAsync(userId.Value, id, cancellationToken)
                    ? Results.NoContent()
                    : Results.NotFound();
            })
            .WithName("DeleteSaveFile")
            .Produces(204)
            .Produces(404);

        return app;
    }

    private static string? NormalizeOptionalText(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
