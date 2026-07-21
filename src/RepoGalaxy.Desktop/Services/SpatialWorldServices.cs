using System.Security.Cryptography;
using System.Text;
using RepoGalaxy.Core.Interfaces;
using RepoGalaxy.Core.Models;

namespace RepoGalaxy.Desktop.Services;

public sealed class SpatialTileSearchService : ISpatialTileSearchService
{
    public SpatialTileSearchResult Search(string query, IReadOnlyList<TileSearchCandidate> candidates, double cameraCenterX, double cameraCenterY)
    {
        query = query.Trim();
        if (query.Length == 0) return new(query, []);
        var matches = candidates.Where(x => Matches(x, query))
            .OrderBy(x => IsRepository(x.Kind) ? 0 : 1)
            .ThenBy(x => DistanceSquared(x.Bounds.CenterX, x.Bounds.CenterY, cameraCenterX, cameraCenterY))
            .ThenBy(x => x.Key, StringComparer.Ordinal)
            .ToList();
        return new(query, matches);
    }

    public static bool Matches(TileSearchCandidate item, string query) =>
        item.Title.Contains(query, StringComparison.OrdinalIgnoreCase)
        || item.Subtitle.Contains(query, StringComparison.OrdinalIgnoreCase)
        || item.Language.Contains(query, StringComparison.OrdinalIgnoreCase)
        || item.Reason.Contains(query, StringComparison.OrdinalIgnoreCase)
        || item.Topics.Any(x => x.Contains(query, StringComparison.OrdinalIgnoreCase));

    private static bool IsRepository(MetroTileKind kind) => kind is MetroTileKind.Repository or MetroTileKind.FeaturedRepository;
    private static double DistanceSquared(double x, double y, double targetX, double targetY) { var dx = x - targetX; var dy = y - targetY; return dx * dx + dy * dy; }
}

public sealed class VirtualTileWorldService : IVirtualTileWorldService
{
    public const int ChunkColumns = 12;
    public const int ChunkRows = 8;
    public const int UnitWithGap = 100;
    private static readonly string[] Accents = ["C#", "Java", "JavaScript", "TypeScript", "Python", "Go", "Rust", "Kotlin", "Vue", "React", "Git", "hardware"];
    private readonly object _cacheGate = new();
    private readonly Dictionary<ChunkCacheKey, ChunkCacheEntry> _chunkCache = [];
    private long _cacheClock;

    public IReadOnlyList<VirtualTileSlot> Materialize(string boardSeed, TileWorldWindow window, IReadOnlyList<TilePlacement> persistentPlacements, IReadOnlyList<TipDefinition> tips)
    {
        if (window.Width <= 0 || window.Height <= 0 || tips.Count == 0) return [];
        var left = FloorChunk(window.X, ChunkColumns * UnitWithGap) - 1;
        var right = FloorChunk(window.X + window.Width, ChunkColumns * UnitWithGap) + 1;
        var top = FloorChunk(window.Y, ChunkRows * UnitWithGap) - 1;
        var bottom = FloorChunk(window.Y + window.Height, ChunkRows * UnitWithGap) + 1;
        var result = new List<VirtualTileSlot>();
        var occupied = OccupiedCells(persistentPlacements);
        var tipsKey = string.Join('\u001f', tips.Select(x => x.Key));
        for (var cy = top; cy <= bottom; cy++)
            for (var cx = left; cx <= right; cx++)
                foreach (var slot in GetChunk(boardSeed, new(cx, cy), tips, tipsKey))
                    if (!IsOccupied(slot, occupied)) result.Add(slot);
        TilePerformanceMetrics.SkeletonCount(result.Count);
        return result;
    }

    public (int Column, int Row) FindNearestCompatibleSlot(string boardSeed, TileWorldWindow preferredWindow, TileSpan span, IReadOnlyList<TilePlacement> persistentPlacements)
    {
        var tips = new[] { new TipDefinition("slot", "LOADING", "正在发现", "数据到达后将在这里原位填充。", "placeholder", span.Columns, span.Rows) };
        var centerColumn = (int)Math.Floor(preferredWindow.CenterX / UnitWithGap);
        var centerRow = (int)Math.Floor(preferredWindow.CenterY / UnitWithGap);
        var centerChunk = new TileChunkCoordinate(FloorChunk(centerColumn, ChunkColumns), FloorChunk(centerRow, ChunkRows));
        for (var radius = 0; radius <= 64; radius++)
        {
            var candidates = Ring(centerChunk, radius)
                .SelectMany(chunk => Slots(boardSeed, chunk, tips))
                .Where(x => x.ColumnSpan == span.Columns && x.RowSpan == span.Rows)
                .Where(slot => !persistentPlacements.Any(x => Overlaps(slot.Column, slot.Row, slot.ColumnSpan, slot.RowSpan, x.Column, x.Row, x.ColumnSpan, x.RowSpan)))
                .OrderBy(slot => Math.Pow(slot.Column - centerColumn, 2) + Math.Pow(slot.Row - centerRow, 2))
                .ThenBy(x => x.Row).ThenBy(x => x.Column).FirstOrDefault();
            if (candidates is not null) return (candidates.Column, candidates.Row);
        }
        return (centerColumn, centerRow);
    }

    private static IEnumerable<VirtualTileSlot> Slots(string seed, TileChunkCoordinate chunk, IReadOnlyList<TipDefinition> tips)
    {
        var spans = Template(chunk, seed);
        for (var i = 0; i < spans.Count; i++)
        {
            var value = Stable(seed, chunk.X, chunk.Y, i);
            var tip = tips[(int)(value % (ulong)tips.Count)];
            var span = spans[i];
            var accent = Accents[(int)((value >> 8) % (ulong)Accents.Length)];
            var column = chunk.X * ChunkColumns + span.Column;
            var row = chunk.Y * ChunkRows + span.Row;
            var key = $"virtual:{chunk.X}:{chunk.Y}:{i}";
            yield return new(key, chunk, column, row, span.Columns, span.Rows,
                new TileContent(key, MetroTileKind.Tip, tip.Title, tip.Body, $"{tip.Category} · 加载占位", accent, IsPlaceholder: true, SourceUrl: tip.SourceUrl));
        }
    }

    private IReadOnlyList<VirtualTileSlot> GetChunk(string seed, TileChunkCoordinate chunk, IReadOnlyList<TipDefinition> tips, string tipsKey)
    {
        var key = new ChunkCacheKey(seed, chunk.X, chunk.Y, tipsKey);
        lock (_cacheGate)
        {
            if (_chunkCache.TryGetValue(key, out var entry))
            {
                entry.LastAccess = ++_cacheClock;
                TilePerformanceMetrics.ChunkCache(true);
                return entry.Slots;
            }
        }

        var slots = Slots(seed, chunk, tips).ToArray();
        lock (_cacheGate)
        {
            TilePerformanceMetrics.ChunkCache(false);
            if (_chunkCache.Count >= 32)
            {
                var oldest = _chunkCache.MinBy(x => x.Value.LastAccess).Key;
                _chunkCache.Remove(oldest);
            }
            _chunkCache[key] = new(slots, ++_cacheClock);
        }
        return slots;
    }

    private static HashSet<(int Column, int Row)> OccupiedCells(IReadOnlyList<TilePlacement> placements)
    {
        var result = new HashSet<(int, int)>();
        foreach (var placement in placements)
            for (var row = placement.Row; row < placement.Row + placement.RowSpan; row++)
                for (var column = placement.Column; column < placement.Column + placement.ColumnSpan; column++) result.Add((column, row));
        return result;
    }

    private static bool IsOccupied(VirtualTileSlot slot, HashSet<(int Column, int Row)> occupied)
    {
        for (var row = slot.Row; row < slot.Row + slot.RowSpan; row++)
            for (var column = slot.Column; column < slot.Column + slot.ColumnSpan; column++)
                if (occupied.Contains((column, row))) return true;
        return false;
    }

    private static IReadOnlyList<(int Column, int Row, int Columns, int Rows)> Template(TileChunkCoordinate chunk, string seed)
    {
        var mirror = (Stable(seed, chunk.X, chunk.Y, -1) & 1) == 1;
        var values = new List<(int, int, int, int)>();
        AddRow(0, 6, 1); AddRow(1, 2, 2); AddRow(3, 2, 1); AddRow(4, 1, 1); AddRow(5, 2, 2); AddRow(7, 6, 1);
        return mirror ? values.Select(x => (ChunkColumns - x.Item1 - x.Item3, x.Item2, x.Item3, x.Item4)).ToList() : values;

        void AddRow(int row, int width, int height)
        {
            for (var column = 0; column < ChunkColumns; column += width) values.Add((column, row, width, height));
        }
    }

    private static IEnumerable<TileChunkCoordinate> Ring(TileChunkCoordinate center, int radius)
    {
        if (radius == 0) { yield return center; yield break; }
        for (var x = -radius; x <= radius; x++) { yield return new(center.X + x, center.Y - radius); yield return new(center.X + x, center.Y + radius); }
        for (var y = -radius + 1; y < radius; y++) { yield return new(center.X - radius, center.Y + y); yield return new(center.X + radius, center.Y + y); }
    }

    private static int FloorChunk(double value, int size) => (int)Math.Floor(value / size);
    private static bool Overlaps(int x, int y, int w, int h, int ox, int oy, int ow, int oh) => x < ox + ow && x + w > ox && y < oy + oh && y + h > oy;
    private static ulong Stable(string seed, int x, int y, int slot)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes($"{seed}:{x}:{y}:{slot}"));
        return BitConverter.ToUInt64(bytes, 0);
    }

    private sealed record ChunkCacheKey(string Seed, int X, int Y, string TipsKey);
    private sealed class ChunkCacheEntry(IReadOnlyList<VirtualTileSlot> slots, long lastAccess) { public IReadOnlyList<VirtualTileSlot> Slots { get; } = slots; public long LastAccess { get; set; } = lastAccess; }
}
