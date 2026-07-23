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
        var covered = new HashSet<(int Column, int Row)>(occupied);
        var tipsKey = string.Join('\u001f', tips.Select(x => x.Key));
        for (var cy = top; cy <= bottom; cy++)
            for (var cx = left; cx <= right; cx++)
            {
                foreach (var slot in GetChunk(boardSeed, new(cx, cy), tips, tipsKey))
                {
                    if (IsOccupied(slot, occupied)) continue;
                    result.Add(slot);
                    MarkCovered(slot, covered);
                }

                // A real tile may intersect only part of a large deterministic
                // skeleton slot. Dropping that whole slot left visible seams.
                // Fill just the uncovered residual cells with stable 1x1 tiles;
                // the normal mixed-span mosaic remains untouched elsewhere.
                for (var row = cy * ChunkRows; row < (cy + 1) * ChunkRows; row++)
                    for (var column = cx * ChunkColumns; column < (cx + 1) * ChunkColumns; column++)
                    {
                        if (covered.Contains((column, row))) continue;
                        var fallback = FallbackSlot(boardSeed, new(cx, cy), column, row, tips);
                        result.Add(fallback);
                        covered.Add((column, row));
                    }
            }
        TilePerformanceMetrics.SkeletonCount(result.Count);
        return result;
    }

    public (int Column, int Row) FindNearestCompatibleSlot(string boardSeed, TileWorldWindow preferredWindow, TileSpan span, IReadOnlyList<TilePlacement> persistentPlacements)
    {
        var occupied = OccupiedCells(persistentPlacements);
        var centerColumn = (int)Math.Floor(preferredWindow.CenterX / UnitWithGap);
        var centerRow = (int)Math.Floor(preferredWindow.CenterY / UnitWithGap);
        var centerChunk = new TileChunkCoordinate(FloorChunk(centerColumn, ChunkColumns), FloorChunk(centerRow, ChunkRows));
        for (var radius = 0; radius <= 64; radius++)
        {
            var candidates = Ring(centerChunk, radius)
                .Distinct()
                // Data tiles must not be limited to the few fixed template
                // slots with exactly the same span. The virtual renderer
                // splits any overlapped skeleton into residual cells, so a
                // free grid rectangle is a safe, continuous replacement.
                .SelectMany(chunk => CandidateSlots(chunk, span))
                .Where(slot => !IsOccupied(slot, occupied))
                .Select(slot => new
                {
                    Slot = slot,
                    Distance = Math.Pow(slot.Column + slot.ColumnSpan / 2d - centerColumn, 2) + Math.Pow(slot.Row + slot.RowSpan / 2d - centerRow, 2),
                    Neighbors = NeighborCount(slot, occupied)
                })
                // Prefer the frontier of the current mosaic. This makes newly
                // synchronized repositories grow the visible data island
                // continuously instead of forming disconnected remote clusters.
                .OrderByDescending(x => x.Neighbors)
                .ThenBy(x => x.Distance)
                .ThenBy(x => x.Slot.Row)
                .ThenBy(x => x.Slot.Column)
                .Select(x => x.Slot)
                .FirstOrDefault();
            if (candidates is not null) return (candidates.Column, candidates.Row);
        }
        return (centerColumn, centerRow);
    }

    private static IEnumerable<VirtualTileSlot> CandidateSlots(TileChunkCoordinate chunk, TileSpan span)
    {
        var minimumColumn = chunk.X * ChunkColumns;
        var minimumRow = chunk.Y * ChunkRows;
        var maximumColumn = minimumColumn + ChunkColumns - span.Columns;
        var maximumRow = minimumRow + ChunkRows - span.Rows;
        for (var row = minimumRow; row <= maximumRow; row++)
            for (var column = minimumColumn; column <= maximumColumn; column++)
            {
                var key = $"candidate:{chunk.X}:{chunk.Y}:{column}:{row}:{span.Columns}:{span.Rows}";
                yield return new VirtualTileSlot(key, chunk, column, row, span.Columns, span.Rows,
                    new TileContent(key, MetroTileKind.Tip, string.Empty, AccentKey: "placeholder", IsPlaceholder: true));
            }
    }

    private static IEnumerable<VirtualTileSlot> Slots(string seed, TileChunkCoordinate chunk, IReadOnlyList<TipDefinition> tips)
    {
        var spans = Template(chunk, seed);
        var exploreCandidates = spans
            .Select((span, index) => (span, index))
            .Where(x => x.span.Columns == 2 && x.span.Rows == 2)
            .Select(x => x.index)
            .ToArray();
        var exploreIndex = exploreCandidates.Length == 0
            ? -1
            : exploreCandidates[(int)(Stable(seed, chunk.X, chunk.Y, -2) % (ulong)exploreCandidates.Length)];
        for (var i = 0; i < spans.Count; i++)
        {
            var value = Stable(seed, chunk.X, chunk.Y, i);
            var span = spans[i];
            var accent = Accents[(int)((value >> 8) % (ulong)Accents.Length)];
            var column = chunk.X * ChunkColumns + span.Column;
            var row = chunk.Y * ChunkRows + span.Row;
            var key = $"virtual:{chunk.X}:{chunk.Y}:{i}";
            if (i == exploreIndex)
            {
                yield return new(key, chunk, column, row, span.Columns, span.Rows,
                    new TileContent(
                        key,
                        MetroTileKind.Explore,
                        "探索未知区域",
                        "从 GitHub 获取下一批项目，并从这里继续铺开。",
                        "DISCOVER · 点击加载",
                        accent,
                        IsPlaceholder: true,
                        PreferredSpan: new TileSpan(span.Columns, span.Rows)));
                continue;
            }
            var tip = tips[(int)(value % (ulong)tips.Count)];
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

    private static void MarkCovered(VirtualTileSlot slot, HashSet<(int Column, int Row)> covered)
    {
        for (var row = slot.Row; row < slot.Row + slot.RowSpan; row++)
            for (var column = slot.Column; column < slot.Column + slot.ColumnSpan; column++)
                covered.Add((column, row));
    }

    private static int NeighborCount(VirtualTileSlot slot, HashSet<(int Column, int Row)> occupied)
    {
        var count = 0;
        for (var row = slot.Row; row < slot.Row + slot.RowSpan; row++)
        {
            if (occupied.Contains((slot.Column - 1, row))) count++;
            if (occupied.Contains((slot.Column + slot.ColumnSpan, row))) count++;
        }
        for (var column = slot.Column; column < slot.Column + slot.ColumnSpan; column++)
        {
            if (occupied.Contains((column, slot.Row - 1))) count++;
            if (occupied.Contains((column, slot.Row + slot.RowSpan))) count++;
        }
        return count;
    }

    private static VirtualTileSlot FallbackSlot(string seed, TileChunkCoordinate chunk, int column, int row, IReadOnlyList<TipDefinition> tips)
    {
        var value = Stable(seed, column, row, 10_000);
        var tip = tips[(int)(value % (ulong)tips.Count)];
        var accent = Accents[(int)((value >> 8) % (ulong)Accents.Length)];
        var key = $"virtual:{chunk.X}:{chunk.Y}:fill:{column}:{row}";
        return new(key, chunk, column, row, 1, 1,
            new TileContent(key, MetroTileKind.Tip, tip.Title, tip.Body,
                $"{tip.Category} · 等待内容", accent, IsPlaceholder: true, SourceUrl: tip.SourceUrl));
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
        // FNV-1a followed by SplitMix64 gives a stable, allocation-free seed.
        // This is layout randomness, not cryptography.
        ulong value = 14695981039346656037UL;
        foreach (var character in seed)
        {
            value ^= character;
            value *= 1099511628211UL;
        }
        value ^= unchecked((uint)x) * 0x9E3779B1UL;
        value ^= unchecked((uint)y) * 0x85EBCA77UL;
        value ^= unchecked((uint)slot) * 0xC2B2AE3DUL;
        value += 0x9E3779B97F4A7C15UL;
        value = (value ^ (value >> 30)) * 0xBF58476D1CE4E5B9UL;
        value = (value ^ (value >> 27)) * 0x94D049BB133111EBUL;
        return value ^ (value >> 31);
    }

    private sealed record ChunkCacheKey(string Seed, int X, int Y, string TipsKey);
    private sealed class ChunkCacheEntry(IReadOnlyList<VirtualTileSlot> slots, long lastAccess) { public IReadOnlyList<VirtualTileSlot> Slots { get; } = slots; public long LastAccess { get; set; } = lastAccess; }
}
