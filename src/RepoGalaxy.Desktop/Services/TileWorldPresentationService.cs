using RepoGalaxy.Core.Interfaces;
using RepoGalaxy.Core.Models;

namespace RepoGalaxy.Desktop.Services;

public sealed class TileWorldPresentationService : ITileWorldPresentationService
{
    private const double UnitWithGap = 100;
    private const double Unit = 96;

    public TileWorldSnapshot CreateSnapshot(TileBoardState board, string? anchorContentKey = null)
    {
        var placements = board.Placements.Where(x => !x.Content.IsPlaceholder).ToArray();
        var bounds = placements.Length == 0
            ? TileWorldContentBounds.Empty
            : new(
                placements.Min(x => x.Column * UnitWithGap),
                placements.Min(x => x.Row * UnitWithGap),
                placements.Max(x => x.Column * UnitWithGap + Width(x.ColumnSpan)),
                placements.Max(x => x.Row * UnitWithGap + Width(x.RowSpan)));
        var anchorPlacement = string.IsNullOrWhiteSpace(anchorContentKey)
            ? null
            : placements.FirstOrDefault(x => x.Content.Key.Equals(anchorContentKey, StringComparison.Ordinal));
        TileWorldAnchor? anchor = anchorPlacement is null
            ? null
            : new(anchorPlacement.Content.Key,
                anchorPlacement.Column * UnitWithGap + Width(anchorPlacement.ColumnSpan) / 2,
                anchorPlacement.Row * UnitWithGap + Width(anchorPlacement.RowSpan) / 2);
        return new(board.Id, board.WorldSeed, placements, bounds, anchor);
    }

    public IReadOnlyList<TilePlacement> QueryVisible(TileWorldSnapshot snapshot, TileWorldViewport viewport, double overscan = 180)
    {
        var window = viewport.WorldWindow;
        var margin = Math.Max(0, overscan) / Math.Max(.01, viewport.Zoom);
        var left = window.X - margin;
        var top = window.Y - margin;
        var right = window.X + window.Width + margin;
        var bottom = window.Y + window.Height + margin;
        return snapshot.Placements.Where(x =>
        {
            var x0 = x.Column * UnitWithGap;
            var y0 = x.Row * UnitWithGap;
            return x0 + Width(x.ColumnSpan) >= left && x0 <= right
                && y0 + Width(x.RowSpan) >= top && y0 <= bottom;
        }).ToArray();
    }

    private static double Width(int span) => span * Unit + (span - 1) * (UnitWithGap - Unit);
}
