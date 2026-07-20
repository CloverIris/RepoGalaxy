using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;

namespace RepoGalaxy.Desktop.Services;

/// <summary>Preserves native resize/snap semantics while exposing a Metro client-area title bar.</summary>
public sealed class WindowsMetroChrome : IDisposable
{
    private const uint WmNcHitTest = 0x0084;
    private const int HtClient = 1;
    private const int HtCaption = 2;
    private readonly Window _window;
    private readonly double _titleBarHeight;
    private readonly SubclassProc _callback;
    private readonly UIntPtr _subclassId;
    private IntPtr _handle;

    private WindowsMetroChrome(Window window, double titleBarHeight)
    {
        _window = window;
        _titleBarHeight = titleBarHeight;
        _callback = WindowProcedure;
        _subclassId = (UIntPtr)(uint)window.GetHashCode();
    }

    public static WindowsMetroChrome? Attach(Window window, double titleBarHeight)
    {
        if (!OperatingSystem.IsWindows() || window.TryGetPlatformHandle()?.Handle is not { } handle || handle == IntPtr.Zero) return null;
        var chrome = new WindowsMetroChrome(window, titleBarHeight) { _handle = handle };
        return SetWindowSubclass(handle, chrome._callback, chrome._subclassId, UIntPtr.Zero) ? chrome : null;
    }

    private IntPtr WindowProcedure(IntPtr windowHandle, uint message, IntPtr wParam, IntPtr lParam, UIntPtr subclassId, UIntPtr referenceData)
    {
        var nativeResult = DefSubclassProc(windowHandle, message, wParam, lParam);
        if (message != WmNcHitTest || nativeResult.ToInt32() != HtClient) return nativeResult;

        var packed = lParam.ToInt64();
        var screen = new PixelPoint(unchecked((short)(packed & 0xffff)), unchecked((short)((packed >> 16) & 0xffff)));
        var client = _window.PointToClient(screen);
        if (client.Y < 0 || client.Y >= _titleBarHeight) return nativeResult;

        var hit = _window.InputHitTest(client);
        return IsInteractive(hit) ? nativeResult : new IntPtr(HtCaption);
    }

    private static bool IsInteractive(object? source)
    {
        for (var control = source as Control; control is not null; control = control.Parent as Control)
            if (control is Button or TextBox or ComboBox) return true;
        return false;
    }

    public void Dispose()
    {
        if (_handle == IntPtr.Zero) return;
        RemoveWindowSubclass(_handle, _callback, _subclassId);
        _handle = IntPtr.Zero;
    }

    private delegate IntPtr SubclassProc(IntPtr windowHandle, uint message, IntPtr wParam, IntPtr lParam, UIntPtr subclassId, UIntPtr referenceData);

    [DllImport("comctl32.dll", SetLastError = true)]
    private static extern bool SetWindowSubclass(IntPtr windowHandle, SubclassProc callback, UIntPtr subclassId, UIntPtr referenceData);

    [DllImport("comctl32.dll")]
    private static extern bool RemoveWindowSubclass(IntPtr windowHandle, SubclassProc callback, UIntPtr subclassId);

    [DllImport("comctl32.dll")]
    private static extern IntPtr DefSubclassProc(IntPtr windowHandle, uint message, IntPtr wParam, IntPtr lParam);
}
