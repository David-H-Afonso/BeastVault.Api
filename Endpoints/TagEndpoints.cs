using Microsoft.EntityFrameworkCore;
using BeastVault.Api.Contracts;
using BeastVault.Api.Infrastructure;
using BeastVault.Api.Domain.Entities;
using BeastVault.Api.Helpers;

namespace BeastVault.Api.Endpoints
{
    public static class TagEndpoints
    {
        public static IEndpointRouteBuilder MapTagEndpoints(this IEndpointRouteBuilder app)
        {
            // Get all tags (user's own + system tags)
            app.MapGet("/tags", async (AppDbContext db, HttpContext ctx) =>
            {
                var userId = ctx.GetUserId();
                if (userId == null) return Results.Unauthorized();

                var tags = await db.Tags
                    .Where(t => t.UserId == null || t.UserId == userId)
                    .OrderBy(t => t.Name)
                    .Select(t => new TagDto
                    {
                        Id = t.Id,
                        Name = t.Name,
                        ImagePath = t.ImagePath,
                        PokemonCount = db.PokemonTags.Count(pt => pt.TagId == t.Id)
                    })
                    .ToListAsync();

                return Results.Ok(tags);
            }).WithTags("Tags").RequireAuthorization();

            // Get a specific tag by ID
            app.MapGet("/tags/{id:int}", async (int id, AppDbContext db, HttpContext ctx) =>
            {
                var userId = ctx.GetUserId();
                if (userId == null) return Results.Unauthorized();

                var tag = await db.Tags.FirstOrDefaultAsync(t => t.Id == id && (t.UserId == null || t.UserId == userId));
                if (tag == null)
                    return Results.NotFound();

                var pokemonCount = await db.PokemonTags.CountAsync(pt => pt.TagId == id);

                return Results.Ok(new TagDto
                {
                    Id = tag.Id,
                    Name = tag.Name,
                    ImagePath = tag.ImagePath,
                    PokemonCount = pokemonCount
                });
            }).WithTags("Tags").RequireAuthorization();

            // Create a new tag
            app.MapPost("/tags", async (CreateTagRequest request, AppDbContext db, HttpContext ctx) =>
            {
                var userId = ctx.GetUserId();
                if (userId == null) return Results.Unauthorized();

                // Check if tag with same name already exists for this user
                var existingTag = await db.Tags
                    .FirstOrDefaultAsync(t => t.Name == request.Name && t.UserId == userId);

                if (existingTag != null)
                    return Results.Conflict($"Tag with name '{request.Name}' already exists");

                var tag = new TagEntity
                {
                    Name = request.Name,
                    UserId = userId
                };

                db.Tags.Add(tag);
                await db.SaveChangesAsync();

                return Results.Created($"/tags/{tag.Id}", new TagDto
                {
                    Id = tag.Id,
                    Name = tag.Name,
                    ImagePath = tag.ImagePath,
                    PokemonCount = 0 // New tag has no Pokemon assigned yet
                });
            }).WithTags("Tags").RequireAuthorization();

            // Update an existing tag
            app.MapPut("/tags/{id:int}", async (int id, UpdateTagRequest request, AppDbContext db, HttpContext ctx) =>
            {
                var userId = ctx.GetUserId();
                if (userId == null) return Results.Unauthorized();

                var tag = await db.Tags.FirstOrDefaultAsync(t => t.Id == id && t.UserId == userId);
                if (tag == null)
                    return Results.NotFound();

                // Check if another tag with the same name already exists (case-sensitive)
                var existingTag = await db.Tags
                    .FirstOrDefaultAsync(t => t.Name == request.Name && t.Id != id && t.UserId == userId);

                if (existingTag != null)
                    return Results.Conflict($"Tag with name '{request.Name}' already exists");

                tag.Name = request.Name;
                await db.SaveChangesAsync();

                var pokemonCount = await db.PokemonTags.CountAsync(pt => pt.TagId == id);

                return Results.Ok(new TagDto
                {
                    Id = tag.Id,
                    Name = tag.Name,
                    ImagePath = tag.ImagePath,
                    PokemonCount = pokemonCount
                });
            }).WithTags("Tags").RequireAuthorization();

            // Delete a tag
            app.MapDelete("/tags/{id:int}", async (int id, AppDbContext db, HttpContext ctx) =>
            {
                var userId = ctx.GetUserId();
                if (userId == null) return Results.Unauthorized();

                var tag = await db.Tags.FirstOrDefaultAsync(t => t.Id == id && t.UserId == userId);
                if (tag == null)
                    return Results.NotFound();

                // Remove all Pokemon-Tag associations first
                var pokemonTags = await db.PokemonTags
                    .Where(pt => pt.TagId == id)
                    .ToListAsync();

                db.PokemonTags.RemoveRange(pokemonTags);

                // Delete the tag image if it exists
                if (!string.IsNullOrEmpty(tag.ImagePath) && File.Exists(tag.ImagePath))
                {
                    try
                    {
                        File.Delete(tag.ImagePath);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Could not delete tag image {tag.ImagePath}: {ex.Message}");
                    }
                }

                // Remove the tag
                db.Tags.Remove(tag);
                await db.SaveChangesAsync();

                return Results.NoContent();
            }).WithTags("Tags").RequireAuthorization();

            // Upload tag image
            app.MapPost("/tags/{id:int}/image", async (int id, IFormFile file, AppDbContext db, HttpContext ctx) =>
            {
                var userId = ctx.GetUserId();
                if (userId == null) return Results.Unauthorized();

                var tag = await db.Tags.FirstOrDefaultAsync(t => t.Id == id && t.UserId == userId);
                if (tag == null)
                    return Results.NotFound();

                // Validate file type
                if (file.ContentType != "image/png")
                    return Results.BadRequest("Only PNG images are allowed");

                // Create tags directory if it doesn't exist
                var tagsDir = Path.Combine("wwwroot", "tags");
                Directory.CreateDirectory(tagsDir);

                // Delete existing image if it exists
                if (!string.IsNullOrEmpty(tag.ImagePath) && File.Exists(tag.ImagePath))
                {
                    try
                    {
                        File.Delete(tag.ImagePath);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Could not delete existing tag image {tag.ImagePath}: {ex.Message}");
                    }
                }

                // Save the new image
                var fileName = $"tag_{id}_{Guid.NewGuid()}.png";
                var filePath = Path.Combine(tagsDir, fileName);

                using (var stream = File.Create(filePath))
                {
                    await file.CopyToAsync(stream);
                }

                // Update the tag
                tag.ImagePath = filePath;
                await db.SaveChangesAsync();

                var pokemonCount = await db.PokemonTags.CountAsync(pt => pt.TagId == id);

                return Results.Ok(new TagDto
                {
                    Id = tag.Id,
                    Name = tag.Name,
                    ImagePath = tag.ImagePath,
                    PokemonCount = pokemonCount
                });
            }).WithTags("Tags").RequireAuthorization();

            // Delete tag image
            app.MapDelete("/tags/{id:int}/image", async (int id, AppDbContext db, HttpContext ctx) =>
            {
                var userId = ctx.GetUserId();
                if (userId == null) return Results.Unauthorized();

                var tag = await db.Tags.FirstOrDefaultAsync(t => t.Id == id && t.UserId == userId);
                if (tag == null)
                    return Results.NotFound();

                if (string.IsNullOrEmpty(tag.ImagePath))
                    return Results.BadRequest("Tag has no image");

                // Delete the image file
                if (File.Exists(tag.ImagePath))
                {
                    try
                    {
                        File.Delete(tag.ImagePath);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Could not delete tag image {tag.ImagePath}: {ex.Message}");
                    }
                }

                // Update the tag
                tag.ImagePath = null;
                await db.SaveChangesAsync();

                var pokemonCount = await db.PokemonTags.CountAsync(pt => pt.TagId == id);

                return Results.Ok(new TagDto
                {
                    Id = tag.Id,
                    Name = tag.Name,
                    ImagePath = tag.ImagePath,
                    PokemonCount = pokemonCount
                });
            }).WithTags("Tags").RequireAuthorization();

            // Get tags assigned to a Pokémon
            app.MapGet("/pokemon/{id:int}/tags", async (int id, AppDbContext db, HttpContext ctx) =>
            {
                var userId = ctx.GetUserId();
                if (userId == null) return Results.Unauthorized();

                var pokemon = await db.Pokemon.FirstOrDefaultAsync(p => p.Id == id && p.UserId == userId);
                if (pokemon == null)
                    return Results.NotFound();

                var tags = await db.PokemonTags
                    .Where(pt => pt.PokemonId == id)
                    .Include(pt => pt.Tag)
                    .Select(pt => new TagDto
                    {
                        Id = pt.Tag.Id,
                        Name = pt.Tag.Name,
                        ImagePath = pt.Tag.ImagePath,
                        PokemonCount = 0 // Not relevant in this context
                    })
                    .OrderBy(t => t.Name)
                    .ToListAsync();

                return Results.Ok(tags);
            }).WithTags("Tags").RequireAuthorization();

            // Assign tags to a Pokémon (replaces all existing tags)
            // Also applies to all Pokemon from files with the same SHA256 (handles duplicates)
            app.MapPut("/pokemon/{id:int}/tags", async (int id, PokemonTagsRequest request, AppDbContext db, HttpContext ctx) =>
            {
                var userId = ctx.GetUserId();
                if (userId == null) return Results.Unauthorized();

                var pokemon = await db.Pokemon
                    .Include(p => p.File)
                    .FirstOrDefaultAsync(p => p.Id == id && p.UserId == userId);

                if (pokemon == null)
                    return Results.NotFound();

                // Validate that all tag IDs exist
                var existingTagIds = await db.Tags
                    .Where(t => request.TagIds.Contains(t.Id))
                    .Select(t => t.Id)
                    .ToListAsync();

                var invalidTagIds = request.TagIds.Except(existingTagIds).ToList();
                if (invalidTagIds.Any())
                    return Results.BadRequest($"Invalid tag IDs: {string.Join(", ", invalidTagIds)}");

                // Find all Pokemon from files with the same SHA256 (handles duplicates)
                var relatedPokemonIds = await db.Pokemon
                    .Join(db.Files, p => p.FileId, f => f.Id, (p, f) => new { Pokemon = p, File = f })
                    .Where(pf => pf.File.Sha256 == pokemon.File.Sha256)
                    .Select(pf => pf.Pokemon.Id)
                    .ToListAsync();

                // Remove existing tag assignments for all related Pokemon
                var existingPokemonTags = await db.PokemonTags
                    .Where(pt => relatedPokemonIds.Contains(pt.PokemonId))
                    .ToListAsync();

                db.PokemonTags.RemoveRange(existingPokemonTags);

                // Add new tag assignments to all related Pokemon
                var newPokemonTags = new List<PokemonTagEntity>();
                foreach (var pokemonId in relatedPokemonIds)
                {
                    foreach (var tagId in request.TagIds)
                    {
                        newPokemonTags.Add(new PokemonTagEntity
                        {
                            PokemonId = pokemonId,
                            TagId = tagId
                        });
                    }
                }

                db.PokemonTags.AddRange(newPokemonTags);
                await db.SaveChangesAsync();

                // Return the updated tags for the specific Pokemon requested
                var updatedTags = await db.PokemonTags
                    .Where(pt => pt.PokemonId == id)
                    .Include(pt => pt.Tag)
                    .Select(pt => new TagDto
                    {
                        Id = pt.Tag.Id,
                        Name = pt.Tag.Name,
                        ImagePath = pt.Tag.ImagePath,
                        PokemonCount = 0 // Not relevant in this context
                    })
                    .OrderBy(t => t.Name)
                    .ToListAsync();

                return Results.Ok(updatedTags);
            }).WithTags("Tags").RequireAuthorization();

            // Remove all tags from a Pokémon (and all Pokemon from files with same SHA256)
            app.MapDelete("/pokemon/{id:int}/tags", async (int id, AppDbContext db, HttpContext ctx) =>
            {
                var userId = ctx.GetUserId();
                if (userId == null) return Results.Unauthorized();

                var pokemon = await db.Pokemon
                    .Include(p => p.File)
                    .FirstOrDefaultAsync(p => p.Id == id && p.UserId == userId);

                if (pokemon == null)
                    return Results.NotFound();

                // Find all Pokemon from files with the same SHA256 (handles duplicates)
                var relatedPokemonIds = await db.Pokemon
                    .Join(db.Files, p => p.FileId, f => f.Id, (p, f) => new { Pokemon = p, File = f })
                    .Where(pf => pf.File.Sha256 == pokemon.File.Sha256)
                    .Select(pf => pf.Pokemon.Id)
                    .ToListAsync();

                var pokemonTags = await db.PokemonTags
                    .Where(pt => relatedPokemonIds.Contains(pt.PokemonId))
                    .ToListAsync();

                db.PokemonTags.RemoveRange(pokemonTags);
                await db.SaveChangesAsync();

                return Results.NoContent();
            }).WithTags("Tags").RequireAuthorization();

            return app;
        }
    }
}
