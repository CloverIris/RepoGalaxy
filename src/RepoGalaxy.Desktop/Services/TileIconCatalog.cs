using System.Xml.Linq;
using Avalonia.Media;
using Avalonia.Platform;

namespace RepoGalaxy.Desktop.Services;

public static class TileIconCatalog
{
    private static readonly Dictionary<string, string> Files = new(StringComparer.OrdinalIgnoreCase)
    {
        ["C#"] = "csharp", ["C++"] = "cpp", ["JavaScript"] = "javascript", ["TypeScript"] = "typescript",
        ["Python"] = "python", ["Go"] = "go", ["Rust"] = "rust", [".NET"] = "dotnet",
        ["Git"] = "git", ["Docker"] = "docker", ["Kubernetes"] = "kubernetes"
    };
    private static readonly Dictionary<string, Geometry?> Cache = new(StringComparer.OrdinalIgnoreCase);
    private static readonly object Gate = new();

    public static Geometry? Get(string key)
    {
        if (!Files.TryGetValue(key ?? string.Empty, out var file)) return null;
        lock (Gate)
        {
            if (Cache.TryGetValue(file, out var cached)) return cached;
            try
            {
                using var stream = AssetLoader.Open(new Uri($"avares://RepoGalaxy.Desktop/Assets/TileIcons/{file}.svg"));
                var document = XDocument.Load(stream, LoadOptions.None);
                var path = document.Descendants().FirstOrDefault(x => x.Name.LocalName == "path")?.Attribute("d")?.Value;
                return Cache[file] = string.IsNullOrWhiteSpace(path) ? null : Geometry.Parse(path);
            }
            catch { return Cache[file] = null; }
        }
    }
}
