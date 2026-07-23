namespace RepoGalaxy.Desktop.Controls;

/// <summary>
/// Viewport-sized, retained-data Tile presenter. It owns no ItemsControl or
/// world-sized child tree: real and virtual tiles are batch-drawn from the
/// current immutable snapshots and hit-tested in world coordinates.
/// </summary>
public sealed class VirtualizedTileWorldControl : ZoomableTileCanvas
{
}
