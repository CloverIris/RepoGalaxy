namespace RepoGalaxy.Desktop.Services;

public static class ApplicationPaths
{
    public static string GetDataDirectory()
    {
        var appDataRoot = OperatingSystem.IsMacOS()
            ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Library", "Application Support")
            : Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var directory = Path.Combine(appDataRoot, "RepoGalaxy");
        Directory.CreateDirectory(directory);
        return directory;
    }

    public static string GetDatabasePath() => Path.Combine(GetDataDirectory(), "repogalaxy.db");

    public static string GetLogFilePath()
    {
        var logDirectory = Path.Combine(GetDataDirectory(), "Logs");
        Directory.CreateDirectory(logDirectory);
        return Path.Combine(logDirectory, "repogalaxy-.log");
    }
}
