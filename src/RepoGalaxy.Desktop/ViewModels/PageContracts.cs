using CommunityToolkit.Mvvm.ComponentModel;
using Avalonia.Media;

namespace RepoGalaxy.Desktop.ViewModels;

public interface ISearchablePage
{
    string SearchText { get; set; }
}

public sealed partial class NavigationItemViewModel : ObservableObject
{
    public NavigationItemViewModel(string key, string title, string iconData, string group = "Primary")
    {
        Key = key;
        Title = title;
        Icon = Geometry.Parse(iconData);
        Group = group;
    }

    public string Key { get; }
    public string Title { get; }
    public Geometry Icon { get; }
    public string Group { get; }
    [ObservableProperty] private bool _isSelected;
    [ObservableProperty] private int _badgeCount;
}
