using CommunityToolkit.Mvvm.ComponentModel;
using Avalonia.Media;

namespace RepoGalaxy.Desktop.ViewModels;

public interface ISearchablePage
{
    string SearchText { get; set; }
}

public sealed partial class NavigationItemViewModel : ObservableObject
{
    public NavigationItemViewModel(string key, string title, string iconData)
    {
        Key = key;
        Title = title;
        Icon = Geometry.Parse(iconData);
        Group = "Primary";
    }

    public NavigationItemViewModel(string key, string title, string glyph, string fallbackIconData, string group = "Primary")
    {
        Key = key;
        Title = title;
        Glyph = glyph;
        Icon = Geometry.Parse(fallbackIconData);
        Group = group;
    }

    public string Key { get; }
    public string Title { get; }
    public Geometry Icon { get; }
    public string Glyph { get; } = string.Empty;
    public bool UseSystemGlyph => OperatingSystem.IsWindows() && !string.IsNullOrWhiteSpace(Glyph);
    public bool UseFallbackIcon => !UseSystemGlyph;
    public string Group { get; }
    [ObservableProperty] private bool _isSelected;
    [ObservableProperty] private int _badgeCount;
}
