namespace RepoGalaxy.Core.Models;

public enum MetroTileKind
{
    Tip,
    Language,
    Technology,
    Repository,
    FeaturedRepository,
    RankingList
}

public readonly record struct TileSpan(int Columns, int Rows)
{
    public static TileSpan For(MetroTileKind kind) => kind switch
    {
        MetroTileKind.Language => new(1, 1),
        MetroTileKind.Technology => new(2, 1),
        MetroTileKind.Repository => new(6, 1),
        MetroTileKind.FeaturedRepository or MetroTileKind.RankingList => new(2, 2),
        _ => new(1, 1)
    };
}

public sealed record TileContent(
    string Key,
    MetroTileKind Kind,
    string Title,
    string Subtitle = "",
    string Caption = "",
    string AccentKey = "",
    long? RepositoryId = null,
    string ImageUrl = "",
    bool IsPlaceholder = false,
    string SourceUrl = "");

public sealed record TilePlacement(
    long Id,
    TileContent Content,
    int Column,
    int Row,
    int ColumnSpan,
    int RowSpan);

public sealed record TileBoardState(
    long Id,
    string ScopeKey,
    FeedSource Source,
    double CameraX,
    double CameraY,
    double Zoom,
    SemanticIndexKind? ActiveIndexKind,
    string ActiveIndexKey,
    double SemanticViewportX,
    double SemanticViewportY,
    int ExtentColumns,
    int ExtentRows,
    IReadOnlyList<TilePlacement> Placements,
    string WorldSeed = "",
    double SemanticViewportWidth = 0,
    double SemanticViewportHeight = 0,
    bool SemanticViewportUserPositioned = false);

public enum SemanticIndexKind { Language, Framework }

public enum SemanticIndexSignalOrigin { Feed, DedicatedTile, Subscription, LocalRepository }

public sealed record SemanticIndexSignal(
    SemanticIndexKind Kind,
    string Title,
    string ContentKey,
    string AssociatedLanguage = "",
    SemanticIndexSignalOrigin Origin = SemanticIndexSignalOrigin.Feed,
    bool HasOfficialLink = false);

public sealed record SemanticIndexPolicy(
    int MaximumLanguages = 16,
    int MaximumFrameworks = 32,
    int MinimumFrameworkRepositories = 2,
    int MaximumTitleLength = 32);

public sealed record SemanticIndexCatalogResult(
    IReadOnlyList<SemanticIndexItem> Items,
    int RejectedSignalCount);

public readonly record struct SemanticViewportState(
    double X,
    double Y,
    double ViewportWidth = 0,
    double ViewportHeight = 0,
    bool IsUserPositioned = false);

public readonly record struct TileChunkCoordinate(int X, int Y);

public readonly record struct TileWorldWindow(double X, double Y, double Width, double Height)
{
    public double CenterX => X + Width / 2;
    public double CenterY => Y + Height / 2;
}

public readonly record struct TileWorldViewport(
    double CameraX,
    double CameraY,
    double Zoom,
    double Width,
    double Height)
{
    public TileWorldWindow WorldWindow => new(
        CameraX,
        CameraY,
        Width / Math.Max(.01, Zoom),
        Height / Math.Max(.01, Zoom));
}

public readonly record struct TileWorldContentBounds(double Left, double Top, double Right, double Bottom)
{
    public double Width => Math.Max(0, Right - Left);
    public double Height => Math.Max(0, Bottom - Top);
    public double CenterX => Left + Width / 2;
    public double CenterY => Top + Height / 2;
    public static TileWorldContentBounds Empty => new(0, 0, 1, 1);
}

public readonly record struct TileWorldAnchor(string ContentKey, double WorldX, double WorldY);

public sealed record TileWorldSnapshot(
    long BoardId,
    string WorldSeed,
    IReadOnlyList<TilePlacement> Placements,
    TileWorldContentBounds ContentBounds,
    TileWorldAnchor? Anchor);

public sealed record VirtualTileSlot(
    string Key,
    TileChunkCoordinate Chunk,
    int Column,
    int Row,
    int ColumnSpan,
    int RowSpan,
    TileContent Content);

public sealed record TileSearchCandidate(
    string Key,
    MetroTileKind Kind,
    string Title,
    string Subtitle,
    string Language,
    IReadOnlyList<string> Topics,
    string Reason,
    TileWorldRect Bounds);

public sealed record SpatialTileSearchResult(string Query, IReadOnlyList<TileSearchCandidate> Matches);

public sealed record SemanticIndexItem(
    string Key,
    string Title,
    SemanticIndexKind Kind,
    int ProjectCount,
    string AccentKey,
    IReadOnlyList<string> ContentKeys);

public sealed record SemanticMosaicPlacement(
    long Id,
    SemanticIndexItem Item,
    int Column,
    int Row,
    int ColumnSpan,
    int RowSpan);

public sealed record SemanticMosaicState(
    long BoardId,
    int ExtentColumns,
    int ExtentRows,
    IReadOnlyList<SemanticMosaicPlacement> Placements);

public readonly record struct CameraState(
    double X,
    double Y,
    double Zoom,
    string FocusedContentKey = "",
    SemanticIndexKind? ActiveIndexKind = null,
    string ActiveIndexKey = "");

public readonly record struct ZoomScaleProfile(
    double MinimumZoom,
    double SemanticFullyVisibleZoom,
    double SemanticFadeEndZoom,
    double DefaultZoom,
    double MaximumZoom);

public readonly record struct TileWorldRect(double X, double Y, double Width, double Height)
{
    public double CenterX => X + Width / 2;
    public double CenterY => Y + Height / 2;
}

public enum ZoomVisualMode { SemanticIndex, TileBoard, DetailTransition, Detail }

public enum DetailPresentationState { Board, Peek, Portal, Snapping, Full, Exiting }

public readonly record struct DetailPortalGeometry(double X, double Y, double Width, double Height)
{
    public static DetailPortalGeometry Empty => new(0, 0, 0, 0);
    public bool IsVisible => Width > 0 && Height > 0;
}

public readonly record struct DetailPortalDecision(DetailPresentationState State, bool StartSnap, bool ExitFull, bool SuppressRightRail);

public readonly record struct ZoomTransitionState(
    ZoomVisualMode Mode,
    double SemanticIndexOpacity,
    double TileBoardOpacity,
    double DetailProgress,
    double FitScale,
    bool ShouldPrefetch);

public sealed record TilePalette(string Background, string Foreground, string SecondaryForeground, string Scrim);

public enum TileActionKind
{
    None = 0,
    Like = 1,
    Bookmark = 2,
    GitHubStar = 3,
    Dislike = 4
}

public readonly record struct TileActionHitRegion(TileActionKind Action, TileWorldRect Bounds);

public readonly record struct TileImageLayout(TileWorldRect Destination, TileWorldRect Source);

public sealed record RepositoryTileLayout(
    TileWorldRect Cover,
    TileWorldRect Text,
    IReadOnlyList<TileActionHitRegion> Actions,
    bool UsesWideLayout,
    bool UsesCover);

public sealed record RepositoryTileActionState(
    bool IsLiked,
    bool IsStarred,
    bool IsSuppressed);

public sealed record RepositoryStarResult(
    bool Success,
    bool RequiresLogin,
    bool IsStarred,
    int Stars,
    string? ErrorCode = null);

public sealed record CategorySuppressionResult(
    IReadOnlyList<string> Signals,
    int AffectedRepositoryCount);

public sealed record TipDefinition(
    string Key,
    string Category,
    string Title,
    string Body,
    string AccentKey,
    int ColumnSpan = 1,
    int RowSpan = 1,
    int? Month = null,
    int? Day = null,
    string Attribution = "",
    string SourceUrl = "");
