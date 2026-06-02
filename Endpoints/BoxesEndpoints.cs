using BeastVault.Api.Application.Interfaces;
using BeastVault.Api.Contracts;
using BeastVault.Api.Domain.Entities;
using BeastVault.Api.Helpers;
using BeastVault.Api.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace BeastVault.Api.Endpoints;

public static class BoxesEndpoints
{
    private const int BoxSize = 30;

    public static IEndpointRouteBuilder MapBoxesEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/boxes", async (AppDbContext db, HttpContext ctx) =>
        {
            var userId = ctx.GetUserId();
            if (userId == null) return Results.Unauthorized();

            await EnsureDefaultBoxesAsync(db, userId.Value);
            var boxes = await GetBoxSummariesAsync(db, userId.Value);
            return Results.Ok(boxes);
        })
        .WithName("GetPokemonBoxes")
        .WithTags("Boxes")
        .RequireAuthorization();

        app.MapPost("/boxes", async (CreatePokemonBoxRequest request, AppDbContext db, HttpContext ctx) =>
        {
            var userId = ctx.GetUserId();
            if (userId == null) return Results.Unauthorized();

            var nextSortOrder = await db.PokemonBoxes
                .Where(b => b.UserId == userId.Value)
                .Select(b => (int?)b.SortOrder)
                .MaxAsync() ?? 0;

            var box = new PokemonBoxEntity
            {
                UserId = userId.Value,
                Name = NormalizeBoxName(request.Name, nextSortOrder + 1),
                SortOrder = nextSortOrder + 1,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            db.PokemonBoxes.Add(box);
            await db.SaveChangesAsync();

            return Results.Created($"/boxes/{box.Id}", new PokemonBoxSummaryDto
            {
                Id = box.Id,
                Name = box.Name,
                SortOrder = box.SortOrder,
                PokemonCount = 0
            });
        })
        .WithName("CreatePokemonBox")
        .WithTags("Boxes")
        .RequireAuthorization();

        app.MapGet("/boxes/{id:int}", async (int id, AppDbContext db, IPokemonService pokemonService, HttpContext ctx) =>
        {
            var userId = ctx.GetUserId();
            if (userId == null) return Results.Unauthorized();

            await EnsureDefaultBoxesAsync(db, userId.Value);
            var detail = await GetBoxDetailAsync(db, pokemonService, userId.Value, id);
            return detail is null ? Results.NotFound() : Results.Ok(detail);
        })
        .WithName("GetPokemonBox")
        .WithTags("Boxes")
        .RequireAuthorization();

        app.MapPatch("/boxes/{id:int}", async (int id, UpdatePokemonBoxRequest request, AppDbContext db, HttpContext ctx) =>
        {
            var userId = ctx.GetUserId();
            if (userId == null) return Results.Unauthorized();

            var box = await db.PokemonBoxes.FirstOrDefaultAsync(b => b.Id == id && b.UserId == userId.Value);
            if (box == null) return Results.NotFound();

            if (!string.IsNullOrWhiteSpace(request.Name))
                box.Name = request.Name.Trim();
            if (request.SortOrder.HasValue)
                box.SortOrder = request.SortOrder.Value;
            box.UpdatedAt = DateTime.UtcNow;

            await db.SaveChangesAsync();
            return Results.NoContent();
        })
        .WithName("UpdatePokemonBox")
        .WithTags("Boxes")
        .RequireAuthorization();

        app.MapDelete("/boxes/{id:int}", async (int id, AppDbContext db, HttpContext ctx) =>
        {
            var userId = ctx.GetUserId();
            if (userId == null) return Results.Unauthorized();

            var box = await db.PokemonBoxes.FirstOrDefaultAsync(b => b.Id == id && b.UserId == userId.Value);
            if (box == null) return Results.NotFound();

            var hasPokemon = await db.PokemonBoxSlots.AnyAsync(s => s.BoxId == id);
            if (hasPokemon) return Results.Conflict("Only empty boxes can be deleted.");

            db.PokemonBoxes.Remove(box);
            await db.SaveChangesAsync();
            return Results.NoContent();
        })
        .WithName("DeletePokemonBox")
        .WithTags("Boxes")
        .RequireAuthorization();

        app.MapPost("/boxes/move", async (MovePokemonBoxSlotRequest request, AppDbContext db, IPokemonService pokemonService, HttpContext ctx) =>
        {
            var userId = ctx.GetUserId();
            if (userId == null) return Results.Unauthorized();
            if (request.TargetSlotIndex is < 0 or >= BoxSize)
                return Results.BadRequest($"TargetSlotIndex must be between 0 and {BoxSize - 1}.");

            await EnsureDefaultBoxesAsync(db, userId.Value);

            await using var tx = await db.Database.BeginTransactionAsync();

            var targetBox = await db.PokemonBoxes
                .FirstOrDefaultAsync(b => b.Id == request.TargetBoxId && b.UserId == userId.Value);
            if (targetBox == null) return Results.NotFound("Target box not found.");

            var pokemonExists = await db.Pokemon
                .AnyAsync(p => p.Id == request.PokemonId && p.UserId == userId.Value);
            if (!pokemonExists) return Results.NotFound("Pokemon not found.");

            var sourceSlot = await db.PokemonBoxSlots
                .Include(s => s.Box)
                .FirstOrDefaultAsync(s => s.PokemonId == request.PokemonId && s.Box.UserId == userId.Value);

            var targetSlot = await db.PokemonBoxSlots
                .Include(s => s.Box)
                .FirstOrDefaultAsync(s =>
                    s.BoxId == request.TargetBoxId &&
                    s.SlotIndex == request.TargetSlotIndex &&
                    s.Box.UserId == userId.Value);

            if (sourceSlot?.BoxId == request.TargetBoxId && sourceSlot.SlotIndex == request.TargetSlotIndex)
            {
                var unchanged = await GetBoxDetailAsync(db, pokemonService, userId.Value, request.TargetBoxId);
                return Results.Ok(unchanged);
            }

            var previousBoxId = sourceSlot?.BoxId;
            var previousSlotIndex = sourceSlot?.SlotIndex;
            var displacedPokemonId = targetSlot?.PokemonId;

            var slotsToRemove = new List<PokemonBoxSlotEntity>();
            if (sourceSlot != null) slotsToRemove.Add(sourceSlot);
            if (targetSlot != null && (sourceSlot == null || targetSlot.PokemonId != sourceSlot.PokemonId))
                slotsToRemove.Add(targetSlot);

            if (slotsToRemove.Count > 0)
            {
                db.PokemonBoxSlots.RemoveRange(slotsToRemove);
                await db.SaveChangesAsync();
            }

            db.PokemonBoxSlots.Add(new PokemonBoxSlotEntity
            {
                BoxId = request.TargetBoxId,
                SlotIndex = request.TargetSlotIndex,
                PokemonId = request.PokemonId,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            });

            if (displacedPokemonId.HasValue && previousBoxId.HasValue && previousSlotIndex.HasValue)
            {
                db.PokemonBoxSlots.Add(new PokemonBoxSlotEntity
                {
                    BoxId = previousBoxId.Value,
                    SlotIndex = previousSlotIndex.Value,
                    PokemonId = displacedPokemonId.Value,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                });
            }

            targetBox.UpdatedAt = DateTime.UtcNow;
            await db.SaveChangesAsync();
            await tx.CommitAsync();

            var detail = await GetBoxDetailAsync(db, pokemonService, userId.Value, request.TargetBoxId);
            return Results.Ok(detail);
        })
        .WithName("MovePokemonBoxSlot")
        .WithTags("Boxes")
        .RequireAuthorization();

        app.MapDelete("/boxes/{boxId:int}/slots/{slotIndex:int}", async (int boxId, int slotIndex, AppDbContext db, HttpContext ctx) =>
        {
            var userId = ctx.GetUserId();
            if (userId == null) return Results.Unauthorized();
            if (slotIndex is < 0 or >= BoxSize)
                return Results.BadRequest($"SlotIndex must be between 0 and {BoxSize - 1}.");

            var slot = await db.PokemonBoxSlots
                .Include(s => s.Box)
                .FirstOrDefaultAsync(s => s.BoxId == boxId && s.SlotIndex == slotIndex && s.Box.UserId == userId.Value);
            if (slot == null) return Results.NotFound();

            db.PokemonBoxSlots.Remove(slot);
            await db.SaveChangesAsync();
            return Results.NoContent();
        })
        .WithName("ClearPokemonBoxSlot")
        .WithTags("Boxes")
        .RequireAuthorization();

        app.MapDelete("/boxes/{boxId:int}/slots", async (int boxId, AppDbContext db, HttpContext ctx) =>
        {
            var userId = ctx.GetUserId();
            if (userId == null) return Results.Unauthorized();

            var box = await db.PokemonBoxes.FirstOrDefaultAsync(b => b.Id == boxId && b.UserId == userId.Value);
            if (box == null) return Results.NotFound();

            var slots = await db.PokemonBoxSlots.Where(s => s.BoxId == boxId).ToListAsync();
            db.PokemonBoxSlots.RemoveRange(slots);
            box.UpdatedAt = DateTime.UtcNow;
            await db.SaveChangesAsync();
            return Results.NoContent();
        })
        .WithName("ClearAllPokemonBoxSlots")
        .WithTags("Boxes")
        .RequireAuthorization();

        return app;
    }

    private static async Task EnsureDefaultBoxesAsync(AppDbContext db, int userId)
    {
        if (await db.PokemonBoxes.AnyAsync(b => b.UserId == userId))
            return;

        var pokemonIds = await db.Pokemon
            .AsNoTracking()
            .Where(p => p.UserId == userId)
            .OrderBy(p => p.SpeciesId)
            .ThenBy(p => p.Form)
            .ThenByDescending(p => p.IsShiny)
            .ThenBy(p => p.Id)
            .Select(p => p.Id)
            .ToListAsync();

        var boxCount = Math.Max(1, (int)Math.Ceiling(pokemonIds.Count / (double)BoxSize));
        var boxes = Enumerable.Range(1, boxCount)
            .Select(i => new PokemonBoxEntity
            {
                UserId = userId,
                Name = $"Box {i}",
                SortOrder = i,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            })
            .ToList();

        db.PokemonBoxes.AddRange(boxes);
        await db.SaveChangesAsync();

        var slots = new List<PokemonBoxSlotEntity>();
        for (var i = 0; i < pokemonIds.Count; i++)
        {
            slots.Add(new PokemonBoxSlotEntity
            {
                BoxId = boxes[i / BoxSize].Id,
                SlotIndex = i % BoxSize,
                PokemonId = pokemonIds[i],
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            });
        }

        if (slots.Count > 0)
        {
            db.PokemonBoxSlots.AddRange(slots);
            await db.SaveChangesAsync();
        }
    }

    private static async Task<List<PokemonBoxSummaryDto>> GetBoxSummariesAsync(AppDbContext db, int userId)
    {
        return await db.PokemonBoxes
            .AsNoTracking()
            .Where(b => b.UserId == userId)
            .OrderBy(b => b.SortOrder)
            .ThenBy(b => b.Id)
            .Select(b => new PokemonBoxSummaryDto
            {
                Id = b.Id,
                Name = b.Name,
                SortOrder = b.SortOrder,
                PokemonCount = b.Slots.Count
            })
            .ToListAsync();
    }

    private static async Task<PokemonBoxDetailDto?> GetBoxDetailAsync(
        AppDbContext db,
        IPokemonService pokemonService,
        int userId,
        int boxId)
    {
        var box = await db.PokemonBoxes
            .AsNoTracking()
            .Where(b => b.Id == boxId && b.UserId == userId)
            .Select(b => new { b.Id, b.Name, b.SortOrder, PokemonCount = b.Slots.Count })
            .FirstOrDefaultAsync();
        if (box == null) return null;

        var slotRows = await db.PokemonBoxSlots
            .AsNoTracking()
            .Where(s => s.BoxId == boxId)
            .OrderBy(s => s.SlotIndex)
            .Select(s => new { s.SlotIndex, s.PokemonId })
            .ToListAsync();

        var pokemonIds = slotRows.Select(s => s.PokemonId).Distinct().ToArray();
        var pokemonById = new Dictionary<int, PokemonListItemDto>();
        if (pokemonIds.Length > 0)
        {
            var list = await pokemonService.GetPokemonListAsync(userId, new AdvancedPokemonQuery
            {
                PokemonIds = pokemonIds,
                Skip = 0,
                Take = Math.Min(500, pokemonIds.Length)
            });
            pokemonById = list.Items.ToDictionary(p => p.Id);
        }

        var slots = slotRows
            .Where(s => pokemonById.ContainsKey(s.PokemonId))
            .Select(s => new PokemonBoxSlotDto
            {
                SlotIndex = s.SlotIndex,
                Pokemon = pokemonById[s.PokemonId]
            })
            .ToList();

        return new PokemonBoxDetailDto
        {
            Id = box.Id,
            Name = box.Name,
            SortOrder = box.SortOrder,
            PokemonCount = box.PokemonCount,
            Slots = slots
        };
    }

    private static string NormalizeBoxName(string? name, int number)
    {
        var trimmed = name?.Trim();
        return string.IsNullOrWhiteSpace(trimmed) ? $"Box {number}" : trimmed;
    }
}
