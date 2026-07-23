using Avalonia;
using Avalonia.Styling;
using Microsoft.Extensions.Logging;
using RepoGalaxy.Core.Interfaces;

namespace RepoGalaxy.Desktop.Services;

public interface IAppearanceService
{
    Task<string> RestoreAsync(CancellationToken cancellationToken = default);
    void Apply(string selection);
}

public sealed class AppearanceService : IAppearanceService
{
    private readonly IUserService _users;
    private readonly ILogger<AppearanceService> _logger;

    public AppearanceService(IUserService users, ILogger<AppearanceService> logger)
    {
        _users = users;
        _logger = logger;
    }

    public async Task<string> RestoreAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            var preferences = await _users.GetPreferencesAsync();
            cancellationToken.ThrowIfCancellationRequested();
            var selection = preferences.UseSystemTheme
                ? "跟随系统"
                : preferences.DarkMode ? "深色" : "浅色";
            Apply(selection);
            return selection;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            _logger.LogWarning(exception, "Unable to restore appearance preferences; using the system theme.");
            Apply("跟随系统");
            return "跟随系统";
        }
    }

    public void Apply(string selection)
    {
        if (Application.Current is null) return;
        Application.Current.RequestedThemeVariant = selection switch
        {
            "浅色" => ThemeVariant.Light,
            "深色" => ThemeVariant.Dark,
            _ => ThemeVariant.Default
        };
    }
}
