using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using RepoGalaxy.Core.Interfaces;
using RepoGalaxy.Core.Models;
using RepoGalaxy.Data.DbContexts;
using RepoGalaxy.Data.Entities;

namespace RepoGalaxy.Data.Services;

public sealed class SemanticMosaicLayoutService : ISemanticMosaicLayoutService
{
    public const int CurrentLayoutVersion = 2;
    private const int ExpansionStep = 4;
    private readonly IDbContextFactory<RepoGalaxyDbContext> _factory;
    private readonly SemaphoreSlim _gate = new(1, 1);

    public SemanticMosaicLayoutService(IDbContextFactory<RepoGalaxyDbContext> factory) => _factory = factory;

    public async Task<SemanticMosaicState> SynchronizeAsync(long boardId, IReadOnlyList<SemanticIndexItem> items, double targetAspect, CancellationToken cancellationToken = default)
    {
        if (boardId <= 0 || items.Count == 0) return new(boardId, CurrentLayoutVersion, 1, 1, []);
        await _gate.WaitAsync(cancellationToken);
        try
        {
            await using var db = await _factory.CreateDbContextAsync(cancellationToken);
            var existing = await db.SemanticIndexPlacements
                .Where(x => x.BoardId == boardId && x.LayoutVersion == CurrentLayoutVersion)
                .OrderBy(x => x.Id).ToListAsync(cancellationToken);
            var requested = items.DistinctBy(x => x.Key).ToDictionary(x => x.Key, StringComparer.Ordinal);
            await db.SemanticIndexPlacements
                .Where(x => x.BoardId == boardId && x.LayoutVersion == CurrentLayoutVersion && !requested.Keys.Contains(x.ItemKey))
                .ExecuteDeleteAsync(cancellationToken);
            existing.RemoveAll(x => !requested.ContainsKey(x.ItemKey));

            foreach (var entity in existing)
            {
                var item = requested[entity.ItemKey];
                Update(entity, item);
            }

            var initial = CalculateExtent(items, targetAspect);
            var columns = Math.Max(initial.Columns, existing.Count == 0 ? 0 : existing.Max(x => x.Column + x.ColumnSpan));
            var rows = Math.Max(initial.Rows, existing.Count == 0 ? 0 : existing.Max(x => x.Row + x.RowSpan));
            var languageRank = items.Where(x => x.Kind == SemanticIndexKind.Language).OrderByDescending(x => x.ProjectCount).ThenBy(x => x.Title, StringComparer.OrdinalIgnoreCase).Select((x, i) => (x.Key, i)).ToDictionary(x => x.Key, x => x.i, StringComparer.Ordinal);
            var frameworkRank = items.Where(x => x.Kind == SemanticIndexKind.Framework).OrderByDescending(x => x.ProjectCount).ThenBy(x => x.Title, StringComparer.OrdinalIgnoreCase).Select((x, i) => (x.Key, i)).ToDictionary(x => x.Key, x => x.i, StringComparer.Ordinal);
            var pending = items.Where(x => existing.All(y => y.ItemKey != x.Key))
                .OrderBy(x => x.Kind).ThenBy(x => x.Title, StringComparer.OrdinalIgnoreCase).ToList();

            foreach (var item in pending)
            {
                var span = Span(item, languageRank, frameworkRank);
                var position = Find(existing, span, columns, rows);
                while (position is null)
                {
                    if (columns / (double)Math.Max(1, rows) > targetAspect) rows += ExpansionStep;
                    else columns += ExpansionStep;
                    position = Find(existing, span, columns, rows);
                }
                var entity = new SemanticIndexPlacementEntity
                {
                    BoardId = boardId, ItemKey = item.Key, LayoutVersion = CurrentLayoutVersion,
                    Column = position.Value.Column, Row = position.Value.Row,
                    ColumnSpan = span.Columns, RowSpan = span.Rows
                };
                Update(entity, item);
                existing.Add(entity);
                db.SemanticIndexPlacements.Add(entity);
            }

            await db.SaveChangesWithRetryAsync(cancellationToken: cancellationToken);
            await db.SemanticIndexPlacements
                .Where(x => x.BoardId == boardId && x.LayoutVersion != CurrentLayoutVersion)
                .ExecuteDeleteAsync(cancellationToken);
            columns = Math.Max(1, existing.Max(x => x.Column + x.ColumnSpan));
            rows = Math.Max(1, existing.Max(x => x.Row + x.RowSpan));
            return Map(boardId, columns, rows, existing);
        }
        finally { _gate.Release(); }
    }

    public async Task ResetAsync(long boardId, CancellationToken cancellationToken = default)
    {
        await using var db = await _factory.CreateDbContextAsync(cancellationToken);
        await db.SemanticIndexPlacements.Where(x => x.BoardId == boardId).ExecuteDeleteAsync(cancellationToken);
    }

    private static TileSpan Span(SemanticIndexItem item, IReadOnlyDictionary<string, int> languages, IReadOnlyDictionary<string, int> frameworks)
    {
        var rank = item.Kind == SemanticIndexKind.Language ? languages[item.Key] : frameworks[item.Key];
        if (rank < 4) return new(2, 2);
        return item.Kind == SemanticIndexKind.Framework ? new(2, 1) : new(1, 1);
    }

    private static (int Columns, int Rows) CalculateExtent(IReadOnlyList<SemanticIndexItem> items, double targetAspect)
    {
        var language = items.Count(x => x.Kind == SemanticIndexKind.Language);
        var framework = items.Count - language;
        var area = Math.Max(16, Math.Min(4, language) * 4 + Math.Max(0, language - 4) + Math.Min(4, framework) * 4 + Math.Max(0, framework - 4) * 2);
        targetAspect = Math.Clamp(targetAspect, 1.1, 2.2);
        var columns = RoundUp(Math.Max(4, (int)Math.Ceiling(Math.Sqrt(area * targetAspect))), ExpansionStep);
        var rows = RoundUp(Math.Max(4, (int)Math.Ceiling(area / (double)columns)), ExpansionStep);
        return (columns, rows);
    }

    private static (int Column, int Row)? Find(IEnumerable<SemanticIndexPlacementEntity> placements, TileSpan span, int columns, int rows)
    {
        var occupied = new HashSet<(int X, int Y)>();
        foreach (var item in placements)
            for (var x = item.Column; x < item.Column + item.ColumnSpan; x++)
                for (var y = item.Row; y < item.Row + item.RowSpan; y++) occupied.Add((x, y));
        for (var row = 0; row <= rows - span.Rows; row++)
            for (var column = 0; column <= columns - span.Columns; column++)
                if (Enumerable.Range(column, span.Columns).All(x => Enumerable.Range(row, span.Rows).All(y => !occupied.Contains((x, y))))) return (column, row);
        return null;
    }

    private static void Update(SemanticIndexPlacementEntity entity, SemanticIndexItem item)
    {
        entity.Title = item.Title;
        entity.Kind = (int)item.Kind;
        entity.ProjectCount = item.ProjectCount;
        entity.AccentKey = item.AccentKey;
        entity.ContentKeysJson = JsonSerializer.Serialize(item.ContentKeys);
        entity.UpdatedAt = DateTimeOffset.UtcNow;
    }

    private static SemanticMosaicState Map(long boardId, int columns, int rows, IEnumerable<SemanticIndexPlacementEntity> values) =>
        new(boardId, CurrentLayoutVersion, columns, rows, values.OrderBy(x => x.Row).ThenBy(x => x.Column).Select(x =>
            new SemanticMosaicPlacement(x.Id,
                new SemanticIndexItem(x.ItemKey, x.Title, (SemanticIndexKind)x.Kind, x.ProjectCount, x.AccentKey, ReadKeys(x.ContentKeysJson)),
                x.Column, x.Row, x.ColumnSpan, x.RowSpan)).ToList());

    private static IReadOnlyList<string> ReadKeys(string json) { try { return JsonSerializer.Deserialize<List<string>>(json) ?? []; } catch { return []; } }
    private static int RoundUp(int value, int step) => (int)Math.Ceiling(value / (double)step) * step;
}
