using System.Runtime.InteropServices;

namespace RepoGalaxy.Desktop.Services;

public static class MotionPreferences
{
    private const uint SpiGetClientAreaAnimation = 0x1042;

    public static bool AnimationsEnabled
    {
        get
        {
            if (!OperatingSystem.IsWindows()) return true;
            try { return SystemParametersInfo(SpiGetClientAreaAnimation, 0, out var enabled, 0) && enabled; }
            catch { return true; }
        }
    }

    [DllImport("user32.dll", SetLastError = false)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SystemParametersInfo(uint action, uint parameter, [MarshalAs(UnmanagedType.Bool)] out bool value, uint flags);
}
