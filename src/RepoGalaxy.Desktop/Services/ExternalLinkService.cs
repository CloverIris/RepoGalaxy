using System.Diagnostics;

namespace RepoGalaxy.Desktop.Services;

public interface IExternalLinkService
{
    bool CanOpen(string? uri);
    bool Open(string? uri);
}

public sealed class ExternalLinkService : IExternalLinkService
{
    public bool CanOpen(string? uri) => Uri.TryCreate(uri, UriKind.Absolute, out var target)
        && target.Scheme.Equals(Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase);

    public bool Open(string? uri)
    {
        if (!CanOpen(uri)) return false;
        try
        {
            if (OperatingSystem.IsMacOS())
            {
                var processInfo = new ProcessStartInfo("open");
                processInfo.ArgumentList.Add(uri!);
                Process.Start(processInfo);
            }
            else
            {
                Process.Start(new ProcessStartInfo { FileName = uri!, UseShellExecute = true });
            }
            return true;
        }
        catch
        {
            return false;
        }
    }
}
