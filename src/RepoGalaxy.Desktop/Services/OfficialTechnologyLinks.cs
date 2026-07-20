namespace RepoGalaxy.Desktop.Services;

public static class OfficialTechnologyLinks
{
    private static readonly IReadOnlyDictionary<string, string> Links = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        ["C#"] = "https://learn.microsoft.com/dotnet/csharp/",
        ["C++"] = "https://isocpp.org/",
        ["JavaScript"] = "https://developer.mozilla.org/docs/Web/JavaScript",
        ["TypeScript"] = "https://www.typescriptlang.org/",
        ["Python"] = "https://www.python.org/",
        ["Go"] = "https://go.dev/",
        ["Rust"] = "https://www.rust-lang.org/",
        ["Java"] = "https://dev.java/",
        ["Kotlin"] = "https://kotlinlang.org/",
        ["Swift"] = "https://www.swift.org/",
        ["Dart"] = "https://dart.dev/",
        ["Ruby"] = "https://www.ruby-lang.org/",
        ["PHP"] = "https://www.php.net/",
        ["PowerShell"] = "https://learn.microsoft.com/powershell/",
        ["React"] = "https://react.dev/",
        ["Vue"] = "https://vuejs.org/",
        ["Angular"] = "https://angular.dev/",
        ["Avalonia"] = "https://avaloniaui.net/",
        [".NET"] = "https://dotnet.microsoft.com/",
        ["Docker"] = "https://www.docker.com/",
        ["Kubernetes"] = "https://kubernetes.io/",
        ["Git"] = "https://git-scm.com/"
    };

    public static string Get(string key) => Links.GetValueOrDefault(key?.Trim() ?? string.Empty) ?? string.Empty;
}
