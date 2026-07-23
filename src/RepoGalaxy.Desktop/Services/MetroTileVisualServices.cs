using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using RepoGalaxy.Core.Interfaces;
using RepoGalaxy.Core.Models;

namespace RepoGalaxy.Desktop.Services;

public sealed class TilePaletteService : ITilePaletteService
{
    private static readonly IReadOnlyDictionary<string, string> Colors =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["C#"] = "#178600", ["C++"] = "#F34B7D", ["C"] = "#555555", ["Java"] = "#B07219",
            ["JavaScript"] = "#F1E05A", ["TypeScript"] = "#3178C6", ["Python"] = "#3572A5", ["Go"] = "#00ADD8",
            ["Rust"] = "#DEA584", ["Kotlin"] = "#A97BFF", ["Swift"] = "#F05138", ["Dart"] = "#00B4AB",
            ["Ruby"] = "#701516", ["PHP"] = "#4F5D95", ["Shell"] = "#89E051", ["PowerShell"] = "#012456",
            ["Vue"] = "#41B883", ["React"] = "#087EA4", ["Angular"] = "#DD0031", ["Avalonia"] = "#7B5CFA",
            [".NET"] = "#512BD4", ["Docker"] = "#2496ED", ["Kubernetes"] = "#326CE5", ["Git"] = "#F05032",
            ["Linux"] = "#E5B400", ["network"] = "#136F8A", ["distributed"] = "#6B4DA2",
            ["placeholder"] = "#2D5F8B", ["history"] = "#7B3FA1", ["quote"] = "#00695C", ["hardware"] = "#9A4A00"
        };

    public TilePalette Create(string accentKey)
    {
        var key = accentKey ?? string.Empty;
        var background = key.Length is 7 or 9 && key[0] == '#'
            ? key
            : Colors.TryGetValue(key, out var value)
                ? value
                : Deterministic(key);
        var whiteRatio = ContrastRatio(background, "#FFFFFF");
        var blackRatio = ContrastRatio(background, "#101010");
        var foreground = whiteRatio >= blackRatio ? "#FFFFFF" : "#101010";
        var secondary = foreground == "#FFFFFF" ? "#EAF2FA" : "#252525";
        var scrim = foreground == "#FFFFFF" ? "#B0000000" : "#B8FFFFFF";
        return new(background, foreground, secondary, scrim);
    }

    public double ContrastRatio(string first, string second)
    {
        var a = Luminance(first);
        var b = Luminance(second);
        return (Math.Max(a, b) + .05) / (Math.Min(a, b) + .05);
    }

    private static double Luminance(string color)
    {
        var text = color.TrimStart('#');
        if (text.Length == 8) text = text[2..];
        if (text.Length != 6) return 0;
        var values = new[] { text[..2], text.Substring(2, 2), text.Substring(4, 2) }
            .Select(x => int.Parse(x, NumberStyles.HexNumber, CultureInfo.InvariantCulture) / 255d)
            .Select(x => x <= .04045 ? x / 12.92 : Math.Pow((x + .055) / 1.055, 2.4))
            .ToArray();
        return values[0] * .2126 + values[1] * .7152 + values[2] * .0722;
    }

    private static string Deterministic(string value)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(value.ToLowerInvariant()));
        var hue = BitConverter.ToUInt16(bytes, 0) % 360;
        const double saturation = .58;
        const double lightness = .38;
        var chroma = (1 - Math.Abs(2 * lightness - 1)) * saturation;
        var part = hue / 60d;
        var x = chroma * (1 - Math.Abs(part % 2 - 1));
        var (r, g, b) = part switch
        {
            < 1 => (chroma, x, 0d),
            < 2 => (x, chroma, 0d),
            < 3 => (0d, chroma, x),
            < 4 => (0d, x, chroma),
            < 5 => (x, 0d, chroma),
            _ => (chroma, 0d, x)
        };
        var m = lightness - chroma / 2;
        return $"#{(int)((r + m) * 255):X2}{(int)((g + m) * 255):X2}{(int)((b + m) * 255):X2}";
    }
}

/// <summary>
/// Loads the audited local knowledge catalog once. The accepted YAML subset is
/// deliberately small so the desktop app does not need a runtime YAML package.
/// </summary>
public sealed class TipCatalog : ITipCatalog
{
    private const string ResourceSuffix = "Assets.Tips.tips.zh-CN.yaml";
    private static readonly Lazy<IReadOnlyList<TipDefinition>> All =
        new(LoadEmbedded, LazyThreadSafetyMode.ExecutionAndPublication);

    public IReadOnlyList<TipDefinition> GetTips(DateOnly date)
    {
        var values = All.Value;
        return values
            .Where(x => x.Month is null || x.Month == date.Month && x.Day == date.Day)
            .ToArray();
    }

    public static IReadOnlyList<TipDefinition> Parse(TextReader reader)
    {
        ArgumentNullException.ThrowIfNull(reader);
        var result = new List<TipDefinition>();
        var keys = new HashSet<string>(StringComparer.Ordinal);
        Dictionary<string, string>? current = null;

        while (reader.ReadLine() is { } rawLine)
        {
            var trimmed = rawLine.Trim();
            if (trimmed.Length == 0 || trimmed.StartsWith('#')) continue;
            if (trimmed.StartsWith("- ", StringComparison.Ordinal))
            {
                if (current is not null) Add(current, result, keys);
                current = new(StringComparer.OrdinalIgnoreCase);
                ReadPair(trimmed[2..], current);
                continue;
            }

            if (current is null)
                throw new InvalidDataException("Tip YAML must begin with a list item.");
            ReadPair(trimmed, current);
        }

        if (current is not null) Add(current, result, keys);
        if (result.Count == 0) throw new InvalidDataException("Tip YAML does not contain any items.");
        return result;
    }

    private static IReadOnlyList<TipDefinition> LoadEmbedded()
    {
        var assembly = typeof(TipCatalog).Assembly;
        var resourceName = assembly.GetManifestResourceNames()
            .SingleOrDefault(x => x.EndsWith(ResourceSuffix, StringComparison.Ordinal));
        if (resourceName is null)
            throw new InvalidDataException($"Embedded tip catalog '{ResourceSuffix}' was not found.");
        using var stream = assembly.GetManifestResourceStream(resourceName)
            ?? throw new InvalidDataException($"Embedded tip catalog '{resourceName}' could not be opened.");
        using var reader = new StreamReader(stream, Encoding.UTF8, true);
        return Parse(reader);
    }

    private static void ReadPair(string line, IDictionary<string, string> target)
    {
        var separator = line.IndexOf(':');
        if (separator <= 0) throw new InvalidDataException($"Invalid tip YAML line: {line}");
        var key = line[..separator].Trim();
        var value = line[(separator + 1)..].Trim();
        target[key] = Unquote(value);
    }

    private static string Unquote(string value)
    {
        if (value.Length >= 2 && value[0] == '"' && value[^1] == '"')
            return JsonSerializer.Deserialize<string>(value) ?? string.Empty;
        return value;
    }

    private static void Add(
        IReadOnlyDictionary<string, string> values,
        ICollection<TipDefinition> result,
        ISet<string> keys)
    {
        var key = Required(values, "key");
        if (!keys.Add(key)) throw new InvalidDataException($"Duplicate tip key '{key}'.");
        var spans = ParseSpans(values.GetValueOrDefault("spans"));
        var primary = spans[0];
        result.Add(new(
            key,
            Required(values, "category"),
            Required(values, "title"),
            Required(values, "body"),
            Required(values, "accent"),
            primary.Columns,
            primary.Rows,
            ParseOptionalInt(values, "month"),
            ParseOptionalInt(values, "day"),
            values.GetValueOrDefault("attribution") ?? string.Empty,
            values.GetValueOrDefault("source") ?? string.Empty,
            spans));
    }

    private static string Required(IReadOnlyDictionary<string, string> values, string key) =>
        values.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value)
            ? value.Trim()
            : throw new InvalidDataException($"Tip field '{key}' is required.");

    private static int? ParseOptionalInt(IReadOnlyDictionary<string, string> values, string key) =>
        values.TryGetValue(key, out var value) && int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed)
            ? parsed
            : null;

    private static IReadOnlyList<TileSpan> ParseSpans(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return [new(1, 1)];
        var spans = new List<TileSpan>();
        foreach (var token in value.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
        {
            var parts = token.Split('x', StringSplitOptions.TrimEntries);
            if (parts.Length != 2 ||
                !int.TryParse(parts[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out var columns) ||
                !int.TryParse(parts[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out var rows) ||
                columns is < 1 or > 8 ||
                rows is < 1 or > 8)
                throw new InvalidDataException($"Invalid tip span '{token}'.");
            var span = new TileSpan(columns, rows);
            if (!spans.Contains(span)) spans.Add(span);
        }
        return spans.Count == 0 ? [new(1, 1)] : spans;
    }
}
