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
    private bool _matchesFilter = true;
    private double _heldBackgroundOpacity = 1;
    private bool _isCategorySuppressed;
    private bool _isLiked;
    private bool _isStarred;

    public MetroTileViewModel(
        TilePlacement placement,
        TilePalette palette,
        FeedItemViewModel? repository = null,
        DashboardListViewModel? ranking = null)
    {
        Placement = placement;
        RepositoryItem = repository;
        Ranking = ranking;
        RenderId = StringComparer.Ordinal.GetHashCode(placement.Content.Key);
        Background = Brush(palette.Background);
        Foreground = Brush(palette.Foreground);
        SecondaryForeground = Brush(palette.SecondaryForeground);
        Scrim = Brush(palette.Scrim);
        RepositoryLayout = CalculateRepositoryLayout(placement, usesCover: false);
        (SubtitleLine1, SubtitleLine2) = WrapSubtitle(Subtitle, placement.ColumnSpan == 6 ? 62 : 30);
    }

    public TilePlacement Placement { get; }
    public TileContent Content => Placement.Content;
    public FeedItemViewModel? RepositoryItem { get; }
    public DashboardListViewModel? Ranking { get; }
    public long Id => Placement.Id;
    public string Key => Content.Key;
    public int RenderId { get; }
    public string Title => RepositoryItem?.Repository.FullName ?? Content.Title;
    public string Subtitle => RepositoryItem?.Repository.Description ?? Content.Subtitle;
    public string SubtitleLine1 { get; }
    public string SubtitleLine2 { get; }
    public string Caption => RepositoryItem?.ReasonText ?? Content.Caption;
    public string Language => RepositoryItem?.Repository.PrimaryLanguage ?? Content.AccentKey;
    public string Stars => RepositoryItem is null ? string.Empty : $"★ {RepositoryItem.Repository.StarsFormatted}";
    public string StarCountText => RepositoryItem?.Repository.StarsFormatted ?? "0";
    public string Forks => RepositoryItem is null ? string.Empty : $"Fork {RepositoryItem.Repository.ForksFormatted}";
    public string SaveText => RepositoryItem?.SaveText ?? string.Empty;
    public string KindLabel => Content.Kind switch
    {
        MetroTileKind.Language => "LANGUAGE",
        MetroTileKind.Technology => "STACK",
        MetroTileKind.Repository => "REPOSITORY",
        MetroTileKind.FeaturedRepository => "FEATURED",
        MetroTileKind.RankingList => "TOP 5",
        _ => Content.IsPlaceholder ? "LOADING · TIP" : "TIP"
    };
    public string Glyph => Content.Kind switch
    {
        MetroTileKind.Language => LanguageGlyph(Content.Title),
        MetroTileKind.Technology => "#",
        MetroTileKind.Repository or MetroTileKind.FeaturedRepository => "</>",
        MetroTileKind.RankingList => "↗",
        _ => "·"
    };
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
    public bool IsLiked => _isLiked;
    public bool IsStarred => _isStarred;
    public bool IsCategorySuppressed => _isCategorySuppressed;
    public RepositoryTileLayout? RepositoryLayout { get; private set; }
    public IBrush Background { get; private set; }
    public IBrush Foreground { get; private set; }
    public IBrush SecondaryForeground { get; private set; }
    public IBrush Scrim { get; private set; }
    [ObservableProperty] private Bitmap? _backgroundImage;
    [ObservableProperty] private bool _isPeekVisible;
    public double DisplayOpacity => (_matchesFilter ? 1 : .15) * (_isCategorySuppressed ? .45 : 1);
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

    public bool MatchesSearch(string value)
    {
        var query = value.Trim();
        return query.Length > 0
            && (Title.Contains(query, StringComparison.OrdinalIgnoreCase)
                || Subtitle.Contains(query, StringComparison.OrdinalIgnoreCase)
                || Caption.Contains(query, StringComparison.OrdinalIgnoreCase)
                || Language.Contains(query, StringComparison.OrdinalIgnoreCase)
                || RepositoryItem?.Repository.Topics.Any(x => x.Contains(query, StringComparison.OrdinalIgnoreCase)) == true);
    }

    public void SetPointerHeld(bool held)
    {
        _heldBackgroundOpacity = held ? .2 : 1;
        IsPeekVisible = held;
        OnPropertyChanged(nameof(BackgroundLayerOpacity));
    }

    public void ApplyActionState(RepositoryTileActionState state)
    {
        _isLiked = state.IsLiked;
        _isStarred = state.IsStarred;
        _isCategorySuppressed = state.IsSuppressed;
        OnPropertyChanged(nameof(IsLiked));
        OnPropertyChanged(nameof(IsStarred));
        OnPropertyChanged(nameof(IsCategorySuppressed));
        OnPropertyChanged(nameof(DisplayOpacity));
    }

    public void SetLiked(bool value)
    {
        _isLiked = value;
        OnPropertyChanged(nameof(IsLiked));
    }

    public void SetStarred(bool value, int stars)
    {
        _isStarred = value;
        if (RepositoryItem is not null) RepositoryItem.Repository.Repository.Stars = stars;
        OnPropertyChanged(nameof(IsStarred));
        OnPropertyChanged(nameof(Stars));
        OnPropertyChanged(nameof(StarCountText));
    }

    public void SetCategorySuppressed(bool value)
    {
        if (_isCategorySuppressed == value) return;
        _isCategorySuppressed = value;
        OnPropertyChanged(nameof(IsCategorySuppressed));
        OnPropertyChanged(nameof(DisplayOpacity));
    }

    public void ApplyImageAsset(TileImageAsset asset, ITilePaletteService palettes)
    {
        RepositoryLayout = CalculateRepositoryLayout(Placement, usesCover: true);
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
        OnPropertyChanged(nameof(RepositoryLayout));
    }

    public void RefreshSavedState() => OnPropertyChanged(nameof(SaveText));

    public static RepositoryTileLayout? CalculateRepositoryLayout(TilePlacement placement, bool usesCover)
    {
        if (placement.Content.Kind is not (MetroTileKind.Repository or MetroTileKind.FeaturedRepository)) return null;
        var width = placement.ColumnSpan * Unit + (placement.ColumnSpan - 1) * Gap;
        var height = placement.RowSpan * Unit + (placement.RowSpan - 1) * Gap;
        const double inset = 8;
        const double actionGap = 4;
        const double actionSize = 28;
        const double starWidth = 76;
        var right = width - inset;
        var actions = new TileActionHitRegion[4];
        actions[3] = new(TileActionKind.Dislike, new(right - actionSize, height - inset - actionSize, actionSize, actionSize));
        right -= actionSize + actionGap;
        actions[2] = new(TileActionKind.GitHubStar, new(right - starWidth, height - inset - actionSize, starWidth, actionSize));
        right -= starWidth + actionGap;
        actions[1] = new(TileActionKind.Bookmark, new(right - actionSize, height - inset - actionSize, actionSize, actionSize));
        right -= actionSize + actionGap;
        actions[0] = new(TileActionKind.Like, new(right - actionSize, height - inset - actionSize, actionSize, actionSize));
        var wide = placement.RowSpan == 1;
        var cover = usesCover
            ? wide ? new TileWorldRect(0, 0, height, height) : new TileWorldRect(0, 0, width, height)
            : new TileWorldRect(0, 0, 0, 0);
        var textLeft = wide && usesCover ? height + 12 : inset;
        var text = new TileWorldRect(textLeft, inset, Math.Max(1, width - textLeft - inset), Math.Max(1, height - inset * 2));
        return new(cover, text, actions, wide, usesCover);
    }

    private static (string First, string Second) WrapSubtitle(string value, int lineLength)
    {
        if (string.IsNullOrWhiteSpace(value)) return (string.Empty, string.Empty);
        var normalized = value.ReplaceLineEndings(" ").Trim();
        if (normalized.Length <= lineLength) return (normalized, string.Empty);
        var split = normalized.LastIndexOf(' ', Math.Min(lineLength, normalized.Length - 1));
        if (split < lineLength / 2) split = Math.Min(lineLength, normalized.Length);
        var first = normalized[..split].TrimEnd();
        var remainder = normalized[split..].TrimStart();
        if (remainder.Length > lineLength)
            remainder = remainder[..Math.Max(1, lineLength - 1)].TrimEnd() + "…";
        return (first, remainder);
    }

    private static IBrush Brush(string value) => new SolidColorBrush(Color.Parse(value));
    private static string LanguageGlyph(string value) => value switch
    {
        "C#" => "C#",
        "C++" => "C+",
        "TypeScript" => "TS",
        "JavaScript" => "JS",
        "Python" => "Py",
        "Rust" => "Rs",
        _ => value.Length <= 2 ? value : value[..1].ToUpperInvariant()
    };
}
