using Microsoft.EntityFrameworkCore;
using BeastVault.Api.Contracts;
using BeastVault.Api.Infrastructure;
using BeastVault.Api.Domain.Entities;
using BeastVault.Api.Helpers;
using BeastVault.Api.Application.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace BeastVault.Api.Endpoints
{
    public static class TagEndpoints
    {
        private static readonly HashSet<string> AllowedTagImageContentTypes = new(StringComparer.OrdinalIgnoreCase)
        {
            "image/png",
            "image/jpeg",
            "image/webp",
            "image/gif"
        };

        private static string GetTagImagePhysicalPath(string imagePath)
        {
            var normalized = imagePath.Replace('\\', '/');
            if (normalized.StartsWith("/tags/", StringComparison.OrdinalIgnoreCase))
            {
                return Path.Combine("wwwroot", normalized.TrimStart('/').Replace('/', Path.DirectorySeparatorChar));
            }

            return imagePath;
        }

        private static void DeleteLocalTagImageIfExists(string? imagePath)
        {
            if (string.IsNullOrWhiteSpace(imagePath)) return;
            if (Uri.TryCreate(imagePath, UriKind.Absolute, out var uri) &&
                (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps || uri.Scheme == "data"))
            {
                return;
            }

            var physicalPath = GetTagImagePhysicalPath(imagePath);
            if (!File.Exists(physicalPath)) return;

            try
            {
                File.Delete(physicalPath);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Could not delete tag image {physicalPath}: {ex.Message}");
            }
        }

        private static TagDto ToTagDto(TagEntity tag, int pokemonCount) => new()
        {
            Id = tag.Id,
            Name = tag.Name,
            ImagePath = tag.ImagePath,
            PokemonCount = pokemonCount,
            Category = tag.Category.ToString(),
            ColorHex = tag.ColorHex,
            SortOrder = tag.SortOrder,
            Description = tag.Description
        };

        public static IEndpointRouteBuilder MapTagEndpoints(this IEndpointRouteBuilder app)
        {
            // Get all tags (user's own + system tags)
            app.MapGet("/tags", async (AppDbContext db, HttpContext ctx) =>
            {
                var userId = ctx.GetUserId();
                if (userId == null) return Results.Unauthorized();

                var tags = await db.Tags
                    .Where(t => t.UserId == null || t.UserId == userId)
                    .OrderBy(t => t.SortOrder)
                    .ThenBy(t => t.Name)
                    .Select(t => new TagDto
                    {
                        Id = t.Id,
                        Name = t.Name,
                        ImagePath = t.ImagePath,
                        PokemonCount = db.PokemonTags.Count(pt => pt.TagId == t.Id),
                        Category = t.Category.ToString(),
                        ColorHex = t.ColorHex,
                        SortOrder = t.SortOrder,
                        Description = t.Description
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
                    PokemonCount = pokemonCount,
                    Category = tag.Category.ToString(),
                    ColorHex = tag.ColorHex,
                    SortOrder = tag.SortOrder,
                    Description = tag.Description
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

                var category = TagCategory.Uncategorized;
                if (!string.IsNullOrEmpty(request.Category))
                    Enum.TryParse(request.Category, true, out category);

                var tag = new TagEntity
                {
                    Name = request.Name,
                    UserId = userId,
                    Category = category,
                    ColorHex = request.ColorHex,
                    Description = request.Description
                };

                db.Tags.Add(tag);
                await db.SaveChangesAsync();

                return Results.Created($"/tags/{tag.Id}", new TagDto
                {
                    Id = tag.Id,
                    Name = tag.Name,
                    ImagePath = tag.ImagePath,
                    PokemonCount = 0,
                    Category = tag.Category.ToString(),
                    ColorHex = tag.ColorHex,
                    SortOrder = tag.SortOrder,
                    Description = tag.Description
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

                if (request.Category != null && Enum.TryParse<TagCategory>(request.Category, true, out var cat))
                    tag.Category = cat;
                if (request.ColorHex != null)
                    tag.ColorHex = request.ColorHex;
                if (request.SortOrder.HasValue)
                    tag.SortOrder = request.SortOrder.Value;
                if (request.Description != null)
                    tag.Description = request.Description;

                await db.SaveChangesAsync();

                var pokemonCount = await db.PokemonTags.CountAsync(pt => pt.TagId == id);

                return Results.Ok(new TagDto
                {
                    Id = tag.Id,
                    Name = tag.Name,
                    ImagePath = tag.ImagePath,
                    PokemonCount = pokemonCount,
                    Category = tag.Category.ToString(),
                    ColorHex = tag.ColorHex,
                    SortOrder = tag.SortOrder,
                    Description = tag.Description
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

                DeleteLocalTagImageIfExists(tag.ImagePath);

                // Remove the tag
                db.Tags.Remove(tag);
                await db.SaveChangesAsync();

                return Results.NoContent();
            }).WithTags("Tags").RequireAuthorization();

            // Upload tag image
            app.MapPost("/tags/{id:int}/image", async (int id, [FromForm] IFormFile file, AppDbContext db, HttpContext ctx) =>
            {
                var userId = ctx.GetUserId();
                if (userId == null) return Results.Unauthorized();

                var tag = await db.Tags.FirstOrDefaultAsync(t => t.Id == id && t.UserId == userId);
                if (tag == null)
                    return Results.NotFound();

                if (file.Length == 0)
                    return Results.BadRequest("Image file is empty");

                if (!AllowedTagImageContentTypes.Contains(file.ContentType))
                    return Results.BadRequest("Only PNG, JPG, WebP, and GIF images are allowed");

                // Create tags directory if it doesn't exist
                var tagsDir = Path.Combine("wwwroot", "tags");
                Directory.CreateDirectory(tagsDir);

                DeleteLocalTagImageIfExists(tag.ImagePath);

                var extension = file.ContentType.ToLowerInvariant() switch
                {
                    "image/jpeg" => ".jpg",
                    "image/webp" => ".webp",
                    "image/gif" => ".gif",
                    _ => ".png"
                };

                // Save the new image
                var fileName = $"tag_{id}_{Guid.NewGuid():N}{extension}";
                var filePath = Path.Combine(tagsDir, fileName);

                using (var stream = File.Create(filePath))
                {
                    await file.CopyToAsync(stream);
                }

                // Update the tag
                tag.ImagePath = $"/tags/{fileName}";
                await db.SaveChangesAsync();

                var pokemonCount = await db.PokemonTags.CountAsync(pt => pt.TagId == id);

                return Results.Ok(ToTagDto(tag, pokemonCount));
            }).DisableAntiforgery().WithTags("Tags").RequireAuthorization();

            // Use a remote tag image URL
            app.MapPut("/tags/{id:int}/image-url", async (int id, TagImageUrlRequest request, AppDbContext db, HttpContext ctx) =>
            {
                var userId = ctx.GetUserId();
                if (userId == null) return Results.Unauthorized();

                var tag = await db.Tags.FirstOrDefaultAsync(t => t.Id == id && t.UserId == userId);
                if (tag == null)
                    return Results.NotFound();

                if (!Uri.TryCreate(request.ImageUrl.Trim(), UriKind.Absolute, out var uri) ||
                    (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
                {
                    return Results.BadRequest("ImageUrl must be an absolute HTTP or HTTPS URL");
                }

                DeleteLocalTagImageIfExists(tag.ImagePath);
                tag.ImagePath = uri.ToString();
                await db.SaveChangesAsync();

                var pokemonCount = await db.PokemonTags.CountAsync(pt => pt.TagId == id);
                return Results.Ok(ToTagDto(tag, pokemonCount));
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

                DeleteLocalTagImageIfExists(tag.ImagePath);

                // Update the tag
                tag.ImagePath = null;
                await db.SaveChangesAsync();

                var pokemonCount = await db.PokemonTags.CountAsync(pt => pt.TagId == id);

                return Results.Ok(ToTagDto(tag, pokemonCount));
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
                    PokemonCount = 0,
                    Category = pt.Tag.Category.ToString(),
                    ColorHex = pt.Tag.ColorHex,
                    SortOrder = pt.Tag.SortOrder,
                    Description = pt.Tag.Description
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
                        PokemonCount = 0,
                        Category = pt.Tag.Category.ToString(),
                        ColorHex = pt.Tag.ColorHex,
                        SortOrder = pt.Tag.SortOrder,
                        Description = pt.Tag.Description
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

            // Bulk tag operations
            app.MapPatch("/pokemon/tags/bulk", async (BulkTagRequest request, ITagService tagService, HttpContext ctx) =>
            {
                var userId = ctx.GetUserId();
                if (userId == null) return Results.Unauthorized();

                if (request.PokemonIds.Length == 0)
                    return Results.BadRequest("PokemonIds must not be empty");

                var result = await tagService.BulkUpdateTagsAsync(userId.Value, request);
                return Results.Ok(result);
            }).WithTags("Tags").RequireAuthorization();

            return app;
        }
    }
}
