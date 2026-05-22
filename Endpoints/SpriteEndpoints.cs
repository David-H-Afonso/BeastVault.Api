namespace BeastVault.Api.Endpoints;

public static class SpriteEndpoints
{
    public static void MapSpriteEndpoints(this WebApplication app)
    {
        app.MapGet("/custom-sprites/search/{pattern}", (string pattern) =>
        {
            var assetsPath = ResolveAssetsPath();
            if (assetsPath == null)
                return Results.NotFound();

            try
            {
                var cleanPattern = Path.GetFileName(pattern);
                var matchingFiles = Directory.GetFiles(assetsPath, cleanPattern + "*");

                if (matchingFiles.Length > 0)
                {
                    var filename = Path.GetFileName(matchingFiles[0]);
                    return Results.Json(new { fileName = filename, url = $"/custom-sprites/{filename}" });
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error searching for sprite pattern '{pattern}': {ex.Message}");
            }

            return Results.NotFound();
        })
        .WithName("SearchCustomSprite")
        .WithTags("Files")
        .Produces(200)
        .Produces(404);

        app.MapGet("/custom-sprites/{fileName}", (string fileName) =>
        {
            var assetsPath = ResolveAssetsPath();

            if (assetsPath == null)
            {
                Console.WriteLine($"❌ Assets folder not found.");
                return Results.NotFound();
            }

            Console.WriteLine($"📂 Using assets path: {assetsPath}");

            var filePath = Path.GetFullPath(Path.Combine(assetsPath, fileName));

            if (!filePath.StartsWith(Path.GetFullPath(assetsPath) + Path.DirectorySeparatorChar) &&
                !filePath.Equals(Path.GetFullPath(assetsPath)))
            {
                Console.WriteLine($"❌ Security violation: {filePath} is outside assets directory");
                return Results.BadRequest("Invalid file path");
            }

            if (File.Exists(filePath))
            {
                Console.WriteLine($"✅ Found exact file: {filePath}");
                var contentType = GetSpriteContentType(fileName);
                return Results.File(filePath, contentType);
            }

            try
            {
                var fileNameWithoutExtension = Path.GetFileNameWithoutExtension(fileName);
                var extension = Path.GetExtension(fileName);

                Console.WriteLine($"🔍 Searching for pattern: {fileNameWithoutExtension}*{extension}");

                var matchingFiles = Directory.GetFiles(assetsPath, fileNameWithoutExtension + "*" + extension);

                Console.WriteLine($"📁 Found {matchingFiles.Length} matching files");

                if (matchingFiles.Length > 0)
                {
                    var matchedFile = matchingFiles[0];
                    Console.WriteLine($"✅ Using matched file: {matchedFile}");

                    var contentType = GetSpriteContentType(fileName);
                    return Results.File(matchedFile, contentType);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Error searching for file pattern: {ex.Message}");
            }

            Console.WriteLine($"❌ File not found: {fileName}");
            return Results.NotFound();
        })
        .WithName("GetCustomSprite")
        .WithTags("Files")
        .Produces(200, contentType: "image/png")
        .Produces(200, contentType: "image/webp")
        .Produces(200, contentType: "application/octet-stream")
        .Produces(400)
        .Produces(404);
    }

    private static string? ResolveAssetsPath()
    {
        var possiblePaths = new List<string>
        {
            Path.Combine(Directory.GetCurrentDirectory(), "assets"),
            Path.Combine(AppContext.BaseDirectory, "assets"),
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "assets")
        };

        var envAssetsPath = Environment.GetEnvironmentVariable("BEASTVAULT_ASSETS_PATH");
        if (!string.IsNullOrEmpty(envAssetsPath))
            possiblePaths.Insert(0, envAssetsPath);

        var parentDir = Directory.GetParent(AppContext.BaseDirectory)?.FullName;
        if (parentDir != null)
            possiblePaths.Add(Path.Combine(parentDir, "assets"));

        possiblePaths = possiblePaths.Distinct().ToList();

        foreach (var path in possiblePaths)
        {
            if (Directory.Exists(path))
                return path;
        }

        return null;
    }

    private static string GetSpriteContentType(string fileName)
    {
        return fileName.EndsWith(".png") ? "image/png" :
               fileName.EndsWith(".webp") ? "image/webp" :
               "application/octet-stream";
    }
}
