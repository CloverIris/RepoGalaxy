using Avalonia.Controls;

namespace RepoGalaxy.Desktop.Services;

public static class PlatformWindowChrome
{
    public static bool UseNativeMacDecorations(Window window)
    {
        if (!OperatingSystem.IsMacOS()) return false;
        window.WindowDecorations = WindowDecorations.Full;
        window.ExtendClientAreaToDecorationsHint = false;
        return true;
    }
}
