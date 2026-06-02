using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace BeastVault.Api.Helpers;

public static class PokedexTextFilters
{
    public static readonly string[] TargetFlavorLanguages = ["en", "es", "ja"];

    public static string NormalizeFlavorLanguage(string? language)
    {
        var normalized = (language ?? "").Trim();
        return normalized.Equals("ja-Hrkt", StringComparison.OrdinalIgnoreCase)
            ? "ja"
            : normalized.ToLowerInvariant();
    }

    public static bool IsTargetFlavorLanguage(string? language)
        => TargetFlavorLanguages.Contains(NormalizeFlavorLanguage(language), StringComparer.OrdinalIgnoreCase);

    public static string CleanFlavorText(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return "";
        return Regex.Replace(text.Replace("\f", " ").Replace("\n", " "), @"\s+", " ").Trim();
    }

    public static bool IsDisplayableFlavorText(string? text)
    {
        var cleaned = CleanFlavorText(text);
        if (string.IsNullOrWhiteSpace(cleaned)) return false;

        var normalized = RemoveDiacritics(cleaned).ToLowerInvariant();
        if (normalized is "no entry" or "no entry." or "no pokedex entry" or "no pokedex entry.")
            return false;

        if (normalized.StartsWith("no entry", StringComparison.Ordinal))
            return false;

        if (normalized.Contains("no pokedex entry", StringComparison.Ordinal)
            || normalized.Contains("has no pokedex entry", StringComparison.Ordinal)
            || normalized.Contains("does not have a pokedex entry", StringComparison.Ordinal)
            || normalized.Contains("no tiene entrada", StringComparison.Ordinal)
            || normalized.Contains("sin entrada", StringComparison.Ordinal))
            return false;

        if (normalized.Contains("pokopia", StringComparison.Ordinal)
            && (normalized.Contains("not in", StringComparison.Ordinal)
                || normalized.Contains("not available", StringComparison.Ordinal)
                || normalized.Contains("unavailable", StringComparison.Ordinal)
                || normalized.Contains("no esta", StringComparison.Ordinal)
                || normalized.Contains("no está", StringComparison.Ordinal)))
            return false;

        return true;
    }

    public static bool IsDisplayableLocation(string? location, string? method)
    {
        var cleanedLocation = CleanFlavorText(location);
        if (string.IsNullOrWhiteSpace(cleanedLocation)) return false;

        var normalized = RemoveDiacritics($"{cleanedLocation} {CleanFlavorText(method)}").ToLowerInvariant();
        if (normalized.Contains("unobtainable", StringComparison.Ordinal)
            || normalized.Contains("unavailable", StringComparison.Ordinal)
            || normalized.Contains("no entry", StringComparison.Ordinal)
            || normalized.Contains("no pokedex entry", StringComparison.Ordinal))
            return false;

        if (normalized.Contains("pokopia", StringComparison.Ordinal)
            && (normalized.Contains("not in", StringComparison.Ordinal)
                || normalized.Contains("not available", StringComparison.Ordinal)
                || normalized.Contains("unavailable", StringComparison.Ordinal)
                || normalized.Contains("no esta", StringComparison.Ordinal)))
            return false;

        return true;
    }

    private static string RemoveDiacritics(string text)
    {
        var normalized = text.Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(normalized.Length);

        foreach (var c in normalized)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
                builder.Append(c);
        }

        return builder.ToString().Normalize(NormalizationForm.FormC);
    }
}
