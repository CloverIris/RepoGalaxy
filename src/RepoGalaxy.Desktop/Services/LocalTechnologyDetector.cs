using System.Text.Json;

namespace RepoGalaxy.Desktop.Services;

public sealed record LocalTechnologySignals(IReadOnlySet<string> Languages, IReadOnlySet<string> Frameworks);

public static class LocalTechnologyDetector
{
    public static LocalTechnologySignals Detect(IEnumerable<string> roots)
    {
        var languages = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var frameworks = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var root in roots.Where(Directory.Exists))
        {
            try
            {
                var files = EnumerateProjectFiles(root, maximumDirectories: 240).ToList();
                if (files.Any(x => x.EndsWith(".csproj", StringComparison.OrdinalIgnoreCase))) languages.Add("C#");
                if (files.Any(x => FileName(x, "Cargo.toml"))) languages.Add("Rust");
                if (files.Any(x => FileName(x, "go.mod"))) languages.Add("Go");
                if (files.Any(x => FileName(x, "pyproject.toml") || FileName(x, "requirements.txt"))) languages.Add("Python");
                if (files.Any(x => FileName(x, "pom.xml") || FileName(x, "build.gradle") || FileName(x, "build.gradle.kts"))) languages.Add("Java");
                if (files.Any(x => FileName(x, "Package.swift"))) languages.Add("Swift");
                if (files.Any(x => FileName(x, "pubspec.yaml"))) languages.Add("Dart");
                foreach (var package in files.Where(x => FileName(x, "package.json"))) ReadPackageSignals(package, languages, frameworks);
                foreach (var project in files.Where(x => x.EndsWith(".csproj", StringComparison.OrdinalIgnoreCase)))
                    if (ReadSmallText(project).Contains("Avalonia", StringComparison.OrdinalIgnoreCase)) frameworks.Add("Avalonia");
            }
            catch { }
        }
        return new(languages, frameworks);
    }

    private static IEnumerable<string> EnumerateProjectFiles(string root, int maximumDirectories)
    {
        var queue = new Queue<(string Path, int Depth)>();
        queue.Enqueue((root, 0));
        var visited = 0;
        while (queue.Count > 0 && visited++ < maximumDirectories)
        {
            var (path, depth) = queue.Dequeue();
            IEnumerable<string> files;
            try { files = Directory.EnumerateFiles(path).Take(250).ToList(); }
            catch { continue; }
            foreach (var file in files.Where(IsProjectSignalFile)) yield return file;
            if (depth >= 4) continue;
            IEnumerable<string> directories;
            try { directories = Directory.EnumerateDirectories(path).Take(100).ToList(); }
            catch { continue; }
            foreach (var directory in directories)
            {
                var name = Path.GetFileName(directory);
                if (name is ".git" or "node_modules" or "bin" or "obj" or ".idea" or ".vs") continue;
                try { if ((File.GetAttributes(directory) & FileAttributes.ReparsePoint) == 0) queue.Enqueue((directory, depth + 1)); }
                catch { }
            }
        }
    }

    private static bool IsProjectSignalFile(string path)
    {
        var name = Path.GetFileName(path);
        return path.EndsWith(".csproj", StringComparison.OrdinalIgnoreCase)
            || name is "Cargo.toml" or "go.mod" or "pyproject.toml" or "requirements.txt" or "package.json" or "pom.xml" or "build.gradle" or "build.gradle.kts" or "Package.swift" or "pubspec.yaml";
    }

    private static void ReadPackageSignals(string path, HashSet<string> languages, HashSet<string> frameworks)
    {
        var text = ReadSmallText(path);
        if (string.IsNullOrWhiteSpace(text)) return;
        languages.Add(text.Contains("typescript", StringComparison.OrdinalIgnoreCase) ? "TypeScript" : "JavaScript");
        try
        {
            using var document = JsonDocument.Parse(text, new JsonDocumentOptions { MaxDepth = 12 });
            foreach (var section in new[] { "dependencies", "devDependencies", "peerDependencies" })
            {
                if (!document.RootElement.TryGetProperty(section, out var dependencies) || dependencies.ValueKind != JsonValueKind.Object) continue;
                foreach (var dependency in dependencies.EnumerateObject())
                {
                    var framework = dependency.Name.ToLowerInvariant() switch
                    {
                        "react" => "React", "vue" => "Vue", "@angular/core" => "Angular", "svelte" => "Svelte",
                        "next" => "Next.js", "electron" => "Electron", "typescript" => "TypeScript", _ => null
                    };
                    if (framework is not null) frameworks.Add(framework);
                }
            }
        }
        catch (JsonException) { }
    }

    private static string ReadSmallText(string path)
    {
        try
        {
            var info = new FileInfo(path);
            return info.Exists && info.Length <= 1024 * 1024 ? File.ReadAllText(path) : string.Empty;
        }
        catch { return string.Empty; }
    }

    private static bool FileName(string path, string expected) => Path.GetFileName(path).Equals(expected, StringComparison.OrdinalIgnoreCase);
}
