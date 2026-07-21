using Avalonia.Media;
using Avalonia.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using RepoGalaxy.Core.Interfaces;
using RepoGalaxy.Core.Models;
using RepoGalaxy.Desktop.Services;

namespace RepoGalaxy.Desktop.ViewModels;

public sealed partial class MetroTileViewModel : ObservableObject
{
    public const double Unit = 96;
    public const double Gap = 4;
    private double _edgeOpacity = 1;
    private double _focusOpacity = 1;
    private bool _matchesFilter = true;
    private double _heldBackgroundOpacity = 1;

    public MetroTileViewModel(TilePlacement placement, TilePalette palette, FeedItemViewModel? repository = null, DashboardListViewModel? ranking = null)
    {
        Placement = placement; RepositoryItem = repository; Ranking = ranking;
        Background = Brush(palette.Background); Foreground = Brush(palette.Foreground); SecondaryForeground = Brush(palette.SecondaryForeground); Scrim = Brush(palette.Scrim);
    }

    public TilePlacement Placement { get; }
    public TileContent Content => Placement.Content;
    public FeedItemViewModel? RepositoryItem { get; }
    public DashboardListViewModel? Ranking { get; }
    public long Id => Placement.Id;
    public string Key => Content.Key;
    public string Title => RepositoryItem?.Repository.FullName ?? Content.Title;
    public string Subtitle => RepositoryItem?.Repository.Description ?? Content.Subtitle;
    public string Caption => RepositoryItem?.ReasonText ?? Content.Caption;
    public string Language => RepositoryItem?.Repository.PrimaryLanguage ?? Content.AccentKey;
    public string Stars => RepositoryItem is null ? string.Empty : $"★ {RepositoryItem.Repository.StarsFormatted}";
    public string Forks => RepositoryItem is null ? string.Empty : $"Fork {RepositoryItem.Repository.ForksFormatted}";
    public string SaveText => RepositoryItem?.SaveText ?? string.Empty;
    public string KindLabel => Content.Kind switch { MetroTileKind.Language => "LANGUAGE", MetroTileKind.Technology => "STACK", MetroTileKind.Repository => "REPOSITORY", MetroTileKind.FeaturedRepository => "FEATURED", MetroTileKind.RankingList => "TOP 5", _ => Content.IsPlaceholder ? "LOADING · TIP" : "TIP" };
    public string Glyph => Content.Kind switch { MetroTileKind.Language => LanguageGlyph(Content.Title), MetroTileKind.Technology => "#", MetroTileKind.Repository or MetroTileKind.FeaturedRepository => "</>", MetroTileKind.RankingList => "↗", _ => "·" };
    public Geometry? IconGeometry => TileIconCatalog.Get(Content.AccentKey);
    public bool HasIcon => IconGeometry is not null;
    public double Left => Placement.Column * (Unit + Gap);
    public double Top => Placement.Row * (Unit + Gap);
    public double Width => Placement.ColumnSpan * Unit + (Placement.ColumnSpan - 1) * Gap;
    public double Height => Placement.RowSpan * Unit + (Placement.RowSpan - 1) * Gap;
    public bool IsRepository => Content.Kind is MetroTileKind.Repository or MetroTileKind.FeaturedRepository;
    public bool IsRanking => Content.Kind == MetroTileKind.RankingList;
    public bool IsLanguage => Content.Kind == MetroTileKind.Language;
    public bool IsTechnology => Content.Kind == MetroTileKind.Technology;
    public bool IsTip => Content.Kind == MetroTileKind.Tip;
    public bool IsFeatured => Content.Kind == MetroTileKind.FeaturedRepository;
    public IBrush Background { get; private set; }
    public IBrush Foreground { get; private set; }
    public IBrush SecondaryForeground { get; private set; }
    public IBrush Scrim { get; private set; }
    [ObservableProperty] private Bitmap? _backgroundImage;
    [ObservableProperty] private bool _isInRenderWindow = true;
    [ObservableProperty] private bool _isFocused;
    [ObservableProperty] private bool _isPeekVisible;
    public int ZIndex => IsFocused ? 1000 : 0;
    public double DisplayOpacity => (_matchesFilter ? 1 : .15) * _edgeOpacity * _focusOpacity;
    public double BackgroundLayerOpacity => _heldBackgroundOpacity;

    public void SetFilter(string value)
    {
        var query = value.Trim();
        _matchesFilter = string.IsNullOrEmpty(query)
            || Title.Contains(query, StringComparison.OrdinalIgnoreCase)
            || Subtitle.Contains(query, StringComparison.OrdinalIgnoreCase)
            || Caption.Contains(query, StringComparison.OrdinalIgnoreCase)
            || Language.Contains(query, StringComparison.OrdinalIgnoreCase)
            || RepositoryItem?.Repository.Topics.Any(x => x.Contains(query, StringComparison.OrdinalIgnoreCase)) == true;
        OnPropertyChanged(nameof(DisplayOpacity));
    }

    public void SetEdgeOpacity(double value)
    {
        _edgeOpacity = Math.Clamp(value, .3, 1);
        OnPropertyChanged(nameof(DisplayOpacity));
    }

    public void SetFocus(bool focused, double detailProgress)
    {
        IsFocused = focused;
        _focusOpacity = focused ? 1 : Math.Clamp(1 - detailProgress * .78, .22, 1);
        OnPropertyChanged(nameof(DisplayOpacity));
        OnPropertyChanged(nameof(ZIndex));
    }

    public void SetPointerHeld(bool held)
    {
        _heldBackgroundOpacity = held ? .2 : 1;
        IsPeekVisible = held;
        OnPropertyChanged(nameof(BackgroundLayerOpacity));
    }

    public void ApplyImageAsset(TileImageAsset asset, ITilePaletteService palettes)
    {
        BackgroundImage = asset.Bitmap;
        var palette = palettes.Create(asset.DominantColor);
        Background = Brush(palette.Background);
        Foreground = Brush(palette.Foreground);
        SecondaryForeground = Brush(palette.SecondaryForeground);
        Scrim = Brush(palette.Scrim);
        OnPropertyChanged(nameof(Background));
        OnPropertyChanged(nameof(Foreground));
        OnPropertyChanged(nameof(SecondaryForeground));
        OnPropertyChanged(nameof(Scrim));
    }

    public void RefreshSavedState() { OnPropertyChanged(nameof(SaveText)); }
    private static IBrush Brush(string value) => new SolidColorBrush(Color.Parse(value));
    private static string LanguageGlyph(string value) => value switch { "C#" => "C#", "C++" => "C+", "TypeScript" => "TS", "JavaScript" => "JS", "Python" => "Py", "Rust" => "Rs", _ => value.Length <= 2 ? value : value[..1].ToUpperInvariant() };
}
