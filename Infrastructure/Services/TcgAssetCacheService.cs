using System.Collections.Concurrent;
using BeastVault.Api.Application.Interfaces;
using BeastVault.Api.Domain.Entities;
using BeastVault.Api.Infrastructure;
using BeastVault.Api.Infrastructure.Configuration;
using Microsoft.EntityFrameworkCore;

namespace BeastVault.Api.Infrastructure.Services;

public sealed record TcgCachedAsset(string Path, string ContentType);

public sealed class TcgAssetCacheService(
    AppDbContext db,
    IHttpClientFactory httpClientFactory,
    StorageConfiguration storage,
    ILogger<TcgAssetCacheService> logger,
    ITcgDexProvider tcgDex)
{
    private const int MaxAssetBytes = 8 * 1024 * 1024;
    private static readonly string[] Extensions = [".webp", ".png", ".jpg", ".gif"];
    private static readonly HashSet<string> AllowedHosts = new(StringComparer.OrdinalIgnoreCase)
    {
        "assets.tcgdex.net",
        "images.pokemontcg.io"
    };
    private static readonly ConcurrentDictionary<string, SemaphoreSlim> AssetLocks = new(StringComparer.Ordinal);
    private static readonly ConcurrentDictionary<string, Lazy<Task<TcgProviderSet?>>> ProviderSetCache = new(StringComparer.OrdinalIgnoreCase);
    private static readonly SemaphoreSlim ProviderSetLookupGate = new(2, 2);
    private readonly HttpClient _client = httpClientFactory.CreateClient("TcgDex");
    private readonly ITcgDexProvider _tcgDex = tcgDex;

    public async Task<TcgCachedAsset?> GetCardAsync(int cardId, string size, CancellationToken cancellationToken)
    {
        var normalizedSize = size.ToLowerInvariant();
        if (normalizedSize is not ("small" or "large")) return null;

        var card = await db.TcgCards.AsNoTracking()
            .Include(x => x.Set)
            .SingleOrDefaultAsync(x => x.Id == cardId, cancellationToken);
        if (card is null) return null;

        var candidates = await BuildCardCandidatesAsync(card, normalizedSize, cancellationToken);
        return await GetOrFetchAsync("cards", card.Id.ToString(), normalizedSize, candidates, cancellationToken);
    }

    public async Task<TcgCachedAsset?> GetSetAsync(int setId, string kind, CancellationToken cancellationToken)
    {
        var normalizedKind = kind.ToLowerInvariant();
        if (normalizedKind is not ("symbol" or "logo")) return null;

        var set = await db.TcgSets.AsNoTracking().SingleOrDefaultAsync(x => x.Id == setId, cancellationToken);
        if (set is null) return null;
        return await GetOrFetchAsync(
            "sets",
            set.Id.ToString(),
            normalizedKind,
            BuildSetCandidates(set, normalizedKind),
            cancellationToken);
    }

    private async Task<TcgCachedAsset?> GetOrFetchAsync(
        string category,
        string id,
        string kind,
        IReadOnlyList<string> candidates,
        CancellationToken cancellationToken)
    {
        var directory = Path.Combine(storage.TcgAssetsDirectory, category, id);
        var existing = FindExisting(directory, kind);
        if (existing is not null) return existing;
        if (candidates.Count == 0) return null;

        var lockKey = Path.Combine(category, id, kind);
        var assetLock = AssetLocks.GetOrAdd(lockKey, _ => new SemaphoreSlim(1, 1));
        await assetLock.WaitAsync(cancellationToken);
        try
        {
            existing = FindExisting(directory, kind);
            if (existing is not null) return existing;

            foreach (var candidate in candidates.Distinct(StringComparer.OrdinalIgnoreCase))
            {
                var downloaded = await TryDownloadAsync(candidate, cancellationToken);
                if (downloaded is null) continue;

                Directory.CreateDirectory(directory);
                var destination = Path.Combine(directory, $"{kind}{downloaded.Value.Extension}");
                if (File.Exists(destination)) return new TcgCachedAsset(destination, downloaded.Value.ContentType);

                var temporary = Path.Combine(directory, $".{kind}-{Guid.NewGuid():N}.tmp");
                try
                {
                    await using (var stream = new FileStream(
                        temporary,
                        FileMode.CreateNew,
                        FileAccess.Write,
                        FileShare.None,
                        81920,
                        FileOptions.Asynchronous | FileOptions.WriteThrough))
                    {
                        await stream.WriteAsync(downloaded.Value.Bytes, cancellationToken);
                        await stream.FlushAsync(cancellationToken);
                    }

                    try
                    {
                        File.Move(temporary, destination, overwrite: false);
                    }
                    catch (IOException) when (File.Exists(destination))
                    {
                        File.Delete(temporary);
                    }

                    return new TcgCachedAsset(destination, downloaded.Value.ContentType);
                }
                finally
                {
                    if (File.Exists(temporary)) File.Delete(temporary);
                }
            }

            return null;
        }
        catch (Exception exception) when (exception is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
        {
            logger.LogWarning(exception, "Unable to cache TCG asset {Category}/{Id}/{Kind}", category, id, kind);
            return null;
        }
        finally
        {
            assetLock.Release();
        }
    }

    private async Task<(byte[] Bytes, string Extension, string ContentType)?> TryDownloadAsync(
        string candidate,
        CancellationToken cancellationToken)
    {
        if (!TryGetAllowedUri(candidate, out var uri)) return null;

        try
        {
            using var response = await _client.GetAsync(uri, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            if (!response.IsSuccessStatusCode) return null;
            if (response.Content.Headers.ContentLength is > MaxAssetBytes) return null;

            var declaredType = response.Content.Headers.ContentType?.MediaType;
            if (declaredType is not null &&
                !declaredType.StartsWith("image/", StringComparison.OrdinalIgnoreCase) &&
                !declaredType.Equals("application/octet-stream", StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }

            await using var source = await response.Content.ReadAsStreamAsync(cancellationToken);
            using var destination = new MemoryStream();
            var buffer = new byte[81920];
            while (true)
            {
                var read = await source.ReadAsync(buffer, cancellationToken);
                if (read == 0) break;
                if (destination.Length + read > MaxAssetBytes) return null;
                await destination.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
            }

            var bytes = destination.ToArray();
            var detected = DetectImage(bytes);
            return detected is null ? null : (bytes, detected.Value.Extension, detected.Value.ContentType);
        }
        catch (Exception exception) when (exception is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
        {
            logger.LogDebug(exception, "TCG asset candidate was unavailable: {Candidate}", candidate);
            return null;
        }
    }

    private static IReadOnlyList<string> BuildCardCandidates(TcgCardEntity card, string size)
    {
        var primaryQuality = size == "small" ? "low" : "high";
        var alternateQuality = size == "small" ? "high" : "low";
        var primarySource = size == "small" ? card.ImageSmall : card.ImageLarge;
        var alternateSource = size == "small" ? card.ImageLarge : card.ImageSmall;
        var result = new List<string>();

        if (card.Provider.Equals("tcgdex", StringComparison.OrdinalIgnoreCase) &&
            !string.IsNullOrWhiteSpace(card.Set.SeriesId) &&
            !string.IsNullOrWhiteSpace(card.Set.ProviderSetId) &&
            !string.IsNullOrWhiteSpace(card.Number))
        {
            var localId = card.Number.Split('/', 2)[0].Trim();
            foreach (var language in new[] { "en", "es", "univ" })
                result.Add(BuildTcgDexCardUrl(language, card.Set.SeriesId, card.Set.ProviderSetId, localId, primaryQuality));

            if (!string.IsNullOrWhiteSpace(primarySource)) result.Add(primarySource);

            foreach (var language in new[] { "en", "es", "univ" })
                result.Add(BuildTcgDexCardUrl(language, card.Set.SeriesId, card.Set.ProviderSetId, localId, alternateQuality));
        }
        else if (!string.IsNullOrWhiteSpace(primarySource))
        {
            result.Add(primarySource);
        }

        if (!string.IsNullOrWhiteSpace(alternateSource)) result.Add(alternateSource);
        return result;
    }

    private async Task<IReadOnlyList<string>> BuildCardCandidatesAsync(
        TcgCardEntity card,
        string size,
        CancellationToken cancellationToken)
    {
        var candidates = BuildCardCandidates(card, size).ToList();
        if (card.Provider.Equals("tcgdex", StringComparison.OrdinalIgnoreCase) &&
            (string.IsNullOrWhiteSpace(card.Set.SeriesId) || candidates.Count == 0) &&
            !string.IsNullOrWhiteSpace(card.Set.ProviderSetId) &&
            !string.IsNullOrWhiteSpace(card.Number))
        {
            try
            {
                var localId = card.Number.Split('/', 2)[0].Trim();
                var providerSet = await GetProviderSetAsync(card.Set.ProviderSetId, cancellationToken);
                var providerCard = providerSet?.Cards.FirstOrDefault(item =>
                    string.Equals(item.Number.Split('/', 2)[0].Trim(), localId, StringComparison.OrdinalIgnoreCase));
                var source = size == "small" ? providerCard?.ImageSmall : providerCard?.ImageLarge;
                if (string.IsNullOrWhiteSpace(source) && !string.IsNullOrWhiteSpace(providerSet?.SeriesId))
                {
                    source = BuildTcgDexCardUrl(
                        "en",
                        providerSet.SeriesId,
                        card.Set.ProviderSetId,
                        localId,
                        size == "small" ? "low" : "high");
                }
                if (!string.IsNullOrWhiteSpace(source)) candidates.Insert(0, source);
            }
            catch (HttpRequestException)
            {
                // Keep the existing candidates and let the asset cache try them.
            }
        }
        return candidates;
    }

    private async Task<TcgProviderSet?> GetProviderSetAsync(string setId, CancellationToken cancellationToken)
    {
        var lazy = new Lazy<Task<TcgProviderSet?>>(
            () => LoadProviderSetAsync(setId),
            LazyThreadSafetyMode.ExecutionAndPublication);
        var cached = ProviderSetCache.GetOrAdd(setId, lazy);
        var result = await cached.Value.WaitAsync(cancellationToken);
        if (result is null) ProviderSetCache.TryRemove(setId, out _);
        return result;
    }

    private async Task<TcgProviderSet?> LoadProviderSetAsync(string setId)
    {
        await ProviderSetLookupGate.WaitAsync(CancellationToken.None);
        try
        {
            return await _tcgDex.GetSetAsync(setId, "en", CancellationToken.None);
        }
        catch (HttpRequestException exception)
        {
            logger.LogDebug(exception, "TCG set metadata was unavailable: {SetId}", setId);
            return null;
        }
        finally
        {
            ProviderSetLookupGate.Release();
        }
    }

    private static IReadOnlyList<string> BuildSetCandidates(TcgSetEntity set, string kind)
    {
        var source = kind == "symbol" ? set.SymbolUrl : set.LogoUrl;
        var result = new List<string>();
        if (!string.IsNullOrWhiteSpace(source)) result.Add(EnsureImageExtension(source));
        if (set.Provider.Equals("tcgdex", StringComparison.OrdinalIgnoreCase) &&
            !string.IsNullOrWhiteSpace(set.SeriesId) &&
            !string.IsNullOrWhiteSpace(set.ProviderSetId))
        {
            result.Insert(0, BuildTcgDexSetUrl("en", set.SeriesId, set.ProviderSetId, kind));
        }
        return result;
    }

    private static string BuildTcgDexCardUrl(string language, string seriesId, string setId, string localId, string quality) =>
        $"https://assets.tcgdex.net/{language}/{Uri.EscapeDataString(seriesId)}/{Uri.EscapeDataString(setId)}/{Uri.EscapeDataString(localId)}/{quality}.webp";

    private static string BuildTcgDexSetUrl(string language, string seriesId, string setId, string kind) =>
        $"https://assets.tcgdex.net/{language}/{Uri.EscapeDataString(seriesId)}/{Uri.EscapeDataString(setId)}/{kind}.webp";

    private static string EnsureImageExtension(string source)
    {
        if (!Uri.TryCreate(source, UriKind.Absolute, out var uri) || Path.HasExtension(uri.AbsolutePath)) return source;
        var builder = new UriBuilder(uri) { Path = $"{uri.AbsolutePath.TrimEnd('/')}.webp" };
        return builder.Uri.ToString();
    }

    private static bool TryGetAllowedUri(string value, out Uri uri)
    {
        uri = null!;
        if (!Uri.TryCreate(value, UriKind.Absolute, out var parsed) || parsed.Scheme != Uri.UriSchemeHttps ||
            parsed.Port != 443 || !string.IsNullOrEmpty(parsed.UserInfo) || !AllowedHosts.Contains(parsed.IdnHost))
        {
            return false;
        }
        uri = parsed;
        return true;
    }

    private static TcgCachedAsset? FindExisting(string directory, string kind)
    {
        foreach (var extension in Extensions)
        {
            var path = Path.Combine(directory, $"{kind}{extension}");
            if (File.Exists(path)) return new TcgCachedAsset(path, ContentTypeFor(extension));
        }
        return null;
    }

    private static (string Extension, string ContentType)? DetectImage(ReadOnlySpan<byte> bytes)
    {
        if (bytes.Length >= 12 && bytes[..4].SequenceEqual("RIFF"u8) && bytes.Slice(8, 4).SequenceEqual("WEBP"u8))
            return (".webp", "image/webp");
        if (bytes.Length >= 8 && bytes[..8].SequenceEqual(new byte[] { 0x89, 0x50, 0x4e, 0x47, 0x0d, 0x0a, 0x1a, 0x0a }))
            return (".png", "image/png");
        if (bytes.Length >= 3 && bytes[0] == 0xff && bytes[1] == 0xd8 && bytes[2] == 0xff)
            return (".jpg", "image/jpeg");
        if (bytes.Length >= 6 && (bytes[..6].SequenceEqual("GIF87a"u8) || bytes[..6].SequenceEqual("GIF89a"u8)))
            return (".gif", "image/gif");
        return null;
    }

    private static string ContentTypeFor(string extension) => extension switch
    {
        ".webp" => "image/webp",
        ".png" => "image/png",
        ".jpg" => "image/jpeg",
        ".gif" => "image/gif",
        _ => "application/octet-stream"
    };
}
