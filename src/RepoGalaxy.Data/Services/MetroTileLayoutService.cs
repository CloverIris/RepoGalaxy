using Microsoft.EntityFrameworkCore;
using RepoGalaxy.Core.Interfaces;
using RepoGalaxy.Core.Models;
using RepoGalaxy.Data.DbContexts;
using RepoGalaxy.Data.Entities;

namespace RepoGalaxy.Data.Services;

public sealed class MetroTileLayoutService : IMetroTileLayoutService
{
    public const int CurrentLayoutVersion = 1;
    private readonly IDbContextFactory<RepoGalaxyDbContext> _factory;
    private readonly SemaphoreSlim _gate = new(1, 1);

    public MetroTileLayoutService(IDbContextFactory<RepoGalaxyDbContext> factory) => _factory = factory;

    public async Task<TileBoardState> LoadAsync(string scopeKey, FeedSource source, CancellationToken cancellationToken = default)
    {
        await using var db = await _factory.CreateDbContextAsync(cancellationToken);
        var board = await db.TileBoards.AsNoTracking().Include(x => x.Placements)
            .FirstOrDefaultAsync(x => x.ScopeKey == NormalizeScope(scopeKey) && x.Source == (int)source && x.LayoutVersion == CurrentLayoutVersion, cancellationToken);
        return board is null ? Empty(scopeKey, source) : Map(board);
    }

    public async Task<TileBoardState> SynchronizeAsync(string scopeKey, FeedSource source, IReadOnlyList<TileContent> content, int minimumColumns, int minimumRows, CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            await using var db = await _factory.CreateDbContextAsync(cancellationToken);
            await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
            var scope = NormalizeScope(scopeKey);
            var board = await db.TileBoards.Include(x => x.Placements)
                .FirstOrDefaultAsync(x => x.ScopeKey == scope && x.Source == (int)source && x.LayoutVersion == CurrentLayoutVersion, cancellationToken);
            if (board is null)
            {
                board = new TileBoardEntity { ScopeKey = scope, Source = (int)source, LayoutVersion = CurrentLayoutVersion };
                db.TileBoards.Add(board);
            }

            board.ExtentColumns = Math.Max(board.ExtentColumns, Math.Max(12, minimumColumns));
            board.ExtentRows = Math.Max(board.ExtentRows, Math.Max(6, minimumRows));
            board.UpdatedAt = DateTimeOffset.UtcNow;

            var requested = content.DistinctBy(x => (x.Kind, x.Key)).ToList();
            var requestedKeys = requested.Select(x => Key(x.Kind, x.Key)).ToHashSet(StringComparer.Ordinal);
            foreach (var stale in board.Placements.Where(x => !requestedKeys.Contains(Key(ParseKind(x.ContentKind), x.ContentKey))).ToList())
            {
                stale.ContentKind = MetroTileKind.Tip.ToString();
                stale.ContentKey = $"vacant:{stale.Id}:{Guid.NewGuid():N}";
                stale.RepositoryId = null;
                stale.Title = "正在发现新内容";
                stale.Subtitle = "同步完成后将在这里出现";
                stale.Caption = "LOADING";
                stale.AccentKey = "placeholder";
                stale.ImageUrl = string.Empty;
                stale.IsPlaceholder = true;
                stale.UpdatedAt = DateTimeOffset.UtcNow;
            }

            foreach (var item in requested)
            {
                var existing = board.Placements.FirstOrDefault(x => x.ContentKind == item.Kind.ToString() && x.ContentKey == item.Key);
                if (existing is not null) { Copy(item, existing); continue; }
                var span = TileSpan.For(item.Kind);
                if (item.Kind == MetroTileKind.Tip && item.Key.Contains(":wide:", StringComparison.Ordinal)) span = new(2, 1);
                if (item.Kind == MetroTileKind.Tip && item.Key.Contains(":large:", StringComparison.Ordinal)) span = new(2, 2);
                var reusable = board.Placements.FirstOrDefault(x => x.IsPlaceholder && x.ColumnSpan == span.Columns && x.RowSpan == span.Rows);
                if (reusable is not null) { Copy(item, reusable); continue; }
                var position = FindPosition(board.Placements, span, board.ExtentColumns, board.ExtentRows);
                while (position is null)
                {
                    board.ExtentColumns += 6;
                    position = FindPosition(board.Placements, span, board.ExtentColumns, board.ExtentRows);
                }
                var placement = new TilePlacementEntity { Board = board, Column = position.Value.Column, Row = position.Value.Row, ColumnSpan = span.Columns, RowSpan = span.Rows };
                Copy(item, placement);
                board.Placements.Add(placement);
            }

            await db.SaveChangesWithRetryAsync(cancellationToken: cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return Map(board);
        }
        finally { _gate.Release(); }
    }

    public async Task SaveViewportAsync(long boardId, double x, double y, CancellationToken cancellationToken = default)
    {
        if (boardId <= 0) return;
        await using var db = await _factory.CreateDbContextAsync(cancellationToken);
        var board = await db.TileBoards.FindAsync([boardId], cancellationToken);
        if (board is null) return;
        board.ViewportX = Math.Max(0, x); board.ViewportY = Math.Max(0, y); board.UpdatedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesWithRetryAsync(cancellationToken: cancellationToken);
    }

    public async Task ResetAsync(string? scopeKey = null, CancellationToken cancellationToken = default)
    {
        await using var db = await _factory.CreateDbContextAsync(cancellationToken);
        var query = db.TileBoards.AsQueryable();
        if (!string.IsNullOrWhiteSpace(scopeKey)) { var scope = NormalizeScope(scopeKey); query = query.Where(x => x.ScopeKey == scope); }
        await query.ExecuteDeleteAsync(cancellationToken);
    }

    private static (int Column, int Row)? FindPosition(IEnumerable<TilePlacementEntity> placements, TileSpan span, int columns, int rows)
    {
        var occupied = new HashSet<(int Column, int Row)>();
        foreach (var item in placements)
            for (var x = item.Column; x < item.Column + item.ColumnSpan; x++)
                for (var y = item.Row; y < item.Row + item.RowSpan; y++) occupied.Add((x, y));
        for (var column = 0; column <= columns - span.Columns; column++)
            for (var row = 0; row <= rows - span.Rows; row++)
                if (Enumerable.Range(column, span.Columns).All(x => Enumerable.Range(row, span.Rows).All(y => !occupied.Contains((x, y))))) return (column, row);
        return null;
    }

    private static void Copy(TileContent source, TilePlacementEntity target)
    {
        target.ContentKind = source.Kind.ToString(); target.ContentKey = source.Key; target.RepositoryId = source.RepositoryId;
        target.Title = source.Title; target.Subtitle = source.Subtitle; target.Caption = source.Caption; target.AccentKey = source.AccentKey;
        target.ImageUrl = source.ImageUrl; target.IsPlaceholder = source.IsPlaceholder; target.UpdatedAt = DateTimeOffset.UtcNow;
    }

    private static TileBoardState Map(TileBoardEntity board) => new(board.Id, board.ScopeKey, (FeedSource)board.Source, board.LayoutVersion, board.ViewportX, board.ViewportY, board.ExtentColumns, board.ExtentRows,
        board.Placements.OrderBy(x => x.Column).ThenBy(x => x.Row).Select(x => new TilePlacement(x.Id,
            new TileContent(x.ContentKey, ParseKind(x.ContentKind), x.Title, x.Subtitle, x.Caption, x.AccentKey, x.RepositoryId, x.ImageUrl, x.IsPlaceholder),
            x.Column, x.Row, x.ColumnSpan, x.RowSpan)).ToList());
    private static TileBoardState Empty(string scopeKey, FeedSource source) => new(0, NormalizeScope(scopeKey), source, CurrentLayoutVersion, 0, 0, 12, 6, []);
    private static string NormalizeScope(string value) => string.IsNullOrWhiteSpace(value) ? "guest" : value.Trim().ToLowerInvariant();
    private static string Key(MetroTileKind kind, string key) => $"{kind}:{key}";
    private static MetroTileKind ParseKind(string value) => Enum.TryParse<MetroTileKind>(value, out var result) ? result : MetroTileKind.Tip;
}
