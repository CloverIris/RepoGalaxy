using Avalonia;
using Avalonia.Headless;
using RepoGalaxy.Desktop;

namespace RepoGalaxy.Desktop.Tests;

public static class TestAppBuilder
{
    private static readonly object Gate = new();
    private static bool _initialized;

    public static void EnsureInitialized()
    {
        if (_initialized) return;
        lock (Gate)
        {
            if (_initialized) return;
            AppBuilder.Configure<App>(() => new App())
                .UseHeadless(new AvaloniaHeadlessPlatformOptions())
                .SetupWithoutStarting();
            _initialized = true;
        }
    }
}
