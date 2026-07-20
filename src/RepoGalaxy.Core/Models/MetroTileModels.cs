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
    bool IsPlaceholder = false);

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
    int LayoutVersion,
    double ViewportX,
    double ViewportY,
    int ExtentColumns,
    int ExtentRows,
    IReadOnlyList<TilePlacement> Placements);

public sealed record TilePalette(string Background, string Foreground, string SecondaryForeground, string Scrim);

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
    string Attribution = "");

