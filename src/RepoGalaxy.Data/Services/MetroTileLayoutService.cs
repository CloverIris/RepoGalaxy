using Microsoft.EntityFrameworkCore;
using System.Security.Cryptography;
using System.Text;
using RepoGalaxy.Core.Interfaces;
using RepoGalaxy.Core.Models;
using RepoGalaxy.Data.DbContexts;
using RepoGalaxy.Data.Entities;

namespace RepoGalaxy.Data.Services;

public sealed class MetroTileLayoutService : IMetroTileLayoutService
{
    private const int ExpansionStep = 6;
    private readonly IDbContextFactory<RepoGalaxyDbContext> _factory;
    private readonly IVirtualTileWorldService? _virtualWorld;
    private readonly SemaphoreSlim _gate = new(1, 1);

    public MetroTileLayoutService(IDbContextFactory<RepoGalaxyDbContext> factory, IVirtualTileWorldService? virtualWorld = null)
    {
        _factory = factory;
        _virtualWorld = virtualWorld;
    }

    public async Task<TileBoardState> LoadAsync(string scopeKey, FeedSource source, CancellationToken cancellationToken = default)
    {
        await using var db = await _factory.CreateDbContextAsync(cancellationToken);
        var board = await db.TileBoards.AsNoTracking().Include(x => x.Placements)
            .FirstOrDefaultAsync(x => x.ScopeKey == NormalizeScope(scopeKey) && x.Source == (int)source, cancellationToken);
        return board is null ? Empty(scopeKey, source) : Map(board);
    }

    public async Task<TileBoardState> SynchronizeAsync(string scopeKey, FeedSource source, IReadOnlyList<TileContent> content, int minimumColumns, int minimumRows, bool reflow = false, CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            await using var db = await _factory.CreateDbContextAsync(cancellationToken);
            await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
            var scope = NormalizeScope(scopeKey);
            // Preserve the caller's stable feed order inside every visual
            // priority. A manual reflow can then make the newly ranked feed
            // immediately visible instead of sorting it back by content key.
            var requested = content.DistinctBy(x => (x.Kind, x.Key)).OrderBy(x => Priority(x.Kind)).ToList();
            var board = await db.TileBoards.Include(x => x.Placements)
                .FirstOrDefaultAsync(x => x.ScopeKey == scope && x.Source == (int)source, cancellationToken);
            if (board is null)
            {
                var extent = CalculateInitialExtent(requested, minimumColumns, minimumRows);
                board = new TileBoardEntity
                {
                    ScopeKey = scope,
                    Source = (int)source,
                    ExtentColumns = extent.Columns,
                    ExtentRows = extent.Rows,
                    Zoom = 1,
                    WorldSeed = Seed(scope, source)
                };
                db.TileBoards.Add(board);
            }

            board.ExtentColumns = Math.Max(board.ExtentColumns, Math.Max(12, minimumColumns));
            board.ExtentRows = Math.Max(board.ExtentRows, Math.Max(8, minimumRows));
            if (string.IsNullOrWhiteSpace(board.WorldSeed)) board.WorldSeed = Seed(scope, source);
            board.UpdatedAt = DateTimeOffset.UtcNow;

            if (reflow && board.Placements.Count > 0)
            {
                // A manual synchronization is an explicit request to bring the
                // whole current data pool back into a coherent visible mosaic.
                // Keep the board identity, camera and seed, but rebuild only
                // the real-slot mapping. Virtual skeletons are never stored.
                db.TilePlacements.RemoveRange(board.Placements);
                board.Placements.Clear();
                var extent = CalculateInitialExtent(requested, minimumColumns, minimumRows);
                board.ExtentColumns = extent.Columns;
                board.ExtentRows = extent.Rows;
                await db.SaveChangesWithRetryAsync(cancellationToken: cancellationToken);
            }

            var requestedKeys = requested.Select(x => Key(x.Kind, x.Key)).ToHashSet(StringComparer.Ordinal);
            foreach (var stale in board.Placements.Where(x => !requestedKeys.Contains(Key(ParseKind(x.ContentKind), x.ContentKey))).ToList())
                MakeVacant(stale);

            foreach (var item in requested)
            {
                var existing = board.Placements.FirstOrDefault(x => x.ContentKind == item.Kind.ToString() && x.ContentKey == item.Key);
                if (existing is not null) { Copy(item, existing); continue; }
                var span = SpanFor(item);
                var reusable = board.Placements.FirstOrDefault(x => x.IsPlaceholder && x.ContentKey.StartsWith("vacant:", StringComparison.Ordinal) && x.ColumnSpan == span.Columns && x.RowSpan == span.Rows);
                if (reusable is not null) { Copy(item, reusable); continue; }

                var position = FindPosition(board.Placements, span, board.ExtentColumns, board.ExtentRows);
                while (position is null)
                {
                    ExpandBalanced(board, minimumColumns, minimumRows);
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

    public async Task<TileBoardState> ReorderRepositoriesAsync(long boardId, IReadOnlyList<long> repositoryIds, TileWorldWindow preferredWindow, CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            await using var db = await _factory.CreateDbContextAsync(cancellationToken);
            await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
            var board = await db.TileBoards.Include(x => x.Placements).FirstOrDefaultAsync(x => x.Id == boardId, cancellationToken);
            if (board is null) return Empty("guest", FeedSource.Trending);

            var repositoryPlacements = board.Placements
                .Where(x => ParseKind(x.ContentKind) is MetroTileKind.Repository or MetroTileKind.FeaturedRepository && x.RepositoryId.HasValue)
                .ToList();
            if (repositoryPlacements.Count == 0 || repositoryIds.Count == 0) return Map(board);

            var snapshots = repositoryPlacements
                .GroupBy(x => x.RepositoryId!.Value)
                .ToDictionary(x => x.Key, x => PlacementPayload.From(x.First()));
            var requestedIds = repositoryIds.Distinct().ToList();
            var orderedPayloads = requestedIds.Where(snapshots.ContainsKey).Select(x => snapshots[x]).ToList();
            orderedPayloads.AddRange(snapshots.Where(x => !requestedIds.Contains(x.Key)).OrderBy(x => x.Key).Select(x => x.Value));

            var targetSlots = repositoryPlacements
                .OrderBy(x => DistanceSquared(
                    x.Column * 100d + (x.ColumnSpan * 100d - 4) / 2,
                    x.Row * 100d + (x.RowSpan * 100d - 4) / 2,
                    preferredWindow.CenterX,
                    preferredWindow.CenterY))
                .ThenBy(x => x.Row)
                .ThenBy(x => x.Column)
                .ToList();
            var assigned = new List<(TilePlacementEntity Slot, PlacementPayload Payload)>();
            var remaining = new List<PlacementPayload>(orderedPayloads);
            foreach (var slot in targetSlots)
            {
                var match = remaining.FindIndex(x => x.ColumnSpan == slot.ColumnSpan && x.RowSpan == slot.RowSpan);
                if (match < 0) continue;
                assigned.Add((slot, remaining[match]));
                remaining.RemoveAt(match);
            }
            var count = assigned.Count;
            if (count == 0) return Map(board);

            // SQLite checks unique indexes row by row. Moving every affected
            // placement through temporary keys makes a repository swap atomic.
            for (var i = 0; i < count; i++) assigned[i].Slot.ContentKey = $"reorder-temp:{Guid.NewGuid():N}";
            await db.SaveChangesWithRetryAsync(cancellationToken: cancellationToken);
            for (var i = 0; i < count; i++) assigned[i].Payload.ApplyTo(assigned[i].Slot);
            board.UpdatedAt = DateTimeOffset.UtcNow;
            await db.SaveChangesWithRetryAsync(cancellationToken: cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return Map(board);
        }
        finally { _gate.Release(); }
    }

    public async Task SaveCameraAsync(long boardId, CameraState camera, CancellationToken cancellationToken = default)
    {
        if (boardId <= 0) return;
        await using var db = await _factory.CreateDbContextAsync(cancellationToken);
        var board = await db.TileBoards.FindAsync([boardId], cancellationToken);
        if (board is null) return;
        board.CameraX = camera.X;
        board.CameraY = camera.Y;
        board.Zoom = Math.Clamp(camera.Zoom, .55, 8);
        board.ActiveIndexKind = camera.ActiveIndexKind is null ? null : (int)camera.ActiveIndexKind.Value;
        board.ActiveIndexKey = camera.ActiveIndexKey ?? string.Empty;
        board.UpdatedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesWithRetryAsync(cancellationToken: cancellationToken);
    }

    public async Task SaveSemanticViewportAsync(long boardId, SemanticViewportState viewport, CancellationToken cancellationToken = default)
    {
        if (boardId <= 0) return;
        await using var db = await _factory.CreateDbContextAsync(cancellationToken);
        var board = await db.TileBoards.FindAsync([boardId], cancellationToken);
        if (board is null) return;
        board.SemanticViewportX = viewport.X;
        board.SemanticViewportY = viewport.Y;
        board.SemanticViewportWidth = viewport.ViewportWidth;
        board.SemanticViewportHeight = viewport.ViewportHeight;
        board.SemanticViewportUserPositioned = viewport.IsUserPositioned;
        board.UpdatedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesWithRetryAsync(cancellationToken: cancellationToken);
    }

    public async Task ResetAsync(string? scopeKey = null, CancellationToken cancellationToken = default)
    {
        await using var db = await _factory.CreateDbContextAsync(cancellationToken);
        var query = db.TileBoards.AsQueryable();
        if (!string.IsNullOrWhiteSpace(scopeKey)) { var scope = NormalizeScope(scopeKey); query = query.Where(x => x.ScopeKey == scope); }
        await query.ExecuteDeleteAsync(cancellationToken);
    }

    private static (int Columns, int Rows) CalculateInitialExtent(IReadOnlyList<TileContent> content, int minimumColumns, int minimumRows)
    {
        var area = Math.Max(96, (int)Math.Ceiling(content.Sum(x => { var span = SpanFor(x); return span.Columns * span.Rows; }) * 1.18));
        const double targetAspect = 16d / 10d;
        var columns = RoundUp(Math.Max(Math.Max(12, minimumColumns), (int)Math.Ceiling(Math.Sqrt(area * targetAspect))), ExpansionStep);
        var rows = RoundUp(Math.Max(Math.Max(8, minimumRows), (int)Math.Ceiling(area / (double)columns)), ExpansionStep);
        return (columns, rows);
    }

    private static void ExpandBalanced(TileBoardEntity board, int minimumColumns, int minimumRows)
    {
        const double targetAspect = 16d / 10d;
        if (board.ExtentColumns / (double)board.ExtentRows > targetAspect) board.ExtentRows += ExpansionStep;
        else board.ExtentColumns += ExpansionStep;
    }

    private static (int Column, int Row)? FindPosition(IEnumerable<TilePlacementEntity> placements, TileSpan span, int columns, int rows)
    {
        var occupied = new HashSet<(int Column, int Row)>();
        foreach (var item in placements)
            for (var x = item.Column; x < item.Column + item.ColumnSpan; x++)
                for (var y = item.Row; y < item.Row + item.RowSpan; y++) occupied.Add((x, y));
        for (var row = 0; row <= rows - span.Rows; row++)
            for (var column = 0; column <= columns - span.Columns; column++)
                if (Enumerable.Range(column, span.Columns).All(x => Enumerable.Range(row, span.Rows).All(y => !occupied.Contains((x, y))))) return (column, row);
        return null;
    }

    private static void MakeVacant(TilePlacementEntity target)
    {
        target.ContentKind = MetroTileKind.Tip.ToString();
        target.ContentKey = $"vacant:{target.Id}:{Guid.NewGuid():N}";
        target.RepositoryId = null;
        target.Title = "正在发现新内容";
        target.Subtitle = "同步完成后将在这里出现";
        target.Caption = "LOADING";
        target.AccentKey = "placeholder";
        target.ImageUrl = string.Empty;
        target.SourceUrl = string.Empty;
        target.IsPlaceholder = true;
        target.UpdatedAt = DateTimeOffset.UtcNow;
    }

    private static void Copy(TileContent source, TilePlacementEntity target)
    {
        target.ContentKind = source.Kind.ToString(); target.ContentKey = source.Key; target.RepositoryId = source.RepositoryId;
        target.Title = source.Title; target.Subtitle = source.Subtitle; target.Caption = source.Caption; target.AccentKey = source.AccentKey;
        target.ImageUrl = source.ImageUrl; target.SourceUrl = source.SourceUrl; target.IsPlaceholder = source.IsPlaceholder; target.UpdatedAt = DateTimeOffset.UtcNow;
    }

    private static TileSpan SpanFor(TileContent item)
    {
        var span = TileSpan.For(item.Kind);
        if (item.Kind == MetroTileKind.Tip && item.Key.Contains(":wide:", StringComparison.Ordinal)) return new(2, 1);
        if (item.Kind == MetroTileKind.Tip && item.Key.Contains(":large:", StringComparison.Ordinal)) return new(2, 2);
        return span;
    }

    private static TileBoardState Map(TileBoardEntity board) => new(board.Id, board.ScopeKey, (FeedSource)board.Source,
        board.CameraX, board.CameraY, board.Zoom <= 0 ? 1 : Math.Max(.55, board.Zoom),
        board.ActiveIndexKind is null ? null : (SemanticIndexKind)board.ActiveIndexKind.Value, board.ActiveIndexKey,
        board.SemanticViewportX, board.SemanticViewportY,
        board.ExtentColumns, board.ExtentRows,
        board.Placements.OrderBy(x => x.Row).ThenBy(x => x.Column).Select(x => new TilePlacement(x.Id,
            new TileContent(x.ContentKey, ParseKind(x.ContentKind), x.Title, x.Subtitle, x.Caption, x.AccentKey, x.RepositoryId, x.ImageUrl, x.IsPlaceholder, x.SourceUrl),
            x.Column, x.Row, x.ColumnSpan, x.RowSpan)).ToList(),
        string.IsNullOrWhiteSpace(board.WorldSeed) ? Seed(board.ScopeKey, (FeedSource)board.Source) : board.WorldSeed,
        board.SemanticViewportWidth, board.SemanticViewportHeight, board.SemanticViewportUserPositioned);

    private static TilePlacement MapPlacement(TilePlacementEntity x) => new(x.Id,
        new TileContent(x.ContentKey, ParseKind(x.ContentKind), x.Title, x.Subtitle, x.Caption, x.AccentKey,
            x.RepositoryId, x.ImageUrl, x.IsPlaceholder, x.SourceUrl),
        x.Column, x.Row, x.ColumnSpan, x.RowSpan);

    private static TileBoardState Empty(string scopeKey, FeedSource source) => new(0, NormalizeScope(scopeKey), source, 0, 0, 1, null, string.Empty, 24, 24, 12, 8, [], Seed(NormalizeScope(scopeKey), source));
    private static int Priority(MetroTileKind kind) => kind switch { MetroTileKind.RankingList => 0, MetroTileKind.Language => 1, MetroTileKind.Technology => 2, MetroTileKind.FeaturedRepository => 3, MetroTileKind.Repository => 4, _ => 5 };
    private static int RoundUp(int value, int step) => (int)Math.Ceiling(value / (double)step) * step;
    private static string NormalizeScope(string value) => string.IsNullOrWhiteSpace(value) ? "guest" : value.Trim().ToLowerInvariant();
    private static string Key(MetroTileKind kind, string key) => $"{kind}:{key}";
    private static MetroTileKind ParseKind(string value) => Enum.TryParse<MetroTileKind>(value, out var result) ? result : MetroTileKind.Tip;
    private static string Seed(string scopeKey, FeedSource source) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes($"{NormalizeScope(scopeKey)}:{source}:tile-world-v3")))[..32];

    private static double DistanceSquared(double x, double y, double targetX, double targetY)
    {
        var dx = x - targetX;
        var dy = y - targetY;
        return dx * dx + dy * dy;
    }

    private sealed record PlacementPayload(
        string ContentKind,
        string ContentKey,
        long? RepositoryId,
        string Title,
        string Subtitle,
        string Caption,
        string AccentKey,
        string ImageUrl,
        string SourceUrl,
        bool IsPlaceholder,
        int ColumnSpan,
        int RowSpan)
    {
        public static PlacementPayload From(TilePlacementEntity source) => new(
            source.ContentKind, source.ContentKey, source.RepositoryId, source.Title,
            source.Subtitle, source.Caption, source.AccentKey, source.ImageUrl,
            source.SourceUrl, source.IsPlaceholder, source.ColumnSpan, source.RowSpan);

        public void ApplyTo(TilePlacementEntity target)
        {
            target.ContentKind = ContentKind;
            target.ContentKey = ContentKey;
            target.RepositoryId = RepositoryId;
            target.Title = Title;
            target.Subtitle = Subtitle;
            target.Caption = Caption;
            target.AccentKey = AccentKey;
            target.ImageUrl = ImageUrl;
            target.SourceUrl = SourceUrl;
            target.IsPlaceholder = IsPlaceholder;
            target.UpdatedAt = DateTimeOffset.UtcNow;
        }
    }
}
